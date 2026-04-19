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
        private DeductionManager _deduction            = null!;
        private bool             _isDeductionBoardOpen = false;
        private bool             _isGameWon            = false;

        // Journal (two-page deduction board) UI state
        private ClueCategory _activeTab            = ClueCategory.Who;
        private Clue?        _selectedWordBankClue = null;
        private int          _wordBankPage      = 0;
        private int          _wordBankPageCount = 0; // last page index, written by Draw, read by Update
        private readonly List<(Rectangle Rect, Clue Clue)> _wordBankClueRects = new();

        private static readonly Rectangle _solveButtonRect =
            new Rectangle(2020 - 160, 1136 - 160, 120, 120);
        private static readonly Rectangle[] _journalTabRects = new[]
        {
            new Rectangle(1060, 85, 215, 80),
            new Rectangle(1275, 85, 215, 80),
            new Rectangle(1490, 85, 215, 80),
            new Rectangle(1705, 85, 215, 80),
        };
        // Paging: sit just below wordBankArea (Y=280+400=680)
        private static readonly Rectangle _journalPrevPageRect = new Rectangle(1050, 692,  90, 35);
        private static readonly Rectangle _journalNextPageRect = new Rectangle(1710, 692,  90, 35);
        // Insert: bottom-right of inspectorArea (X=1050+700=1750, Y=740+250=990)
        private static readonly Rectangle _journalInsertRect   = new Rectangle(1610, 940, 140, 50);
        private static readonly Rectangle _journalSubmitRect   = new Rectangle(1330, 1040, 340, 70);
        private static readonly Rectangle _journalCloseRect    = new Rectangle(1930,   30,  60, 60);

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
        private SpriteFont  _dialogueFont    = null!;
        private Texture2D   _dialogueBoxTex  = null!;
        private Texture2D   _notebookBgTex   = null!;
        private Texture2D   _tabTex          = null!;

        // ── Dialogue pagination & typewriter ──────────────────────────────────
        private string[] _dialoguePages        = Array.Empty<string>();
        private int      _currentDialoguePage  = 0;
        private float    _typewriterTimer       = 0f;
        private const float TYPEWRITER_SPEED    = 45f;

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

            _dialogueFont   = Content.Load<SpriteFont>("Shared/dialogue_font");
            _dialogueBoxTex = Content.Load<Texture2D>("Shared/ui_dialogue_box");
            _notebookBgTex  = Content.Load<Texture2D>("Shared/ui_notebook_bg");
            _tabTex         = Content.Load<Texture2D>("Shared/ui_tab");

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
            _isDialogueActive     = false;
            _currentInteraction   = null;
            _isNotebookOpen       = false;
            _isDeductionBoardOpen = false;
            _isGameWon            = false;
            _activeTab            = ClueCategory.Who;
            _selectedWordBankClue = null;
            _wordBankPage         = 0;
            _hotReloadTimer       = 0f;

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
            _deduction = new DeductionManager(levelConfig.DeductionSentence);

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
                        if (_journalCloseRect.Contains(vm))
                        {
                            _isDeductionBoardOpen = false;
                            _selectedWordBankClue = null;
                        }
                        else
                        {
                            // Tab clicks
                            for (int i = 0; i < _journalTabRects.Length; i++)
                            {
                                if (_journalTabRects[i].Contains(vm))
                                {
                                    _activeTab            = (ClueCategory)i;
                                    _wordBankPage         = 0;
                                    _selectedWordBankClue = null;
                                    break;
                                }
                            }

                            // Word bank item clicks
                            foreach (var (rect, clue) in _wordBankClueRects)
                            {
                                if (rect.Contains(vm))
                                {
                                    _selectedWordBankClue = clue;
                                    break;
                                }
                            }

                            // Paging
                            if (_journalPrevPageRect.Contains(vm) && _wordBankPage > 0)
                                _wordBankPage--;
                            if (_journalNextPageRect.Contains(vm) && _wordBankPage < _wordBankPageCount)
                                _wordBankPage++;

                            // Insert selected clue into matching slot
                            if (_journalInsertRect.Contains(vm) && _selectedWordBankClue != null)
                            {
                                var target = _deduction.Slots
                                    .Find(s => s.Category == _selectedWordBankClue.Category);
                                if (target != null)
                                    target.SelectedClueId = _selectedWordBankClue.Id;
                            }

                            // Submit
                            if (_journalSubmitRect.Contains(vm))
                            {
                                if (_deduction.ValidateCase())
                                    _isGameWon = true;
                            }

                            // Left page slot clicks -> switch active tab
                            foreach (var slot in _deduction.Slots)
                            {
                                if (slot.Bounds.Contains(vm))
                                {
                                    _activeTab            = slot.Category;
                                    _wordBankPage         = 0;
                                    _selectedWordBankClue = null;
                                    break;
                                }
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
                    int totalChars = _dialoguePages[_currentDialoguePage]
                        .Replace("[", "").Replace("]", "").Length;

                    if (_cat.IsInteractPressed())
                    {
                        if (_typewriterTimer < totalChars)
                        {
                            _typewriterTimer = totalChars;
                        }
                        else
                        {
                            if (_currentDialoguePage < _dialoguePages.Length - 1)
                            {
                                _currentDialoguePage++;
                                _typewriterTimer = 0f;
                            }
                            else
                            {
                                _isDialogueActive = false;
                            }
                        }
                    }
                    else
                    {
                        _typewriterTimer += (float)gameTime.ElapsedGameTime.TotalSeconds * TYPEWRITER_SPEED;
                    }
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
                        _currentInteraction  = _activeInteractable.Data;
                        _dialoguePages       = _currentInteraction.Text.Split('|');
                        _currentDialoguePage = 0;
                        _typewriterTimer     = 0f;
                        _isDialogueActive    = true;
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
                    var boxRect = new Rectangle(
                        (SCREEN_WIDTH - 1400) / 2,
                        SCREEN_HEIGHT - 450 - 40,
                        1400, 450);

                    const int PAD_X = 140;
                    const int PAD_Y = 100;

                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                    _spriteBatch.Draw(_dialogueBoxTex, boxRect, Color.White);

                    int totalCharsOnPage = _dialoguePages[_currentDialoguePage]
                        .Replace("[", "").Replace("]", "").Length;
                    bool typingDone = _typewriterTimer >= totalCharsOnPage;

                    DrawRichText(
                        _spriteBatch,
                        _dialogueFont,
                        _dialoguePages[_currentDialoguePage],
                        _currentInteraction.Keywords,
                        new Vector2(boxRect.X + PAD_X, boxRect.Y + PAD_Y),
                        boxRect.Width - PAD_X * 2,
                        (int)_typewriterTimer);

                    bool isLastPage = _currentDialoguePage >= _dialoguePages.Length - 1;
                    string hint     = typingDone
                        ? (isLastPage ? "[ Space ] to dismiss" : "[ Space ] to continue")
                        : "[ Space ] to skip";
                    var    hintSize = _dialogueFont.MeasureString(hint);
                    _spriteBatch.DrawString(
                        _dialogueFont,
                        hint,
                        new Vector2(
                            boxRect.Right  - PAD_X - hintSize.X,
                            boxRect.Bottom - PAD_Y * 0.6f - hintSize.Y),
                        new Color(90, 70, 50));
                    _spriteBatch.End();
                }

                // ════════════════════════════════════════════════════════════════
                // PASS 7 — NOTEBOOK UI  (hidden while dialogue is open)
                // ════════════════════════════════════════════════════════════════
                if (!_isDialogueActive)
                {
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
                }

                // ════════════════════════════════════════════════════════════════
                // PASS 8 — DEDUCTION BOARD / WIN STATE  (hidden while dialogue is open)
                // ════════════════════════════════════════════════════════════════
                if ((_isDeductionBoardOpen || _isGameWon) && !_isDialogueActive)
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
                        // ── Full-screen journal background ────────────────────
                        _spriteBatch.Draw(_notebookBgTex,
                            new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT),
                            Color.White);

                        // Safe zones — tweak to match ui_notebook_bg.png art
                        var leftPageArea  = new Rectangle(250, 300, 650, 600);
                        var wordBankArea  = new Rectangle(1050, 280, 750, 400);
                        var inspectorArea = new Rectangle(1150, 800, 600, 250);

                        // ── LEFT PAGE: Mad-Libs sentence ──────────────────────
                        float lx      = leftPageArea.X;
                        float ly      = leftPageArea.Y;
                        float lineH   = _dialogueFont.LineSpacing + 16;
                        float spaceW  = _dialogueFont.MeasureString(" ").X;
                        int   maxRight = leftPageArea.Right;

                        //var sentTitleSz = _dialogueFont.MeasureString("What happened?");
                        //_spriteBatch.DrawString(_dialogueFont, "What happened?",
                          //  new Vector2(leftPageArea.X, leftPageArea.Y - sentTitleSz.Y - 12),
                            //_tabColors[(int)_activeTab]);

                        string[] slotCatLabels = { "WHO", "WHAT", "WHY", "WHERE/WHEN" };

                        foreach (var seg in _deduction.Segments)
                        {
                            if (seg is TextSegment ts)
                            {
                                string txt = ts.Text;
                                int    pos = 0;
                                while (pos < txt.Length)
                                {
                                    if (txt[pos] == ' ')
                                    {
                                        if (lx > leftPageArea.X) lx += spaceW;
                                        pos++;
                                    }
                                    else
                                    {
                                        int end = pos;
                                        while (end < txt.Length && txt[end] != ' ') end++;
                                        string word  = txt[pos..end];
                                        float  wordW = _dialogueFont.MeasureString(word).X;
                                        if (lx > leftPageArea.X && lx + wordW > maxRight)
                                        { lx = leftPageArea.X; ly += lineH; }
                                        _spriteBatch.DrawString(_dialogueFont, word,
                                            new Vector2(lx, ly), _inkColor);
                                        lx  += wordW;
                                        pos  = end;
                                    }
                                }
                            }
                            else if (seg is SlotSegment ss)
                            {
                                var    slot  = ss.Slot;
                                string label = slot.SelectedClueId != null
                                    ? (_notebook.UnlockedClues
                                           .Find(c => c.Id == slot.SelectedClueId)?.Name
                                       ?? slot.TagLabel)
                                    : $"[ {slotCatLabels[(int)slot.Category]} ]";
                                float textW  = _dialogueFont.MeasureString(label).X;
                                int   slotW  = (int)textW + 24;
                                int   slotH  = _dialogueFont.LineSpacing + 8;
                                if (lx > leftPageArea.X && lx + slotW > maxRight)
                                { lx = leftPageArea.X; ly += lineH; }
                                var slotColor = _tabColors[(int)slot.Category];
                                var slotRect  = new Rectangle((int)lx, (int)ly, slotW, slotH);
                                _spriteBatch.Draw(_debugPixel, slotRect, slotColor * 0.3f);
                                DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, slotRect, slotColor);
                                float tX = slotRect.X + (slotRect.Width  - textW) * 0.5f;
                                float tY = slotRect.Y + (slotRect.Height - _dialogueFont.LineSpacing) * 0.5f;
                                _spriteBatch.DrawString(_dialogueFont, label, new Vector2(tX, tY), _inkColor);
                                slot.Bounds = slotRect;
                                lx += slotW + 8;
                            }
                        }

                        // ── RIGHT PAGE: Tabs ──────────────────────────────────
                        for (int i = 0; i < _journalTabRects.Length; i++)
                        {
                            bool isActive = (ClueCategory)i == _activeTab;
                            var  tr       = isActive
                                ? new Rectangle(
                                    _journalTabRects[i].X,
                                    _journalTabRects[i].Y - 10,
                                    _journalTabRects[i].Width,
                                    _journalTabRects[i].Height + 10)
                                : _journalTabRects[i];
                            _spriteBatch.Draw(_tabTex, tr,
                                _tabColors[i] * (isActive ? 1f : 0.6f));
                            var tabLblSz = _dialogueFont.MeasureString(_tabLabels[i]);
                            _spriteBatch.DrawString(_dialogueFont, _tabLabels[i],
                                new Vector2(
                                    tr.X + (tr.Width  - tabLblSz.X) * 0.5f,
                                    tr.Y + (tr.Height - tabLblSz.Y) * 0.5f),
                                Color.White);
                        }

                        // ── RIGHT PAGE: Word Bank (flow layout) ───────────────
                        const float spacingX  = 16f;
                        const float spacingY  = 16f;
                        const float boxHeight = 40f;

                        float currentX   = wordBankArea.X;
                        float currentY   = wordBankArea.Y;
                        int   flowPage   = 0;

                        _wordBankClueRects.Clear();

                        var filteredWB = _notebook.UnlockedClues
                            .FindAll(c => c.Category == _activeTab);

                        foreach (var cl in filteredWB)
                        {
                            var   textSz   = _dialogueFont.MeasureString(cl.Name);
                            float boxWidth = textSz.X + 24f;

                            // Line wrap
                            if (currentX + boxWidth > wordBankArea.Right)
                            {
                                currentX  = wordBankArea.X;
                                currentY += boxHeight + spacingY;
                            }

                            // Page wrap (leave 40px for paging arrows)
                            if (currentY + boxHeight > wordBankArea.Bottom - 40)
                            {
                                flowPage++;
                                currentX = wordBankArea.X;
                                currentY = wordBankArea.Y;
                            }

                            if (flowPage == _wordBankPage)
                            {
                                var  tagR = new Rectangle((int)currentX, (int)currentY,
                                                          (int)boxWidth, (int)boxHeight);
                                bool sel  = cl == _selectedWordBankClue;
                                _spriteBatch.Draw(_debugPixel, tagR,
                                    sel ? _tabColors[(int)_activeTab]
                                        : _tabColors[(int)_activeTab] * 0.25f);
                                DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, tagR,
                                    _tabColors[(int)_activeTab] * (sel ? 1f : 0.6f));
                                float nameX = tagR.X + (tagR.Width  - textSz.X) * 0.5f;
                                float nameY = tagR.Y + (tagR.Height - textSz.Y) * 0.5f;
                                _spriteBatch.DrawString(_dialogueFont, cl.Name,
                                    new Vector2(nameX, nameY),
                                    sel ? Color.White : _inkColor);
                                _wordBankClueRects.Add((tagR, cl));
                            }

                            currentX += boxWidth + spacingX;
                        }

                        _wordBankPageCount = flowPage; // written here, read by Update

                        if (filteredWB.Count == 0)
                        {
                            _spriteBatch.DrawString(_dialogueFont, "-- no clues found yet --",
                                new Vector2(wordBankArea.X, wordBankArea.Y + 10), Color.Gray);
                        }

                        // ── Paging controls ───────────────────────────────────
                        if (_wordBankPageCount > 0)
                        {
                            if (_wordBankPage > 0)
                            {
                                _spriteBatch.Draw(_debugPixel, _journalPrevPageRect,
                                    new Color(40, 40, 60));
                                var prevSz = _dialogueFont.MeasureString("<");
                                _spriteBatch.DrawString(_dialogueFont, "<",
                                    new Vector2(
                                        _journalPrevPageRect.X + (_journalPrevPageRect.Width  - prevSz.X) * 0.5f,
                                        _journalPrevPageRect.Y + (_journalPrevPageRect.Height - prevSz.Y) * 0.5f),
                                    Color.White);
                            }
                            if (_wordBankPage < _wordBankPageCount)
                            {
                                _spriteBatch.Draw(_debugPixel, _journalNextPageRect,
                                    new Color(40, 40, 60));
                                var nextSz = _dialogueFont.MeasureString(">");
                                _spriteBatch.DrawString(_dialogueFont, ">",
                                    new Vector2(
                                        _journalNextPageRect.X + (_journalNextPageRect.Width  - nextSz.X) * 0.5f,
                                        _journalNextPageRect.Y + (_journalNextPageRect.Height - nextSz.Y) * 0.5f),
                                    Color.White);
                            }
                            var pageLbl   = $"{_wordBankPage + 1} / {_wordBankPageCount + 1}";
                            var pageLblSz = _dialogueFont.MeasureString(pageLbl);
                            _spriteBatch.DrawString(_dialogueFont, pageLbl,
                                new Vector2(
                                    (_journalPrevPageRect.Right + _journalNextPageRect.X) * 0.5f
                                        - pageLblSz.X * 0.5f,
                                    _journalPrevPageRect.Y + (_journalPrevPageRect.Height - pageLblSz.Y) * 0.5f),
                                Color.LightGray);
                        }

                        // ── Inspector Panel (no debug background) ─────────────
                        if (_selectedWordBankClue != null)
                        {
                            var insertBtn = _journalInsertRect;
                            _spriteBatch.DrawString(_dialogueFont, _selectedWordBankClue.Name,
                                new Vector2(inspectorArea.X, inspectorArea.Y),
                                _tabColors[(int)_selectedWordBankClue.Category]);
                            DrawWrappedString(_spriteBatch, _dialogueFont,
                                _selectedWordBankClue.Context,
                                new Vector2(inspectorArea.X, inspectorArea.Y + 40),
                                inspectorArea.Width, _dialogueFont.LineSpacing + 4, _inkColor);
                            _spriteBatch.Draw(_debugPixel, insertBtn,
                                _tabColors[(int)_selectedWordBankClue.Category]);
                            var insSz = _dialogueFont.MeasureString("INSERT");
                            _spriteBatch.DrawString(_dialogueFont, "INSERT",
                                new Vector2(
                                    insertBtn.X + (insertBtn.Width  - insSz.X) * 0.5f,
                                    insertBtn.Y + (insertBtn.Height - insSz.Y) * 0.5f),
                                Color.White);
                        }

                        // ── Submit & validation ───────────────────────────────
                        if (!string.IsNullOrEmpty(_deduction.ValidationMessage))
                        {
                            bool isCorrect = _deduction.ValidationMessage.StartsWith("Case");
                            var  vmSz      = _dialogueFont.MeasureString(_deduction.ValidationMessage);
                            _spriteBatch.DrawString(_dialogueFont, _deduction.ValidationMessage,
                                new Vector2(
                                    _journalSubmitRect.X + (_journalSubmitRect.Width  - vmSz.X) * 0.5f,
                                    _journalSubmitRect.Y - vmSz.Y - 8),
                                isCorrect ? Color.LimeGreen : Color.OrangeRed);
                        }
                        _spriteBatch.Draw(_debugPixel, _journalSubmitRect, new Color(20, 100, 40));
                        var subLbl = "SUBMIT";
                        var subSz  = _dialogueFont.MeasureString(subLbl);
                        _spriteBatch.DrawString(_dialogueFont, subLbl,
                            new Vector2(
                                _journalSubmitRect.X + (_journalSubmitRect.Width  - subSz.X) * 0.5f,
                                _journalSubmitRect.Y + (_journalSubmitRect.Height - subSz.Y) * 0.5f),
                            Color.White);

                        // ── Close button (X) ──────────────────────────────────
                        _spriteBatch.Draw(_debugPixel, _journalCloseRect, new Color(100, 40, 40));
                        var closeSz = _dialogueFont.MeasureString("X");
                        _spriteBatch.DrawString(_dialogueFont, "X",
                            new Vector2(
                                _journalCloseRect.X + (_journalCloseRect.Width  - closeSz.X) * 0.5f,
                                _journalCloseRect.Y + (_journalCloseRect.Height - closeSz.Y) * 0.5f),
                            Color.White);
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

        private static readonly Color _inkColor = new Color(40, 30, 20);

        private static List<(string Text, Color HighlightColor)> ParseSpans(
            string text, Keyword[] keywords)
        {
            var result = new List<(string, Color)>();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('[', i);
                if (open == -1)
                {
                    result.Add((text[i..], Color.Transparent));
                    break;
                }
                if (open > i)
                    result.Add((text[i..open], Color.Transparent));

                int close = text.IndexOf(']', open + 1);
                if (close == -1)
                {
                    result.Add((text[open..], Color.Transparent));
                    break;
                }

                string bracketText = text[(open + 1)..close];
                var kwColor = Color.Transparent;
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
            float       maxWidth,
            int         maxChars = int.MaxValue)
        {
            var   spans      = ParseSpans(text, keywords);
            float x          = origin.X;
            float y          = origin.Y;
            float lineH      = font.LineSpacing;
            int   charsDrawn = 0;

            foreach (var (spanText, highlightColor) in spans)
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

                        if (charsDrawn >= maxChars) return;

                        string drawToken  = charsDrawn + token.Length > maxChars
                            ? token.Substring(0, maxChars - charsDrawn)
                            : token;
                        float  drawTokenW = font.MeasureString(drawToken).X;

                        if (highlightColor != Color.Transparent)
                        {
                            spriteBatch.Draw(_debugPixel,
                                new Rectangle((int)x, (int)y + 8, (int)drawTokenW, (int)lineH - 12),
                                highlightColor * 0.4f);
                        }
                        spriteBatch.DrawString(font, drawToken, new Vector2(x, y), _inkColor);
                        x          += tokenW;
                        charsDrawn += token.Length;
                    }
                }
            }
        }
    }
}
