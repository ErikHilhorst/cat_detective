using CatDetective.Entities;
using CatDetective.Map;
using CatDetective.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;

namespace CatDetective
{
    /// <summary>
    /// Root game class for the Cat Detective prototype.
    ///
    /// SCREEN RESOLUTION
    ///   2020 × 1136 — matches the art assets exactly so no scaling artefacts occur.
    ///
    /// RENDER PIPELINE (in order each frame, Playing state only):
    ///
    ///   Pass 1 — Base Background  (AlphaBlend, Deferred)
    ///   Pass 2 — Blob Shadow      (AlphaBlend, Deferred)
    ///   Pass 3 — Y-Sorted Entities (NonPremultiplied, FrontToBack)
    ///   Pass 4 — Lighting / Sunbeams (Additive, Deferred)
    ///   Pass 5 — Debug overlay (F1)
    ///   Pass 6 — Dialogue UI
    ///   Pass 7 — Notebook UI
    ///   Pass 8 — Deduction board / win state
    /// </summary>
    public class Game1 : Game
    {
        // ── Internal resolution ────────────────────────────────────────────────
        private const int   SCREEN_WIDTH   = 2020;
        private const int   SCREEN_HEIGHT  = 1136;
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
        private Cat        _cat             = null!;
        private List<Prop> _foregroundProps = new();

        // ── World data from Tiled ──────────────────────────────────────────────
        private List<Rectangle>          _solidBoundaries = new();
        private List<InteractableEntity> _interactables   = new();

        // ── Active interactable (updated each frame for highlighting) ──────────
        private InteractableEntity? _activeInteractable;

        // ── Interaction system ─────────────────────────────────────────────────
        private Dictionary<string, InteractionData> _interactionDatabase = null!;
        private bool             _isDialogueActive;
        private InteractionData? _currentInteraction;

        // ── Notebook / inventory ───────────────────────────────────────────────
        private NotebookManager _notebook       = null!;
        private bool         _isNotebookOpen = false;
        private ClueCategory _selectedTab    = ClueCategory.Who;
        private MouseState   _prevMouseState;

        // ── Deduction board ────────────────────────────────────────────────────
        private DeductionManager _deduction               = null!;
        private bool             _isDeductionBoardOpen    = false;
        private int              _activeDropdownSlotIndex = -1;
        private bool             _isGameWon               = false;

        private static readonly Rectangle _solveButtonRect =
            new Rectangle(2020 - 160, 1136 - 160, 120, 120);
        private static readonly Rectangle _deductionPanelRect =
            new Rectangle(650, 340, 720, 620);
        private static readonly Rectangle _submitButtonRect =
            new Rectangle(860, 820, 300, 80);

        // Notebook UI rectangles — all in virtual 2020×1136 screen space.
        private static readonly Rectangle _notebookButtonRect =
            new Rectangle(2020 - 120, 20, 100, 100);
        private static readonly Rectangle _notebookPanelRect =
            new Rectangle(2020 - 450, 140, 430, 950);
        private static readonly Rectangle[] _tabRects = BuildTabRects();
        private static Rectangle[] BuildTabRects()
        {
            const int tabH = 60;
            int       tabW = _notebookPanelRect.Width / 4;
            int       y    = _notebookPanelRect.Y;
            return new[]
            {
                new Rectangle(_notebookPanelRect.X + 0 * tabW, y, tabW, tabH),
                new Rectangle(_notebookPanelRect.X + 1 * tabW, y, tabW, tabH),
                new Rectangle(_notebookPanelRect.X + 2 * tabW, y, tabW, tabH),
                new Rectangle(_notebookPanelRect.X + 3 * tabW, y, tabW, tabH),
            };
        }
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
        private Texture2D     _debugPixel  = null!;
        private bool          _showDebug   = true;   // F1 toggles
        private KeyboardState _prevKbState;

        // ── Hot-reload (level_config.json) ─────────────────────────────────────
        private string   _levelConfigSourcePath = "";
        private DateTime _levelConfigLastWrite;
        private float    _hotReloadTimer;

        // ── Game state / scene selection ───────────────────────────────────────
        private enum GameState { DevMenu, Playing }
        private GameState    _currentState    = GameState.DevMenu;
        private List<string> _availableScenes = new();

