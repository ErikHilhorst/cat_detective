using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using CatDetective.Map;
using CatDetective.Systems;

namespace CatDetective
{
    /// <summary>
    /// "Wait for the art" gates + the loading screen.
    ///
    /// Web: textures stream in through a worker (Systems/WebTextures.cs). Every
    /// entry point that is about to need a set of textures - starting a case
    /// (NEW GAME / TUTORIAL / the tutorial hand-off), CONTINUE, a door - goes
    /// through <see cref="WhenAssetsReady"/>: if everything is already in
    /// memory the action runs at once, otherwise the game shows the loading
    /// screen (GameState.Loading) with real progress and runs the action when
    /// the last texture lands. Nothing ever blocks the browser's main thread.
    /// Desktop: assets load from disk instantly; the gate just runs the action.
    /// </summary>
    public partial class Game1
    {
        /// <summary>Textures LoadContent needs. The web boot waits for exactly these.</summary>
        public static readonly string[] BootTextures =
        {
            "Shared/walk_animation_forward", "Shared/walk_animation_upward", "Shared/shadow_blob",
            "Shared/ui_dialogue_box", "Shared/ui_notebook_bg", "Shared/ui_notebook_score_card",
            "Shared/menu_room", "Shared/menu_logo", "Shared/menu_cat",
            "Shared/speech_bubble", "Shared/arrow",
            "Shared/who", "Shared/how", "Shared/why", "Shared/where",
            "Shared/dikkie/dikkie_main", "Shared/dikkie/dikkie_crouch", "Shared/dikkie/dikkie_sit_paw",
            "Shared/dikkie/dikkie_reach", "Shared/dikkie/dikkie_scratch", "Shared/dikkie/dikkie_belly",
            "Shared/dikkie/dikkie_stretch",
        };

        private Action?          _loadingAction;
        private Func<string[]>?  _loadingAssetsFn;
        private string[]         _loadingAssets = Array.Empty<string>();
        private bool             _loadingResolved;    // asset list evaluated + prioritized
        private bool             _loadingOverRoom;    // door: dim the current room instead of the study
        private int              _loadingFrames;      // loading frames actually presented
        private GameState        _loadingReturnState;
        private string           _loadingLabel = "";
        private int              _loadingDone, _loadingTotal;
        private float            _loadingShown;       // seconds on screen (fade-in)
        private float            _loadingBar;         // eased bar fill shown, 0..1

#if BLAZORGL
        private WebContentManager? WebContent => Content as WebContentManager;
#endif

        /// <summary>
        /// Runs <paramref name="action"/> once every texture whose asset name
        /// starts with one of <paramref name="assets"/> is in memory.
        ///
        /// Web: the loading screen goes up IN THE CLICK FRAME, always - even
        /// when every texture is already in memory the action itself (JSON
        /// reads, texture uploads, nav grid) takes a moment on a phone, and a
        /// click with no visible response for 2-3 s feels broken. The asset
        /// list (it reads JSON) and the action only run after at least one
        /// loading frame has reached the screen.
        /// Desktop: assets load from disk instantly; the gate just runs the action.
        /// </summary>
        private void WhenAssetsReady(Func<string[]> assets, string label, Action action)
        {
#if BLAZORGL
            _loadingAction      = action;
            _loadingAssetsFn    = assets;
            _loadingAssets      = Array.Empty<string>();
            _loadingResolved    = false;
            _loadingOverRoom    = _currentState == GameState.Playing;
            _loadingFrames      = 0;
            _loadingLabel       = label;
            _loadingDone        = 0;
            _loadingTotal       = 0;
            _loadingShown       = 0f;
            _loadingBar         = 0f;
            _loadingReturnState = _currentState;
            _currentState       = GameState.Loading;
#else
            action();
#endif
        }

