using CatDetective.Entities;
using CatDetective.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.IO;

namespace CatDetective
{
    /// <summary>
    /// Root game class for the Cat Detective prototype.
    ///
    /// SCREEN RESOLUTION
    ///   1072 × 1136 — matches the art assets exactly so no scaling artefacts occur.
    ///
    /// RENDER PIPELINE (in order each frame):
    ///
    ///   Pass 1 — Base Background  (AlphaBlend, Deferred)
    ///     bg_base.png drawn at (0,0).
    ///
    ///   Pass 2 — Blob Shadow  (AlphaBlend, Deferred)
    ///     shadow_blob.png drawn under the cat's feet BEFORE the cat sprite,
    ///     so it composites underneath.
    ///
    ///   Pass 3 — Y-Sorted Entities  (NonPremultiplied, FrontToBack)
    ///     Cat and props drawn together, sorted by floor-Y depth.
    ///     Props are full-room overlays (same size as background) drawn at (0,0);
    ///     their LayerDepth is derived from a logical sortY, not their draw position.
    ///     Prop alpha lerps to 0.4 when the cat walks behind them.
    ///
    ///   Pass 4 — Lighting / Sunbeams  (Additive, Deferred)
    ///     mask_sunbeams.png drawn at (0,0). Additive blend washes out the scene
    ///     naturally where beams hit — no shaders needed (GDD "El Mariachi" trick).
    /// </summary>
    public class Game1 : Game
    {
        // ── Internal resolution — matches bg_base.png / prop overlay dimensions ─
        private const int   SCREEN_WIDTH   = 1072;
        private const int   SCREEN_HEIGHT  = 1136;

        // ── Display scale — shrinks the OS window while keeping proportions ────
        // Change this one constant to resize the window (e.g. 0.5 for half size).
        private const float DISPLAY_SCALE  = 0.6f;
        private static readonly int WINDOW_WIDTH  = (int)(SCREEN_WIDTH  * DISPLAY_SCALE);
        private static readonly int WINDOW_HEIGHT = (int)(SCREEN_HEIGHT * DISPLAY_SCALE);

        // ── MonoGame core ──────────────────────────────────────────────────────
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch     _spriteBatch   = null!;
        private RenderTarget2D  _renderTarget  = null!;

        // ── Scene textures ─────────────────────────────────────────────────────
        private Texture2D _bgBase       = null!;
        private Texture2D _sunbeamsMask = null!;

        // ── Entities ───────────────────────────────────────────────────────────
        private Cat  _cat     = null!;
        private Prop _desk    = null!;
        private Prop _cabinet = null!;

        // ── World data from Tiled ──────────────────────────────────────────────
        private List<Rectangle>                   _solidBoundaries = new();
        private List<(string Name, Rectangle Rect)> _interactables = new();

        // ── Interaction system ─────────────────────────────────────────────────
        private Dictionary<string, InteractionData> _interactionDatabase = null!;
        private bool            _isDialogueActive;
        private InteractionData? _currentInteraction;
        private List<string>    _collectedClues = new();

        // ── UI ─────────────────────────────────────────────────────────────────
        private SpriteFont _dialogueFont = null!;

        // ── Debug overlay ──────────────────────────────────────────────────────
        private Texture2D    _debugPixel  = null!;
        private bool         _showDebug   = true;   // F1 toggles
        private KeyboardState _prevKbState;

