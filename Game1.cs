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
        // Set per-room to match the background image exactly (no centering, no dead zone).
        // UpdateLayout() recomputes all UI positions whenever these change.
        private int         SCREEN_WIDTH  = 1456;
        private int         SCREEN_HEIGHT = 816;
        private const float DISPLAY_SCALE = 1.0f; // 1.0 = native bg pixel size

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

        // ── Transfer zones and the one the cat is currently standing in ────────
        private List<TransferZone>  _transferZones       = new();
        private TransferZone?       _activeTransferZone;

        // ── Interaction system ─────────────────────────────────────────────────
        private Dictionary<string, InteractionData> _interactionDatabase = null!;
        private bool             _isDialogueActive;
        private InteractionData? _currentInteraction;

        // ── Notebook / inventory ───────────────────────────────────────────────
        private NotebookManager _notebook = null!;
        private MouseState      _prevMouseState;

        // ── Deduction board ────────────────────────────────────────────────────
        private DeductionManager _deduction            = null!;
        private bool             _isDeductionBoardOpen = false;
        private bool             _isFinalSolveMode     = false; // true = macro board, false = local room board
        private bool             _isGameWon            = false;

        private bool AllRoomsSolved =>
            _roomSolvedStates.Count > 0 && !_roomSolvedStates.ContainsValue(false);

        // Journal (two-page deduction board) UI state
        private ClueCategory _activeTab            = ClueCategory.Who;
        private Clue?        _selectedWordBankClue = null;
        private int          _wordBankPage      = 0;
        private int          _wordBankPageCount = 0; // last page index, written by Draw, read by Update
        private readonly List<(Rectangle Rect, Clue Clue)> _wordBankClueRects = new();

        // UI layout — all computed by UpdateLayout() so they scale with any background size.
        // Reference design space: 2020×1136. UpdateLayout() maps these to SCREEN_WIDTH×SCREEN_HEIGHT.
        private Rectangle   _solveButtonRect;
        private Rectangle   _finalSolveButtonRect;

        // Hotspot menu: one pre-rendered 800×150 bar per active tab.
        // Visual order on the image: Who | How(What) | Where(WhereWhen) | Why
        // This differs from enum order (Who=0,What=1,Why=2,WhereWhen=3), so map explicitly.
        private Vector2     _tabImagePos;
        private Rectangle[] _tabHotspots      = Array.Empty<Rectangle>();
        private static readonly ClueCategory[] _tabHotspotCategories =
        {
            ClueCategory.Who,
            ClueCategory.What,
            ClueCategory.WhereWhen,
            ClueCategory.Why,
        };
        private Rectangle _journalPrevPageRect;
        private Rectangle _journalNextPageRect;
        private Rectangle _journalInsertRect;
        private Rectangle _journalSubmitRect;
        private Rectangle _journalClearRect;
        private Rectangle _journalCloseRect;

        // Transient HUD toast (progression feedback: room solved, FINAL SOLVE locked, ...)
        private string _toastMessage = "";
        private float  _toastTimer;
        private const float TOAST_DURATION = 4f;

        private static readonly Color[] _tabColors = new[]
        {
            new Color(200, 160,  35),   // Who       — yellow (matches the tab-bar art)
            new Color( 80, 200, 100),   // What      — green
            new Color(230, 140,  40),   // Why       — orange
            new Color(160,  80, 220),   // WhereWhen — purple
        };

        // ── UI ─────────────────────────────────────────────────────────────────
        private SpriteFont  _dialogueFont    = null!;
        private Texture2D   _dialogueBoxTex  = null!;
        private Texture2D   _notebookBgTex   = null!;
        private Dictionary<ClueCategory, Texture2D> _tabTextures = new(); // deduction board bar (Pass 8)

        // ── Dialogue pagination & typewriter ──────────────────────────────────
        private string[] _dialoguePages        = Array.Empty<string>();
        private int      _currentDialoguePage  = 0;
        private float    _typewriterTimer       = 0f;
        private const float TYPEWRITER_SPEED    = 45f;

        // ── Debug overlay ──────────────────────────────────────────────────────
        private Texture2D     _debugPixel  = null!;
        private bool          _showDebug   = true;   // F1 toggles
        private KeyboardState _prevKbState;

        // ── Screenshot mode  (--screenshot <caseId> <roomId> [journal|final]) ──
        private bool   _screenshotMode  = false;
        private string _screenshotCase  = "";
        private string _screenshotRoom  = "";
        private string _screenshotView  = "";   // "" = room, "journal" = local board, "final" = final board
        private float  _screenshotTimer = 0f;
        private bool   _screenshotSaved = false;
        private const float SCREENSHOT_DELAY = 1.5f;

        // ── Hot-reload (level_config.json) ─────────────────────────────────────
        private string   _levelConfigSourcePath = "";
        private DateTime _levelConfigLastWrite;
        private float    _hotReloadTimer;

        // ── Game state / scene selection ───────────────────────────────────────
        private enum GameState { DevMenu, Playing }
        private GameState    _currentState    = GameState.DevMenu;
        private List<string> _availableScenes = new();

        // ── Case / room tracking ───────────────────────────────────────────────
        private string                   _currentCaseId    = "";
        private string                   _currentRoomId    = "";
        private string                   _spawnPointName   = "";
        private Dictionary<string, bool> _roomSolvedStates = new();
        private IReadOnlyList<string>    _caseRooms        = Array.Empty<string>();

        // Filled local sentences captured at solve time (the local DeductionManager
        // is recreated on every LoadRoom, so this is the only durable copy).
        private readonly Dictionary<string, string> _roomSolvedSentences = new();

        // Clue ids referenced by the current room's keywords — the room counter's basis.
        private readonly HashSet<string> _currentRoomClueIds = new();

        // Slot the player last clicked on the board; INSERT prefers it.
        private DeductionSlot? _insertTargetSlot;

        // ── Deduction — macro (case-level) and local (room-level) ─────────────
        private DeductionManager  _localDeduction = null!;

        // ── Shared textures (loaded once, reused across scenes) ───────────────
        private Texture2D _walkForwardTex = null!;
        private Texture2D _walkUpwardTex  = null!;
        private Texture2D _shadowTex      = null!;

        // ══════════════════════════════════════════════════════════════════════
        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth  = (int)(SCREEN_WIDTH  * DISPLAY_SCALE),
                PreferredBackBufferHeight = (int)(SCREEN_HEIGHT * DISPLAY_SCALE),
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Parse --screenshot <caseId> <roomId>
            string[] cliArgs = System.Environment.GetCommandLineArgs();
            for (int i = 1; i < cliArgs.Length; i++)
            {
                if (cliArgs[i] == "--screenshot" && i + 2 < cliArgs.Length)
                {
                    _screenshotMode = true;
                    _screenshotCase = cliArgs[i + 1];
                    _screenshotRoom = cliArgs[i + 2];
                    if (i + 3 < cliArgs.Length)
                        _screenshotView = cliArgs[i + 3];
                    break;
                }
            }
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

            // Shared sprite sheets — loaded once, passed into Cat on each LoadRoom.
            _walkForwardTex = Content.Load<Texture2D>("Shared/walk_animation_forward");
            _walkUpwardTex  = Content.Load<Texture2D>("Shared/walk_animation_upward");
            _shadowTex      = Content.Load<Texture2D>("Shared/shadow_blob");

            _debugPixel = new Texture2D(GraphicsDevice, 1, 1);
            _debugPixel.SetData(new[] { Color.White });

            _dialogueFont   = Content.Load<SpriteFont>("Shared/dialogue_font");
            _dialogueBoxTex = Content.Load<Texture2D>("Shared/ui_dialogue_box");
            _notebookBgTex  = Content.Load<Texture2D>("Shared/ui_notebook_bg");

            _tabTextures[ClueCategory.Who]       = Content.Load<Texture2D>("Shared/who");
            _tabTextures[ClueCategory.What]      = Content.Load<Texture2D>("Shared/how");
            _tabTextures[ClueCategory.Why]       = Content.Load<Texture2D>("Shared/why");
            _tabTextures[ClueCategory.WhereWhen] = Content.Load<Texture2D>("Shared/where");

            string configPath = Path.Combine(Content.RootDirectory, "scenes_config.json");
            _availableScenes = SceneConfigParser.GetAvailableScenes(configPath);

            UpdateLayout();

            if (_screenshotMode)
            {
                LoadCase(_screenshotCase);
                if (_screenshotRoom != "entrance")
                    LoadRoom(_screenshotRoom, "spawn_default");

                // Optional view arg opens the journal so board layouts can be captured.
                if (_screenshotView == "journal" || _screenshotView == "final")
                {
                    foreach (var id in _currentRoomClueIds)
                        _notebook.UnlockClue(id);
                    _isDeductionBoardOpen = true;
                    _isFinalSolveMode     = _screenshotView == "final";
                    if (_isFinalSolveMode)
                    {
                        foreach (var room in _caseRooms)
                            _notebook.UnlockMacroCluesForRoom(room);
                    }
                    _selectedWordBankClue = _notebook.UnlockedClues.Count > 0
                        ? _notebook.UnlockedClues[0] : null;
                    _activeTab = _selectedWordBankClue?.Category ?? ClueCategory.Who;
                }
                // "dialogue" opens the room's longest interaction text, fully typed,
                // so text-box fit can be verified from a capture.
                else if (_screenshotView == "dialogue")
                {
                    InteractableEntity? longest = null;
                    foreach (var entity in _interactables)
                        if (entity.Data != null &&
                            (longest?.Data == null ||
                             entity.Data.Text.Length > longest.Data.Text.Length))
                            longest = entity;
                    if (longest?.Data != null)
                    {
                        _currentInteraction  = longest.Data;
                        _dialoguePages       = _currentInteraction.Text.Split('|');
                        _currentDialoguePage = 0;
                        _typewriterTimer     = 999999f;   // fully typed ((int) cast safe)
                        _isDialogueActive    = true;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        private void LoadCase(string caseId)
        {
            // Case-level resets (do not reset on room transitions).
            _isGameWon            = false;
            _isDeductionBoardOpen = false;
            _isFinalSolveMode     = false;
            _activeTab            = ClueCategory.Who;
            _selectedWordBankClue = null;
            _wordBankPage         = 0;
            _hotReloadTimer       = 0f;

            _currentCaseId = caseId;

            _sunbeamsMask = Content.Load<Texture2D>("Shared/mask_sunbeams");

            string configPath = Path.Combine(Content.RootDirectory, "scenes_config.json");
            _ambientColor = SceneConfigParser.GetAmbientColor(configPath, caseId);

            // Load case config: global clue database + macro deduction sentence.
            string caseConfigPath = Path.Combine(
                Content.RootDirectory, "Levels", caseId, "case_config.json");
            var caseConfig = LevelConfigParser.LoadCase(caseConfigPath);

            _notebook  = new NotebookManager(caseConfig.Clues);
            _deduction = new DeductionManager(
                caseConfig.DeductionSentence,
                caseConfig.FinalSolveClueIds,
                id => _notebook.GetClue(id)?.Category);

            // Pre-populate so AllRoomsSolved requires every room, not just the first solved one.
            _caseRooms        = caseConfig.Rooms;
            _roomSolvedStates = new Dictionary<string, bool>();
            foreach (var room in _caseRooms)
                _roomSolvedStates[room] = false;
            _roomSolvedSentences.Clear();

            LoadRoom("entrance", spawnPointName: "spawn_default");

            _currentState = GameState.Playing;
        }

        // ══════════════════════════════════════════════════════════════════════
        private void LoadRoom(string roomId, string spawnPointName)
        {
            // Room-level resets.
            _foregroundProps.Clear();
            _solidBoundaries.Clear();
            _interactables.Clear();
            _isDialogueActive   = false;
            _currentInteraction = null;

            _currentRoomId  = roomId;
            _spawnPointName = spawnPointName;

            string roomBase = Path.Combine(
                Content.RootDirectory, "Levels", _currentCaseId, roomId);
            string contentBase = $"Levels/{_currentCaseId}/{roomId}";

            // Load room config: props, interactables, local deduction sentence.
            string roomConfigPath = Path.Combine(roomBase, "room_config.json");
            var roomConfig = LevelConfigParser.LoadRoom(roomConfigPath);

            _interactionDatabase = roomConfig.Interactables;
            _localDeduction = new DeductionManager(
                roomConfig.LocalDeductionSentence,
                roomConfig.LocalDeductionClueIds,
                id => _notebook.GetClue(id)?.Category);
            _insertTargetSlot = null;

            // Room counter basis: every clue id this room's keywords can unlock.
            _currentRoomClueIds.Clear();
            foreach (var data in _interactionDatabase.Values)
                foreach (var kw in data.Keywords)
                    _currentRoomClueIds.Add(kw.Id);

            // Re-entering a solved room: show its board as a completed recap.
            if (_roomSolvedStates.TryGetValue(roomId, out var wasSolved) && wasSolved)
            {
                foreach (var slot in _localDeduction.Slots)
                    if (slot.CorrectClueId != "")
                        slot.SelectedClueId = slot.CorrectClueId;
                _localDeduction.ValidationMessage = "Case solved!";
            }

            // Hot-reload tracks room_config.json.
            _levelConfigSourcePath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "Content", "Levels", _currentCaseId, roomId, "room_config.json"));
            _levelConfigLastWrite = File.Exists(_levelConfigSourcePath)
                ? File.GetLastWriteTime(_levelConfigSourcePath)
                : DateTime.MinValue;

            // Room background — drives the virtual canvas size.
            _bgBase = Content.Load<Texture2D>($"{contentBase}/bg_base");

            // Resize virtual canvas and window to match the background exactly.
            if (_bgBase.Width != SCREEN_WIDTH || _bgBase.Height != SCREEN_HEIGHT)
            {
                SCREEN_WIDTH  = _bgBase.Width;
                SCREEN_HEIGHT = _bgBase.Height;
                _renderTarget?.Dispose();
                _renderTarget = new RenderTarget2D(GraphicsDevice, SCREEN_WIDTH, SCREEN_HEIGHT);
                _graphics.PreferredBackBufferWidth  = (int)(SCREEN_WIDTH  * DISPLAY_SCALE);
                _graphics.PreferredBackBufferHeight = (int)(SCREEN_HEIGHT * DISPLAY_SCALE);
                _graphics.ApplyChanges();
                GameObject.SetScreenHeight(SCREEN_HEIGHT);
                UpdateLayout();
            }

            // Background exactly fills the canvas — no centering offset needed.
            _cameraTransform = Matrix.Identity;

            // Parse Tiled map for this room.
            string mapPath = Path.Combine(roomBase, "room_map.json");
            MapParser.Parse(mapPath, Content, contentBase, _interactionDatabase, spawnPointName,
                out _solidBoundaries, out var triggers, out _interactables,
                out _transferZones, out Vector2? spawnPoint);

            Vector2 catStart = spawnPoint ?? new Vector2(500, 500);
            _cat = new Cat(_walkForwardTex, _walkUpwardTex, startPosition: catStart,
                           frameCount: 12, columns: 6, rows: 2)
            {
                ShadowTexture = _shadowTex
            };

            // Build foreground props.
            foreach (var propConfig in roomConfig.Props)
            {
                var tex = Content.Load<Texture2D>($"{contentBase}/{propConfig.Texture}");

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
        }

        // ── Dev menu button layout ─────────────────────────────────────────────
        private Rectangle GetSceneButtonRect(int index)
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
                            LoadCase(_availableScenes[i]);
                            break;
                        }
                    }
                }
            }
            else // Playing
            {
                // Screenshot mode: save render target to file then exit.
                if (_screenshotMode && !_screenshotSaved)
                {
                    _screenshotTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_screenshotTimer >= SCREENSHOT_DELAY)
                    {
                        _screenshotSaved = true;
                        SaveScreenshot();
                        Exit();
                        return;
                    }
                }

                if (!_isGameWon && clicked)
                {
                    if (_isDeductionBoardOpen)
                    {
                        if (_journalCloseRect.Contains(vm))
                        {
                            _isDeductionBoardOpen = false;
                            _isFinalSolveMode     = false;
                            _selectedWordBankClue = null;
                            _insertTargetSlot     = null;
                        }
                        else
                        {
                            var activeDeduction = _isFinalSolveMode ? _deduction : _localDeduction;

                            // A solved room's board is a read-only recap: browsing stays
                            // enabled, but inserting and submitting are locked.
                            bool boardLocked = !_isFinalSolveMode &&
                                _roomSolvedStates.TryGetValue(_currentRoomId, out var solved) && solved;

                            // Tab clicks
                            for (int i = 0; i < _tabHotspots.Length; i++)
                            {
                                if (_tabHotspots[i].Contains(vm))
                                {
                                    _activeTab            = _tabHotspotCategories[i];
                                    _wordBankPage         = 0;
                                    _selectedWordBankClue = null;
                                    _insertTargetSlot     = null;
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

                            // Insert selected clue: prefer the slot the player clicked,
                            // then the first EMPTY slot of the category, then the first slot.
                            // (A sentence can hold two slots of the same category.)
                            if (!boardLocked && _journalInsertRect.Contains(vm) && _selectedWordBankClue != null)
                            {
                                var slots  = activeDeduction.Slots;
                                var target =
                                    (_insertTargetSlot != null &&
                                     _insertTargetSlot.Category == _selectedWordBankClue.Category &&
                                     slots.Contains(_insertTargetSlot))
                                        ? _insertTargetSlot
                                        : slots.Find(s => s.Category == _selectedWordBankClue.Category
                                                       && s.SelectedClueId == null)
                                          ?? slots.Find(s => s.Category == _selectedWordBankClue.Category);
                                if (target != null)
                                    target.SelectedClueId = _selectedWordBankClue.Id;
                            }

                            // Clear all slots (reset the board without redoing anything)
                            if (!boardLocked && _journalClearRect.Contains(vm))
                            {
                                foreach (var slot in activeDeduction.Slots)
                                    slot.SelectedClueId = null;
                                activeDeduction.ValidationMessage = "";
                                _insertTargetSlot = null;
                            }

                            // Submit
                            if (!boardLocked && _journalSubmitRect.Contains(vm))
                            {
                                if (_isFinalSolveMode)
                                {
                                    if (_deduction.ValidateCase())
                                        _isGameWon = true;
                                }
                                else
                                {
                                    if (_localDeduction.ValidateCase())
                                    {
                                        _roomSolvedStates[_currentRoomId] = true;
                                        _roomSolvedSentences[_currentRoomId] =
                                            _localDeduction.BuildFilledSentence(
                                                id => _notebook.GetClue(id)?.Name);
                                        _notebook.UnlockMacroCluesForRoom(_currentRoomId);
                                        _isDeductionBoardOpen = false;
                                        _selectedWordBankClue = null;
                                        _insertTargetSlot     = null;

                                        int done = SolvedRoomCount();
                                        ShowToast(done == _caseRooms.Count
                                            ? "All rooms solved - the FINAL SOLVE board is unlocked!"
                                            : $"Room solved! ({done}/{_caseRooms.Count}) " +
                                              "Solve every room to unlock the FINAL SOLVE.");
                                    }
                                }
                            }

                            // Left page slot clicks -> switch active tab + target the slot
                            // for INSERT. Clicking a FILLED slot removes its clue.
                            foreach (var slot in activeDeduction.Slots)
                            {
                                if (slot.Bounds.Contains(vm))
                                {
                                    if (!boardLocked && slot.SelectedClueId != null)
                                    {
                                        slot.SelectedClueId = null;
                                        activeDeduction.ValidationMessage = "";
                                    }
                                    _activeTab            = slot.Category;
                                    _wordBankPage         = 0;
                                    _selectedWordBankClue = null;
                                    _insertTargetSlot     = slot;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (_solveButtonRect.Contains(vm))
                        {
                            _isFinalSolveMode     = false;
                            _isDeductionBoardOpen = true;
                            _selectedWordBankClue = null;
                            _wordBankPage         = 0;
                        }
                        else if (_finalSolveButtonRect.Contains(vm))
                        {
                            if (AllRoomsSolved)
                            {
                                _isFinalSolveMode     = true;
                                _isDeductionBoardOpen = true;
                                _selectedWordBankClue = null;
                                _wordBankPage         = 0;
                            }
                            else
                            {
                                ShowToast($"Locked - solve every room's deduction first " +
                                          $"({SolvedRoomCount()}/{_caseRooms.Count} solved).");
                            }
                        }
                    }
                }

                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_toastTimer > 0f)
                    _toastTimer -= dt;

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

                    // Transfer zones: check first; a doorway takes priority over props.
                    _activeTransferZone = null;
                    foreach (var zone in _transferZones)
                    {
                        if (_cat.CollisionBox.Intersects(zone.TriggerRect))
                        {
                            _activeTransferZone = zone;
                            break;
                        }
                    }

                    if (_activeTransferZone != null && _cat.IsInteractPressed())
                    {
                        LoadRoom(_activeTransferZone.TargetRoom, _activeTransferZone.TargetSpawn);
                        return; // state was rebuilt; skip rest of this Update
                    }

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
                                var fresh = LevelConfigParser.LoadRoom(_levelConfigSourcePath);
                                foreach (var entity in _interactables)
                                {
                                    if (fresh.Interactables.TryGetValue(entity.Id, out var data))
                                        entity.Data = data;
                                }
                                Console.WriteLine("[HotReload] room_config.json reloaded.");
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
                    foreach (var zone in _transferZones)
                        DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, zone.TriggerRect, Color.Lime);
                    foreach (var entity in _interactables)
                        DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, entity.TriggerZone, Color.Yellow);
                    DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, _cat.CollisionBox, Color.Cyan);
                    _spriteBatch.End();
                }

                // ════════════════════════════════════════════════════════════════
                // PASS 6 — DIALOGUE UI
                // ════════════════════════════════════════════════════════════════
                if (_isDialogueActive && _currentInteraction != null)
                {
                    int boxW    = Math.Min(1400, SCREEN_WIDTH - 40);
                    var boxRect = new Rectangle(
                        (SCREEN_WIDTH - boxW) / 2,
                        SCREEN_HEIGHT - 450 - 40,
                        boxW, 450);

                    const int PAD_X = 140;
                    const int PAD_Y = 100;

                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                    _spriteBatch.Draw(_dialogueBoxTex, boxRect, Color.White);

                    int totalCharsOnPage = _dialoguePages[_currentDialoguePage]
                        .Replace("[", "").Replace("]", "").Length;
                    bool typingDone = _typewriterTimer >= totalCharsOnPage;

                    // Dialogue text renders below full size so long clue texts stay
                    // inside the box art instead of running past its borders.
                    const float dialogueScale = 0.72f;
                    DrawRichText(
                        _spriteBatch,
                        _dialogueFont,
                        _dialoguePages[_currentDialoguePage],
                        _currentInteraction.Keywords,
                        new Vector2(boxRect.X + PAD_X, boxRect.Y + PAD_Y),
                        boxRect.Width - PAD_X * 2,
                        (int)_typewriterTimer,
                        dialogueScale);

                    bool isLastPage = _currentDialoguePage >= _dialoguePages.Length - 1;
                    string hint     = typingDone
                        ? (isLastPage ? "[ Enter ] to dismiss" : "[ Enter ] to continue")
                        : "[ Enter ] to skip";
                    var    hintSize = _dialogueFont.MeasureString(hint) * dialogueScale;
                    _spriteBatch.DrawString(
                        _dialogueFont,
                        hint,
                        new Vector2(
                            boxRect.Right  - PAD_X - hintSize.X,
                            boxRect.Bottom - PAD_Y * 0.6f - hintSize.Y),
                        new Color(90, 70, 50),
                        0f, Vector2.Zero, dialogueScale, SpriteEffects.None, 0f);
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
                        DrawUiButton(_solveButtonRect, new Color(170, 120, 30), "SOLVE", Color.White);

                        // FINAL SOLVE — locked until every room is solved; shows progress.
                        if (AllRoomsSolved)
                        {
                            DrawUiButton(_finalSolveButtonRect, new Color(190, 60, 45),
                                "FINAL\nSOLVE", Color.White);
                        }
                        else
                        {
                            DrawUiButton(_finalSolveButtonRect, new Color(70, 70, 78),
                                $"FINAL SOLVE\n{SolvedRoomCount()}/{_caseRooms.Count} rooms",
                                Color.LightGray, maxLabelScale: 0.6f);
                        }
                    }

                    // Transfer-zone prompt: centred at the bottom of the play area.
                    if (_activeTransferZone != null)
                    {
                        const string enterHint   = "[ Enter ] to enter";
                        var          enterHintSz = _dialogueFont.MeasureString(enterHint);
                        _spriteBatch.DrawString(
                            _dialogueFont, enterHint,
                            new Vector2(
                                (SCREEN_WIDTH  - enterHintSz.X) * 0.5f,
                                SCREEN_HEIGHT  - enterHintSz.Y - 60),
                            Color.White);
                    }

                    // Clue counters: current room progress + case-wide total.
                    if (!_isGameWon && !_isDeductionBoardOpen)
                    {
                        int roomFound = 0;
                        foreach (var c in _notebook.UnlockedClues)
                            if (_currentRoomClueIds.Contains(c.Id)) roomFound++;

                        const float hudScale = 0.8f;
                        string hud = $"Clues ({ToDisplayName(_currentRoomId)}): " +
                                     $"{roomFound}/{_currentRoomClueIds.Count}    " +
                                     $"Case: {_notebook.UnlockedClues.Count}/{_notebook.TotalClueCount}";
                        _spriteBatch.DrawString(_dialogueFont, hud, new Vector2(26, 18),
                            Color.Black * 0.6f, 0f, Vector2.Zero, hudScale, SpriteEffects.None, 0f);
                        _spriteBatch.DrawString(_dialogueFont, hud, new Vector2(24, 16),
                            Color.White, 0f, Vector2.Zero, hudScale, SpriteEffects.None, 0f);
                    }

                    // Transient toast — bottom-center, fades out at the end.
                    if (_toastTimer > 0f && !_isGameWon)
                    {
                        const float toastScale = 0.8f;
                        float alpha   = Math.Min(1f, _toastTimer / 0.5f);
                        var   toastSz = _dialogueFont.MeasureString(_toastMessage) * toastScale;
                        var   toastPos = new Vector2(
                            (SCREEN_WIDTH - toastSz.X) * 0.5f,
                            SCREEN_HEIGHT - toastSz.Y - 120);
                        var bgRect = new Rectangle(
                            (int)(toastPos.X - 20), (int)(toastPos.Y - 10),
                            (int)(toastSz.X + 40),  (int)(toastSz.Y + 20));
                        _spriteBatch.Draw(_debugPixel, bgRect, Color.Black * (0.65f * alpha));
                        _spriteBatch.DrawString(_dialogueFont, _toastMessage, toastPos,
                            Color.White * alpha, 0f, Vector2.Zero, toastScale, SpriteEffects.None, 0f);
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
                        // Scale journal areas from 2020×1136 reference space to current canvas.
                        float jsx = (float)SCREEN_WIDTH  / 2020f;
                        float jsy = (float)SCREEN_HEIGHT / 1136f;
                        int J(float v, float s) => (int)(v * s);
                        var leftPageArea  = new Rectangle(J(250,jsx), J(300,jsy), J(650,jsx), J(600,jsy));
                        var wordBankArea  = new Rectangle(J(1050,jsx), J(280,jsy), J(750,jsx), J(400,jsy));
                        var inspectorArea = new Rectangle(J(1150,jsx), J(800,jsy), J(600,jsx), J(250,jsy));

                        // Journal text scales: the panel rects shrink with the canvas, so
                        // the 32pt font must shrink with them or it overflows every box.
                        float jt  = 0.80f * jsy;   // body text
                        float jti = 0.66f * jsy;   // inspector text (longest content, densest)

                        // ── LEFT PAGE: Mad-Libs sentence ──────────────────────
                        float lx      = leftPageArea.X;
                        float ly      = leftPageArea.Y;
                        float lineH   = _dialogueFont.LineSpacing * jt + 12 * jsy;
                        float spaceW  = _dialogueFont.MeasureString(" ").X * jt;
                        int   maxRight = leftPageArea.Right;

                        //var sentTitleSz = _dialogueFont.MeasureString("What happened?");
                        //_spriteBatch.DrawString(_dialogueFont, "What happened?",
                          //  new Vector2(leftPageArea.X, leftPageArea.Y - sentTitleSz.Y - 12),
                            //_tabColors[(int)_activeTab]);

                        string[] slotCatLabels = { "WHO", "WHAT", "WHY", "WHERE/WHEN" };

                        var activeDeduction = _isFinalSolveMode ? _deduction : _localDeduction;

                        foreach (var seg in activeDeduction.Segments)
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
                                        float  wordW = _dialogueFont.MeasureString(word).X * jt;
                                        if (lx > leftPageArea.X && lx + wordW > maxRight)
                                        { lx = leftPageArea.X; ly += lineH; }
                                        _spriteBatch.DrawString(_dialogueFont, word,
                                            new Vector2(lx, ly), _inkColor,
                                            0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
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
                                float textW  = _dialogueFont.MeasureString(label).X * jt;
                                int   slotW  = (int)(textW + 20 * jt);
                                int   slotH  = (int)(_dialogueFont.LineSpacing * jt + 8);
                                if (lx > leftPageArea.X && lx + slotW > maxRight)
                                { lx = leftPageArea.X; ly += lineH; }
                                var slotColor = _tabColors[(int)slot.Category];
                                var slotRect  = new Rectangle((int)lx, (int)ly, slotW, slotH);
                                _spriteBatch.Draw(_debugPixel, slotRect, slotColor * 0.3f);
                                DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, slotRect, slotColor);
                                float tX = slotRect.X + (slotRect.Width  - textW) * 0.5f;
                                float tY = slotRect.Y + (slotRect.Height - _dialogueFont.LineSpacing * jt) * 0.5f;
                                _spriteBatch.DrawString(_dialogueFont, label, new Vector2(tX, tY), _inkColor,
                                    0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                                slot.Bounds = slotRect;
                                lx += slotW + 8;
                            }
                        }

                        // ── LEFT PAGE: rooms overview — solved state + clue counts.
                        // Shown on every board so the player can see which room still
                        // hides clues and which deductions remain.
                        {
                            float oy = Math.Max(ly + lineH * 1.6f, J(560, jsy));
                            _spriteBatch.DrawString(_dialogueFont, "Investigation:",
                                new Vector2(leftPageArea.X, oy), _inkColor,
                                0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                            oy += lineH;
                            foreach (var room in _caseRooms)
                            {
                                bool solved = _roomSolvedStates.TryGetValue(room, out var s) && s;
                                var (found, total) = _notebook.GetRoomClueCounts(room);
                                _spriteBatch.DrawString(_dialogueFont,
                                    $"{(solved ? "[x]" : "[  ]")} {ToDisplayName(room)}  -  clues {found}/{total}",
                                    new Vector2(leftPageArea.X, oy),
                                    solved ? new Color(30, 130, 50)
                                           : (found == total ? _inkColor : Color.Gray),
                                    0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                                oy += _dialogueFont.LineSpacing * jt + 6;
                            }
                        }

                        // Case-wide clue counter — top of the left page.
                        _spriteBatch.DrawString(_dialogueFont,
                            $"Case clues: {_notebook.UnlockedClues.Count}/{_notebook.TotalClueCount}",
                            new Vector2(leftPageArea.X, J(230, jsy)), _inkColor * 0.8f,
                            0f, Vector2.Zero, jt, SpriteEffects.None, 0f);

                        // ── RIGHT PAGE: Tab bar (pre-rendered full-bar image) ──
                        // Scaled with the canvas so the art lands exactly on the hotspots.
                        _spriteBatch.Draw(_tabTextures[_activeTab], _tabImagePos, null, Color.White,
                            0f, Vector2.Zero, new Vector2(jsx, jsy), SpriteEffects.None, 0f);

                        // ── RIGHT PAGE: Word Bank (flow layout) ───────────────
                        float spacingX  = 14f * jsx;
                        float spacingY  = 12f * jsy;
                        float boxHeight = _dialogueFont.LineSpacing * jt + 10f;

                        float currentX   = wordBankArea.X;
                        float currentY   = wordBankArea.Y;
                        int   flowPage   = 0;

                        _wordBankClueRects.Clear();

                        var sourceClues = _isFinalSolveMode
                            ? _notebook.GetMacroClues()
                            : _notebook.GetCluesForRoom(_currentRoomId);
                        var filteredWB = sourceClues.FindAll(c => c.Category == _activeTab);

                        foreach (var cl in filteredWB)
                        {
                            var   textSz   = _dialogueFont.MeasureString(cl.Name) * jt;
                            float boxWidth = textSz.X + 20f * jt;

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
                                    sel ? Color.White : _inkColor,
                                    0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                                _wordBankClueRects.Add((tagR, cl));
                            }

                            currentX += boxWidth + spacingX;
                        }

                        _wordBankPageCount = flowPage; // written here, read by Update

                        if (filteredWB.Count == 0)
                        {
                            _spriteBatch.DrawString(_dialogueFont, "-- no clues found yet --",
                                new Vector2(wordBankArea.X, wordBankArea.Y + 10), Color.Gray,
                                0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                        }

                        // ── Paging controls ───────────────────────────────────
                        if (_wordBankPageCount > 0)
                        {
                            if (_wordBankPage > 0)
                                DrawUiButton(_journalPrevPageRect, new Color(40, 40, 60), "<", Color.White);
                            if (_wordBankPage < _wordBankPageCount)
                                DrawUiButton(_journalNextPageRect, new Color(40, 40, 60), ">", Color.White);

                            var pageLbl   = $"{_wordBankPage + 1} / {_wordBankPageCount + 1}";
                            var pageLblSz = _dialogueFont.MeasureString(pageLbl) * jt;
                            _spriteBatch.DrawString(_dialogueFont, pageLbl,
                                new Vector2(
                                    (_journalPrevPageRect.Right + _journalNextPageRect.X) * 0.5f
                                        - pageLblSz.X * 0.5f,
                                    _journalPrevPageRect.Y + (_journalPrevPageRect.Height - pageLblSz.Y) * 0.5f),
                                Color.LightGray, 0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                        }

                        // ── Inspector Panel (no debug background) ─────────────
                        if (_selectedWordBankClue != null)
                        {
                            _spriteBatch.DrawString(_dialogueFont, _selectedWordBankClue.Name,
                                new Vector2(inspectorArea.X, inspectorArea.Y),
                                _tabColors[(int)_selectedWordBankClue.Category],
                                0f, Vector2.Zero, jt, SpriteEffects.None, 0f);

                            // The room-deduction recap only appears on the final board,
                            // where cross-room recall matters; on local boards it read
                            // as clutter glued under the description.
                            string  descText   = _selectedWordBankClue.InspectorDescription;
                            string? recapText  = null;
                            if (_isFinalSolveMode && _selectedWordBankClue.IsMacroClue &&
                                _roomSolvedSentences.TryGetValue(_selectedWordBankClue.RoomId,
                                                                 out var sourceSentence))
                                recapText =
                                    $"{ToDisplayName(_selectedWordBankClue.RoomId)}: \"{sourceSentence}\"";

                            // Auto-fit: shrink the text scale until description (+ recap)
                            // fits inside the inspector paper instead of spilling out.
                            float availH   = inspectorArea.Height - (_dialogueFont.LineSpacing * jt + 6);
                            float fitScale = jti;
                            for (int tries = 0; tries < 6; tries++)
                            {
                                float h = MeasureWrappedHeight(_dialogueFont, descText,
                                    inspectorArea.Width, _dialogueFont.LineSpacing * fitScale + 4, fitScale);
                                if (recapText != null)
                                    h += 6 + MeasureWrappedHeight(_dialogueFont, recapText,
                                        inspectorArea.Width, _dialogueFont.LineSpacing * fitScale + 4, fitScale);
                                if (h <= availH) break;
                                fitScale *= 0.88f;
                            }

                            float descBottom = DrawWrappedString(_spriteBatch, _dialogueFont,
                                descText,
                                new Vector2(inspectorArea.X, inspectorArea.Y + _dialogueFont.LineSpacing * jt + 6),
                                inspectorArea.Width, _dialogueFont.LineSpacing * fitScale + 4, _inkColor, fitScale);

                            if (recapText != null)
                            {
                                DrawWrappedString(_spriteBatch, _dialogueFont, recapText,
                                    new Vector2(inspectorArea.X, descBottom + 6),
                                    inspectorArea.Width, _dialogueFont.LineSpacing * fitScale + 4,
                                    new Color(95, 75, 130), fitScale);
                            }
                            DrawUiButton(_journalInsertRect,
                                _tabColors[(int)_selectedWordBankClue.Category], "INSERT", Color.White,
                                maxLabelScale: jt);
                        }

                        // A solved room's board is a locked recap — no CLEAR/SUBMIT.
                        bool drawLocked = !_isFinalSolveMode &&
                            _roomSolvedStates.TryGetValue(_currentRoomId, out var roomDone) && roomDone;

                        // ── Submit / clear & validation ───────────────────────
                        if (!string.IsNullOrEmpty(activeDeduction.ValidationMessage))
                        {
                            bool isCorrect = activeDeduction.ValidationMessage.StartsWith("Case");
                            var  vmSz      = _dialogueFont.MeasureString(activeDeduction.ValidationMessage) * jt;
                            _spriteBatch.DrawString(_dialogueFont, activeDeduction.ValidationMessage,
                                new Vector2(
                                    _journalSubmitRect.X + (_journalSubmitRect.Width  - vmSz.X) * 0.5f,
                                    _journalSubmitRect.Y - vmSz.Y - 8),
                                isCorrect ? Color.LimeGreen : Color.OrangeRed,
                                0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                        }
                        if (!drawLocked)
                        {
                            DrawUiButton(_journalClearRect,  new Color(110, 60, 40), "CLEAR",  Color.White,
                                maxLabelScale: jt);
                            DrawUiButton(_journalSubmitRect, new Color(20, 100, 40), "SUBMIT", Color.White,
                                maxLabelScale: jt);
                        }

                        // ── Close button (X) ──────────────────────────────────
                        DrawUiButton(_journalCloseRect, new Color(100, 40, 40), "X", Color.White);
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
                new Rectangle(0, 0, (int)(SCREEN_WIDTH * DISPLAY_SCALE), (int)(SCREEN_HEIGHT * DISPLAY_SCALE)),
                Color.White);
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private int SolvedRoomCount()
        {
            int n = 0;
            foreach (var solved in _roomSolvedStates.Values)
                if (solved) n++;
            return n;
        }

        private void ShowToast(string message)
        {
            _toastMessage = message;
            _toastTimer   = TOAST_DURATION;
        }

        /// <summary>
        /// Code-drawn UI button: drop shadow, fill, lighter border, hover highlight,
        /// and a label auto-scaled to fit inside the rect.
        /// </summary>
        private void DrawUiButton(Rectangle rect, Color fill, string label, Color labelColor,
                                  float maxLabelScale = 0.85f)
        {
            var mouse   = Mouse.GetState();
            var vm      = new Point((int)(mouse.X / DISPLAY_SCALE), (int)(mouse.Y / DISPLAY_SCALE));
            bool hover  = rect.Contains(vm);

            _spriteBatch.Draw(_debugPixel,
                new Rectangle(rect.X + 4, rect.Y + 4, rect.Width, rect.Height),
                Color.Black * 0.35f);
            _spriteBatch.Draw(_debugPixel, rect, hover ? Color.Lerp(fill, Color.White, 0.18f) : fill);
            DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, rect,
                Color.Lerp(fill, Color.White, hover ? 0.7f : 0.45f));

            var size  = _dialogueFont.MeasureString(label);
            float fitW = (rect.Width  - 16) / Math.Max(size.X, 1f);
            float fitH = (rect.Height - 10) / Math.Max(size.Y, 1f);
            float scale = Math.Min(maxLabelScale, Math.Min(fitW, fitH));
            var pos = new Vector2(
                rect.X + (rect.Width  - size.X * scale) * 0.5f,
                rect.Y + (rect.Height - size.Y * scale) * 0.5f);
            _spriteBatch.DrawString(_dialogueFont, label, pos + new Vector2(2, 2),
                Color.Black * 0.4f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_dialogueFont, label, pos,
                labelColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private float DrawWrappedString(
            SpriteBatch spriteBatch,
            SpriteFont  font,
            string      text,
            Vector2     origin,
            float       maxWidth,
            float       lineHeight,
            Color       color,
            float       scale = 1f)
        {
            float x = origin.X, y = origin.Y;
            string[] words = text.Split(' ');
            foreach (var word in words)
            {
                float wordW  = font.MeasureString(word).X * scale;
                float spaceW = font.MeasureString(" ").X * scale;
                if (x > origin.X && x + wordW > origin.X + maxWidth)
                {
                    x  = origin.X;
                    y += lineHeight;
                }
                spriteBatch.DrawString(font, word, new Vector2(x, y), color,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                x += wordW + spaceW;
            }
            return y + lineHeight;
        }

        /// <summary>Height DrawWrappedString would occupy — same wrap rules, no drawing.</summary>
        private static float MeasureWrappedHeight(
            SpriteFont font, string text, float maxWidth, float lineHeight, float scale)
        {
            float x = 0f, y = 0f;
            foreach (var word in text.Split(' '))
            {
                float wordW  = font.MeasureString(word).X * scale;
                float spaceW = font.MeasureString(" ").X * scale;
                if (x > 0f && x + wordW > maxWidth)
                {
                    x  = 0f;
                    y += lineHeight;
                }
                x += wordW + spaceW;
            }
            return y + lineHeight;
        }

        // ── Layout ────────────────────────────────────────────────────────────
        // Recomputes all UI rectangles for the current SCREEN_WIDTH × SCREEN_HEIGHT.
        // All source coordinates are in the original 2020×1136 design space.
        private void UpdateLayout()
        {
            float sx = (float)SCREEN_WIDTH  / 2020f;
            float sy = (float)SCREEN_HEIGHT / 1136f;
            int S(float v) => (int)v;

            _solveButtonRect      = new Rectangle(S((2020 - 160) * sx), S((1136 - 160) * sy), S(120 * sx), S(120 * sy));
            _finalSolveButtonRect = new Rectangle(S((2020 - 160) * sx), S((1136 - 290) * sy), S(120 * sx), S(110 * sy));

            // Tab bar image is 800×150 (four 200px humps), drawn scaled at _tabImagePos —
            // hotspots mirror those humps exactly in the same reference space.
            _tabImagePos = new Vector2(1050 * sx, 100 * sy);
            _tabHotspots = new Rectangle[4];
            for (int i = 0; i < 4; i++)
                _tabHotspots[i] = new Rectangle(
                    S((1050 + i * 200) * sx), S(100 * sy), S(200 * sx), S(150 * sy));

            _journalPrevPageRect = new Rectangle(S(1050 * sx), S(692 * sy), S(90 * sx), S(35 * sy));
            _journalNextPageRect = new Rectangle(S(1710 * sx), S(692 * sy), S(90 * sx), S(35 * sy));
            // Top-right of the inspector paper, level with the clue name —
            // keeps the button clear of the wrapped description text.
            _journalInsertRect   = new Rectangle(S(1610 * sx), S(800 * sy), S(140 * sx), S(50 * sy));
            _journalSubmitRect   = new Rectangle(S(1400 * sx), S(1040 * sy), S(270 * sx), S(70 * sy));
            _journalClearRect    = new Rectangle(S(1200 * sx), S(1040 * sy), S(170 * sx), S(70 * sy));
            _journalCloseRect    = new Rectangle(S(1930 * sx), S(30  * sy), S(60  * sx), S(60 * sy));

        }

        // ── Screenshot helper ──────────────────────────────────────────────────
        private void SaveScreenshot()
        {
            // Navigate three levels up from the binary folder to the project root.
            string dir = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "debug_output"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"{_screenshotCase}_{_screenshotRoom}.png");
            using var stream = File.OpenWrite(path);
            _renderTarget.SaveAsPng(stream, _renderTarget.Width, _renderTarget.Height);
            Console.WriteLine($"[Screenshot] Saved → {Path.GetFullPath(path)}");
        }

        private static readonly Color _inkColor = new Color(40, 30, 20);

        /// <summary>"living_room" → "Living Room".</summary>
        private static string ToDisplayName(string roomId)
        {
            var parts = roomId.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
            return string.Join(' ', parts);
        }

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
            int         maxChars = int.MaxValue,
            float       scale    = 1f)
        {
            var   spans      = ParseSpans(text, keywords);
            float x          = origin.X;
            float y          = origin.Y;
            float lineH      = font.LineSpacing * scale;
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
                    float  tokenW = font.MeasureString(token).X * scale;

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
                        float  drawTokenW = font.MeasureString(drawToken).X * scale;

                        if (highlightColor != Color.Transparent)
                        {
                            spriteBatch.Draw(_debugPixel,
                                new Rectangle((int)x, (int)(y + 8 * scale),
                                              (int)drawTokenW, (int)(lineH - 12 * scale)),
                                highlightColor * 0.4f);
                        }
                        spriteBatch.DrawString(font, drawToken, new Vector2(x, y), _inkColor,
                            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                        x          += tokenW;
                        charsDrawn += token.Length;
                    }
                }
            }
        }
    }
}
