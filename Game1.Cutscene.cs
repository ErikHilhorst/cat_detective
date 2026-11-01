using CatDetective.Entities;
using CatDetective.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace CatDetective
{
    /// <summary>
    /// Scripted scenes (case_script.json): the cold open / news-van roll call,
    /// the gathering that replaces the one-shot final board, and the epilogue.
    ///
    /// Flow: StartCutscene(beats, onDone) switches to GameState.Cutscene and plays
    /// beats on the fixed menu canvas. The gathering alternates cutscenes with
    /// small deduction boards: each step's board opens in the normal Playing
    /// state (journal UI, final-solve mode) and a valid submit plays the step's
    /// success beats, then the next step's intro. Closing the board aborts the
    /// gathering; FINAL SOLVE resumes it at the same step.
    /// </summary>
    public partial class Game1
    {
        // ── Cutscene state ────────────────────────────────────────────────────
        private CaseScript?      _caseScript;
        private List<ScriptBeat> _cutBeats = new();
        private int              _cutIndex;
        private float            _cutTime;          // seconds since the beat started
        private Action?          _cutDone;
        private Action?          _cutEsc;           // null = Esc jumps to the last beat
        private string?          _cutBg;            // background carried across beats
        private string?          _cutPrevWho;
        private readonly Dictionary<string, Texture2D?> _scriptTex = new();
        private readonly Random  _shakeRng = new();

        // ── Gathering state ───────────────────────────────────────────────────
        private bool _gatheringActive;
        private int  _gatheringStep;
        private bool _gatheringIntroSeen;

        private const float STAMP_HOLD   = 0.75f;   // stamp shows alone before text types
        private const float SLIDE_TIME   = 0.18f;
        private const float SHAKE_TIME   = 0.45f;
        private const float FLASH_TIME   = 0.30f;
        private const float CUT_TEXT_SCALE = 0.72f;   // = DIALOGUE_TEXT_SCALE

        private GatheringStep? CurrentGatheringStep =>
            _caseScript?.Gathering is { } g && _gatheringStep < g.Steps.Count
                ? g.Steps[_gatheringStep] : null;

        private bool HasGathering => _caseScript?.Gathering is { Steps.Count: > 0 };

        private void LoadCaseScript(string caseId) =>
            _caseScript = CaseScriptParser.Load(Content.RootDirectory, caseId);

        // ══════════════════════════════════════════════════════════════════════
        // Player
        // ══════════════════════════════════════════════════════════════════════
        private void StartCutscene(List<ScriptBeat> beats, Action onDone, Action? onEsc = null)
        {
            if (beats.Count == 0) { onDone(); return; }
            _cutBeats   = beats;
            _cutIndex   = 0;
            _cutTime    = 0f;
            _cutDone    = onDone;
            _cutEsc     = onEsc;
            _cutPrevWho = null;
            _isDialogueActive     = false;
            _isTopicMenuOpen      = false;
            _isDeductionBoardOpen = false;
            SetCanvas(MENU_CANVAS_W, MENU_CANVAS_H);
            _currentState = GameState.Cutscene;
            EnterBeat();
        }

        private void EnterBeat()
        {
            var beat = _cutBeats[_cutIndex];
            if (beat.Bg != null) _cutBg = beat.Bg;
            _cutTime = 0f;
        }

        private float BeatTextDelay(ScriptBeat beat) =>
            string.IsNullOrEmpty(beat.Stamp) ? 0.15f : STAMP_HOLD;

        private int BeatVisibleChars(ScriptBeat beat) =>
            beat.Card ? int.MaxValue
                      : Math.Max(0, (int)((_cutTime - BeatTextDelay(beat)) * TYPEWRITER_SPEED));

        private void UpdateCutscene(float dt, bool confirm, bool esc)
        {
            if (_cutBeats.Count == 0) return;
            var beat = _cutBeats[_cutIndex];
            if (!_screenshotMode) _cutTime += dt;   // screenshots freeze the frame

            if (esc)
            {
                if (_cutEsc != null) { var e = _cutEsc; _cutEsc = null; e(); return; }
                if (_cutIndex < _cutBeats.Count - 1)
                {
                    _cutPrevWho = beat.Who;
                    _cutIndex   = _cutBeats.Count - 1;
                    EnterBeat();
                    _cutTime = 999f;
                    return;
                }
                FinishCutscene();
                return;
            }

            if (!confirm) return;
            if (BeatVisibleChars(beat) < beat.Text.Length)
            {
                _cutTime = 999f;                    // finish typing (and skip fx)
                return;
            }
            if (_cutIndex < _cutBeats.Count - 1)
            {
                _cutPrevWho = beat.Who;
                _cutIndex++;
                EnterBeat();
            }
            else
                FinishCutscene();
        }

        private void FinishCutscene()
        {
            var done = _cutDone;
            _cutDone = null;
            _cutBeats = new();
            done?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Intro / gathering / ending wiring
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Plays the script's intro if the case has one; false = no script intro.</summary>
        private bool TryStartScriptIntro(string caseId)
        {
            LoadCaseScript(caseId);
            if (_caseScript == null || _caseScript.Intro.Count == 0) return false;
            StartCutscene(_caseScript.Intro,
                onDone: () => StartCase(caseId),
                onEsc:  () => StartCase(caseId));
            return true;
        }

        /// <summary>FINAL SOLVE with a scripted gathering: start or resume it.</summary>
        private void StartGathering()
        {
            if (_caseScript == null) LoadCaseScript(_currentCaseId);
            if (!HasGathering) return;

            // Every chip a step can ask for is handed over up front: the player
            // has solved every room and heard every confrontation by now, and a
            // stuck step (answer never picked up) would dead-end the finale.
            // tools/story_sim reports which answers were NOT found naturally.
            foreach (var id in _caseScript!.GatheringClueIds())
                _notebook.UnlockClue(id);

            _gatheringActive = true;
            _gatheringStep   = Math.Clamp(_gatheringStep, 0, _caseScript.Gathering!.Steps.Count - 1);

            var beats = new List<ScriptBeat>();
            if (!_gatheringIntroSeen) beats.AddRange(_caseScript.Gathering.Intro);
            beats.AddRange(CurrentGatheringStep!.Intro);
            _gatheringIntroSeen = true;
            StartCutscene(beats, OpenGatheringBoard, onEsc: OpenGatheringBoard);
        }

        private void OpenGatheringBoard()
        {
            var step = CurrentGatheringStep;
            if (step == null) { FinishCase(); return; }

            SetCanvas(_bgBase.Width, _bgBase.Height);
            _currentState = GameState.Playing;
            _deduction = new DeductionManager(step.Sentence, step.Answers,
                id => _notebook.GetClue(id)?.Category);
            _isFinalSolveMode     = true;
            _isDeductionBoardOpen = true;
            _journalOnNotesPage   = false;
            _selectedWordBankClue = null;
            _insertTargetSlot     = null;
            _wordBankPage         = 0;
            if (_deduction.Slots.Count > 0) _activeTab = _deduction.Slots[0].Category;
        }

        /// <summary>Valid submit on a gathering board.</summary>
        private void OnGatheringStepSolved()
        {
            var g    = _caseScript!.Gathering!;
            var step = g.Steps[_gatheringStep];
            _gatheringStep++;
            _isDeductionBoardOpen = false;
            _selectedWordBankClue = null;
            _insertTargetSlot     = null;

            var beats = new List<ScriptBeat>(step.Success);
            if (_gatheringStep < g.Steps.Count)
            {
                beats.AddRange(g.Steps[_gatheringStep].Intro);
                StartCutscene(beats, OpenGatheringBoard, onEsc: OpenGatheringBoard);
            }
            else
            {
                beats.AddRange(_caseScript.Ending);
                FinishCase();
                StartCutscene(beats, ReturnToMainMenu);
            }
        }

        /// <summary>Board closed mid-gathering: back to the room; FINAL SOLVE resumes.</summary>
        private void AbortGathering()
        {
            _gatheringActive  = false;
            _isFinalSolveMode = false;
        }

        /// <summary>Case over: the save slot is cleared (CONTINUE dims).</summary>
        private void FinishCase()
        {
            _gatheringActive = false;
            if (!_screenshotMode)
            {
                SaveSystem.DeleteSave();
                _saveExists = false;
            }
        }

        /// <summary>Wrong gathering submit: prefix the suspect's taunt to the fit count.</summary>
        private void ApplyGatheringWrongLine()
        {
            var step = CurrentGatheringStep;
            string msg = _deduction.ValidationMessage;
            const string prefix = "Incorrect logic - ";
            if (step == null || step.WrongLine.Length == 0 || !msg.StartsWith(prefix)) return;
            _deduction.ValidationMessage = $"{step.WrongLine} ({msg[prefix.Length..]})";
        }

        // ══════════════════════════════════════════════════════════════════════
        // Drawing
        // ══════════════════════════════════════════════════════════════════════
        private Texture2D? ScriptTexture(string path)
        {
            if (_scriptTex.TryGetValue(path, out var cached)) return cached;
            Texture2D? tex = null;
            try { tex = Content.Load<Texture2D>(path); }
            catch (Exception ex) { Console.WriteLine($"[CaseScript] missing texture '{path}': {ex.Message}"); }
            _scriptTex[path] = tex;
            return tex;
        }

        private Rectangle CutsceneBoxRect
        {
            get
            {
                int w = Math.Min(1320, SCREEN_WIDTH - 60);
                return new Rectangle((SCREEN_WIDTH - w) / 2, SCREEN_HEIGHT - 248, w, 236);
            }
        }

        private void DrawCutscene()
        {
            GraphicsDevice.Clear(Color.Black);
            if (_cutBeats.Count == 0) return;
            var beat = _cutBeats[_cutIndex];

            // Screen shake: everything except the flash is offset.
            Vector2 shake = Vector2.Zero;
            if (beat.Fx.Contains("shake") && _cutTime < SHAKE_TIME)
            {
                float a = 14f * (1f - _cutTime / SHAKE_TIME);
                shake = new Vector2((float)(_shakeRng.NextDouble() * 2 - 1) * a,
                                    (float)(_shakeRng.NextDouble() * 2 - 1) * a);
            }

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                transformMatrix: Matrix.CreateTranslation(shake.X, shake.Y, 0));

            DrawScriptBackground(_cutBg);
            float dim = beat.Dim ?? 0.35f;
            if (dim > 0f)
                _spriteBatch.Draw(_debugPixel, new Rectangle(-40, -40, SCREEN_WIDTH + 80, SCREEN_HEIGHT + 80),
                    Color.Black * dim);

            if (beat.Lineup is { Count: > 0 }) DrawLineup(beat.Lineup, beat.Who);
            if (!beat.Card && !string.IsNullOrEmpty(beat.Who)) DrawStandIn(beat);

            if (beat.Card) DrawScriptCard(beat.Text);
            else           DrawScriptDialogue(beat);

            if (!string.IsNullOrEmpty(beat.Stamp)) DrawStamp(beat.Stamp);

            string footer = "Enter - continue   Esc - skip";
            var fSz = _dialogueFont.MeasureString(footer) * 0.5f;
            _spriteBatch.DrawString(_dialogueFont, footer,
                new Vector2(SCREEN_WIDTH - fSz.X - 24f, 14f),
                new Color(170, 170, 180) * 0.8f, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

            _spriteBatch.End();

            if (beat.Fx.Contains("flash") && _cutTime < FLASH_TIME)
            {
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
                _spriteBatch.Draw(_debugPixel, new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT),
                    Color.White * (1f - _cutTime / FLASH_TIME));
                _spriteBatch.End();
            }
        }

        private void DrawScriptBackground(string? bgId)
        {
            ScriptBackground? bg = null;
            if (bgId != null) _caseScript?.Backgrounds.TryGetValue(bgId, out bg);
            if (bg == null) return;   // black

            bool drewAny = false;
            foreach (var layer in bg.Layers)
            {
                var tex = ScriptTexture(layer);
                if (tex == null) continue;
                drewAny = true;
                Rectangle dest;
                if (bg.Fit == "contain")
                {
                    float s = Math.Min((float)SCREEN_WIDTH / tex.Width, (float)SCREEN_HEIGHT / tex.Height);
                    int w = (int)(tex.Width * s), h = (int)(tex.Height * s);
                    dest = new Rectangle((SCREEN_WIDTH - w) / 2, (SCREEN_HEIGHT - h) / 2, w, h);
                }
                else
                {
                    float s = Math.Max((float)SCREEN_WIDTH / tex.Width, (float)SCREEN_HEIGHT / tex.Height);
                    int w = (int)(tex.Width * s), h = (int)(tex.Height * s);
                    dest = new Rectangle((SCREEN_WIDTH - w) / 2, (SCREEN_HEIGHT - h) / 2, w, h);
                }
                _spriteBatch.Draw(tex, dest, Color.White);
            }

            if (!drewAny && bg.Fallback == "night") DrawNightFallback();
        }

        /// <summary>Code-drawn stand-in for missing exterior art: night sky, hedge, gate, headlights.</summary>
        private void DrawNightFallback()
        {
            int W = SCREEN_WIDTH, H = SCREEN_HEIGHT;
            for (int i = 0; i < 16; i++)
            {
                var c = Color.Lerp(new Color(14, 18, 44), new Color(70, 52, 88), i / 15f);
                _spriteBatch.Draw(_debugPixel, new Rectangle(0, i * H / 16, W, H / 16 + 1), c);
            }
            _spriteBatch.Draw(_debugPixel, new Rectangle(0, (int)(H * 0.62f), W, H), new Color(20, 34, 26));
            for (int x = W / 2 - 300; x <= W / 2 + 300; x += 46)   // gate bars
                _spriteBatch.Draw(_debugPixel, new Rectangle(x, (int)(H * 0.30f), 6, (int)(H * 0.40f)),
                    new Color(30, 30, 36));
            _spriteBatch.Draw(_debugPixel, new Rectangle(W / 2 - 300, (int)(H * 0.30f), 600, 8), new Color(30, 30, 36));
        }

        private void DrawStandIn(ScriptBeat beat)
        {
            if (_caseScript == null || !_caseScript.Cast.TryGetValue(beat.Who!, out var member)) return;
            var tex = ScriptTexture(member.Sprite);
            if (tex == null) return;

            float h  = SCREEN_HEIGHT * 0.93f;
            float w  = tex.Width * (h / tex.Height);
            float cx = beat.Pos switch
            {
                "left"  => SCREEN_WIDTH * 0.27f,
                "right" => SCREEN_WIDTH * 0.73f,
                _       => SCREEN_WIDTH * 0.5f,
            };

            // New speaker slides in from below with a fade.
            float t      = beat.Who == _cutPrevWho ? 1f : Math.Clamp(_cutTime / SLIDE_TIME, 0f, 1f);
            float ease   = 1f - (1f - t) * (1f - t);
            float yOff   = (1f - ease) * 60f;
            var   dest   = new Rectangle((int)(cx - w / 2), (int)(SCREEN_HEIGHT - h + 40 + yOff), (int)w, (int)h);
            _spriteBatch.Draw(tex, dest, Color.White * ease);
        }

        private void DrawLineup(List<string> ids, string? highlight)
        {
            const int FACE = 84, GAP = 12;
            int total = ids.Count * FACE + (ids.Count - 1) * GAP;
            int x = (SCREEN_WIDTH - total) / 2;
            int y = 40;
            foreach (var id in ids)
            {
                bool hot = id == highlight;
                var rect = new Rectangle(x, y, FACE, FACE);
                _spriteBatch.Draw(_debugPixel, new Rectangle(rect.X - 3, rect.Y - 3, FACE + 6, FACE + 6),
                    hot ? Color.Gold : new Color(20, 20, 24));
                _spriteBatch.Draw(_debugPixel, rect, new Color(40, 36, 44));
                if (_caseScript!.Cast.TryGetValue(id, out var m) && ScriptTexture(m.Sprite) is { } tex)
                {
                    // Same face crop as dialogue portraits: top third, middle 60%.
                    var src = new Rectangle((int)(tex.Width * 0.2f), 0, (int)(tex.Width * 0.6f), (int)(tex.Width * 0.6f));
                    _spriteBatch.Draw(tex, rect, src, Color.White * (hot || highlight == null ? 1f : 0.45f));
                }
                x += FACE + GAP;
            }
        }

        private void DrawScriptDialogue(ScriptBeat beat)
        {
            var box = CutsceneBoxRect;

            if (!string.IsNullOrEmpty(beat.Chyron)) DrawChyron(beat.Chyron, box.Y - 104);

            _spriteBatch.Draw(_dialogueBoxTex, box, Color.White);

            string speaker = beat.Speaker
                ?? (beat.Who != null && _caseScript!.Cast.TryGetValue(beat.Who, out var m) ? m.Name : "");
            if (speaker.Length > 0)
            {
                const float ls = 0.62f;
                var sz    = _dialogueFont.MeasureString(speaker) * ls;
                var plate = new Rectangle(box.X + 70, box.Y - (int)sz.Y + 8, (int)sz.X + 36, (int)sz.Y + 10);
                _spriteBatch.Draw(_debugPixel, plate, new Color(52, 38, 30));
                _spriteBatch.DrawString(_dialogueFont, speaker,
                    new Vector2(plate.X + 18, plate.Y + 5), Color.Gold,
                    0f, Vector2.Zero, ls, SpriteEffects.None, 0f);
            }

            var area = CutsceneTextArea(box);
            DrawRichText(_spriteBatch, _dialogueFont, beat.Text, Array.Empty<Keyword>(),
                new Vector2(area.X, area.Y), area.Width,
                maxChars: BeatVisibleChars(beat), scale: CUT_TEXT_SCALE);
        }

        private static Rectangle CutsceneTextArea(Rectangle box) =>
            new(box.X + (int)(box.Width * 0.075f), box.Y + 40,
                (int)(box.Width * 0.85f), box.Height - 72);

        private void DrawChyron(string text, int y)
        {
            const float s = 0.55f;
            var sz   = _dialogueFont.MeasureString(text) * s;
            int x    = 70;
            var live = new Rectangle(x, y, 110, (int)sz.Y + 18);
            _spriteBatch.Draw(_debugPixel, live, new Color(196, 32, 38));
            var lsz = _dialogueFont.MeasureString("LIVE") * s;
            _spriteBatch.DrawString(_dialogueFont, "LIVE",
                new Vector2(live.X + (live.Width - lsz.X) / 2, live.Y + 9), Color.White,
                0f, Vector2.Zero, s, SpriteEffects.None, 0f);
            var bar = new Rectangle(live.Right, y, (int)sz.X + 40, live.Height);
            _spriteBatch.Draw(_debugPixel, bar, new Color(18, 24, 52) * 0.95f);
            _spriteBatch.DrawString(_dialogueFont, text, new Vector2(bar.X + 20, bar.Y + 9), Color.White,
                0f, Vector2.Zero, s, SpriteEffects.None, 0f);
        }

        private void DrawStamp(string word)
        {
            // Slam in (big -> normal), hold, then fade under the typing text.
            float t     = _cutTime;
            float scale = t < 0.12f ? MathHelper.Lerp(4.2f, 2.4f, t / 0.12f) : 2.4f;
            float alpha = t < STAMP_HOLD + 0.4f ? 1f : Math.Max(0f, 1f - (t - STAMP_HOLD - 0.4f) / 0.5f);
            if (alpha <= 0f) return;

            var size   = _dialogueFont.MeasureString(word);
            var origin = size / 2f;
            var pos    = new Vector2(SCREEN_WIDTH / 2f, SCREEN_HEIGHT * 0.32f);
            const float rot = -0.07f;

            _spriteBatch.Draw(_debugPixel,
                new Rectangle(0, (int)(pos.Y - size.Y * 1.5f), SCREEN_WIDTH, (int)(size.Y * 3f)),
                new Color(120, 14, 18) * (0.55f * alpha));
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    if (dx != 0 || dy != 0)
                        _spriteBatch.DrawString(_dialogueFont, word, pos + new Vector2(dx, dy) * 3f,
                            new Color(60, 8, 10) * alpha, rot, origin, scale, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_dialogueFont, word, pos, new Color(255, 246, 225) * alpha,
                rot, origin, scale, SpriteEffects.None, 0f);
        }

        private void DrawScriptCard(string text)
        {
            string[] lines = text.Split('\n');
            float blockH = 0f;
            bool first = true;
            foreach (var line in lines)
            {
                if (line.Length == 0) { blockH += 34f; continue; }
                blockH += _dialogueFont.LineSpacing * (first ? 2.0f : 0.9f);
                first = false;
            }
            float cy = (SCREEN_HEIGHT - blockH) * 0.5f;
            first = true;
            foreach (var line in lines)
            {
                if (line.Length == 0) { cy += 34f; continue; }
                float s   = first ? 2.0f : 0.9f;
                var   sz  = _dialogueFont.MeasureString(line) * s;
                var   pos = new Vector2((SCREEN_WIDTH - sz.X) * 0.5f, cy);
                _spriteBatch.DrawString(_dialogueFont, line, pos + new Vector2(4, 4), Color.Black * 0.8f,
                    0f, Vector2.Zero, s, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_dialogueFont, line, pos, first ? Color.Gold : new Color(225, 220, 205),
                    0f, Vector2.Zero, s, SpriteEffects.None, 0f);
                cy += _dialogueFont.LineSpacing * s;
                first = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Verification: `--screenshot ui scriptcheck <case>` measures every beat
        // against the cutscene text box and prints overflows (no window needed
        // beyond the brief screenshot-mode run).
        // ══════════════════════════════════════════════════════════════════════
        private void RunScriptCheck(string caseId)
        {
            LoadCaseScript(caseId);
            if (_caseScript == null) { Console.WriteLine($"[ScriptCheck] {caseId}: no case_script.json"); return; }
            SetCanvas(MENU_CANVAS_W, MENU_CANVAS_H);
            var area = CutsceneTextArea(CutsceneBoxRect);

            int problems = 0, total = 0;
            void Check(string where, List<ScriptBeat> beats)
            {
                for (int i = 0; i < beats.Count; i++)
                {
                    var b = beats[i];
                    total++;
                    if (b.Card) continue;
                    float h = MeasureRichTextHeight(_dialogueFont, b.Text, area.Width, CUT_TEXT_SCALE);
                    if (h > area.Height)
                    {
                        problems++;
                        Console.WriteLine($"[ScriptCheck] OVERFLOW {where}[{i}] {h:0}px > {area.Height}px: {b.Text[..Math.Min(60, b.Text.Length)]}...");
                    }
                    foreach (char c in b.Text + (b.Speaker ?? "") + (b.Chyron ?? "") + (b.Stamp ?? ""))
                        if (c != '\n' && (c < 32 || c > 126))
                        {
                            problems++;
                            Console.WriteLine($"[ScriptCheck] NON-ASCII {where}[{i}]: '{c}'");
                            break;
                        }
                    if (b.Who is { Length: > 0 } && !_caseScript.Cast.ContainsKey(b.Who))
                    { problems++; Console.WriteLine($"[ScriptCheck] UNKNOWN CAST {where}[{i}]: {b.Who}"); }
                    if (b.Bg != null && !_caseScript.Backgrounds.ContainsKey(b.Bg))
                    { problems++; Console.WriteLine($"[ScriptCheck] UNKNOWN BG {where}[{i}]: {b.Bg}"); }
                }
            }
            Check("intro", _caseScript.Intro);
            if (_caseScript.Gathering != null)
            {
                Check("gathering.intro", _caseScript.Gathering.Intro);
                foreach (var s in _caseScript.Gathering.Steps)
                {
                    Check($"{s.Id}.intro", s.Intro);
                    Check($"{s.Id}.success", s.Success);
                }
            }
            Check("ending", _caseScript.Ending);
            foreach (var (id, m) in _caseScript.Cast)
                if (ScriptTexture(m.Sprite) == null) { problems++; Console.WriteLine($"[ScriptCheck] MISSING SPRITE cast '{id}': {m.Sprite}"); }
            foreach (var (id, bg) in _caseScript.Backgrounds)
                foreach (var l in bg.Layers)
                    if (ScriptTexture(l) == null)
                        Console.WriteLine($"[ScriptCheck] note: bg '{id}' layer missing ({l}) - fallback '{bg.Fallback}'");
            Console.WriteLine($"[ScriptCheck] {caseId}: {total} beats, {problems} problem(s).");
        }
    }
}