        // ══════════════════════════════════════════════════════════════════════
        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth  = WINDOW_WIDTH,
                PreferredBackBufferHeight = WINDOW_HEIGHT,
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Initialize()
        {
            GameObject.SetScreenHeight(SCREEN_HEIGHT);
            base.Initialize();
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void LoadContent()
        {
            _spriteBatch  = new SpriteBatch(GraphicsDevice);
            _renderTarget = new RenderTarget2D(GraphicsDevice, SCREEN_WIDTH, SCREEN_HEIGHT);

            // ── Scene textures ───────────────────────────────────────────────
            _bgBase       = Content.Load<Texture2D>("bg_base");
            _sunbeamsMask = Content.Load<Texture2D>("mask_sunbeams");

            // ── Cat ──────────────────────────────────────────────────────────
            // Sprite sheet: 2100 × 700 px, 11 frames in a 6×2 grid.
            // Both directions use the forward sheet until the up-facing sheet is ready.
            var walkForward = Content.Load<Texture2D>("walk_animation_forward");
            var walkUpward  = Content.Load<Texture2D>("walk_animation_upward");
            var shadow      = Content.Load<Texture2D>("shadow_blob");

            // Start position: centre-X, lower floor area.
            // Adjust Y once the room art is reviewed in-engine.
            _cat = new Cat(walkForward, walkUpward, startPosition: new Vector2(536, 900),
                           frameCount: 12, columns: 6, rows: 2)
            {
                ShadowTexture = shadow
            };

            // ── Props ────────────────────────────────────────────────────────
            // Full-room overlay PNGs: same 1072 × 1136 canvas as bg_base.
            // sortY is the approximate Y of each prop's floor contact point within
            // the image. Tune these values after reviewing the art in-engine.
            var deskTex    = Content.Load<Texture2D>("prop_desk");
            var cabinetTex = Content.Load<Texture2D>("prop_cabinet");

            // ── Debug pixel ─────────────────────────────────────────────────
            _debugPixel = new Texture2D(GraphicsDevice, 1, 1);
            _debugPixel.SetData(new[] { Color.White });

            // ── Dialogue font ────────────────────────────────────────────────
            _dialogueFont = Content.Load<SpriteFont>("dialogue_font");

            // ── Interaction database (hardcoded; swap for data file later) ───
            // [bracket] tokens in Text are tinted using the matching Keyword.Color.
            _interactionDatabase = new Dictionary<string, InteractionData>
            {
                { "inspect_answering_machine", new InteractionData(
                    "BEEP. 'Hey, it's Lance's manager. We need you at the house " +
                    "[the day before the premiere]! Don't be late!' " +
                    "BEEP. 'Hi honey, don't forget my birthday on [Sunday]!'",
                    new[] {
                        new Keyword("the day before the premiere", "the_day_before_the_premiere", InteractionData.Plot),
                        new Keyword("Sunday",                      "sunday",                      InteractionData.Plot),
                    }) },

                { "inspect_takeout_bag", new InteractionData(
                    "Smells like leftover [Fish Tacos]. The delivery receipt says it " +
                    "was brought to the [Downtown Office].",
                    new[] {
                        new Keyword("Fish Tacos",      "fish_tacos",      InteractionData.Misc),
                        new Keyword("Downtown Office", "downtown_office", InteractionData.Plot),
                    }) },

                { "inspect_calendar", new InteractionData(
                    "The month is almost over. The detective has circled [Thursday] " +
                    "for the dentist, and [Friday] is marked with a giant question mark.",
                    new[] {
                        new Keyword("Thursday", "thursday", InteractionData.Plot),
                        new Keyword("Friday",   "friday",   InteractionData.Plot),
                    }) },

                { "inspect_newspaper", new InteractionData(
                    "'Pirate Cove 3' premieres this [Saturday]! But production halts " +
                    "as Lance's prized macaw, Rudebeak, is reported as a [stolen pet]!",
                    new[] {
                        new Keyword("Saturday",   "saturday",   InteractionData.Plot),
                        new Keyword("stolen pet", "stolen_pet", InteractionData.Crime),
                    }) },

                { "inspect_trash_can", new InteractionData(
                    "A bunch of unpaid bills, and a shiny VIP parking pass for a [Malibu Mansion].",
                    new[] {
                        new Keyword("Malibu Mansion", "malibu_mansion", InteractionData.Plot),
                    }) },
            };

            // ── Parse Tiled map ──────────────────────────────────────────────
            string mapPath = Path.Combine(Content.RootDirectory, "room_map.json");
            MapParser.Parse(mapPath, out _solidBoundaries, out var triggers, out _interactables);

            // ── Wire trigger zones to props ───────────────────────────────────
            var deskTrigger    = new Rectangle(230, 490, 280, 210); // fallback
            var cabinetTrigger = new Rectangle(590, 340, 220, 200); // fallback

            foreach (var (name, rect) in triggers)
            {
                var lower = name.ToLowerInvariant();
                if (lower.Contains("desk"))    deskTrigger    = rect;
                if (lower.Contains("cabinet")) cabinetTrigger = rect;
            }

            // sortY values: approximate Y of each prop's floor contact in the
            // full-room overlay image. Tune after reviewing in-engine.
            _desk    = new Prop(deskTex,    sortY: 750f, deskTrigger);
            _cabinet = new Prop(cabinetTex, sortY: 580f, cabinetTrigger);
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Update(GameTime gameTime)
        {
            var kbState = Keyboard.GetState();
            if (kbState.IsKeyDown(Keys.Escape))
                Exit();

            // Toggle debug overlay with F1 (on key-down, not held)
            if (kbState.IsKeyDown(Keys.F1) && !_prevKbState.IsKeyDown(Keys.F1))
                _showDebug = !_showDebug;
            _prevKbState = kbState;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_isDialogueActive)
            {
                // Consume the interact check so the key-state advances in Cat.
                // If Space is pressed while dialogue is open, dismiss it.
                if (_cat.IsInteractPressed())
                    _isDialogueActive = false;
            }
            else
            {
                _cat.Update(gameTime);
                _cat.MoveWithCollision(dt, _solidBoundaries);

                // Check whether the cat stepped into an interactable zone.
                var zone = _cat.GetActiveInteractionZone(_interactables);
                if (zone != null && _cat.IsInteractPressed())
                {
                    if (_interactionDatabase.TryGetValue(zone.Value.Name, out var data))
                    {
                        _currentInteraction = data;
                        _isDialogueActive   = true;

                        // Collect any new clue IDs.
                        foreach (var kw in data.Keywords)
                            if (!_collectedClues.Contains(kw.Id))
                                _collectedClues.Add(kw.Id);
                    }
                }
            }

            _desk.CheckFadeTrigger(_cat.CollisionBox);
            _cabinet.CheckFadeTrigger(_cat.CollisionBox);
            _desk.Update(gameTime);
            _cabinet.Update(gameTime);

            base.Update(gameTime);
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Draw(GameTime gameTime)
        {
            // ── Render all passes into the full-resolution render target ─────
            GraphicsDevice.SetRenderTarget(_renderTarget);
            GraphicsDevice.Clear(Color.Black);

            // ════════════════════════════════════════════════════════════════
            // PASS 1 — BASE BACKGROUND
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _spriteBatch.Draw(_bgBase, Vector2.Zero, Color.White);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 2 — BLOB SHADOW
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _cat.DrawShadow(_spriteBatch);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 3 — Y-SORTED ENTITIES
            // NonPremultiplied required: textures have straight alpha (see .mgcb),
            // and prop alpha modulation must fade cleanly without colour darkening.
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.NonPremultiplied);
            _desk.Draw(_spriteBatch);
            _cabinet.Draw(_spriteBatch);
            _cat.Draw(_spriteBatch);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 4 — LIGHTING / SUNBEAMS  (additive — El Mariachi trick)
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
            _spriteBatch.Draw(_sunbeamsMask, Vector2.Zero, Color.White);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 5 — DEBUG OVERLAY  (F1 to toggle)
            // AlphaBlend + Deferred; drawn last so it sits on top of everything.
            // ════════════════════════════════════════════════════════════════
            if (_showDebug)
            {
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                // Collision walls — red
                foreach (var wall in _solidBoundaries)
                    DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, wall, Color.Red);

                // Cat feet collision box — cyan
                DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, _cat.CollisionBox, Color.Cyan);

                _spriteBatch.End();
            }