        // ── Shared textures (loaded once, reused across scenes) ───────────────
        private Texture2D _walkForwardTex = null!;
        private Texture2D _walkUpwardTex  = null!;
        private Texture2D _shadowTex      = null!;

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

            // Shared sprite sheets — loaded once, passed into Cat on each LoadScene.
            _walkForwardTex = Content.Load<Texture2D>("Shared/walk_animation_forward");
            _walkUpwardTex  = Content.Load<Texture2D>("Shared/walk_animation_upward");
            _shadowTex      = Content.Load<Texture2D>("Shared/shadow_blob");

            _debugPixel = new Texture2D(GraphicsDevice, 1, 1);
            _debugPixel.SetData(new[] { Color.White });

            _dialogueFont = Content.Load<SpriteFont>("Shared/dialogue_font");

            string configPath = Path.Combine(Content.RootDirectory, "scenes_config.json");
            _availableScenes = SceneConfigParser.GetAvailableScenes(configPath);
        }

        // ══════════════════════════════════════════════════════════════════════
        private void LoadScene(string sceneId)
        {
            // Reset per-scene state.
            _foregroundProps.Clear();
            _solidBoundaries.Clear();
            _interactables.Clear();
            _isDialogueActive        = false;
            _currentInteraction      = null;
            _isNotebookOpen          = false;
            _isDeductionBoardOpen    = false;
            _activeDropdownSlotIndex = -1;
            _isGameWon               = false;
            _hotReloadTimer          = 0f;

            // Scene-specific textures.
            _bgBase       = Content.Load<Texture2D>($"Levels/{sceneId}/bg_base");
            _sunbeamsMask = Content.Load<Texture2D>("Shared/mask_sunbeams");

            float offsetX = (SCREEN_WIDTH - _bgBase.Width) / 2f;
            _cameraTransform = Matrix.CreateTranslation(offsetX, 0, 0);

            // Level config (clues, interactables, deduction solution).
            string levelConfigPath = Path.Combine(
                Content.RootDirectory, "Levels", sceneId, "level_config.json");
            var levelConfig = LevelConfigParser.Load(levelConfigPath);

            _interactionDatabase = levelConfig.Interactables;
            _notebook            = new NotebookManager(levelConfig.Clues);
            _deduction           = new DeductionManager(levelConfig.DeductionSlots);

            _levelConfigSourcePath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "Content", "Levels", sceneId, "level_config.json"));
            _levelConfigLastWrite  = File.Exists(_levelConfigSourcePath)
                ? File.GetLastWriteTime(_levelConfigSourcePath)
                : DateTime.MinValue;

            // Ambient color from scenes_config.json.
            string configPath = Path.Combine(Content.RootDirectory, "scenes_config.json");
            _ambientColor = SceneConfigParser.GetAmbientColor(configPath, sceneId);

            // Parse Tiled map.
            string mapPath = Path.Combine(
                Content.RootDirectory, "Levels", sceneId, "room_map.json");
            MapParser.Parse(mapPath, Content, sceneId, _interactionDatabase,
                out _solidBoundaries, out var triggers, out _interactables,
                out Vector2? spawnPoint);

            Vector2 catStart = spawnPoint ?? new Vector2(500, 500);
            _cat = new Cat(_walkForwardTex, _walkUpwardTex, startPosition: catStart,
                           frameCount: 12, columns: 6, rows: 2)
            {
                ShadowTexture = _shadowTex
            };

            // Build foreground props from level config.
            foreach (var propConfig in levelConfig.Props)
            {
                var tex = Content.Load<Texture2D>($"Levels/{sceneId}/" + propConfig.Texture);

                var triggerRect = Rectangle.Empty;
                foreach (var (name, rect) in triggers)
                {
                    if (name.Equals(propConfig.TriggerName, StringComparison.OrdinalIgnoreCase))
                    {
                        triggerRect = rect;
                        break;
                    }
                }

                _foregroundProps.Add(new Prop(tex, propConfig.SortY, triggerRect));
            }

            _currentState = GameState.Playing;
        }

        // ── Dev menu button layout ─────────────────────────────────────────────
        private static Rectangle GetSceneButtonRect(int index)
        {
            const int BTN_W = 600, BTN_H = 80, BTN_SPACING = 20;
            int x = (SCREEN_WIDTH - BTN_W) / 2;
            int y = 300 + index * (BTN_H + BTN_SPACING);
            return new Rectangle(x, y, BTN_W, BTN_H);
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Update(GameTime gameTime)
        {
            var kbState = Keyboard.GetState();
            if (kbState.IsKeyDown(Keys.Escape))
                Exit();

            if (kbState.IsKeyDown(Keys.F1) && !_prevKbState.IsKeyDown(Keys.F1))
                _showDebug = !_showDebug;
            _prevKbState = kbState;

            var  mouseState = Mouse.GetState();
            bool clicked    = mouseState.LeftButton  == ButtonState.Pressed &&
                              _prevMouseState.LeftButton == ButtonState.Released;
            var vm = new Point(
                (int)(mouseState.X / DISPLAY_SCALE),
                (int)(mouseState.Y / DISPLAY_SCALE));

            if (_currentState == GameState.DevMenu)
            {
                if (clicked)
                {
                    for (int i = 0; i < _availableScenes.Count; i++)
                    {
                        if (GetSceneButtonRect(i).Contains(vm))
                        {
                            LoadScene(_availableScenes[i]);
                            break;
                        }
                    }
                }
            }
            else // Playing
            {
                if (!_isGameWon && clicked)
                {
                    if (_isDeductionBoardOpen)
                    {
                        if (_activeDropdownSlotIndex >= 0)
                        {
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

                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_isDialogueActive)
                {
                    if (_cat.IsInteractPressed())
                        _isDialogueActive = false;
                }
                else
                {
                    _cat.Update(gameTime);
                    _cat.MoveWithCollision(dt, _solidBoundaries);

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

                foreach (var prop in _foregroundProps)
                {
                    prop.CheckFadeTrigger(_cat.CollisionBox);
                    prop.Update(gameTime);
                }

                // Hot-reload: poll level_config.json every 0.5 s.
                _hotReloadTimer += dt;
                if (_hotReloadTimer >= 0.5f)
                {
                    _hotReloadTimer = 0f;
                    if (File.Exists(_levelConfigSourcePath))
                    {
                        var writeTime = File.GetLastWriteTime(_levelConfigSourcePath);
                        if (writeTime > _levelConfigLastWrite)
                        {
                            _levelConfigLastWrite = writeTime;
                            try
                            {
                                var fresh = LevelConfigParser.Load(_levelConfigSourcePath);
                                foreach (var entity in _interactables)
                                {
                                    if (fresh.Interactables.TryGetValue(entity.Id, out var data))
                                        entity.Data = data;
                                }
                                Console.WriteLine("[HotReload] level_config.json reloaded.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[HotReload] Skipped (file locked?): {ex.Message}");
                            }
                        }
                    }
                }
            }

            _prevMouseState = mouseState;
            base.Update(gameTime);
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(_renderTarget);

            if (_currentState == GameState.DevMenu)
            {
                GraphicsDevice.Clear(new Color(20, 20, 30));

                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                const string title     = "DEV MENU - SELECT SCENE";
                var          titleSize = _dialogueFont.MeasureString(title);
                _spriteBatch.DrawString(
                    _dialogueFont, title,
                    new Vector2((SCREEN_WIDTH - titleSize.X) * 0.5f, 160),
                    Color.White);

                for (int i = 0; i < _availableScenes.Count; i++)
                {
                    var btnRect = GetSceneButtonRect(i);
                    _spriteBatch.Draw(_debugPixel, btnRect, new Color(40, 60, 100));

                    var labelSize = _dialogueFont.MeasureString(_availableScenes[i]);
                    _spriteBatch.DrawString(
                        _dialogueFont, _availableScenes[i],
                        new Vector2(
                            btnRect.X + (btnRect.Width  - labelSize.X) * 0.5f,
                            btnRect.Y + (btnRect.Height - labelSize.Y) * 0.5f),
                        Color.White);
                }

                _spriteBatch.End();
            }
            else // Playing
            {
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
                foreach (var prop in _foregroundProps)
                    prop.Draw(_spriteBatch);
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
                // ════════════════════════════════════════════════════════════════
                if (_showDebug)
                {
                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                        transformMatrix: _cameraTransform);
                    foreach (var wall in _solidBoundaries)
                        DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, wall, Color.Red);
                    DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, _cat.CollisionBox, Color.Cyan);
                    _spriteBatch.End();
                }

                // ════════════════════════════════════════════════════════════════
                // PASS 6 — DIALOGUE UI
                // ════════════════════════════════════════════════════════════════
                if (_isDialogueActive && _currentInteraction != null)
                {
                    const int BOX_PADDING = 20;
                    const int BOX_HEIGHT  = 130;
                    const int BOX_MARGIN  = 16;
                    var boxRect = new Rectangle(
                        BOX_MARGIN,
                        SCREEN_HEIGHT - BOX_HEIGHT - BOX_MARGIN,
                        SCREEN_WIDTH - BOX_MARGIN * 2,
                        BOX_HEIGHT);

                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                    _spriteBatch.Draw(_debugPixel, boxRect, Color.Black * 0.75f);

                    DrawRichText(
                        _spriteBatch,
                        _dialogueFont,
                        _currentInteraction.Text,
                        _currentInteraction.Keywords,
                        new Vector2(boxRect.X + BOX_PADDING, boxRect.Y + BOX_PADDING),
                        boxRect.Width - BOX_PADDING * 2);

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
                // ════════════════════════════════════════════════════════════════
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                if (!_isGameWon)
                {
                    _spriteBatch.Draw(_debugPixel, _solveButtonRect, Color.DarkGoldenrod);
                    var solveLbl = "SOLVE";
                    var solveSz  = _dialogueFont.MeasureString(solveLbl);
                    _spriteBatch.DrawString(
                        _dialogueFont, solveLbl,
                        new Vector2(
                            _solveButtonRect.X + (_solveButtonRect.Width  - solveSz.X) * 0.5f,
                            _solveButtonRect.Y + (_solveButtonRect.Height - solveSz.Y) * 0.5f),
                        Color.White);
                }

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
                    var contentRect = new Rectangle(
                        _notebookPanelRect.X,
                        _notebookPanelRect.Y + 60,
                        _notebookPanelRect.Width,
                        _notebookPanelRect.Height - 60);
                    _spriteBatch.Draw(_debugPixel, contentRect, Color.Black * 0.82f);

                    for (int i = 0; i < _tabRects.Length; i++)
                    {
                        bool active = (ClueCategory)i == _selectedTab;
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

                    const int CLUE_PADDING = 16;
                    float cx    = contentRect.X + CLUE_PADDING;
                    float cy    = contentRect.Y + CLUE_PADDING;
                    float maxW  = contentRect.Width - CLUE_PADDING * 2;
                    float lineH = _dialogueFont.LineSpacing + 6;

                    bool any = false;
                    foreach (var clue in _notebook.UnlockedClues)
                    {
                        if (clue.Category != _selectedTab) continue;
                        any = true;
                        _spriteBatch.DrawString(_dialogueFont, "* ", new Vector2(cx, cy), Color.LightGray);
                        float bulletW = _dialogueFont.MeasureString("* ").X;
                        cy = DrawWrappedString(_spriteBatch, _dialogueFont, $"{clue.Name.ToUpper()} - {clue.Context}",
                                 new Vector2(cx + bulletW, cy),
                                 maxW - bulletW, lineH,
                                 Color.White);
                        cy += 4f;
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

                        _spriteBatch.DrawString(_dialogueFont, banner,
                            bannerPos + new Vector2(6, 6), Color.Black * 0.8f,
                            0f, Vector2.Zero, bannerScale, SpriteEffects.None, 0f);
                        _spriteBatch.DrawString(_dialogueFont, banner,
                            bannerPos, Color.Gold,
                            0f, Vector2.Zero, bannerScale, SpriteEffects.None, 0f);

                        const string sub   = "Press Escape to quit";
                        var          subSz = _dialogueFont.MeasureString(sub);
                        _spriteBatch.DrawString(
                            _dialogueFont, sub,
                            new Vector2((SCREEN_WIDTH - subSz.X) * 0.5f, bannerPos.Y + bannerSz.Y + 20),
                            Color.LightGray);
                    }
                    else
                    {
                        _spriteBatch.Draw(_debugPixel,
                            new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT),
                            Color.Black * 0.7f);

                        _spriteBatch.Draw(_debugPixel, _deductionPanelRect, new Color(15, 25, 55));

                        const string panelTitle = "DEDUCTION BOARD";
                        var          titleSz    = _dialogueFont.MeasureString(panelTitle);
                        _spriteBatch.DrawString(
                            _dialogueFont, panelTitle,
                            new Vector2(
                                _deductionPanelRect.X + (_deductionPanelRect.Width - titleSz.X) * 0.5f,
                                _deductionPanelRect.Y + 12),
                            Color.White);

                        const string instr   = "Tap a slot to fill it, then submit your deduction.";
                        var          instrSz = _dialogueFont.MeasureString(instr);
                        _spriteBatch.DrawString(
                            _dialogueFont, instr,
                            new Vector2(
                                _deductionPanelRect.X + (_deductionPanelRect.Width - instrSz.X) * 0.5f,
                                _deductionPanelRect.Y + 12 + titleSz.Y + 4),
                            Color.LightGray);

                        string[] catLabels = { "WHO", "WHAT", "WHY", "WHERE/WHEN" };
                        const int BADGE_W  = 130;
                        for (int i = 0; i < _deduction.Slots.Count; i++)
                        {
                            var slot      = _deduction.Slots[i];
                            var slotColor = _tabColors[(int)slot.Category];

                            _spriteBatch.Draw(_debugPixel, slot.Bounds, slotColor * 0.25f);

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

                            string slotText  = "[ TAP TO SELECT ]";
                            Color  textColor = Color.DimGray;
                            if (slot.SelectedClueId != null)
                            {
                                var found = _notebook.UnlockedClues.Find(c => c.Id == slot.SelectedClueId);
                                if (found != null)
                                {
                                    slotText  = found.Name;
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

                        if (!string.IsNullOrEmpty(_deduction.ValidationMessage))
                        {
                            var  msgSz   = _dialogueFont.MeasureString(_deduction.ValidationMessage);
                            bool correct = _deduction.ValidationMessage == "Correct!";
                            _spriteBatch.DrawString(
                                _dialogueFont, _deduction.ValidationMessage,
                                new Vector2(
                                    _submitButtonRect.X + (_submitButtonRect.Width  - msgSz.X) * 0.5f,
                                    _submitButtonRect.Y - msgSz.Y - 10),
                                correct ? Color.LimeGreen : Color.OrangeRed);
                        }

                        _spriteBatch.Draw(_debugPixel, _submitButtonRect, new Color(20, 100, 40));
                        var submitLbl = "SUBMIT";
                        var submitSz  = _dialogueFont.MeasureString(submitLbl);
                        _spriteBatch.DrawString(
                            _dialogueFont, submitLbl,
                            new Vector2(
                                _submitButtonRect.X + (_submitButtonRect.Width  - submitSz.X) * 0.5f,
                                _submitButtonRect.Y + (_submitButtonRect.Height - submitSz.Y) * 0.5f),
                            Color.White);

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
                                    var itemSz = _dialogueFont.MeasureString(filtered[i].Name);
                                    _spriteBatch.DrawString(
                                        _dialogueFont, filtered[i].Name,
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
            } // end Playing

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
                float wordW  = font.MeasureString(word).X;
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

        private void DrawRichText(
            SpriteBatch spriteBatch,
            SpriteFont  font,
            string      text,
            Keyword[]   keywords,
            Vector2     origin,
            float       maxWidth)
        {
            var   spans = ParseSpans(text, keywords);
            float x     = origin.X;
            float y     = origin.Y;
            float lineH = font.LineSpacing;

            foreach (var (spanText, color) in spans)
            {
                int pos = 0;
                while (pos < spanText.Length)
                {
                    bool isSpace = spanText[pos] == ' ';
                    int  start   = pos;
                    while (pos < spanText.Length && (spanText[pos] == ' ') == isSpace)
                        pos++;
                    string token  = spanText[start..pos];
                    float  tokenW = font.MeasureString(token).X;

                    if (isSpace)
                    {
                        if (x > origin.X) x += tokenW;
                    }
                    else
                    {
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
