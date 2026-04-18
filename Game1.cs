using CatDetective.Entities;
using CatDetective.Map;
using CatDetective.Systems;
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
        // ── Internal resolution — 16:9 canvas (1136 × 16/9 ≈ 2020) ─────────────
        private const int   SCREEN_WIDTH   = 2020;
        private const int   SCREEN_HEIGHT  = 1136;
        // Original room art width; the art is centred inside the wider canvas.
        private const int   ROOM_WIDTH     = 1072;

        // ── Display scale — shrinks the OS window while keeping proportions ────
        // Change this one constant to resize the window (e.g. 0.5 for half size).
        private const float DISPLAY_SCALE  = 0.6f;
        private static readonly int WINDOW_WIDTH  = (int)(SCREEN_WIDTH  * DISPLAY_SCALE);
        private static readonly int WINDOW_HEIGHT = (int)(SCREEN_HEIGHT * DISPLAY_SCALE);

        // ── MonoGame core ──────────────────────────────────────────────────────
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch     _spriteBatch      = null!;
        private RenderTarget2D  _renderTarget     = null!;
        private Matrix          _cameraTransform;
        private Color           _ambientColor     = Color.Black;

        // ── Scene textures ─────────────────────────────────────────────────────
        private Texture2D _bgBase       = null!;
        private Texture2D _sunbeamsMask = null!;

        // ── Entities ───────────────────────────────────────────────────────────
        private Cat  _cat     = null!;
        private Prop _desk    = null!;
        private Prop _cabinet = null!;

        // ── World data from Tiled ──────────────────────────────────────────────
        private List<Rectangle>          _solidBoundaries = new();
        private List<InteractableEntity> _interactables   = new();

        // ── Active interactable (updated each frame for highlighting) ──────────
        private InteractableEntity? _activeInteractable;

        // ── Interaction system ─────────────────────────────────────────────────
        private Dictionary<string, InteractionData> _interactionDatabase = null!;
        private bool            _isDialogueActive;
        private InteractionData? _currentInteraction;

        // ── Notebook / inventory ───────────────────────────────────────────────
        private NotebookManager _notebook       = null!;
        private bool         _isNotebookOpen = false;
        private ClueCategory _selectedTab    = ClueCategory.Who;
        private MouseState   _prevMouseState;

        // ── Deduction board ────────────────────────────────────────────────────
        private DeductionManager _deduction              = null!;
        private bool             _isDeductionBoardOpen   = false;
        private int              _activeDropdownSlotIndex = -1;
        private bool             _isGameWon              = false;

        private static readonly Rectangle _solveButtonRect =
            new Rectangle(2020 - 160, 1136 - 160, 120, 120);
        private static readonly Rectangle _deductionPanelRect =
            new Rectangle(650, 340, 720, 620);
        private static readonly Rectangle _submitButtonRect =
            new Rectangle(860, 820, 300, 80);

        // Notebook UI rectangles — all in virtual 2020×1136 screen space.
        private static readonly Rectangle _notebookButtonRect =
            new Rectangle(2020 - 120, 20, 100, 100);
        private static readonly Rectangle _notebookPanelRect  =
            new Rectangle(2020 - 450, 140, 430, 950);
        // Four equal tabs across the top of the panel (107 px each).
        private static readonly Rectangle[] _tabRects = BuildTabRects();
        private static Rectangle[] BuildTabRects()
        {
            const int tabH = 60;
            int       tabW = _notebookPanelRect.Width / 4;   // 107
            int       y    = _notebookPanelRect.Y;
            return new[]
            {
                new Rectangle(_notebookPanelRect.X + 0 * tabW, y, tabW, tabH),
                new Rectangle(_notebookPanelRect.X + 1 * tabW, y, tabW, tabH),
                new Rectangle(_notebookPanelRect.X + 2 * tabW, y, tabW, tabH),
                new Rectangle(_notebookPanelRect.X + 3 * tabW, y, tabW, tabH),
            };
        }
        // One tint per tab category (index matches ClueCategory enum order).
        private static readonly Color[] _tabColors = new[]
        {
            new Color( 80, 120, 230),   // Who       — blue
            new Color( 80, 200, 100),   // What      — green
            new Color(230, 140,  40),   // Why       — orange
            new Color(160,  80, 220),   // WhereWhen — purple
        };
        private static readonly string[] _tabLabels =
            { "Who", "What", "Why", "Where/When" };

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

            float offsetX = (SCREEN_WIDTH - ROOM_WIDTH) / 2f;
            _cameraTransform = Matrix.CreateTranslation(offsetX, 0, 0);

            base.Initialize();
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void LoadContent()
        {
            _spriteBatch  = new SpriteBatch(GraphicsDevice);
            _renderTarget = new RenderTarget2D(GraphicsDevice, SCREEN_WIDTH, SCREEN_HEIGHT);

            // ── Scene textures ───────────────────────────────────────────────
            _bgBase       = Content.Load<Texture2D>("Levels/detective_office/bg_base");
            _sunbeamsMask = Content.Load<Texture2D>("Shared/mask_sunbeams");

            // ── Cat ──────────────────────────────────────────────────────────
            // Sprite sheet: 2100 × 700 px, 11 frames in a 6×2 grid.
            // Both directions use the forward sheet until the up-facing sheet is ready.
            var walkForward = Content.Load<Texture2D>("Shared/walk_animation_forward");
            var walkUpward  = Content.Load<Texture2D>("Shared/walk_animation_upward");
            var shadow      = Content.Load<Texture2D>("Shared/shadow_blob");

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
            var deskTex    = Content.Load<Texture2D>("Levels/detective_office/prop_desk");
            var cabinetTex = Content.Load<Texture2D>("Levels/detective_office/prop_cabinet");

            // ── Debug pixel ─────────────────────────────────────────────────
            _debugPixel = new Texture2D(GraphicsDevice, 1, 1);
            _debugPixel.SetData(new[] { Color.White });

            // ── Dialogue font ────────────────────────────────────────────────
            _dialogueFont = Content.Load<SpriteFont>("Shared/dialogue_font");

            // ── Level config (clues, interactables, deduction solution) ─────
            string levelConfigPath = Path.Combine(
                Content.RootDirectory, "Levels", "detective_office", "level_config.json");
            var levelConfig = LevelConfigParser.Load(levelConfigPath);

            _interactionDatabase = levelConfig.Interactables;
            _notebook            = new NotebookManager(levelConfig.Clues);
            _deduction           = new DeductionManager(levelConfig.DeductionSlots);

            // ── Scene config ─────────────────────────────────────────────────
            string configPath = Path.Combine(Content.RootDirectory, "scenes_config.json");
            _ambientColor = SceneConfigParser.GetAmbientColor(configPath, "detective_office");

            // ── Parse Tiled map ──────────────────────────────────────────────
            string mapPath = Path.Combine(
                Content.RootDirectory, "Levels", "detective_office", "room_map.json");
            MapParser.Parse(mapPath, Content, "detective_office", _interactionDatabase,
                out _solidBoundaries, out var triggers, out _interactables);

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

            // ── Notebook mouse input ──────────────────────────────────────────
            // Convert OS-window mouse coords to virtual 2020×1136 screen space.
            var mouseState = Mouse.GetState();
            if (mouseState.LeftButton == ButtonState.Pressed &&
                _prevMouseState.LeftButton == ButtonState.Released)
            {
                var vm = new Point(
                    (int)(mouseState.X / DISPLAY_SCALE),
                    (int)(mouseState.Y / DISPLAY_SCALE));

                if (!_isGameWon)
                {
                    if (_isDeductionBoardOpen)
                    {
                        if (_activeDropdownSlotIndex >= 0)
                        {
                            // Player is picking a clue from the dropdown.
                            var slot     = _deduction.Slots[_activeDropdownSlotIndex];
                            var filtered = _notebook.UnlockedClues.FindAll(c => c.Category == slot.Category);
                            bool hit     = false;
                            for (int i = 0; i < filtered.Count; i++)
                            {
                                var itemRect = new Rectangle(
                                    slot.Bounds.X, slot.Bounds.Bottom + i * 60,
                                    slot.Bounds.Width, 60);
                                if (itemRect.Contains(vm))
                                {
                                    slot.SelectedClueId      = filtered[i].Id;
                                    _activeDropdownSlotIndex = -1;
                                    hit = true;
                                    break;
                                }
                            }
                            if (!hit)
                                _activeDropdownSlotIndex = -1;
                        }
                        else
                        {
                            if (_submitButtonRect.Contains(vm))
                            {
                                if (_deduction.ValidateCase())
                                    _isGameWon = true;
                            }
                            else
                            {
                                bool hitSlot = false;
                                for (int i = 0; i < _deduction.Slots.Count; i++)
                                {
                                    if (_deduction.Slots[i].Bounds.Contains(vm))
                                    {
                                        _activeDropdownSlotIndex = i;
                                        hitSlot = true;
                                        break;
                                    }
                                }
                                // Click outside the panel closes the board.
                                if (!hitSlot && !_deductionPanelRect.Contains(vm))
                                    _isDeductionBoardOpen = false;
                            }
                        }
                    }
                    else
                    {
                        if (_solveButtonRect.Contains(vm))
                        {
                            _isDeductionBoardOpen = true;
                            _isNotebookOpen       = false;
                        }
                        else if (_notebookButtonRect.Contains(vm))
                        {
                            _isNotebookOpen = !_isNotebookOpen;
                        }
                        else if (_isNotebookOpen)
                        {
                            for (int i = 0; i < _tabRects.Length; i++)
                            {
                                if (_tabRects[i].Contains(vm))
                                {
                                    _selectedTab = (ClueCategory)i;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            _prevMouseState = mouseState;

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

                // Find the first interactable the cat overlaps (for highlight + input).
                _activeInteractable = null;
                foreach (var entity in _interactables)
                {
                    if (_cat.CollisionBox.Intersects(entity.TriggerZone))
                    {
                        _activeInteractable = entity;
                        break;
                    }
                }

                if (_activeInteractable != null && _cat.IsInteractPressed()
                    && _activeInteractable.Data != null)
                {
                    _currentInteraction = _activeInteractable.Data;
                    _isDialogueActive   = true;
                    foreach (var kw in _activeInteractable.Data.Keywords)
                        _notebook.UnlockClue(kw.Id);
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
            GraphicsDevice.Clear(_ambientColor);

            // ════════════════════════════════════════════════════════════════
            // PASS 1 — BASE BACKGROUND
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                transformMatrix: _cameraTransform);
            _spriteBatch.Draw(_bgBase, Vector2.Zero, Color.White);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 2 — BLOB SHADOW
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                transformMatrix: _cameraTransform);
            _cat.DrawShadow(_spriteBatch);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 3 — Y-SORTED ENTITIES
            // NonPremultiplied required: textures have straight alpha (see .mgcb),
            // and prop alpha modulation must fade cleanly without colour darkening.
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.NonPremultiplied,
                transformMatrix: _cameraTransform);
            _desk.Draw(_spriteBatch);
            _cabinet.Draw(_spriteBatch);
            _cat.Draw(_spriteBatch);
            foreach (var entity in _interactables)
                entity.Draw(_spriteBatch,
                    isHighlighted: entity == _activeInteractable,
                    totalSeconds:  gameTime.TotalGameTime.TotalSeconds);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 4 — LIGHTING / SUNBEAMS  (additive — El Mariachi trick)
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                transformMatrix: _cameraTransform);
            _spriteBatch.Draw(_sunbeamsMask, Vector2.Zero, Color.White);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 5 — DEBUG OVERLAY  (F1 to toggle)
            // Uses _cameraTransform so debug rectangles align with the art.
            // ════════════════════════════════════════════════════════════════
            if (_showDebug)
            {
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    transformMatrix: _cameraTransform);

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

            // ════════════════════════════════════════════════════════════════
            // PASS 7 — NOTEBOOK UI
            // AlphaBlend, NO camera transform — fixed to virtual screen coords.
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Solve button — bottom-right corner, always visible.
            if (!_isGameWon)
            {
                _spriteBatch.Draw(_debugPixel, _solveButtonRect, Color.DarkGoldenrod);
                var solveLbl  = "SOLVE";
                var solveSz   = _dialogueFont.MeasureString(solveLbl);
                _spriteBatch.DrawString(
                    _dialogueFont, solveLbl,
                    new Vector2(
                        _solveButtonRect.X + (_solveButtonRect.Width  - solveSz.X) * 0.5f,
                        _solveButtonRect.Y + (_solveButtonRect.Height - solveSz.Y) * 0.5f),
                    Color.White);
            }

            // Toggle button — always visible.
            _spriteBatch.Draw(_debugPixel, _notebookButtonRect, Color.DimGray);
            var btnLabel     = "Notes";
            var btnLabelSize = _dialogueFont.MeasureString(btnLabel);
            _spriteBatch.DrawString(
                _dialogueFont, btnLabel,
                new Vector2(
                    _notebookButtonRect.X + (_notebookButtonRect.Width  - btnLabelSize.X) * 0.5f,
                    _notebookButtonRect.Y + (_notebookButtonRect.Height - btnLabelSize.Y) * 0.5f),
                Color.White);

            if (_isNotebookOpen)
            {
                // Panel background — drawn below the tabs.
                var contentRect = new Rectangle(
                    _notebookPanelRect.X,
                    _notebookPanelRect.Y + 60,              // below tabs
                    _notebookPanelRect.Width,
                    _notebookPanelRect.Height - 60);
                _spriteBatch.Draw(_debugPixel, contentRect, Color.Black * 0.82f);

                // Tabs.
                for (int i = 0; i < _tabRects.Length; i++)
                {
                    bool active = (ClueCategory)i == _selectedTab;
                    // Active tab is taller (grows upward) to look "selected".
                    var tr = active
                        ? new Rectangle(_tabRects[i].X, _tabRects[i].Y - 8,
                                        _tabRects[i].Width, _tabRects[i].Height + 8)
                        : _tabRects[i];

                    _spriteBatch.Draw(_debugPixel, tr,
                        active ? _tabColors[i] : _tabColors[i] * 0.55f);

                    var labelSize = _dialogueFont.MeasureString(_tabLabels[i]);
                    _spriteBatch.DrawString(
                        _dialogueFont, _tabLabels[i],
                        new Vector2(
                            tr.X + (tr.Width  - labelSize.X) * 0.5f,
                            tr.Y + (tr.Height - labelSize.Y) * 0.5f),
                        Color.White);
                }

                // Clue list — filtered to active tab.
                const int CLUE_PADDING = 16;
                float cx = contentRect.X + CLUE_PADDING;
                float cy = contentRect.Y + CLUE_PADDING;
                float maxW = contentRect.Width - CLUE_PADDING * 2;
                float lineH = _dialogueFont.LineSpacing + 6;

                bool any = false;
                foreach (var clue in _notebook.UnlockedClues)
                {
                    if (clue.Category != _selectedTab) continue;
                    any = true;
                    // Bullet point.
                    _spriteBatch.DrawString(_dialogueFont, "* ", new Vector2(cx, cy), Color.LightGray);
                    float bulletW = _dialogueFont.MeasureString("* ").X;
                    // Word-wrap the display text.
                    cy = DrawWrappedString(_spriteBatch, _dialogueFont, clue.DisplayText,
                             new Vector2(cx + bulletW, cy),
                             maxW - bulletW, lineH,
                             Color.White);
                    cy += 4f;   // small gap between entries
                }

                if (!any)
                {
                    _spriteBatch.DrawString(
                        _dialogueFont, "- nothing yet -",
                        new Vector2(cx, cy), Color.Gray);
                }
            }

            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 8 — DEDUCTION BOARD / WIN STATE
            // AlphaBlend, NO camera transform — fixed to virtual screen coords.
            // ════════════════════════════════════════════════════════════════
            if (_isDeductionBoardOpen || _isGameWon)
            {
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                if (_isGameWon)
                {
                    _spriteBatch.Draw(_debugPixel,
                        new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT),
                        Color.Black * 0.85f);

                    const string banner      = "CASE CLOSED!";
                    const float  bannerScale = 3f;
                    var          bannerSz    = _dialogueFont.MeasureString(banner) * bannerScale;
                    var          bannerPos   = new Vector2(
                        (SCREEN_WIDTH  - bannerSz.X) * 0.5f,
                        (SCREEN_HEIGHT - bannerSz.Y) * 0.5f);

                    // Shadow
                    _spriteBatch.DrawString(_dialogueFont, banner,
                        bannerPos + new Vector2(6, 6), Color.Black * 0.8f,
                        0f, Vector2.Zero, bannerScale, SpriteEffects.None, 0f);
                    // Main text
                    _spriteBatch.DrawString(_dialogueFont, banner,
                        bannerPos, Color.Gold,
                        0f, Vector2.Zero, bannerScale, SpriteEffects.None, 0f);

                    const string sub    = "Press Escape to quit";
                    var          subSz  = _dialogueFont.MeasureString(sub);
                    _spriteBatch.DrawString(
                        _dialogueFont, sub,
                        new Vector2((SCREEN_WIDTH - subSz.X) * 0.5f, bannerPos.Y + bannerSz.Y + 20),
                        Color.LightGray);
                }
                else
                {
                    // Full-screen dim overlay
                    _spriteBatch.Draw(_debugPixel,
                        new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT),
                        Color.Black * 0.7f);

                    // Panel background
                    _spriteBatch.Draw(_debugPixel, _deductionPanelRect, new Color(15, 25, 55));

                    // Panel title
                    const string panelTitle = "DEDUCTION BOARD";
                    var          titleSz    = _dialogueFont.MeasureString(panelTitle);
                    _spriteBatch.DrawString(
                        _dialogueFont, panelTitle,
                        new Vector2(
                            _deductionPanelRect.X + (_deductionPanelRect.Width - titleSz.X) * 0.5f,
                            _deductionPanelRect.Y + 12),
                        Color.White);

                    // Instruction line
                    const string instr   = "Tap a slot to fill it, then submit your deduction.";
                    var          instrSz = _dialogueFont.MeasureString(instr);
                    _spriteBatch.DrawString(
                        _dialogueFont, instr,
                        new Vector2(
                            _deductionPanelRect.X + (_deductionPanelRect.Width - instrSz.X) * 0.5f,
                            _deductionPanelRect.Y + 12 + titleSz.Y + 4),
                        Color.LightGray);

                    // Slots
                    string[] catLabels = { "WHO", "WHAT", "WHY", "WHERE/WHEN" };
                    const int BADGE_W  = 130;
                    for (int i = 0; i < _deduction.Slots.Count; i++)
                    {
                        var slot      = _deduction.Slots[i];
                        var slotColor = _tabColors[(int)slot.Category];

                        // Slot background
                        _spriteBatch.Draw(_debugPixel, slot.Bounds, slotColor * 0.25f);

                        // Category badge (left strip)
                        var badgeRect = new Rectangle(
                            slot.Bounds.X, slot.Bounds.Y, BADGE_W, slot.Bounds.Height);
                        _spriteBatch.Draw(_debugPixel, badgeRect, slotColor);
                        var catLblSz = _dialogueFont.MeasureString(catLabels[(int)slot.Category]);
                        _spriteBatch.DrawString(
                            _dialogueFont, catLabels[(int)slot.Category],
                            new Vector2(
                                badgeRect.X + (badgeRect.Width  - catLblSz.X) * 0.5f,
                                badgeRect.Y + (badgeRect.Height - catLblSz.Y) * 0.5f),
                            Color.White);

                        // Clue text (right portion)
                        string slotText  = "[ TAP TO SELECT ]";
                        Color  textColor = Color.DimGray;
                        if (slot.SelectedClueId != null)
                        {
                            var found = _notebook.UnlockedClues.Find(c => c.Id == slot.SelectedClueId);
                            if (found != null)
                            {
                                slotText  = found.DisplayText;
                                textColor = Color.White;
                            }
                        }
                        var slotTextSz = _dialogueFont.MeasureString(slotText);
                        _spriteBatch.DrawString(
                            _dialogueFont, slotText,
                            new Vector2(
                                slot.Bounds.X + BADGE_W + 10,
                                slot.Bounds.Y + (slot.Bounds.Height - slotTextSz.Y) * 0.5f),
                            textColor);
                    }

                    // Validation message (above submit button)
                    if (!string.IsNullOrEmpty(_deduction.ValidationMessage))
                    {
                        var msgSz    = _dialogueFont.MeasureString(_deduction.ValidationMessage);
                        bool correct = _deduction.ValidationMessage == "Correct!";
                        _spriteBatch.DrawString(
                            _dialogueFont, _deduction.ValidationMessage,
                            new Vector2(
                                _submitButtonRect.X + (_submitButtonRect.Width  - msgSz.X) * 0.5f,
                                _submitButtonRect.Y - msgSz.Y - 10),
                            correct ? Color.LimeGreen : Color.OrangeRed);
                    }

                    // Submit button
                    _spriteBatch.Draw(_debugPixel, _submitButtonRect, new Color(20, 100, 40));
                    var submitLbl  = "SUBMIT";
                    var submitSz   = _dialogueFont.MeasureString(submitLbl);
                    _spriteBatch.DrawString(
                        _dialogueFont, submitLbl,
                        new Vector2(
                            _submitButtonRect.X + (_submitButtonRect.Width  - submitSz.X) * 0.5f,
                            _submitButtonRect.Y + (_submitButtonRect.Height - submitSz.Y) * 0.5f),
                        Color.White);

                    // Dropdown (drawn last so it sits on top of slots)
                    if (_activeDropdownSlotIndex >= 0)
                    {
                        var activeSlot = _deduction.Slots[_activeDropdownSlotIndex];
                        var filtered   = _notebook.UnlockedClues
                            .FindAll(c => c.Category == activeSlot.Category);

                        if (filtered.Count == 0)
                        {
                            var emptyRect = new Rectangle(
                                activeSlot.Bounds.X, activeSlot.Bounds.Bottom,
                                activeSlot.Bounds.Width, 60);
                            _spriteBatch.Draw(_debugPixel, emptyRect, new Color(50, 50, 60));
                            _spriteBatch.DrawString(
                                _dialogueFont, "No clues found yet.",
                                new Vector2(emptyRect.X + 12, emptyRect.Y + 10),
                                Color.Gray);
                        }
                        else
                        {
                            for (int i = 0; i < filtered.Count; i++)
                            {
                                var itemRect = new Rectangle(
                                    activeSlot.Bounds.X, activeSlot.Bounds.Bottom + i * 60,
                                    activeSlot.Bounds.Width, 60);
                                _spriteBatch.Draw(_debugPixel, itemRect,
                                    i % 2 == 0 ? new Color(25, 40, 80) : new Color(35, 52, 100));
                                var itemSz = _dialogueFont.MeasureString(filtered[i].DisplayText);
                                _spriteBatch.DrawString(
                                    _dialogueFont, filtered[i].DisplayText,
                                    new Vector2(
                                        itemRect.X + 12,
                                        itemRect.Y + (itemRect.Height - itemSz.Y) * 0.5f),
                                    Color.White);
                            }
                        }
                    }
                }

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
        /// Draws <paramref name="text"/> word-wrapped within <paramref name="maxWidth"/>
        /// starting at <paramref name="origin"/>.
        /// Returns the Y coordinate of the line AFTER the last drawn line.
        /// </summary>
        private float DrawWrappedString(
            SpriteBatch spriteBatch,
            SpriteFont  font,
            string      text,
            Vector2     origin,
            float       maxWidth,
            float       lineHeight,
            Color       color)
        {
            float x = origin.X, y = origin.Y;
            string[] words = text.Split(' ');
            foreach (var word in words)
            {
                float wordW = font.MeasureString(word).X;
                float spaceW = font.MeasureString(" ").X;
                if (x > origin.X && x + wordW > origin.X + maxWidth)
                {
                    x  = origin.X;
                    y += lineHeight;
                }
                spriteBatch.DrawString(font, word, new Vector2(x, y), color);
                x += wordW + spaceW;
            }
            return y + lineHeight;
        }

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