            // ════════════════════════════════════════════════════════════════
            // PASS 6 — DIALOGUE UI
            // AlphaBlend, drawn last so it overlays everything.
            // ════════════════════════════════════════════════════════════════
            if (_isDialogueActive && _currentInteraction != null)
            {
                const int BOX_PADDING  = 20;
                const int BOX_HEIGHT   = 130;
                const int BOX_MARGIN   = 16;
                var boxRect = new Rectangle(
                    BOX_MARGIN,
                    SCREEN_HEIGHT - BOX_HEIGHT - BOX_MARGIN,
                    SCREEN_WIDTH - BOX_MARGIN * 2,
                    BOX_HEIGHT);

                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                // Semi-transparent dark background panel.
                _spriteBatch.Draw(_debugPixel, boxRect, Color.Black * 0.75f);

                // Rich-text dialogue — [keywords] are tinted by their Keyword.Color.
                DrawRichText(
                    _spriteBatch,
                    _dialogueFont,
                    _currentInteraction.Text,
                    _currentInteraction.Keywords,
                    new Vector2(boxRect.X + BOX_PADDING, boxRect.Y + BOX_PADDING),
                    boxRect.Width - BOX_PADDING * 2);

                // "Press Space to continue" hint, bottom-right of the box.
                const string hint     = "[ Space ] to continue";
                var          hintSize = _dialogueFont.MeasureString(hint);
                _spriteBatch.DrawString(
                    _dialogueFont,
                    hint,
                    new Vector2(
                        boxRect.Right  - BOX_PADDING - hintSize.X,
                        boxRect.Bottom - BOX_PADDING - hintSize.Y),
                    Color.LightGray);

                _spriteBatch.End();
            }

