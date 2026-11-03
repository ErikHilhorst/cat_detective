using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CatDetective
{
    /// <summary>
    /// Main menu + settings, staged in Dikkie's sunny study (Shared/menu_room,
    /// menu_logo, menu_cat - sources in "art trials/menu"):
    ///   TITLE SPLASH (boot only): the room, the gold "Dikkie Detective" logo and
    ///     Dikkie asleep on his paw-print blanket on the desk, gently breathing,
    ///     with drifting "z"s and dust motes in the window light. Any key / click
    ///     / tap dismisses it (on web that gesture also starts the music).
    ///   MENU: over MENU_REVEAL_SECONDS the logo and cat glide to the left half
    ///     and a parchment panel slides in from the right holding the buttons.
    ///   SETTINGS: same scene, the panel holds the settings controls.
    /// Menus always run at the fixed MENU_CANVAS (1456x816).
    /// </summary>
    public partial class Game1
    {
        private const string GAME_TAGLINE        = "the casebook of a very curious cat";
        private const float  MENU_REVEAL_SECONDS = 0.8f;

        private static readonly Color MenuAccentInk = new(150, 58, 42);   // red ink accents
        private static readonly Color MenuCream     = new(255, 244, 220);

        /// <summary>The parchment panel that holds the menu / settings controls.</summary>
        private Rectangle RightPage => new(
            (int)(SCREEN_WIDTH * 0.585f), (int)(SCREEN_HEIGHT * 0.07f),
            (int)(SCREEN_WIDTH * 0.375f), (int)(SCREEN_HEIGHT * 0.86f));

        private static float EaseInOut(float t) => t * t * (3f - 2f * t);

        /// <summary>Reveal progress 0 (title splash) -> 1 (menu), eased.</summary>
        private float MenuReveal => _titleSplash ? 0f : EaseInOut(Math.Clamp(_menuRevealT, 0f, 1f));

        /// <summary>Panel slide offset: off-screen right at reveal 0, home at 1.</summary>
        private int MenuPanelDx => (int)((1f - MenuReveal) * (SCREEN_WIDTH - RightPage.X + 40));

        private static Vector2 LerpV(Vector2 a, Vector2 b, float t) => a + (b - a) * t;

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
        /// The study scene shared by splash, menu and settings: room, dust motes,
        /// sleeping Dikkie, "z"s, logo, and (as the reveal runs) the parchment
        /// panel. Leaves an AlphaBlend batch OPEN for the panel's content.
        /// </summary>
        private void BeginMenuScene()
        {
            GraphicsDevice.Clear(new Color(40, 28, 20));
            float e = MenuReveal;
            float t = _uiTime;

            // ── Room, cover-fitted (keeps the desk edge in view) ──────────────
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            float k  = Math.Max(SCREEN_WIDTH / (float)_menuRoomTex.Width, SCREEN_HEIGHT / (float)_menuRoomTex.Height);
            int   vw = (int)(SCREEN_WIDTH / k), vh = (int)(SCREEN_HEIGHT / k);
            var   src = new Rectangle((_menuRoomTex.Width - vw) / 2, (int)((_menuRoomTex.Height - vh) * 0.8f), vw, vh);
            _spriteBatch.Draw(_menuRoomTex, new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT), src, Color.White);

            // Dust motes drifting up through the window light (left half).
            var dotSrc = new Rectangle(0, 0, ROUND_SIZE, ROUND_SIZE);
            for (int i = 0; i < 16; i++)
            {
                float fx    = Hash01(i * 12.9898f);
                float fy    = Hash01(i * 78.233f);
                float drift = (t * (0.010f + 0.006f * fy) + fx) % 1f;
                float x = SCREEN_WIDTH  * (0.06f + 0.42f * fx) + 30f * MathF.Sin(t * 0.4f + i);
                float y = SCREEN_HEIGHT * (0.75f - 0.65f * drift);
                float a = 0.35f * MathF.Sin(MathF.PI * drift);
                int   r = 3 + i % 3;
                _spriteBatch.Draw(_uiRound, new Rectangle((int)x, (int)y, r, r), dotSrc,
                    new Color(255, 236, 180) * a);
            }

            // Soft warm shade once the menu is up, so the parchment pops off the busy wall.
            if (e > 0f)
                _spriteBatch.Draw(_debugPixel, new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT),
                    new Color(30, 18, 8) * (0.22f * e));
            _spriteBatch.End();

            // ── Dikkie asleep on his blanket + the logo (straight alpha) ──────
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp);
            float catW    = MathHelper.Lerp(SCREEN_WIDTH * 0.46f, SCREEN_WIDTH * 0.42f, e);
            float catS    = catW / _menuCatTex.Width;
            var   catFeet = LerpV(new Vector2(SCREEN_WIDTH * 0.50f, SCREEN_HEIGHT + 6f),
                                  new Vector2(SCREEN_WIDTH * 0.30f, SCREEN_HEIGHT + 6f), e);
            float breathe = 0.010f * MathF.Sin(t * 1.6f);
            _spriteBatch.Draw(_menuCatTex, catFeet, null, Color.White, 0f,
                new Vector2(_menuCatTex.Width * 0.5f, _menuCatTex.Height),
                new Vector2(catS * (1f + breathe * 0.35f), catS * (1f + breathe)),
                SpriteEffects.None, 0f);

            // Logo: big and centred on the splash, upper-left in the menu.
            float logoW   = MathHelper.Lerp(SCREEN_WIDTH * 0.60f, SCREEN_WIDTH * 0.42f, e);
            float logoS   = logoW / _menuLogoTex.Width;
            var   logoTop = LerpV(new Vector2(SCREEN_WIDTH * 0.50f, SCREEN_HEIGHT * 0.05f),
                                  new Vector2(SCREEN_WIDTH * 0.30f, SCREEN_HEIGHT * 0.07f), e);
            float bob = 3f * MathF.Sin(t * 1.1f);
            _spriteBatch.Draw(_menuLogoTex, logoTop + new Vector2(0, bob), null, Color.White, 0f,
                new Vector2(_menuLogoTex.Width * 0.5f, 0f), logoS, SpriteEffects.None, 0f);
            _spriteBatch.End();

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // "z z z" drifting up from the sleeping head.
            var head = catFeet + new Vector2(-0.04f * catW, -0.86f * _menuCatTex.Height * catS);
            for (int i = 0; i < 3; i++)
            {
                float p  = (t * 0.32f + i / 3f) % 1f;
                float a  = MathF.Sin(MathF.PI * p);
                float sc = 0.55f + 0.55f * p;
                var  pos = head + new Vector2(p * 70f + 8f * MathF.Sin(t * 2f + i), -p * 70f);
                _spriteBatch.DrawString(_dialogueFont, "z", pos + new Vector2(2, 2), UiInk * (0.5f * a),
                    0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_dialogueFont, "z", pos, MenuCream * a,
                    0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
            }

            // Splash prompt, fading out as the menu comes in.
            if (e < 1f)
            {
                string prompt = _pointer.LastWasTouch ? "Tap to wake Dikkie" : "Click or press any key";
                float  pulse  = 0.92f + 0.08f * MathF.Sin(t * 3f);
                DrawNotePill(prompt, SCREEN_HEIGHT * 0.475f, (1f - e) * pulse);
            }

            // Parchment panel sliding in from the right.
            if (e > 0f)
            {
                var rp = RightPage; rp.X += MenuPanelDx;
                DrawParchment(rp);
            }
        }

        /// <summary>Cheap deterministic 0..1 hash (fixed mote layout, no Random state).</summary>
        private static float Hash01(float seed)
        {
            float v = MathF.Sin(seed) * 43758.5453f;
            return v - MathF.Floor(v);
        }

        /// <summary>Small faded-ink note centred near the bottom of the panel.</summary>
        private void DrawRightPageNote(string text, Color color, float fromBottom = 0.07f)
        {
            var rp = RightPage; rp.X += MenuPanelDx;
            float scale = Math.Min(0.56f, rp.Width * 0.86f / _dialogueFont.MeasureString(text).X);
            DrawCentered(text, rp.Center.X, rp.Bottom - rp.Height * fromBottom - 18f, scale, color);
        }

        private void DrawMainMenu()
        {
            BeginMenuScene();
            if (_titleSplash) { _spriteBatch.End(); return; }

            int dx = MenuPanelDx;
            var rp = RightPage; rp.X += dx;
            DrawCentered("Contents", rp.Center.X, _mainMenuButtonRects[0].Y - 108f, 0.85f, UiInk);
            DrawCentered(GAME_TAGLINE, rp.Center.X, _mainMenuButtonRects[0].Y - 60f, 0.5f, UiInk * 0.65f);
            DrawInkRule(rp.Center.X, _mainMenuButtonRects[0].Y - 18f, rp.Width * 0.30f);

            for (int i = 0; i < _mainMenuButtonRects.Length; i++)
            {
                bool enabled  = IsMenuItemEnabled(i);
                bool selected = i == _menuIndex && enabled && _menuRevealT >= 1f;
                var  r        = _mainMenuButtonRects[i]; r.X += dx;
                DrawUiButton(r, selected ? UiYellow : UiCream,
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

            // Dev hint: tiny, bottom-right corner.
            const string devHint = "F12 dev";
            var dSz = _dialogueFont.MeasureString(devHint) * 0.45f;
            _spriteBatch.DrawString(_dialogueFont, devHint,
                new Vector2(SCREEN_WIDTH - dSz.X - 10f, SCREEN_HEIGHT - dSz.Y - 6f),
                new Color(235, 220, 190) * 0.45f, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

            _spriteBatch.End();
        }

        private void DrawSettingsScreen()
        {
            BeginMenuScene();

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
        /// Menu + settings layout inside the parchment panel (called from
        /// UpdateLayout; menus always run at the fixed menu canvas).
        /// </summary>
        private void LayoutMenuScreens()
        {
            var rp = RightPage;
            int bw = (int)(rp.Width * 0.62f), bh = 56, gap = 16;
            int bx = rp.Center.X - bw / 2;
            int total = _mainMenuLabels.Length * bh + (_mainMenuLabels.Length - 1) * gap;
            int by = rp.Y + (int)(rp.Height * 0.57f) - total / 2;
            _mainMenuButtonRects = new Rectangle[_mainMenuLabels.Length];
            for (int i = 0; i < _mainMenuButtonRects.Length; i++)
                _mainMenuButtonRects[i] = new Rectangle(bx, by + i * (bh + gap), bw, bh);

            // Settings: label above each control row, Back at the panel foot.
            int left = rp.X + (int)(rp.Width * 0.08f);
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