        private void UpdateLoading(float dt)
        {
            _loadingShown += dt;
            float target = _loadingTotal > 0 ? _loadingDone / (float)_loadingTotal : 0f;
            _loadingBar = MathHelper.Lerp(_loadingBar, target, 1f - MathF.Exp(-dt * 12f));
            if (target - _loadingBar < 0.01f) _loadingBar = target;

            // Feedback first: nothing heavy until the loading screen is visible.
            if (_loadingFrames < 1) return;

            if (!_loadingResolved)
            {
                _loadingAssets   = _loadingAssetsFn?.Invoke() ?? Array.Empty<string>();
                _loadingResolved = true;
#if BLAZORGL
                if (WebTextures.Active) WebTextures.Prioritize(_loadingAssets);
#endif
                return;
            }
#if BLAZORGL
            if (WebContent != null && WebTextures.Active)
            {
                WebContent.Pump(30);
                (_loadingDone, _loadingTotal) = WebTextures.Progress(_loadingAssets);
                if (_loadingDone < _loadingTotal) return;
            }
            else { _loadingDone = _loadingTotal = 1; }
#endif
            // Let the full bar reach the screen before the (blocking) action runs.
            if (_loadingTotal > 0 && _loadingBar < 0.999f) return;
            if (_loadingFrames < 2) return;

            var action = _loadingAction;
            _loadingAction   = null;
            _loadingAssetsFn = null;
            _currentState    = _loadingReturnState;
            action?.Invoke();
        }

        /// <summary>Draw, while Loading, after the world: room-dim variant + presented-frame count.</summary>
        private void DrawLoadingAfterWorld()
        {
            if (_loadingOverRoom)
            {
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                float a = 0.5f + 0.5f * Math.Clamp(_loadingShown / 0.2f, 0f, 1f);
                _spriteBatch.Draw(_debugPixel, new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT),
                    new Color(30, 18, 8) * (0.45f * a));
                DrawLoadingCard(new Vector2(SCREEN_WIDTH * 0.5f, SCREEN_HEIGHT * 0.5f), a);
                _spriteBatch.End();
            }
            _loadingFrames++;
        }

        /// <summary>Background drain while playing: decoded textures move into the cache.</summary>
        private void PumpBackgroundTextures(bool safeFrame)
        {
#if BLAZORGL
            if (safeFrame) WebContent?.Pump(4);
#endif
        }

        /// <summary>
        /// Everything a case start needs: the cutscene script's backgrounds and
        /// cast sprites, plus the room the player lands in (default: the case's
        /// first room).
        /// </summary>
        private string[] CaseStartAssets(string caseId, string? roomId)
        {
            var list = new List<string> { "Shared/" };
            var script = CaseScriptParser.Load(Content.RootDirectory, caseId);
            if (script != null)
            {
                foreach (var bg in script.Backgrounds.Values) list.AddRange(bg.Layers);
                foreach (var cast in script.Cast.Values)
                    if (cast.Sprite.Length > 0) list.Add(cast.Sprite);
            }
            if (string.IsNullOrEmpty(roomId))
            {
                string cfgPath = Path.Combine(Content.RootDirectory, "Levels", caseId, "case_config.json");
                if (GameFile.Exists(cfgPath))
                {
                    var cfg = LevelConfigParser.LoadCase(cfgPath);
                    if (cfg.Rooms.Count > 0) roomId = cfg.Rooms[0];
                }
            }
            if (!string.IsNullOrEmpty(roomId)) list.Add($"Levels/{caseId}/{roomId}/");
            return list.ToArray();
        }

        private string[] RoomAssets(string roomId) =>
            new[] { $"Levels/{_currentCaseId}/{roomId}/" };

        /// <summary>The study scene; the loading card sits where the menu page was.</summary>
        private void DrawLoadingScreen()
        {
            BeginMenuScene(showPrompt: false);
            float a  = 0.5f + 0.5f * Math.Clamp(_loadingShown / 0.2f, 0f, 1f);
            bool  menuLayout = MenuReveal > 0.5f;
            float cx = menuLayout ? RightPage.Center.X : SCREEN_WIDTH * 0.5f;
            float cy = menuLayout ? SCREEN_HEIGHT * 0.5f : SCREEN_HEIGHT * 0.62f;
            DrawLoadingCard(new Vector2(cx, cy), a, card: !menuLayout);   // the menu page IS the card
            _spriteBatch.End();
        }