            // ── Blit render target to the OS window at display scale ─────────
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            _spriteBatch.Draw(
                _renderTarget,
                new Rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT),
                Color.White);
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Splits <paramref name="text"/> into plain/coloured spans by scanning for
        /// <c>[bracket]</c> tokens and matching them against <paramref name="keywords"/>.
        /// Returns a list of <c>(string segment, Color color)</c> in source order.
        /// </summary>
        private static List<(string Text, Color Color)> ParseSpans(
            string text, Keyword[] keywords)
        {
            var result = new List<(string, Color)>();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('[', i);
                if (open == -1)
                {
                    result.Add((text[i..], Color.White));
                    break;
                }
                if (open > i)
                    result.Add((text[i..open], Color.White));

                int close = text.IndexOf(']', open + 1);
                if (close == -1)
                {
                    result.Add((text[open..], Color.White));
                    break;
                }

                string bracketText = text[(open + 1)..close];
                var kwColor = Color.White;
                foreach (var kw in keywords)
                {
                    if (string.Equals(kw.DisplayText, bracketText,
                            System.StringComparison.OrdinalIgnoreCase))
                    {
                        kwColor = kw.Color;
                        break;
                    }
                }
                result.Add((bracketText, kwColor));
                i = close + 1;
            }
            return result;
        }

        /// <summary>
        /// Draws <paramref name="text"/> word-wrapped to <paramref name="maxWidth"/>,
        /// colouring any <c>[bracketed]</c> tokens using their matching
        /// <see cref="Keyword.Color"/>.
        /// </summary>
        private void DrawRichText(
            SpriteBatch spriteBatch,
            SpriteFont  font,
            string      text,
            Keyword[]   keywords,
            Vector2     origin,
            float       maxWidth)
        {
            var   spans   = ParseSpans(text, keywords);
            float x       = origin.X;
            float y       = origin.Y;
            float lineH   = font.LineSpacing;

            foreach (var (spanText, color) in spans)
            {
                // Walk through the span as alternating runs of spaces / non-spaces
                // so we can measure and wrap word by word while keeping inter-word
                // spacing tight rather than adding a hard space prefix each time.
                int pos = 0;
                while (pos < spanText.Length)
                {
                    bool isSpace = spanText[pos] == ' ';
                    int  start   = pos;
                    while (pos < spanText.Length && (spanText[pos] == ' ') == isSpace)
                        pos++;
                    string token = spanText[start..pos];
                    float  tokenW = font.MeasureString(token).X;

                    if (isSpace)
                    {
                        // Apply spacing only when not at the start of a fresh line.
                        if (x > origin.X) x += tokenW;
                    }
                    else
                    {
                        // Wrap before the word if it would overflow.
                        if (x > origin.X && x + tokenW > origin.X + maxWidth)
                        {
                            x  = origin.X;
                            y += lineH;
                        }
                        spriteBatch.DrawString(font, token, new Vector2(x, y), color);
                        x += tokenW;
                    }
                }
            }
        }
    }
}
