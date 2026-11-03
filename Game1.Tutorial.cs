using Microsoft.Xna.Framework;
using System;

namespace CatDetective
{
    /// <summary>
    /// "Training Day": the one-room tutorial (Content/Levels/tutorial).
    ///
    /// Everything the player DOES is the normal game - clues, boards, Solve and
    /// Final Solve; intro + outro are case_script.json cutscenes. The only
    /// tutorial-specific code is this guide card (device-aware controls + a
    /// 3-step checklist that ticks itself off) and the first-run hand-off: the
    /// very first NEW GAME plays the tutorial and its outro leads straight into
    /// Malibu (settings.json "tutorialSeen"); a replay from the menu returns there.
    /// </summary>
    public partial class Game1
    {
        private const string TUTORIAL_CASE = "tutorial";

        /// <summary>Case the tutorial outro hands over to (first run); null = main menu.</summary>
        private string? _tutorialLeadsToCase;

        /// <summary>After a scripted outro: on to the next case, or back to the menu.</summary>
        private void AfterCaseOutro()
        {
            string? next = _tutorialLeadsToCase;
            _tutorialLeadsToCase = null;
            if (next != null) StartCaseIntro(next);
            else              ReturnToMainMenu();
        }

        /// <summary>
        /// Top-centre parchment card in the tutorial room: how to move/look on
        /// THIS device, then the three steps of every case with live ticks.
        /// </summary>
        private void DrawTutorialGuide()
        {
            if (_currentCaseId != TUTORIAL_CASE) return;

            bool touch = _pointer.LastWasTouch;
            string controls = touch
                ? "Tap the floor to walk.  Tap a thing, then tap it again to look."
                : "Walk: WASD / arrows (or click).  Look: Enter (or click it twice).";

            var (found, total) = _notebook.GetRoomClueCounts(
                _currentRoomId, id => !_ungatedClueIds.Contains(id));
            bool cluesDone  = total > 0 && found >= total;
            bool roomSolved = _roomSolvedStates.TryGetValue(_currentRoomId, out var rs) && rs;

            var steps = new (string Text, bool Done)[]
            {
                ($"Find the clues  ({found}/{total})", cluesDone),
                ("Press Solve and fill in what happened here", roomSolved),
                ("Press Final Solve to close the case", false),
            };
            int current = Array.FindIndex(steps, s => !s.Done);

            const float ctrlScale = 0.5f, stepScale = 0.56f;
            const int   padX = 30, padY = 16, chip = 20, rowGap = 6;
            float lineH  = _dialogueFont.LineSpacing * stepScale;
            float ctrlH  = _dialogueFont.LineSpacing * ctrlScale;
            float innerW = _dialogueFont.MeasureString(controls).X * ctrlScale;
            foreach (var s in steps)
                innerW = Math.Max(innerW, chip + 12 + _dialogueFont.MeasureString(s.Text).X * stepScale);

            int h = (int)(padY * 2 + ctrlH + 14 + steps.Length * (lineH + rowGap));
            var card = new Rectangle((int)(SCREEN_WIDTH - innerW) / 2 - padX, 10,
                                     (int)innerW + padX * 2, h);
            DrawParchment(card);

            float x = card.X + padX, y = card.Y + padY;
            _spriteBatch.DrawString(_dialogueFont, controls, new Vector2(x, y), UiInk * 0.8f,
                0f, Vector2.Zero, ctrlScale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
            y += ctrlH + 4;
            _spriteBatch.Draw(_debugPixel, new Rectangle((int)x, (int)y, (int)innerW, 1), UiInk * 0.3f);
            y += 10;

            for (int i = 0; i < steps.Length; i++)
            {
                var (text, done) = steps[i];
                bool now = i == current;
                var  dot = new Rectangle((int)x, (int)(y + (lineH - chip) * 0.5f), chip, chip);
                if (done)     DrawChip(dot, UiGreen, 0.8f, lifted: false);
                else if (now) DrawChip(dot, UiYellow, 0.8f, lifted: true);
                else          DrawChip(dot, UiCream, 0.45f, lifted: false);
                if (done)     DrawCentered("v", dot.Center.X, dot.Y - 2, 0.42f, UiInk * 0.8f);

                Color ink = done ? UiInk * 0.45f : now ? UiInk : UiInk * 0.6f;
                _spriteBatch.DrawString(_dialogueFont, text, new Vector2(x + chip + 12, y), ink,
                    0f, Vector2.Zero, stepScale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
                y += lineH + rowGap;
            }
        }
    }
}