        /// <summary>
        /// Parchment card: label, Dikkie trotting along the top of a rounded
        /// progress bar (the cozy-kit track/fill/outline), and a small status
        /// line. Expects an open AlphaBlend batch; leaves one open.
        /// </summary>
        private void DrawLoadingCard(Vector2 center, float a, bool card = true)
        {
            int cw = Math.Min(540, SCREEN_WIDTH - 64), ch = 190;
            var box = new Rectangle((int)(center.X - cw / 2f), (int)(center.Y - ch / 2f), cw, ch);
            if (card) DrawParchment(box, a);

            string label = _loadingLabel;
            float  ls    = Math.Min(0.62f, (cw - 72) / Math.Max(1f, _dialogueFont.MeasureString(label).X));
            DrawCentered(label, box.Center.X, box.Y + 22, ls, UiInk * a);

            // Track, fill (with a lit top stripe), ink outline - the button look.
            var bar = new Rectangle(box.X + 46, box.Y + 130, cw - 92, 20);
            var src = new Rectangle(0, 0, ROUND_SIZE, ROUND_SIZE);
            DrawSoft(new Rectangle(bar.X, bar.Y + 2, bar.Width, bar.Height), 3, Color.Black * (0.18f * a));
            DrawNineSlice(_uiRound, src, ROUND_RADIUS + 2, bar, 10, UiSlate * a);
            float p  = Math.Clamp(_loadingBar, 0f, 1f);
            int   fw = (int)(bar.Width * p);
            if (fw >= 6)
            {
                var fill = new Rectangle(bar.X, bar.Y, Math.Max(20, fw), bar.Height);
                DrawNineSlice(_uiRound, src, ROUND_RADIUS + 2, fill, 10, UiYellow * a);
                var lit = new Rectangle(fill.X + 7, fill.Y + 4, fill.Width - 14, 4);
                if (lit.Width > 0) _spriteBatch.Draw(_uiRound, lit, src, Color.White * (0.4f * a));
            }
            DrawNineSlice(_uiOutline, src, ROUND_RADIUS + 2, bar, 10, UiInk * (0.75f * a));

            string status = !_loadingResolved || _loadingTotal == 0
                ? "Getting things ready" + new string('.', 1 + (int)(_uiTime * 2.5f) % 3)
                : $"{(int)MathF.Round(p * 100f)}%";
            DrawCentered(status, box.Center.X, bar.Bottom + 7, 0.44f, UiInk * (0.6f * a));

            // Dikkie trots along the fill's leading edge (walk sheet: 6x2 grid,
            // 11 frames, faces left -> flip to face right). Straight alpha.
            _spriteBatch.End();
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp);
            int   fwid  = _walkForwardTex.Width / 6, fhei = _walkForwardTex.Height / 2;
            int   frame = 10 - (int)(_uiTime / 0.07f) % 11;
            var   fsrc  = new Rectangle(frame % 6 * fwid, frame / 6 * fhei, fwid, fhei);
            float cs    = 84f / fhei;
            float catX  = MathHelper.Clamp(bar.X + fw, bar.X + 20, bar.Right - 20);
            _spriteBatch.Draw(_shadowTex, new Rectangle((int)catX - 22, bar.Y - 7, 44, 10), Color.White * (0.55f * a));
            _spriteBatch.Draw(_walkForwardTex, new Vector2(catX, bar.Y + 2), fsrc, Color.White * a, 0f,
                new Vector2(fwid * 0.5f, fhei), cs, SpriteEffects.FlipHorizontally, 0f);
            _spriteBatch.End();
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        }
    }
}
