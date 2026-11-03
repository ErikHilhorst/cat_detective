using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CatDetective
{
    /// <summary>
    /// Main menu + settings, drawn as Dikkie's open casebook on the desk
    /// (ui_notebook_score_card.png - the clean spread, already loaded for the
    /// Case Notes page, so the menu costs no extra content loads on web).
    ///   LEFT page : title "Dikkie Detective" in ink, subtitle, Dikkie himself.
    ///   RIGHT page: the menu as the book's "Contents" (cozy-kit buttons), or the
    ///               settings controls.
    /// Page geometry is measured from the art in its own 1408x768 pixels and
    /// mapped to the canvas, so it follows the stretch exactly.
    /// </summary>
    public partial class Game1
    {
        private const string GAME_TITLE_1 = "Dikkie";
        private const string GAME_TITLE_2 = "Detective";
        private const string GAME_TAGLINE = "the casebook of a very curious cat";

        private static readonly Color MenuAccentInk = new(150, 58, 42);   // red ink for "Detective"

        // Pages inside the spread art (1408x768 art pixels).
        private static readonly Rectangle ArtLeftPage  = new(135, 55, 565, 680);
        private static readonly Rectangle ArtRightPage = new(712, 55, 578, 680);

        private Rectangle ArtToCanvas(Rectangle a)
        {
            float kx = SCREEN_WIDTH  / (float)_notesBgTex.Width;
            float ky = SCREEN_HEIGHT / (float)_notesBgTex.Height;
            return new Rectangle((int)(a.X * kx), (int)(a.Y * ky), (int)(a.Width * kx), (int)(a.Height * ky));
        }

        private Rectangle LeftPage  => ArtToCanvas(ArtLeftPage);
        private Rectangle RightPage => ArtToCanvas(ArtRightPage);

        private void DrawCentered(string text, float centerX, float y, float scale, Color color)
        {
            var sz = _dialogueFont.MeasureString(text) * scale;
            _spriteBatch.DrawString(_dialogueFont, text, new Vector2(centerX - sz.X * 0.5f, y),
                color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>A short hand-drawn ink rule with a dot at each end.</summary>
        private void DrawInkRule(float centerX, float y, float width)
        {
            _spriteBatch.Draw(_debugPixel,
                new Rectangle((int)(centerX - width * 0.5f), (int)y, (int)width, 2), UiInk * 0.55f);
            var dot = new Rectangle(0, 0, ROUND_SIZE, ROUND_SIZE);
            foreach (float x in new[] { centerX - width * 0.5f - 6, centerX + width * 0.5f + 2 })
                DrawNineSlice(_uiRound, dot, ROUND_RADIUS + 2,
                    new Rectangle((int)x, (int)y - 1, 4, 4), 2, UiInk * 0.6f);
        }

        /// <summary>
        /// Desk + open book + the left (title) page. Leaves the AlphaBlend batch
        /// OPEN for the caller's right-page content.
        /// </summary>
        private void BeginCasebookSpread()
        {
            GraphicsDevice.Clear(new Color(40, 28, 20));
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _spriteBatch.Draw(_notesBgTex, new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT), Color.White);

            var   lp = LeftPage;
            float cx = lp.Center.X;
            float y  = lp.Y + lp.Height * 0.10f;

            // Title: "Dikkie" big in ink, "Detective" in red ink, fitted to the page.
            float t1 = Math.Min(2.2f, lp.Width * 0.78f / _dialogueFont.MeasureString(GAME_TITLE_1).X);
            DrawCentered(GAME_TITLE_1, cx, y, t1, UiInk);
            y += _dialogueFont.LineSpacing * t1 * 0.88f;
            float t2 = t1 * 0.62f;
            DrawCentered(GAME_TITLE_2, cx, y, t2, MenuAccentInk);
            y += _dialogueFont.LineSpacing * t2 + 10f;

            DrawInkRule(cx, y, lp.Width * 0.42f);
            y += 16f;
            DrawCentered(GAME_TAGLINE, cx, y, 0.6f, UiInk * 0.7f);
            _spriteBatch.End();

            // Dikkie, sitting pretty on the page (idle frame of the walk sheet) with
            // a slow breathing bob. Straight-alpha sprites -> NonPremultiplied.
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            const int cols = 6, rows = 2, idleFrame = 11;
            int   fw = _walkForwardTex.Width / cols, fh = _walkForwardTex.Height / rows;
            var   src = new Rectangle((idleFrame % cols) * fw, (idleFrame / cols) * fh, fw, fh);
            float catScale = lp.Height * 0.40f / fh;
            float breathe  = 1f + 0.012f * MathF.Sin(_uiTime * 2.2f);
            var   feet     = new Vector2(cx, lp.Bottom - lp.Height * 0.10f);

            // The sheet frame has empty space under the paws: tuck the shadow up to them.
            float shadowW = fw * catScale * 0.55f;
            float pawY    = feet.Y - fh * catScale * 0.12f;
            _spriteBatch.Draw(_shadowTex,
                new Rectangle((int)(feet.X - shadowW * 0.5f), (int)(pawY - shadowW * 0.12f),
                              (int)shadowW, (int)(shadowW * 0.24f)),
                Color.White * 0.45f);
            _spriteBatch.Draw(_walkForwardTex, feet, src, Color.White, 0f,
                new Vector2(fw * 0.5f, fh), new Vector2(catScale, catScale * breathe),
                SpriteEffects.FlipHorizontally, 0f);   // faces into the book
            _spriteBatch.End();

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        }

        /// <summary>Small faded-ink note centred near the bottom of the right page.</summary>
        private void DrawRightPageNote(string text, Color color, float fromBottom = 0.07f)
        {
            var rp = RightPage;
            float scale = Math.Min(0.56f, rp.Width * 0.9f / _dialogueFont.MeasureString(text).X);
            DrawCentered(text, rp.Center.X, rp.Bottom - rp.Height * fromBottom - 18f, scale, color);
        }

        private void DrawMainMenu()
        {
            BeginCasebookSpread();

            var rp = RightPage;
            DrawCentered("Contents", rp.Center.X, _mainMenuButtonRects[0].Y - 80f, 0.85f, UiInk);
            DrawInkRule(rp.Center.X, _mainMenuButtonRects[0].Y - 22f, rp.Width * 0.30f);

            for (int i = 0; i < _mainMenuButtonRects.Length; i++)
            {
                bool enabled  = IsMenuItemEnabled(i);
                bool selected = i == _menuIndex && enabled;
                DrawUiButton(_mainMenuButtonRects[i], selected ? UiYellow : UiCream,
                             _mainMenuLabels[i], enabled: enabled, selected: selected,
                             maxLabelScale: 0.72f);
            }

            if (_confirmNewGame)
                DrawRightPageNote(_pointer.LastWasTouch
                        ? "This overwrites your saved case. Tap New Game again to confirm."
                        : "This overwrites your saved case. Press Enter again to confirm.",
                    MenuAccentInk);
            else if (!IsMenuItemEnabled(0))
                DrawRightPageNote("New here? The Tutorial teaches the ropes.", UiInk * 0.6f);

            // Dev hint: tiny, on the desk edge.
            const string devHint = "F12 dev";
            var dSz = _dialogueFont.MeasureString(devHint) * 0.45f;
            _spriteBatch.DrawString(_dialogueFont, devHint,
                new Vector2(SCREEN_WIDTH - dSz.X - 10f, SCREEN_HEIGHT - dSz.Y - 6f),
                new Color(235, 220, 190) * 0.45f, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

            _spriteBatch.End();
        }

        private void DrawSettingsScreen()
        {
            BeginCasebookSpread();

            var   rp         = RightPage;
            const float labelScale = 0.66f;

            DrawCentered("Settings", rp.Center.X, rp.Y + rp.Height * 0.08f, 0.85f, UiInk);
            DrawInkRule(rp.Center.X, rp.Y + rp.Height * 0.08f + 60f, rp.Width * 0.30f);

            // ── Music volume ──────────────────────────────────────────────────
            _spriteBatch.DrawString(_dialogueFont, "Music volume",
                new Vector2(_settingsVolMinusRect.X, _settingsVolMinusRect.Y - 40f),
                UiInk, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
            DrawUiButton(_settingsVolMinusRect, UiCream, "-");
            int filledCells = (int)Math.Round(_settings.MusicVolume * 10f);
            var cellSrc = new Rectangle(0, 0, ROUND_SIZE, ROUND_SIZE);
            for (int i = 0; i < _settingsVolCellRects.Length; i++)
            {
                bool filled = i < filledCells;
                DrawNineSlice(_uiRound, cellSrc, ROUND_RADIUS + 2, _settingsVolCellRects[i], 8,
                    filled ? UiYellow : UiSlate * 0.55f);
                DrawNineSlice(_uiOutline, cellSrc, ROUND_RADIUS + 2, _settingsVolCellRects[i], 8,
                    UiInk * (filled ? 0.75f : 0.35f));
            }
            DrawUiButton(_settingsVolPlusRect, UiCream, "+");

            // ── CRT filter ────────────────────────────────────────────────────
            _spriteBatch.DrawString(_dialogueFont, "CRT filter",
                new Vector2(_settingsCrtToggleRect.X, _settingsCrtToggleRect.Y - 40f),
                UiInk, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
            DrawUiButton(_settingsCrtToggleRect,
                _settings.CrtEnabled ? UiGreen : UiCream,
                _settings.CrtEnabled ? "On" : "Off", maxLabelScale: 0.7f);
            _spriteBatch.DrawString(_dialogueFont, "old-TV scanlines",
                new Vector2(_settingsCrtToggleRect.Right + 18f,
                            _settingsCrtToggleRect.Center.Y - _dialogueFont.LineSpacing * 0.25f),
                UiInk * 0.55f, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

            DrawUiButton(_settingsBackRect, UiCream, "Back", maxLabelScale: 0.72f);
            DrawRightPageNote("Left/Right or click to change volume.   Esc - back", UiInk * 0.55f, 0.02f);

            _spriteBatch.End();
        }

        /// <summary>
        /// Menu + settings layout on the right page (called from UpdateLayout;
        /// menus always run at the fixed menu canvas).
        /// </summary>
        private void LayoutMenuScreens()
        {
            if (_notesBgTex == null) return;
            var rp = RightPage;
            int bw = (int)(rp.Width * 0.62f), bh = 56, gap = 16;
            int bx = rp.Center.X - bw / 2;
            int total = _mainMenuLabels.Length * bh + (_mainMenuLabels.Length - 1) * gap;
            int by = rp.Y + (int)(rp.Height * 0.53f) - total / 2;
            _mainMenuButtonRects = new Rectangle[_mainMenuLabels.Length];
            for (int i = 0; i < _mainMenuButtonRects.Length; i++)
                _mainMenuButtonRects[i] = new Rectangle(bx, by + i * (bh + gap), bw, bh);

            // Settings: label above each control row, Back at the page foot.
            int left = rp.X + (int)(rp.Width * 0.12f);
            int rowY = rp.Y + (int)(rp.Height * 0.30f);
            _settingsVolMinusRect = new Rectangle(left, rowY, 48, 52);
            _settingsVolCellRects = new Rectangle[10];
            int cellW = 30, cellGap = 6;
            for (int i = 0; i < 10; i++)
                _settingsVolCellRects[i] = new Rectangle(
                    left + 48 + 12 + i * (cellW + cellGap), rowY + 4, cellW, 44);
            _settingsVolPlusRect = new Rectangle(
                left + 48 + 12 + 10 * (cellW + cellGap) + 6, rowY, 48, 52);

            _settingsCrtToggleRect = new Rectangle(left, rp.Y + (int)(rp.Height * 0.52f), 120, 52);
            _settingsBackRect      = new Rectangle(bx, rp.Y + (int)(rp.Height * 0.74f), bw, bh);
        }
    }
}
