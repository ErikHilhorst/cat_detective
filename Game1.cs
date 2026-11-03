using CatDetective.Entities;
using CatDetective.Map;
using CatDetective.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;
using System.IO;

namespace CatDetective
{
    /// <summary>
    /// Root game class for Dikkie Detective.
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
    ///   Pass 4b — Indicators (speech bubble over characters with unheard topics,
    ///             paw-print arrows over transfer zones)
    ///   Pass 5 — Debug overlay (F1)
    ///   Pass 6 — Dialogue UI
    ///   Pass 7 — Notebook UI
    ///   Pass 8 — Deduction board
    ///   Pass 9 — CRT overlay (optional, all states; drawn into the render target)
    /// </summary>
    public partial class Game1 : Game
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

        // ── Audio / user settings ──────────────────────────────────────────────
        private Song?        _bgMusic;
        private SettingsData _settings = new();
        private CrtOverlay   _crt      = null!;
#if BLAZORGL
        private bool         _webMusicStarted;   // set on the first user gesture
#endif

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

        // Rooms entered at least once this case - feeds RequiresVisited transfer
        // gates (linear-order beats, e.g. bedroom before living room).
        private readonly HashSet<string> _visitedRooms = new();

        private bool IsTransferUnlocked(TransferZone zone) =>
            zone.RequiresVisited.Length == 0 || _visitedRooms.Contains(zone.RequiresVisited);

        // ── Interaction system ─────────────────────────────────────────────────
        private Dictionary<string, InteractionData> _interactionDatabase = null!;
        private bool             _isDialogueActive;
        private InteractionData? _currentInteraction;

        // ── Interrogation topic menu ───────────────────────────────────────────
        private Keyword[] _currentKeywords = Array.Empty<Keyword>();  // keywords of the segment on screen
        private bool      _isTopicMenuOpen;
        private int       _selectedTopicIndex;
        private readonly List<(DialogueTopic Topic, int Index)> _menuTopics = new();
        private string    _dialogueEntityId = "";
        private readonly HashSet<string> _visitedTopics = new();      // "room/entity/topicIndex"
        private bool      _menuUpPressed, _menuDownPressed;           // edge-detected each Update

        // Solve-gate toast: solved room id -> characters (name, roomId) whose
        // confrontation topic that solve unlocks.
        private readonly Dictionary<string, List<(string Name, string RoomId)>> _solveGateIndex = new();

        // Every requiresSolve topic in the case is a confrontation; the FINAL SOLVE
        // board stays locked until each one has been heard (visited).
        private readonly List<(string RoomId, string EntityId, int TopicIndex, string Name)>
            _confrontationTopics = new();

        // Clue ids with at least one ungated source (intro/object keywords, or a
        // topic without gates). Clues missing from this set only drop from gated
        // topics - confrontation evidence - so the SOLVE-button pulse ignores
        // them: it must be able to fire before the confrontations happen.
        private readonly HashSet<string> _ungatedClueIds = new();

        // ── Notebook / inventory ───────────────────────────────────────────────
        private NotebookManager _notebook = null!;
        private MouseState      _prevMouseState;

        // ── Deduction board ────────────────────────────────────────────────────
        private DeductionManager _deduction            = null!;
        private bool             _isDeductionBoardOpen = false;
        private bool             _isFinalSolveMode     = false; // true = macro board, false = local room board

        private bool AllRoomsSolved =>
            _roomSolvedStates.Count > 0 && !_roomSolvedStates.ContainsValue(false);

        private bool IsConfrontationHeard((string RoomId, string EntityId, int TopicIndex, string Name) c) =>
            _visitedTopics.Contains($"{c.RoomId}/{c.EntityId}/{c.TopicIndex}");

        private bool AllConfrontationsHeard
        {
            get
            {
                foreach (var c in _confrontationTopics)
                    if (!IsConfrontationHeard(c)) return false;
                return true;
            }
        }

        private int HeardConfrontationCount()
        {
            int n = 0;
            foreach (var c in _confrontationTopics)
                if (IsConfrontationHeard(c)) n++;
            return n;
        }

        // Journal (two-page deduction board) UI state
        private ClueCategory _activeTab            = ClueCategory.Who;
        private Clue?        _selectedWordBankClue = null;
        private int          _wordBankPage      = 0;
        private int          _wordBankPageCount = 0; // last page index, written by Draw, read by Update
        private readonly List<(Rectangle Rect, Clue Clue)> _wordBankClueRects = new();

        // Casebook page 2 — "Case Notes": the investigation score card (room checklist,
        // case clue total, solved-deduction recaps). Kept OFF the deduction spread so
        // the board itself stays uncluttered; the player turns the page to review progress.
        private bool _journalOnNotesPage = false;

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
        private Rectangle _journalTurnPageRect; // board page, bottom-right: forward to Case Notes
        private Rectangle _journalTurnBackRect; // notes page, bottom-left: back to the board

        // Transient HUD toast (progression feedback: room solved, FINAL SOLVE locked, ...)
        // Queued so back-to-back events (room solved + confrontation unlocked) both show.
        private string _toastMessage = "";
        private float  _toastTimer;
        private readonly Queue<string> _toastQueue = new();
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
        private Texture2D   _notesBgTex      = null!; // casebook page 2 (score card spread)
        private Texture2D   _speechBubbleTex = null!; // Pass 4b: unheard-topics marker
        private Texture2D   _arrowTex        = null!; // Pass 4b: doorway marker (art points right)
        private Dictionary<ClueCategory, Texture2D> _tabTextures = new(); // deduction board bar (Pass 8)

        // ── Dialogue pagination & typewriter ──────────────────────────────────
        private string[] _dialoguePages        = Array.Empty<string>();
        private int      _currentDialoguePage  = 0;
        private float    _typewriterTimer       = 0f;
        private const float TYPEWRITER_SPEED    = 45f;

        // ── Dialogue box geometry (shared by Pass 6 and PaginateDialogue) ─────
        private const int   DIALOGUE_PAD_X      = 140;
        private const int   DIALOGUE_PAD_Y      = 100;
        private const float DIALOGUE_TEXT_SCALE = 0.72f;
        private Rectangle DialogueBoxRect
        {
            get
            {
                int boxW = Math.Min(1400, SCREEN_WIDTH - 40);
                return new Rectangle((SCREEN_WIDTH - boxW) / 2, SCREEN_HEIGHT - 450 - 40, boxW, 450);
            }
        }

        // ── Dialogue portrait: top-crop of the speaker's own sprite ───────────
        // Characters only (entities with topics); reserves a fixed text shift so
        // pagination and drawing agree on the wrap width.
        private Texture2D? _dialoguePortrait;
        private Rectangle  _dialoguePortraitSource;
        private const int PORTRAIT_MAX_W    = 300;
        private const int PORTRAIT_MAX_H    = 280;
        private const int PORTRAIT_TEXT_GAP = 24;

        // ── Debug overlay ──────────────────────────────────────────────────────
        private Texture2D     _debugPixel  = null!;
        private bool          _showDebug   = false;  // F1 toggles
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
        private string   _caseConfigSourcePath  = "";
        private DateTime _caseConfigLastWrite;
        private float    _hotReloadTimer;

        // ── Game state / scene selection ───────────────────────────────────────
        // MainMenu is the boot state; DevMenu (the scene picker) stays reachable
        // from it via F12 as a dev tool.
        private enum GameState { MainMenu, Settings, CaseIntro, Playing, EndScene, DevMenu, Cutscene }
        private GameState    _currentState    = GameState.MainMenu;
        private List<string> _availableScenes = new();

        // ── Main menu / settings UI ────────────────────────────────────────────
        // Web build: a browser tab cannot close itself, so QUIT is dropped.
        private static readonly string[] _mainMenuLabels =
#if BLAZORGL
            { "Continue", "New Game", "Tutorial", "Settings" };
#else
            { "Continue", "New Game", "Tutorial", "Settings", "Quit" };
#endif
        private int         _menuIndex           = 1;   // default: NEW GAME
        // Title splash (boot only): sleeping Dikkie + logo until any key/click,
        // then _menuRevealT eases 0 -> 1 while the menu panel slides in.
        private bool        _titleSplash         = true;
        private float       _menuRevealT;
        private Texture2D   _menuRoomTex = null!, _menuLogoTex = null!, _menuCatTex = null!;
        private Rectangle[] _mainMenuButtonRects = Array.Empty<Rectangle>();
        private Rectangle   _settingsBackRect;
        private Rectangle   _settingsVolMinusRect;
        private Rectangle   _settingsVolPlusRect;
        private Rectangle[] _settingsVolCellRects = Array.Empty<Rectangle>();
        private Rectangle   _settingsCrtToggleRect;

        // ── Case intro / end scene ─────────────────────────────────────────────
        private Texture2D?     _posterTex;
        private Texture2D?     _rudebeakTex;
        private string[]       _introPages   = Array.Empty<string>();
        private int            _introPageIndex;
        private string         _introCaseId  = "";
        private EndSceneBeat[] _endBeats     = Array.Empty<EndSceneBeat>();
        private int            _endBeatIndex;

        // Cached so the menu doesn't hit the filesystem every frame; refreshed on
        // every save write/delete and on returning to the menu.
        private bool _saveExists;
        // Two-step NEW GAME confirm while a save exists.
        private bool _confirmNewGame;
        // Suppresses gate toasts while a save is being replayed into a fresh case.
        private bool _isRestoring;

        // Fixed canvas for every non-Playing state (rooms resize it per background).
        private const int MENU_CANVAS_W = 1456;
        private const int MENU_CANVAS_H = 816;

        // ── Case / room tracking ───────────────────────────────────────────────
        private string                   _currentCaseId    = "";
        private string                   _currentRoomId    = "";
        private string                   _spawnPointName   = "";
        private Dictionary<string, bool> _roomSolvedStates = new();
        private IReadOnlyList<string>    _caseRooms        = Array.Empty<string>();

        // Filled local sentences captured at solve time (the local DeductionManager
        // is recreated on every LoadRoom, so this is the only durable copy).
        private readonly Dictionary<string, string> _roomSolvedSentences = new();


        // Slot the player last clicked on the board; INSERT prefers it.
        private DeductionSlot? _insertTargetSlot;

        // ── Deduction — macro (case-level) and local (room-level) ─────────────
        private DeductionManager  _localDeduction = null!;

        // Warms upcoming rooms' textures into the ContentManager during frames
        // where a blocking load can't be felt (web loads are synchronous XHRs).
        private RoomPreloader? _preloader;

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
#if BLAZORGL
            // KNI enforces the Reach 2048px texture cap that desktop MonoGame
            // never did - the 2100px cat walk sheets need HiDef (4096 max).
            _graphics.GraphicsProfile = GraphicsProfile.HiDef;
#endif
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

#if !BLAZORGL
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
#endif
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Initialize()
        {
            Window.Title = "Dikkie Detective";
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
            CreateUiTextures();   // cozy UI kit (Game1.UiStyle.cs)
            _notebookBgTex  = Content.Load<Texture2D>("Shared/ui_notebook_bg");
            _notesBgTex     = Content.Load<Texture2D>("Shared/ui_notebook_score_card");
            _menuRoomTex    = Content.Load<Texture2D>("Shared/menu_room");
            _menuLogoTex    = Content.Load<Texture2D>("Shared/menu_logo");
            _menuCatTex     = Content.Load<Texture2D>("Shared/menu_cat");
            LayoutMenuScreens();   // menu lives on the parchment panel (Game1.MenuScreens.cs)
            _speechBubbleTex = Content.Load<Texture2D>("Shared/speech_bubble");
            _arrowTex        = Content.Load<Texture2D>("Shared/arrow");

            _tabTextures[ClueCategory.Who]       = Content.Load<Texture2D>("Shared/who");
            _tabTextures[ClueCategory.What]      = Content.Load<Texture2D>("Shared/how");
            _tabTextures[ClueCategory.Why]       = Content.Load<Texture2D>("Shared/why");
            _tabTextures[ClueCategory.WhereWhen] = Content.Load<Texture2D>("Shared/where");

            string configPath = Path.Combine(Content.RootDirectory, "scenes_config.json");
            _availableScenes = SceneConfigParser.GetAvailableScenes(configPath);

            _settings   = SaveSystem.LoadSettings();
            _saveExists = SaveSystem.SaveExists();
            _menuIndex  = _saveExists ? 0 : 1;
            _crt        = new CrtOverlay(GraphicsDevice);

            // Preload queue, in the order the player is likely to need rooms:
            // the saved room first (CONTINUE lands there), then every case in
            // scene order (each case queues its rooms in rooms-list order, so
            // NEW GAME's entry room leads). Pumped one asset per safe frame
            // from Update(); by the time the menu/intro has been read, the
            // first room is warm and LoadRoom is near-instant.
            if (!_screenshotMode)
            {
                _preloader = new RoomPreloader(Content);
                if (_saveExists && SaveSystem.LoadGame() is { } bootSave)
                    _preloader.QueueRoom(bootSave.CaseId, bootSave.RoomId);
                foreach (var sceneId in _availableScenes)
                    _preloader.QueueCase(sceneId);
            }

            // Music starts with the app (the menu has music too) and loops forever.
            // Skipped in screenshot mode (headless captures).
            // Web: browsers block audio until the first user gesture, so playback
            // starts from Update() instead (raw mp3 via HTMLAudioElement).
#if !BLAZORGL
            if (!_screenshotMode)
            {
                _bgMusic = Content.Load<Song>("Shared/moonlit_cat_case");
                MediaPlayer.IsRepeating = true;
                MediaPlayer.Volume      = _settings.MusicVolume;
                MediaPlayer.Play(_bgMusic);
            }
#endif

            UpdateLayout();

            if (_screenshotMode && _screenshotCase == "ui")
            {
                // Pseudo-case "ui": captures menu/intro/end screens without a level.
                SetupUiScreenshot();
            }
            else if (_screenshotMode)
            {
                LoadCase(_screenshotCase);
                if (_screenshotRoom != _currentRoomId)
                    LoadRoom(_screenshotRoom, "spawn_default");

                // "crt" captures the plain room with the CRT overlay forced on.
                if (_screenshotView == "crt")
                {
                    _settings.CrtEnabled = true;
                }
                // Optional view arg opens the journal so board layouts can be captured.
                else if (_screenshotView == "journal" || _screenshotView == "final")
                {
                    _notebook.UnlockAllCluesForRoom(_currentRoomId);
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
                // "gather[:N]" opens gathering step N's board (all bank chips found).
                else if (_screenshotView.StartsWith("gather") && HasGathering)
                {
                    var parts = _screenshotView.Split(':');
                    _gatheringStep = parts.Length > 1 && int.TryParse(parts[1], out int gs) ? gs : 0;
                    foreach (var id in _caseScript!.GatheringClueIds())
                        _notebook.UnlockClue(id);
                    _gatheringActive = true;
                    OpenGatheringBoard();
                    _selectedWordBankClue = _notebook.GetClue(CurrentGatheringStep!.Answers[0]);
                }
                // "notes" captures casebook page 2 at its worst case: every room
                // marked solved, recap = the room's raw local sentence template.
                else if (_screenshotView == "notes")
                {
                    _notebook.UnlockAllCluesForRoom(_currentRoomId);
                    _isDeductionBoardOpen = true;
                    _journalOnNotesPage   = true;
                    foreach (var room in _caseRooms)
                    {
                        string cfgPath = Path.Combine(Content.RootDirectory,
                            "Levels", _screenshotCase, room, "room_config.json");
                        if (!GameFile.Exists(cfgPath)) continue;
                        var cfg = LevelConfigParser.LoadRoom(cfgPath);
                        if (cfg.LocalDeductionSentence.Length == 0) continue;
                        _roomSolvedStates[room]    = true;
                        _roomSolvedSentences[room] = cfg.LocalDeductionSentence;
                    }
                }
                // "dialogue" opens the room's longest text segment (intro or topic
                // response), fully typed, so text-box fit can be verified from a capture.
                else if (_screenshotView == "dialogue")
                {
                    InteractableEntity? best = null;
                    string    bestText = "";
                    Keyword[] bestKw   = Array.Empty<Keyword>();
                    foreach (var entity in _interactables)
                    {
                        if (entity.Data == null) continue;
                        if (entity.Data.Text.Length > bestText.Length)
                        {
                            best = entity; bestText = entity.Data.Text; bestKw = entity.Data.Keywords;
                        }
                        foreach (var topic in entity.Data.Topics)
                            if (topic.Text.Length > bestText.Length)
                            {
                                best = entity; bestText = topic.Text; bestKw = topic.Keywords;
                            }
                    }
                    if (best?.Data != null)
                    {
                        _currentInteraction  = best.Data;
                        _currentKeywords     = bestKw;
                        _dialogueEntityId    = best.Id;
                        SetDialoguePortrait(best);
                        _dialoguePages       = PaginateDialogue(bestText, _dialoguePortrait != null);
                        _currentDialoguePage = 0;
                        _typewriterTimer     = 999999f;   // fully typed ((int) cast safe)
                        _isDialogueActive    = true;
                    }
                }
                // "topics" opens the room's fullest interrogation menu with every
                // gated topic unlocked, so the worst-case menu layout can be verified.
                else if (_screenshotView == "topics")
                {
                    InteractableEntity? fullest = null;
                    foreach (var entity in _interactables)
                        if (entity.Data != null && entity.Data.Topics.Length >
                            (fullest?.Data?.Topics.Length ?? 0))
                            fullest = entity;
                    if (fullest?.Data != null)
                    {
                        foreach (var topic in fullest.Data.Topics)
                        {
                            if (topic.RequiresClue.Length > 0)
                                _notebook.UnlockClue(topic.RequiresClue);
                            if (topic.RequiresSolve.Length > 0)
                                _roomSolvedStates[topic.RequiresSolve] = true;
                        }
                        _currentInteraction  = fullest.Data;
                        _currentKeywords     = fullest.Data.Keywords;
                        _dialogueEntityId    = fullest.Id;
                        SetDialoguePortrait(fullest);
                        _dialoguePages       = PaginateDialogue(fullest.Data.Text, _dialoguePortrait != null);
                        _currentDialoguePage = 0;
                        _typewriterTimer     = 999999f;
                        _isDialogueActive    = true;
                        OpenTopicMenu();
                    }
                }
                // "tap" tap-selects the room's first character (gold outline +
                // "Tap again" prompt) and plans the walk (path in the F1 overlay).
                else if (_screenshotView == "tap")
                {
                    var who = _interactables.Find(e => e.Data != null && e.Data.Topics.Length > 0)
                              ?? _interactables.Find(e => e.Data != null);
                    if (who != null)
                    {
                        HandleWorldTap(who.SpriteBounds.Center);
                        _showDebug = true;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        private void LoadCase(string caseId)
        {
            // Case-level resets (do not reset on room transitions).
            _isDeductionBoardOpen = false;
            _isFinalSolveMode     = false;
            _activeTab            = ClueCategory.Who;
            _selectedWordBankClue = null;
            _wordBankPage         = 0;
            _hotReloadTimer       = 0f;

            _currentCaseId = caseId;
            _gatheringActive    = false;
            _gatheringStep      = 0;
            _gatheringIntroSeen = false;
            LoadCaseScript(caseId);
            _visitedTopics.Clear();   // heard-topic state must not leak across cases
            _visitedRooms.Clear();    // linear-order gates reset per case

            _sunbeamsMask = Content.Load<Texture2D>("Shared/mask_sunbeams");

            string configPath = Path.Combine(Content.RootDirectory, "scenes_config.json");
            _ambientColor = SceneConfigParser.GetAmbientColor(configPath, caseId);

            // Load case config: global clue database + macro deduction sentence.
            string caseConfigPath = Path.Combine(
                Content.RootDirectory, "Levels", caseId, "case_config.json");
            var caseConfig = LevelConfigParser.LoadCase(caseConfigPath);

            _notebook  = new NotebookManager(caseConfig.Clues);
            BuildGateIndex(caseId, caseConfig.Rooms);
            BuildFinalDeduction(caseConfig);

#if !BLAZORGL
            // Hot-reload also tracks case_config.json (clue texts, final board).
            _caseConfigSourcePath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "Content", "Levels", caseId, "case_config.json"));
            _caseConfigLastWrite = File.Exists(_caseConfigSourcePath)
                ? File.GetLastWriteTime(_caseConfigSourcePath)
                : DateTime.MinValue;
#endif

            // Pre-populate so AllRoomsSolved requires every room, not just the first solved one.
            _caseRooms        = caseConfig.Rooms;
            _roomSolvedStates = new Dictionary<string, bool>();
            foreach (var room in _caseRooms)
                _roomSolvedStates[room] = false;
            _roomSolvedSentences.Clear();

            LoadRoom(caseConfig.Rooms.Count > 0 ? caseConfig.Rooms[0] : "entrance",
                     spawnPointName: "spawn_default");

            _currentState = GameState.Playing;
        }

        /// <summary>Local board for the current room (LoadRoom + hot-reload).</summary>
        private void BuildLocalDeduction(RoomConfig roomConfig)
        {
            _localDeduction = new DeductionManager(
                roomConfig.LocalDeductionSentence,
                roomConfig.LocalDeductionClueIds,
                id => _notebook.GetClue(id)?.Category);
            _insertTargetSlot = null;

            // Re-entering a solved room: show its board as a completed recap.
            if (_roomSolvedStates.TryGetValue(_currentRoomId, out var wasSolved) && wasSolved)
            {
                foreach (var slot in _localDeduction.Slots)
                    if (slot.CorrectClueId != "")
                        slot.SelectedClueId = slot.CorrectClueId;
                _localDeduction.ValidationMessage = "Case solved!";
            }
        }

        /// <summary>Final board from case config (LoadCase + hot-reload).</summary>
        private void BuildFinalDeduction(CaseConfig caseConfig)
        {
            _deduction = new DeductionManager(
                caseConfig.DeductionSentence,
                caseConfig.FinalSolveClueIds,
                id => _notebook.GetClue(id)?.Category);
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
            _isTopicMenuOpen    = false;
            _dialoguePortrait   = null;

            _currentRoomId  = roomId;
            _spawnPointName = spawnPointName;
            _visitedRooms.Add(roomId);   // feeds RequiresVisited transfer gates

            string roomBase = Path.Combine(
                Content.RootDirectory, "Levels", _currentCaseId, roomId);
            string contentBase = $"Levels/{_currentCaseId}/{roomId}";

            // Load room config: props, interactables, local deduction sentence.
            string roomConfigPath = Path.Combine(roomBase, "room_config.json");
            var roomConfig = LevelConfigParser.LoadRoom(roomConfigPath);

            _interactionDatabase = roomConfig.Interactables;
            BuildLocalDeduction(roomConfig);

#if !BLAZORGL
            // Hot-reload tracks room_config.json (dev-only; no source tree on web).
            _levelConfigSourcePath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "Content", "Levels", _currentCaseId, roomId, "room_config.json"));
            _levelConfigLastWrite = File.Exists(_levelConfigSourcePath)
                ? File.GetLastWriteTime(_levelConfigSourcePath)
                : DateTime.MinValue;
#endif

            // Room background — drives the virtual canvas size.
            _bgBase = Content.Load<Texture2D>($"{contentBase}/bg_base");

            // Resize virtual canvas and window to match the background exactly.
            SetCanvas(_bgBase.Width, _bgBase.Height);

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

            // Tap-to-walk: fresh walk grid for this room's collisions.
            _navGrid = new NavGrid(_solidBoundaries, SCREEN_WIDTH, SCREEN_HEIGHT,
                                   _cat.NavHalfWidth, _cat.BoxHeight);
            ClearTapState();

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

#if BLAZORGL
            // WebGL cannot read pixels back from a plain content texture, so the
            // lazy GetData-based silhouette build in InteractableEntity would
            // crash mid-gameplay. Pre-render every silhouette now (render-target
            // route), while no SpriteBatch is open.
            foreach (var entity in _interactables)
                if (entity.Texture != null)
                    InteractableEntity.PrewarmSilhouette(_spriteBatch, entity.Texture);
#endif

            // Tap hit-testing: where each walk-behind prop is opaque, so a tap on
            // the couch never selects the speaker hidden behind it.
            foreach (var prop in _foregroundProps)
                prop.BuildOpacityMask(_spriteBatch);
        }

        /// <summary>
        /// Resizes the virtual canvas, render target, and OS window. No-op when the
        /// size is unchanged. Rooms call this with their background size; every
        /// non-Playing state pins the canvas to MENU_CANVAS_W x MENU_CANVAS_H so
        /// menu layouts stay stable.
        /// </summary>
        private void SetCanvas(int width, int height)
        {
            if (width == SCREEN_WIDTH && height == SCREEN_HEIGHT)
                return;
            SCREEN_WIDTH  = width;
            SCREEN_HEIGHT = height;
            _renderTarget?.Dispose();
            _renderTarget = new RenderTarget2D(GraphicsDevice, SCREEN_WIDTH, SCREEN_HEIGHT);
#if BLAZORGL
            // The browser owns the canvas size; only the virtual canvas changes.
            // Draw() letterbox-blits the render target into whatever the real
            // canvas currently is (see WasmScale).
            WasmScale.GameW = SCREEN_WIDTH;
            WasmScale.GameH = SCREEN_HEIGHT;
#else
            _graphics.PreferredBackBufferWidth  = (int)(SCREEN_WIDTH  * DISPLAY_SCALE);
            _graphics.PreferredBackBufferHeight = (int)(SCREEN_HEIGHT * DISPLAY_SCALE);
            _graphics.ApplyChanges();
#endif
            GameObject.SetScreenHeight(SCREEN_HEIGHT);
            UpdateLayout();
        }

        /// <summary>Closes every in-game overlay and returns to the main menu.</summary>
        private void ReturnToMainMenu()
        {
            _isDialogueActive     = false;
            _isTopicMenuOpen      = false;
            _isDeductionBoardOpen = false;
            _isFinalSolveMode     = false;
            _selectedWordBankClue = null;
            _insertTargetSlot     = null;
            SetCanvas(MENU_CANVAS_W, MENU_CANVAS_H);
            _saveExists     = SaveSystem.SaveExists();
            _confirmNewGame = false;
            _menuIndex      = IsMenuItemEnabled(0) ? 0 : 1;
            _titleSplash    = false;   // the splash is a boot-only moment
            _menuRevealT    = 1f;
            _currentState   = GameState.MainMenu;
        }

        /// <summary>
        /// Snapshots the durable case state into the single save slot. Called on
        /// room transfers, local solves, and Escape-to-menu - not on every clue,
        /// so a window-X quit can lose progress since the last of those (accepted
        /// for a rudimentary save system).
        /// </summary>
        private void AutoSave()
        {
            if (_screenshotMode || _currentCaseId.Length == 0)
                return;

            var save = new SaveData
            {
                CaseId     = _currentCaseId,
                RoomId     = _currentRoomId,
                SpawnPoint = _spawnPointName,
                SavedAtUtc = DateTime.UtcNow.ToString("o"),
            };
            foreach (var clue in _notebook.UnlockedClues)        save.UnlockedClueIds.Add(clue.Id);
            foreach (var (room, solved) in _roomSolvedStates)    save.RoomSolvedStates[room] = solved;
            foreach (var (room, text) in _roomSolvedSentences)   save.RoomSolvedSentences[room] = text;
            foreach (var topic in _visitedTopics)                save.VisitedTopics.Add(topic);
            foreach (var room in _visitedRooms)                  save.VisitedRooms.Add(room);
            // FiredGateToasts stays in the save schema for compatibility, but
            // clue-gate toasts were removed (playtest 5) - nothing to record.

            _saveExists = SaveSystem.SaveGame(save);
        }

        /// <summary>
        /// Rebuilds a saved session: loads the case fresh, then replays the saved
        /// clue ids through the notebook (toasts suppressed via _isRestoring) and
        /// copies the solved/visited state back before entering the saved room.
        /// </summary>
        private void RestoreFromSave(SaveData save)
        {
            string configPath = Path.Combine(
                Content.RootDirectory, "Levels", save.CaseId, "case_config.json");
            if (!GameFile.Exists(configPath))
            {
                Console.WriteLine($"[SaveSystem] Saved case '{save.CaseId}' no longer exists - deleting save.");
                SaveSystem.DeleteSave();
                _saveExists = false;
                return;
            }

            _isRestoring = true;
            LoadCase(save.CaseId);

            foreach (var clueId in save.UnlockedClueIds)
                _notebook.UnlockClue(clueId);

            foreach (var (room, solved) in save.RoomSolvedStates)
                if (_roomSolvedStates.ContainsKey(room))
                    _roomSolvedStates[room] = solved;

            _roomSolvedSentences.Clear();
            foreach (var (room, text) in save.RoomSolvedSentences)
                _roomSolvedSentences[room] = text;

            _visitedTopics.Clear();
            foreach (var topic in save.VisitedTopics)
                _visitedTopics.Add(topic);

            // Visited rooms feed the RequiresVisited transfer gates. Older saves
            // lack the list - seed it with the saved room and every solved room so
            // a mid-case save can't re-lock doors the player already went through.
            foreach (var room in save.VisitedRooms)
                _visitedRooms.Add(room);
            _visitedRooms.Add(save.RoomId);
            foreach (var (room, solved) in save.RoomSolvedStates)
                if (solved) _visitedRooms.Add(room);

            // (save.FiredGateToasts is ignored - clue-gate toasts were removed.)

            // Re-enter the saved room so a solved room's board shows its recap.
            // (_roomSolvedStates holds a key for every room in the case.)
            // Resume at the spawn the player originally entered the room through -
            // never a raw position, so a restore can't land inside collision boxes.
            if (_roomSolvedStates.ContainsKey(save.RoomId) && save.RoomId != _currentRoomId)
                LoadRoom(save.RoomId, string.IsNullOrEmpty(save.SpawnPoint)
                    ? "spawn_default" : save.SpawnPoint);

            // The replay must not leave queued toasts behind.
            _toastQueue.Clear();
            _toastMessage = "";
            _toastTimer   = 0f;

            _isRestoring = false;
        }

        /// <summary>Menu-driven case start; ignores cases that don't exist yet.</summary>
        private void StartCase(string caseId)
        {
            string configPath = Path.Combine(
                Content.RootDirectory, "Levels", caseId, "case_config.json");
            if (!GameFile.Exists(configPath))
            {
                Console.WriteLine($"[Menu] Case '{caseId}' not found - ignoring.");
                return;
            }
            LoadCase(caseId);
        }

        /// <summary>
        /// Case complete: clears the save slot (CONTINUE dims - the case is over)
        /// and rolls the epilogue. Reached only from a valid final-solve submit.
        /// </summary>
        private void StartEndScene(string caseId)
        {
            _endBeats     = CaseScripts.GetEndScene(caseId);
            _endBeatIndex = 0;
            _typewriterTimer = 0f;

            foreach (var beat in _endBeats)
                if (beat.ShowRudebeak)
                    _rudebeakTex ??= Content.Load<Texture2D>("Levels/malibu_mansion/rudebeak");

            if (!_screenshotMode)
            {
                SaveSystem.DeleteSave();
                _saveExists = false;
            }

            _isDialogueActive     = false;
            _isTopicMenuOpen      = false;
            _isDeductionBoardOpen = false;
            _isFinalSolveMode     = false;
            SetCanvas(MENU_CANVAS_W, MENU_CANVAS_H);
            _currentState = GameState.EndScene;
        }

        /// <summary>
        /// Opens the poster-and-typewriter intro for a case, falling straight
        /// through to the case itself when it has no authored intro.
        /// </summary>
        private void StartCaseIntro(string caseId)
        {
            string configPath = Path.Combine(
                Content.RootDirectory, "Levels", caseId, "case_config.json");
            if (!GameFile.Exists(configPath))
            {
                Console.WriteLine($"[Menu] Case '{caseId}' not found - ignoring.");
                return;
            }

            if (TryStartScriptIntro(caseId))
                return;

            var pages = CaseScripts.GetIntro(caseId);
            if (pages.Length == 0)
            {
                LoadCase(caseId);
                return;
            }

            _posterTex ??= Content.Load<Texture2D>("Shared/case_poster");
            _introCaseId     = caseId;
            _introPages      = pages;
            _introPageIndex  = 0;
            // Negative start = a beat of quiet so the poster lands before the
            // typewriter begins (playtest: text started too abruptly).
            _typewriterTimer = -1.2f * TYPEWRITER_SPEED;
            SetCanvas(MENU_CANVAS_W, MENU_CANVAS_H);
            _currentState = GameState.CaseIntro;
        }

        /// <summary>CONTINUE is only offered while a saved case exists.</summary>
        private bool IsMenuItemEnabled(int index) =>
            index != 0 || _saveExists;

        private void ActivateMainMenuItem(int index)
        {
            switch (_mainMenuLabels[index].ToUpperInvariant())   // labels are display-cased
            {
                case "CONTINUE":
                    var save = SaveSystem.LoadGame();
                    if (save != null)
                        RestoreFromSave(save);
                    else
                    {
                        _saveExists = false;
                        _menuIndex  = 1;
                    }
                    break;

                case "NEW GAME":
                    // A second Enter/click confirms overwriting an existing save.
                    if (_saveExists && !_confirmNewGame)
                    {
                        _confirmNewGame = true;
                    }
                    else
                    {
                        _confirmNewGame = false;
                        SaveSystem.DeleteSave();
                        _saveExists = false;
                        // First ever New Game: Training Day first, then straight on
                        // into Malibu (Game1.Tutorial.cs). Marked seen at the start,
                        // so quitting halfway never forces it again.
                        if (!_settings.TutorialSeen)
                        {
                            _settings.TutorialSeen = true;
                            SaveSystem.SaveSettings(_settings);
                            _tutorialLeadsToCase = "malibu_mansion";
                            StartCaseIntro("tutorial");
                        }
                        else
                        {
                            StartCaseIntro("malibu_mansion");
                        }
                    }
                    break;

                case "TUTORIAL":
                    _tutorialLeadsToCase = null;   // replay from the menu: back to the menu after
                    StartCaseIntro("tutorial");
                    break;
                case "SETTINGS": _currentState = GameState.Settings;    break;
#if !BLAZORGL
                case "QUIT":     Exit();                                break;
#endif
            }
        }

        /// <summary>Puts the game straight into a UI state for --screenshot ui captures.</summary>
        private void SetupUiScreenshot()
        {
            switch (_screenshotRoom)
            {
                case "settings": _titleSplash = false; _menuRevealT = 1f; _currentState = GameState.Settings; break;
                case "title":    _currentState = GameState.MainMenu; break;   // boot splash
                case "menu":
                    _titleSplash  = false;
                    _menuRevealT  = 1f;
                    _currentState = GameState.MainMenu;
                    break;

                // Poster intro, fully typed; optional view arg = page index.
                case "intro":
                    StartCaseIntro("malibu_mansion");
                    if (int.TryParse(_screenshotView, out int page))
                        _introPageIndex = Math.Clamp(page, 0, _introPages.Length - 1);
                    _typewriterTimer = 999999f;
                    break;

                // End scene, fully typed; optional view arg = beat index.
                case "end":
                    StartEndScene("malibu_mansion");
                    if (int.TryParse(_screenshotView, out int beat))
                        _endBeatIndex = Math.Clamp(beat, 0, _endBeats.Length - 1);
                    _typewriterTimer = 999999f;
                    break;

                // Headless save/restore check: loads the save slot exactly like the
                // CONTINUE button and captures whatever room it lands in.
                case "continue":
                    var save = SaveSystem.LoadGame();
                    if (save != null)
                        RestoreFromSave(save);
                    else
                        Console.WriteLine("[Screenshot] No save to continue - staying on the menu.");
                    break;

                // Scripted scenes: `ui script <section> [beat]`, section =
                // intro | gathering | ending (gathering = every gathering beat
                // in play order). Beat shown fully typed, fx settled.
                case "script":
                {
                    // Optional "@case" suffix: intro:0@tutorial (default malibu_mansion).
                    var at = _screenshotView.Split('@');
                    LoadCaseScript(at.Length > 1 ? at[1] : "malibu_mansion");
                    var parts = at[0].Split(':');
                    var beats = ScriptSection(parts[0]);
                    int idx   = parts.Length > 1 && int.TryParse(parts[1], out int bi) ? bi : 0;
                    StartCutscene(beats, () => _currentState = GameState.MainMenu);
                    _cutIndex = Math.Clamp(idx, 0, Math.Max(0, beats.Count - 1));
                    _cutBg = null;
                    for (int i = 0; i <= _cutIndex && i < beats.Count; i++)
                        if (beats[i].Bg != null) _cutBg = beats[i].Bg;
                    // ":stamp" freezes mid-stamp (fx visible) instead of fully typed.
                    _cutTime = parts.Length > 2 && parts[2] == "stamp" ? 0.45f : 999f;
                    break;
                }

                // Tap-to-walk check: walks the real cat to every target (Game1.TapControls.cs).
#if !BLAZORGL
                case "navtest":
                    RunNavTest(string.IsNullOrEmpty(_screenshotView) ? "malibu_mansion" : _screenshotView);
                    Exit();
                    return;
#endif

                // Layout check: measures every beat against the cutscene box.
                case "scriptcheck":
                    RunScriptCheck(string.IsNullOrEmpty(_screenshotView) ? "malibu_mansion" : _screenshotView);
                    _currentState = GameState.MainMenu;
                    break;

                default: _currentState = GameState.MainMenu; break;
            }
        }

        /// <summary>Screenshot helper: a script section flattened into play order.</summary>
        private List<ScriptBeat> ScriptSection(string section)
        {
            var list = new List<ScriptBeat>();
            if (_caseScript == null) return list;
            switch (section)
            {
                case "intro":  list.AddRange(_caseScript.Intro); break;
                case "ending": list.AddRange(_caseScript.Ending); break;
                case "gathering" when _caseScript.Gathering != null:
                    list.AddRange(_caseScript.Gathering.Intro);
                    foreach (var st in _caseScript.Gathering.Steps)
                    {
                        list.AddRange(st.Intro);
                        list.AddRange(st.Success);
                    }
                    break;
            }
            return list;
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
        /// <summary>
        /// Scans every room's config once per case and maps each gate clue id to
        /// the characters whose topics it unlocks, so a cross-room find ("the
        /// glove") can nudge the player back to the right person ("Basil").
        /// </summary>
        private void BuildGateIndex(string caseId, IReadOnlyList<string> rooms)
        {
            _solveGateIndex.Clear();
            _confrontationTopics.Clear();
            _ungatedClueIds.Clear();
            foreach (var roomId in rooms)
            {
                string configPath = Path.Combine(
                    Content.RootDirectory, "Levels", caseId, roomId, "room_config.json");
                if (!GameFile.Exists(configPath)) continue;

                var config = LevelConfigParser.LoadRoom(configPath);
                foreach (var (entityId, data) in config.Interactables)
                {
                    string name = data.DisplayName.Length > 0
                        ? data.DisplayName
                        : ToDisplayName(entityId.Replace("inspect_", ""));
                    foreach (var kw in data.Keywords)
                        _ungatedClueIds.Add(kw.Id);
                    for (int t = 0; t < data.Topics.Length; t++)
                    {
                        var topic = data.Topics[t];
                        if (topic.RequiresClue.Length == 0 && topic.RequiresSolve.Length == 0)
                            foreach (var kw in topic.Keywords)
                                _ungatedClueIds.Add(kw.Id);
                        if (topic.RequiresSolve.Length > 0)
                        {
                            if (!_solveGateIndex.TryGetValue(topic.RequiresSolve, out var list))
                                _solveGateIndex[topic.RequiresSolve] = list = new();
                            if (!list.Contains((name, roomId)))
                                list.Add((name, roomId));

                            // Confrontation registry for the FINAL SOLVE lock.
                            _confrontationTopics.Add((roomId, entityId, t, name));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Parses the timestamp convention in clue names ("Manifest Entry -
        /// 7:00 PM", "Hallway Rumble - 7 PM") into minutes since midnight.
        /// Drives the chronological WHERE/WHEN word bank and the Case Notes
        /// timeline. False when the name carries no time.
        /// </summary>
        private static bool TryParseClueTime(string name, out int minutes)
        {
            minutes = 0;
            var m = System.Text.RegularExpressions.Regex.Match(
                name, @"(\d{1,2})(?::(\d{2}))?\s*(AM|PM)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            int h  = int.Parse(m.Groups[1].Value) % 12;
            int mm = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            if (m.Groups[3].Value.Equals("PM", StringComparison.OrdinalIgnoreCase)) h += 12;
            minutes = h * 60 + mm;
            return true;
        }

        /// <summary>Stable in-place sort: timed clues chronologically, untimed after in original order.</summary>
        private static void SortCluesByTime(List<Clue> clues)
        {
            var keyed = new List<(int Key, int Index, Clue Clue)>(clues.Count);
            for (int i = 0; i < clues.Count; i++)
                keyed.Add((TryParseClueTime(clues[i].Name, out var t) ? t : int.MaxValue, i, clues[i]));
            keyed.Sort((a, b) => a.Key != b.Key
                ? a.Key.CompareTo(b.Key)
                : a.Index.CompareTo(b.Index));
            for (int i = 0; i < clues.Count; i++) clues[i] = keyed[i].Clue;
        }

        /// <summary>
        /// True when a (clue, solve) gate pair is satisfied: the gate clue (if any)
        /// is unlocked AND the gate room (if any) has a solved local board.
        /// Shared by topics and alt-intro texts.
        /// </summary>
        private bool GateSatisfied(string requiresClue, string requiresSolve) =>
            (requiresClue.Length == 0 || _notebook.IsUnlocked(requiresClue)) &&
            (requiresSolve.Length == 0 ||
             (_roomSolvedStates.TryGetValue(requiresSolve, out var solved) && solved));

        private bool IsTopicAvailable(DialogueTopic topic) =>
            GateSatisfied(topic.RequiresClue, topic.RequiresSolve);

        /// <summary>
        /// Dialogue-label name: applies the revealName swap ("The Sound Guy" ->
        /// "D. Marsh") once its clue is found. Toasts deliberately do NOT use this
        /// (BuildGateIndex captures the pre-reveal name - the anti-leak rule).
        /// </summary>
        /// <summary>Opens an interactable's dialogue (Enter, or the second tap).</summary>
        private void OpenInteraction(InteractableEntity entity)
        {
            if (entity.Data == null) return;
            _currentInteraction  = entity.Data;
            _currentKeywords     = _currentInteraction.Keywords;
            _dialogueEntityId    = entity.Id;
            SetDialoguePortrait(entity);

            // Alt intro: characters acknowledge investigation progress.
            // The regular keywords still unlock and highlight either way.
            string introText =
                _currentInteraction.AltText.Length > 0 &&
                GateSatisfied(_currentInteraction.AltTextRequiresClue,
                              _currentInteraction.AltTextRequiresSolve)
                    ? _currentInteraction.AltText
                    : _currentInteraction.Text;

            _dialoguePages       = PaginateDialogue(introText, _dialoguePortrait != null);
            _currentDialoguePage = 0;
            _typewriterTimer     = 0f;
            _isDialogueActive    = true;
            _isTopicMenuOpen     = false;
            _cat.SteerTarget     = null;
            _navPath.Clear();
            foreach (var kw in entity.Data.Keywords)
                _notebook.UnlockClue(kw.Id);
        }

        private string ResolveDisplayName(InteractionData data, string entityId)
        {
            if (data.RevealName.Length > 0 && data.RevealNameOnClue.Length > 0 &&
                _notebook.IsUnlocked(data.RevealNameOnClue))
                return data.RevealName;
            return data.DisplayName.Length > 0
                ? data.DisplayName
                : ToDisplayName(entityId.Replace("inspect_", ""));
        }

        /// <summary>
        /// True if the entity offers at least one topic that is currently
        /// available (gate clue found, or ungated) and not yet heard.
        /// Drives the speech-bubble indicator in Pass 4b.
        /// </summary>
        private bool HasUnseenTopics(InteractableEntity entity)
        {
            if (entity.Data == null) return false;
            var topics = entity.Data.Topics;
            for (int i = 0; i < topics.Length; i++)
            {
                if (!IsTopicAvailable(topics[i]))
                    continue;
                if (!_visitedTopics.Contains($"{_currentRoomId}/{entity.Id}/{i}"))
                    return true;
            }
            return false;
        }

        // NOTE (playtest 5): clue-gate unlock toasts ("X might have some explaining
        // to do...") were removed - they told the player where to go before they
        // could choose. Solve-gated confrontation toasts remain (a solve is an
        // earned reward); everything else is discovered via the speech bubbles.

        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Opens the interrogation menu for <see cref="_currentInteraction"/>.
        /// Topics gated behind a clue the player hasn't found are hidden entirely,
        /// so their prompts don't spoil what evidence exists.
        /// </summary>
        private void OpenTopicMenu()
        {
            _menuTopics.Clear();
            var topics = _currentInteraction!.Topics;
            for (int i = 0; i < topics.Length; i++)
            {
                if (IsTopicAvailable(topics[i]))
                    _menuTopics.Add((topics[i], i));
            }
            _selectedTopicIndex = 0;
            _isTopicMenuOpen    = true;
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Update(GameTime gameTime)
        {
            var kbState = Keyboard.GetState();

            // Edge-detected keys (computed before _prevKbState is overwritten).
            // Escape is contextual per state - it only quits from the main menu.
            bool escPressed     = kbState.IsKeyDown(Keys.Escape) && !_prevKbState.IsKeyDown(Keys.Escape);
            bool confirmPressed = kbState.IsKeyDown(Keys.Enter)  && !_prevKbState.IsKeyDown(Keys.Enter);
            bool f12Pressed     = kbState.IsKeyDown(Keys.F12)    && !_prevKbState.IsKeyDown(Keys.F12);
            bool leftPressed    = kbState.IsKeyDown(Keys.Left)   && !_prevKbState.IsKeyDown(Keys.Left);
            bool rightPressed   = kbState.IsKeyDown(Keys.Right)  && !_prevKbState.IsKeyDown(Keys.Right);

            if (kbState.IsKeyDown(Keys.F1) && !_prevKbState.IsKeyDown(Keys.F1))
                _showDebug = !_showDebug;

            _menuUpPressed =
                (kbState.IsKeyDown(Keys.W) || kbState.IsKeyDown(Keys.Up)) &&
                !(_prevKbState.IsKeyDown(Keys.W) || _prevKbState.IsKeyDown(Keys.Up));
            _menuDownPressed =
                (kbState.IsKeyDown(Keys.S) || kbState.IsKeyDown(Keys.Down)) &&
                !(_prevKbState.IsKeyDown(Keys.S) || _prevKbState.IsKeyDown(Keys.Down));

            // Any fresh key press (title splash: "press any key").
            bool anyKeyPressed = false;
            foreach (var k in kbState.GetPressedKeys())
                if (!_prevKbState.IsKeyDown(k)) { anyKeyPressed = true; break; }

            _prevKbState = kbState;

            var  mouseState = Mouse.GetState();
            // Mouse and touch share one pointer (Systems/PointerInput.cs): every
            // click-driven screen works with a finger, too.
            _pointer.Update(mouseState, kbState, (float)gameTime.ElapsedGameTime.TotalSeconds);
            bool clicked    = _pointer.Pressed;
#if BLAZORGL
            // Canvas is window-sized and letterboxed; invert that transform.
            var vm = WasmScale.ToGame(_pointer.RawPosition.X, _pointer.RawPosition.Y);

            // Browser autoplay policy: the looping music can only start after
            // the first user gesture. Touch counts on RELEASE (touchend is a
            // user activation, touchstart is not).
            if (!_webMusicStarted &&
                (clicked || _pointer.Released || kbState.GetPressedKeys().Length > 0))
            {
                _webMusicStarted = true;
                BrowserAudio.Play("music", "Content/Shared/moonlit_cat_case.mp3",
                    loop: true, volume: _settings.MusicVolume);
            }
#else
            var vm = new Point(
                (int)(_pointer.RawPosition.X / DISPLAY_SCALE),
                (int)(_pointer.RawPosition.Y / DISPLAY_SCALE));
#endif
            _pointerGamePos = vm;

#if !BLAZORGL
            // Screenshot mode: save render target to file then exit. Runs in every
            // state so ui (menu/settings/intro/end) captures work too.
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
#endif

            if (_currentState == GameState.MainMenu)
            {
#if !BLAZORGL
                if (escPressed)
                {
                    Exit();
                    return;
                }
#endif
                if (f12Pressed)
                {
                    _currentState = GameState.DevMenu;
                }
                else if (_titleSplash)
                {
                    if (anyKeyPressed || clicked) _titleSplash = false;
                }
                else if (_menuRevealT < 1f)
                {
                    // Panel still sliding in: swallow input so the dismissing
                    // click can never land on a button.
                    _menuRevealT = Math.Min(1f, _menuRevealT +
                        (float)gameTime.ElapsedGameTime.TotalSeconds / MENU_REVEAL_SECONDS);
                }
                else
                {
                    int count = _mainMenuLabels.Length;
                    if (_menuUpPressed || _menuDownPressed)
                        _confirmNewGame = false;   // navigating away cancels the overwrite prompt
                    if (_menuUpPressed)
                        do { _menuIndex = (_menuIndex - 1 + count) % count; }
                        while (!IsMenuItemEnabled(_menuIndex));
                    if (_menuDownPressed)
                        do { _menuIndex = (_menuIndex + 1) % count; }
                        while (!IsMenuItemEnabled(_menuIndex));

                    if (confirmPressed && IsMenuItemEnabled(_menuIndex))
                    {
                        ActivateMainMenuItem(_menuIndex);
                    }
                    else if (clicked)
                    {
                        for (int i = 0; i < _mainMenuButtonRects.Length; i++)
                        {
                            if (_mainMenuButtonRects[i].Contains(vm) && IsMenuItemEnabled(i))
                            {
                                _menuIndex = i;
                                ActivateMainMenuItem(i);
                                break;
                            }
                        }
                    }
                }
            }
            else if (_currentState == GameState.Settings)
            {
                if (escPressed || (clicked && _settingsBackRect.Contains(vm)))
                {
                    _currentState = GameState.MainMenu;
                }
                else
                {
                    float vol = _settings.MusicVolume;
                    if (leftPressed  || (clicked && _settingsVolMinusRect.Contains(vm))) vol -= 0.1f;
                    if (rightPressed || (clicked && _settingsVolPlusRect.Contains(vm)))  vol += 0.1f;
                    if (clicked)
                    {
                        for (int i = 0; i < _settingsVolCellRects.Length; i++)
                        {
                            if (_settingsVolCellRects[i].Contains(vm))
                            {
                                vol = (i + 1) / 10f;
                                break;
                            }
                        }
                    }
                    vol = Math.Clamp((float)Math.Round(vol, 1), 0f, 1f);
                    if (Math.Abs(vol - _settings.MusicVolume) > 0.001f)
                    {
                        _settings.MusicVolume = vol;
#if BLAZORGL
                        BrowserAudio.Volume("music", vol);
#else
                        MediaPlayer.Volume    = vol;
#endif
                        SaveSystem.SaveSettings(_settings);
                    }

                    if (clicked && _settingsCrtToggleRect.Contains(vm))
                    {
                        _settings.CrtEnabled = !_settings.CrtEnabled;
                        SaveSystem.SaveSettings(_settings);
                    }
                }
            }
            else if (_currentState == GameState.CaseIntro)
            {
                if (escPressed)
                {
                    StartCase(_introCaseId);   // skip the intro entirely
                }
                else
                {
                    int totalChars = _introPages[_introPageIndex].Length;
                    if (confirmPressed || clicked)
                    {
                        if (_typewriterTimer < totalChars)
                            _typewriterTimer = totalChars;         // finish typing
                        else if (_introPageIndex < _introPages.Length - 1)
                        {
                            _introPageIndex++;                     // next page
                            _typewriterTimer = -0.5f * TYPEWRITER_SPEED;
                        }
                        else
                            StartCase(_introCaseId);               // into the case
                    }
                    else
                    {
                        _typewriterTimer +=
                            (float)gameTime.ElapsedGameTime.TotalSeconds * TYPEWRITER_SPEED;
                    }
                }
            }
            else if (_currentState == GameState.EndScene)
            {
                var beat       = _endBeats[_endBeatIndex];
                int totalChars = beat.IsCard ? 0 : beat.Text.Length;   // cards show instantly
                if (escPressed)
                {
                    if (_endBeatIndex < _endBeats.Length - 1)
                    {
                        _endBeatIndex    = _endBeats.Length - 1;   // jump to the card
                        _typewriterTimer = 999999f;
                    }
                    else
                        ReturnToMainMenu();
                }
                else if (confirmPressed || clicked)
                {
                    if (_typewriterTimer < totalChars)
                        _typewriterTimer = totalChars;
                    else if (_endBeatIndex < _endBeats.Length - 1)
                    {
                        _endBeatIndex++;
                        _typewriterTimer = 0f;
                    }
                    else
                        ReturnToMainMenu();
                }
                else
                {
                    _typewriterTimer +=
                        (float)gameTime.ElapsedGameTime.TotalSeconds * TYPEWRITER_SPEED;
                }
            }
            else if (_currentState == GameState.Cutscene)
            {
                UpdateCutscene((float)gameTime.ElapsedGameTime.TotalSeconds,
                    confirmPressed || clicked, escPressed);
            }
            else if (_currentState == GameState.DevMenu)
            {
                if (escPressed)
                {
                    _currentState = GameState.MainMenu;
                }
                else if (clicked)
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
            else if (_currentState == GameState.Playing)
            {
                // Escape backs out one layer at a time: topic menu / dialogue ->
                // deduction board -> main menu.
                if (escPressed)
                {
                    if (_isTopicMenuOpen || _isDialogueActive)
                    {
                        _isDialogueActive = false;
                        _isTopicMenuOpen  = false;
                    }
                    else if (_isDeductionBoardOpen)
                    {
                        if (_journalOnNotesPage)
                        {
                            // One layer at a time: notes page turns back to the board first.
                            _journalOnNotesPage = false;
                        }
                        else
                        {
                            _isDeductionBoardOpen = false;
                            _isFinalSolveMode     = false;
                            _selectedWordBankClue = null;
                            _insertTargetSlot     = null;
                            if (_gatheringActive) AbortGathering();
                        }
                    }
                    else
                    {
                        AutoSave();
                        ReturnToMainMenu();
                        _prevMouseState = mouseState;
                        return;
                    }
                }

                if (clicked)
                {
                    if (_isDeductionBoardOpen)
                    {
                        if (_journalCloseRect.Contains(vm))
                        {
                            _isDeductionBoardOpen = false;
                            _isFinalSolveMode     = false;
                            _journalOnNotesPage   = false;
                            _selectedWordBankClue = null;
                            _insertTargetSlot     = null;
                            if (_gatheringActive) AbortGathering();
                        }
                        else if (_journalOnNotesPage)
                        {
                            // Case Notes page: every board hotspot is dead; only the
                            // page-turn back to the deduction spread responds.
                            if (_journalTurnBackRect.Contains(vm))
                                _journalOnNotesPage = false;
                        }
                        else if (_journalTurnPageRect.Contains(vm))
                        {
                            _journalOnNotesPage = true;
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
                                if (_isFinalSolveMode && _gatheringActive)
                                {
                                    if (_deduction.ValidateCase())
                                        OnGatheringStepSolved();
                                    else
                                        ApplyGatheringWrongLine();
                                }
                                else if (_isFinalSolveMode)
                                {
                                    if (_deduction.ValidateCase())
                                    {
                                        // A scripted outro (case_script.json "ending", no
                                        // gathering - the tutorial) plays as a cutscene;
                                        // otherwise the typewriter end scene.
                                        if (_caseScript is { Ending.Count: > 0 } && !HasGathering)
                                        {
                                            _isDeductionBoardOpen = false;
                                            _isFinalSolveMode     = false;
                                            FinishCase();
                                            StartCutscene(_caseScript.Ending, AfterCaseOutro, AfterCaseOutro);
                                            _prevMouseState = mouseState;
                                            return;
                                        }
                                        StartEndScene(_currentCaseId);
                                    }
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
                                            ? (AllConfrontationsHeard
                                                ? "All rooms solved - the FINAL SOLVE board is unlocked!"
                                                : "All rooms solved - confront the suspects to unlock the FINAL SOLVE.")
                                            : $"Room solved! ({done}/{_caseRooms.Count}) " +
                                              "Solve every room to unlock the FINAL SOLVE.");

                                        // Solve-gated confrontations: nudge toward the
                                        // character(s) this deduction just cornered.
                                        // Always names the room (playtest: a bare name
                                        // sent players hunting in the wrong room).
                                        if (_solveGateIndex.TryGetValue(_currentRoomId, out var cornered))
                                        {
                                            string who = string.Join(" and ", cornered.ConvertAll(
                                                g => $"{g.Name} ({ToDisplayName(g.RoomId)})"));
                                            ShowToast($"Your deduction corners {who} - " +
                                                      "they have some explaining to do...");
                                        }

                                        AutoSave();
                                    }
                                    else if (_localDeduction.Slots.Exists(sl =>
                                                 !string.IsNullOrEmpty(sl.CorrectClueId) &&
                                                 !_notebook.IsUnlocked(sl.CorrectClueId)) &&
                                             _localDeduction.ValidationMessage.StartsWith("Incorrect"))
                                    {
                                        // Playtest r2: a board whose answer chip was never
                                        // found gave "N/M fit" with no way to tell.
                                        _localDeduction.ValidationMessage =
                                            _localDeduction.ValidationMessage.TrimEnd('.') +
                                            " - a piece is still out there.";
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
                    else if (_isDialogueActive)
                    {
                        // Tap anywhere = Enter; the corner X = Escape; topic rows
                        // are tapped directly (Game1.TapControls.cs).
                        HandleDialogueTap(vm);
                    }
                    else
                    {
                        if (_menuButtonRect.Contains(vm))
                        {
                            // Touch has no Escape key: same as Esc from the room.
                            AutoSave();
                            ReturnToMainMenu();
                            _prevMouseState = mouseState;
                            return;
                        }
                        else if (_solveButtonRect.Contains(vm))
                        {
                            _isFinalSolveMode     = false;
                            _isDeductionBoardOpen = true;
                            _journalOnNotesPage   = false;
                            _selectedWordBankClue = null;
                            _wordBankPage         = 0;
                        }
                        else if (_finalSolveButtonRect.Contains(vm))
                        {
                            if (AllRoomsSolved && AllConfrontationsHeard && HasGathering)
                            {
                                StartGathering();
                                _prevMouseState = mouseState;
                                return;
                            }
                            else if (AllRoomsSolved && AllConfrontationsHeard)
                            {
                                _isFinalSolveMode     = true;
                                _isDeductionBoardOpen = true;
                                _journalOnNotesPage   = false;
                                _selectedWordBankClue = null;
                                _wordBankPage         = 0;
                            }
                            else if (AllRoomsSolved)
                            {
                                // Person/story nudge listing who still awaits their
                                // confrontation - names from config (no reveal leak).
                                var remaining = new List<string>();
                                foreach (var c in _confrontationTopics)
                                {
                                    if (IsConfrontationHeard(c)) continue;
                                    string label = $"{c.Name} ({ToDisplayName(c.RoomId)})";
                                    if (!remaining.Contains(label)) remaining.Add(label);
                                }
                                ShowToast($"Locked - {string.Join(" and ", remaining)} " +
                                          "still owe you some explaining...");
                            }
                            else
                            {
                                ShowToast($"Locked - solve every room's deduction first " +
                                          $"({SolvedRoomCount()}/{_caseRooms.Count} solved).");
                            }
                        }
                        else
                        {
                            // Not on a HUD button: tap-to-walk / tap-to-select.
                            HandleWorldTap(vm);
                        }
                    }
                }

                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

                // Toasts are hidden while the dialogue box is open (Pass 7), and gate
                // toasts usually fire mid-dialogue - hold the timer until it can be seen.
                if (!_isDialogueActive)
                {
                    if (_toastTimer > 0f)
                        _toastTimer -= dt;
                    if (_toastTimer <= 0f && _toastQueue.Count > 0)
                    {
                        _toastMessage = _toastQueue.Dequeue();
                        _toastTimer   = TOAST_DURATION;
                    }
                }

                if (_isDialogueActive)
                {
                    if (_isTopicMenuOpen)
                    {
                        int optionCount = _menuTopics.Count + 1;   // +1 = the Leave entry
                        if (_menuUpPressed)
                            _selectedTopicIndex = (_selectedTopicIndex - 1 + optionCount) % optionCount;
                        if (_menuDownPressed)
                            _selectedTopicIndex = (_selectedTopicIndex + 1) % optionCount;

                        bool tapAct = _tapTopicIndex >= 0 && _tapTopicIndex < optionCount;
                        if (tapAct) _selectedTopicIndex = _tapTopicIndex;
                        _tapTopicIndex = -1;

                        if (_cat.IsInteractPressed() || tapAct)
                        {
                            if (_selectedTopicIndex >= _menuTopics.Count)
                            {
                                _isDialogueActive = false;
                                _isTopicMenuOpen  = false;
                            }
                            else
                            {
                                var (topic, index) = _menuTopics[_selectedTopicIndex];
                                _visitedTopics.Add($"{_currentRoomId}/{_dialogueEntityId}/{index}");
                                foreach (var kw in topic.Keywords)
                                    _notebook.UnlockClue(kw.Id);
                                _currentKeywords     = topic.Keywords;
                                _dialoguePages       = PaginateDialogue(topic.Text, _dialoguePortrait != null);
                                _currentDialoguePage = 0;
                                _typewriterTimer     = 0f;
                                _isTopicMenuOpen     = false;
                            }
                        }
                    }
                    else
                    {
                        int totalChars = _dialoguePages[_currentDialoguePage]
                            .Replace("[", "").Replace("]", "").Length;

                        bool tapAdvance = _dialogueTapAdvance;
                        _dialogueTapAdvance = false;
                        if (_cat.IsInteractPressed() || tapAdvance)
                        {
                            if (_typewriterTimer < totalChars)
                            {
                                _typewriterTimer = totalChars;
                            }
                            else if (_currentDialoguePage < _dialoguePages.Length - 1)
                            {
                                _currentDialoguePage++;
                                _typewriterTimer = 0f;
                            }
                            else if (_currentInteraction != null && _currentInteraction.Topics.Length > 0)
                            {
                                OpenTopicMenu();
                            }
                            else
                            {
                                _isDialogueActive = false;
                            }
                        }
                        else
                        {
                            _typewriterTimer += (float)gameTime.ElapsedGameTime.TotalSeconds * TYPEWRITER_SPEED;
                        }
                    }
                }
                else
                {
                    // Tap-to-walk feeds the cat a waypoint; movement keys override
                    // it (and cancel the tap path) inside Cat.Update.
                    FollowTapPath();
                    _cat.Update(gameTime);
                    if (_cat.KeyboardMoving) ClearTapState();
                    _cat.MoveWithCollision(dt, _solidBoundaries);
                    UpdateTapStuck(dt);

                    // One Enter read per frame: a doorway the cat stands in takes it
                    // first, exactly like the old two-call order.
                    bool enterPressed = _cat.IsInteractPressed();

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

                    // Second tap on a selected door (now or on arrival) = Enter.
                    TransferZone? doorToUse = null;
                    if (_activeTransferZone != null && enterPressed)
                    {
                        doorToUse    = _activeTransferZone;
                        enterPressed = false;
                    }
                    else if (_tapActOnArrival && _tapDoor != null &&
                             _cat.CollisionBox.Intersects(_tapDoor.TriggerRect))
                    {
                        doorToUse        = _tapDoor;
                        _tapActOnArrival = false;
                    }

                    if (doorToUse != null)
                    {
                        if (!IsTransferUnlocked(doorToUse))
                        {
                            ShowToast(doorToUse.LockedText.Length > 0
                                ? doorToUse.LockedText
                                : "Not yet. Something else needs looking at first.");
                        }
                        else
                        {
                            LoadRoom(doorToUse.TargetRoom, doorToUse.TargetSpawn);
                            AutoSave();
                            return; // state was rebuilt; skip rest of this Update
                        }
                    }

                    _activeInteractable = null;
                    foreach (var entity in _interactables)
                    {
                        if (_cat.CollisionBox.Intersects(entity.InteractZone))
                        {
                            _activeInteractable = entity;
                            break;
                        }
                    }

                    // The tap-selected entity wins over whatever else is in range.
                    if (_tapEntity != null && _cat.CollisionBox.Intersects(_tapEntity.InteractZone))
                        _activeInteractable = _tapEntity;

                    InteractableEntity? toOpen = null;
                    if (_activeInteractable != null && enterPressed)
                        toOpen = _activeInteractable;
                    else if (_tapActOnArrival && _tapEntity != null &&
                             _cat.CollisionBox.Intersects(_tapEntity.InteractZone))
                    {
                        toOpen           = _tapEntity;
                        _tapActOnArrival = false;
                    }

                    if (toOpen != null && toOpen.Data != null)
                        OpenInteraction(toOpen);
                }

                foreach (var prop in _foregroundProps)
                {
                    prop.CheckFadeTrigger(_cat.CollisionBox);
                    prop.Update(gameTime);
                }

#if !BLAZORGL
                // Hot-reload: poll level_config.json every 0.5 s (dev-only).
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
                                // Don't swap the board out from under an open journal.
                                if (!_isDeductionBoardOpen)
                                    BuildLocalDeduction(fresh);
                                Console.WriteLine("[HotReload] room_config.json reloaded.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[HotReload] Skipped (file locked?): {ex.Message}");
                            }
                        }
                    }
                    if (File.Exists(_caseConfigSourcePath))
                    {
                        var writeTime = File.GetLastWriteTime(_caseConfigSourcePath);
                        if (writeTime > _caseConfigLastWrite)
                        {
                            _caseConfigLastWrite = writeTime;
                            try
                            {
                                var fresh = LevelConfigParser.LoadCase(_caseConfigSourcePath);
                                _notebook.ReplaceDefinitions(fresh.Clues);
                                if (!_isDeductionBoardOpen && !_gatheringActive)
                                    BuildFinalDeduction(fresh);
                                Console.WriteLine("[HotReload] case_config.json reloaded.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[HotReload] Skipped (file locked?): {ex.Message}");
                            }
                        }
                    }
                }
#endif
            }

            // Asset warm-up: at most one blocking load per frame, and only on
            // frames where the stall can't be felt - any non-Playing screen
            // (menus, intro, end scene), or in-game while the player is reading
            // (dialogue/board open) or simply not holding a movement key. This
            // frame's input was already processed above, so the stall only
            // delays the next frame of a screen that isn't animating movement.
            if (_preloader != null && _preloader.HasWork)
            {
                bool moving = kbState.IsKeyDown(Keys.W) || kbState.IsKeyDown(Keys.A) ||
                              kbState.IsKeyDown(Keys.S) || kbState.IsKeyDown(Keys.D) ||
                              kbState.IsKeyDown(Keys.Up)   || kbState.IsKeyDown(Keys.Down) ||
                              kbState.IsKeyDown(Keys.Left) || kbState.IsKeyDown(Keys.Right);
                if (_currentState != GameState.Playing ||
                    _isDialogueActive || _isDeductionBoardOpen || !moving)
                {
                    _preloader.Pump();
                }
            }

            _prevMouseState = mouseState;
            base.Update(gameTime);
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Draw(GameTime gameTime)
        {
            _uiTime = (float)gameTime.TotalGameTime.TotalSeconds;
#if BLAZORGL
            // Snapshot the real canvas size BEFORE binding the render target
            // (the viewport follows the bound target).
            WasmScale.WindowWidth  = GraphicsDevice.Viewport.Width;
            WasmScale.WindowHeight = GraphicsDevice.Viewport.Height;
#endif
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
            else if (_currentState == GameState.MainMenu)
            {
                DrawMainMenu();
            }
            else if (_currentState == GameState.Settings)
            {
                DrawSettingsScreen();
            }
            else if (_currentState == GameState.CaseIntro)
            {
                DrawCaseIntro();
            }
            else if (_currentState == GameState.EndScene)
            {
                DrawEndScene();
            }
            else if (_currentState == GameState.Cutscene)
            {
                DrawCutscene();
            }
            else if (_currentState == GameState.Playing)
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
                        totalSeconds:  gameTime.TotalGameTime.TotalSeconds,
                        isSelected:    ReferenceEquals(entity, _tapEntity));
                _spriteBatch.End();

                // ════════════════════════════════════════════════════════════════
                // PASS 4 — LIGHTING / SUNBEAMS  (additive — El Mariachi trick)
                // ════════════════════════════════════════════════════════════════
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                    transformMatrix: _cameraTransform);
                _spriteBatch.Draw(_sunbeamsMask, Vector2.Zero, Color.White);
                _spriteBatch.End();

                // ════════════════════════════════════════════════════════════════
                // PASS 4b — TOPIC INDICATORS  (world-anchored speech bubble over
                // characters with available, unheard topics)
                // ════════════════════════════════════════════════════════════════
                if (!_isDialogueActive)
                {
                    // NonPremultiplied: both indicator sprites are built with
                    // PremultiplyAlpha=False, like every other sprite.
                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                        transformMatrix: _cameraTransform);
                    float bob = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 2.5) * 4f;

                    const float BUBBLE_DRAW_W = 40f; // on-screen width; keep subtle
                    float bubbleScale  = BUBBLE_DRAW_W / _speechBubbleTex.Width;
                    // Origin = bottom-right, where the bubble's tail is - it sits
                    // top-left of the speaker with the tail pointing at the head.
                    var   bubbleOrigin = new Vector2(_speechBubbleTex.Width,
                                                     _speechBubbleTex.Height);
                    foreach (var entity in _interactables)
                    {
                        if (!HasUnseenTopics(entity)) continue;
                        // Anchor at the drawn sprite's top-left, not the
                        // (floor-level) trigger zone.
                        float anchorY = entity.TriggerZone.Y;
                        float anchorX = entity.TriggerZone.Center.X;
                        if (entity.Texture != null)
                        {
                            float scale = entity.Data?.Scale ?? 1f;
                            float drawnW = entity.Texture.Width  * scale;
                            anchorY = Math.Min(anchorY,
                                entity.Position.Y - entity.Texture.Height * scale);
                            // Character art carries transparent side margins, so
                            // pull in toward the figure rather than the full edge.
                            anchorX = entity.Position.X - drawnW * 0.22f;
                        }
                        var pos = new Vector2(anchorX, anchorY + 12f + bob);
                        _spriteBatch.Draw(_speechBubbleTex, pos, null, Color.White, 0f,
                            bubbleOrigin, bubbleScale, SpriteEffects.None, 0f);
                    }

                    // Doorway markers: permanent paw-print arrows over transfer
                    // zones so exits are discoverable without the F1 overlay.
                    // The art points right; left mirrors it (keeps the paw prints
                    // upright), up/down rotate it a quarter turn.
                    const float ARROW_DRAW_W = 64f; // on-screen length; keep subtle
                    float arrowScale  = ARROW_DRAW_W / _arrowTex.Width;
                    var   arrowOrigin = new Vector2(_arrowTex.Width  * 0.5f,
                                                    _arrowTex.Height * 0.5f);
                    foreach (var zone in _transferZones)
                    {
                        ArrowHeading(zone, out bool horizontal, out bool pointsNegative);
                        float rotation = horizontal ? 0f
                            : (pointsNegative ? -MathHelper.PiOver2 : MathHelper.PiOver2);
                        var effects = horizontal && pointsNegative
                            ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                        // Vertical extent above the zone depends on orientation
                        // (rotation happens around the sprite's center).
                        // Standing in the zone: the arrow goes gold and pulses,
                        // echoing the SOLVE button's "ready" cue. Gated doorways
                        // (RequiresVisited unmet) draw dim grey and never pulse.
                        // Tap-selected doors pulse gold too (first tap of tap-to-act).
                        bool  isActive  = ReferenceEquals(zone, _activeTransferZone) ||
                                          ReferenceEquals(zone, _tapDoor);
                        bool  locked    = !IsTransferUnlocked(zone);
                        float drawScale = arrowScale;
                        Color tint      = locked ? new Color(110, 110, 115) * 0.65f
                                                 : Color.White * 0.9f;
                        if (isActive && locked && ReferenceEquals(zone, _tapDoor))
                        {
                            float pulse = 0.5f + 0.5f * (float)Math.Sin(
                                gameTime.TotalGameTime.TotalSeconds * 6.0);
                            drawScale = arrowScale * (1.1f + 0.15f * pulse);
                        }
                        if (isActive && !locked)
                        {
                            float pulse = 0.5f + 0.5f * (float)Math.Sin(
                                gameTime.TotalGameTime.TotalSeconds * 6.0);
                            drawScale = arrowScale * (1.1f + 0.15f * pulse);
                            tint = Color.Lerp(new Color(255, 215, 120),
                                              new Color(255, 195, 60), pulse);
                        }
                        float halfExtentY = (horizontal ? _arrowTex.Height : _arrowTex.Width)
                                            * drawScale * 0.5f;
                        float halfExtentX = (horizontal ? _arrowTex.Width : _arrowTex.Height)
                                            * drawScale * 0.5f;
                        var cpos = new Vector2(
                            zone.TriggerRect.Center.X,
                            zone.TriggerRect.Y - 8f - halfExtentY + bob);
                        // Zones can hug the room edge; keep the arrow on-canvas.
                        cpos.X = MathHelper.Clamp(cpos.X, halfExtentX + 6f, SCREEN_WIDTH  - halfExtentX - 6f);
                        cpos.Y = MathHelper.Clamp(cpos.Y, halfExtentY + 6f, SCREEN_HEIGHT - halfExtentY - 6f);
                        _spriteBatch.Draw(_arrowTex, cpos, null, tint, rotation,
                            arrowOrigin, drawScale, effects, 0f);
                    }
                    _spriteBatch.End();
                }

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
                    // Tap-to-walk path: magenta waypoint boxes.
                    foreach (var wp in _navPath)
                        DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel,
                            new Rectangle((int)wp.X - 4, (int)wp.Y - 4, 8, 8), Color.Magenta);
                    _spriteBatch.End();
                }

                // ════════════════════════════════════════════════════════════════
                // PASS 6 — DIALOGUE UI
                // ════════════════════════════════════════════════════════════════
                if (_isDialogueActive && _currentInteraction != null)
                {
                    var boxRect = DialogueBoxRect;

                    const int PAD_X = DIALOGUE_PAD_X;
                    const int PAD_Y = DIALOGUE_PAD_Y;

                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                    _spriteBatch.Draw(_dialogueBoxTex, boxRect, Color.White);

                    // Dialogue text renders below full size so long clue texts stay
                    // inside the box art instead of running past its borders.
                    const float dialogueScale = DIALOGUE_TEXT_SCALE;
                    var hintColor = new Color(90, 70, 50);

                    // Portrait: the speaker's own sprite (face crop for characters,
                    // full sprite for objects), drawn larger than in-world so it
                    // reads clearly. Text shifts right by the reserved width (same
                    // shift PaginateDialogue budgeted for).
                    float textX = boxRect.X + PAD_X;
                    if (_dialoguePortrait != null)
                    {
                        var src = _dialoguePortraitSource;
                        float pScale = Math.Min(
                            (float)PORTRAIT_MAX_W / src.Width,
                            (float)PORTRAIT_MAX_H / src.Height);
                        var dest = new Rectangle(
                            boxRect.X + PAD_X - 20,
                            boxRect.Y + PAD_Y - 30,
                            (int)(src.Width * pScale),
                            (int)(src.Height * pScale));
                        _spriteBatch.Draw(_dialoguePortrait, dest, src, Color.White);
                        textX = boxRect.X + PAD_X + PORTRAIT_MAX_W + PORTRAIT_TEXT_GAP;
                    }
                    float textMaxWidth = boxRect.Right - PAD_X - textX;

                    // Name label: who/what the player is looking at, small, top-left
                    // corner of the box (playtest: identities got lost between rooms).
                    string nameLabel = ResolveDisplayName(_currentInteraction, _dialogueEntityId);
                    if (nameLabel.Length > 0)
                    {
                        const float nameScale = 0.58f;
                        _spriteBatch.DrawString(_dialogueFont, nameLabel,
                            new Vector2(textX, boxRect.Y + PAD_Y - 52),
                            hintColor * 0.85f, 0f, Vector2.Zero, nameScale,
                            SpriteEffects.None, 0f);
                    }

                    // Corner X: the touch stand-in for Escape (closes the conversation).
                    DrawUiButton(DialogueCloseRect, UiRed, "X", maxLabelScale: 0.6f);

                    if (_isTopicMenuOpen)
                    {
                        const float menuScale = TOPIC_MENU_SCALE;
                        float lineH = TopicLineHeight;
                        var   pos   = new Vector2(textX, boxRect.Y + PAD_Y - 20);

                        _spriteBatch.DrawString(_dialogueFont, "The detective considers his next move...",
                            pos, hintColor, 0f, Vector2.Zero, menuScale, SpriteEffects.None, 0f);
                        pos.Y += lineH + 8f;

                        for (int i = 0; i <= _menuTopics.Count; i++)
                        {
                            bool   isLeave  = i == _menuTopics.Count;
                            bool   selected = i == _selectedTopicIndex;
                            string label    = isLeave ? "Pad away. (Leave)" : _menuTopics[i].Topic.Prompt;
                            bool   visited  = !isLeave && _visitedTopics.Contains(
                                $"{_currentRoomId}/{_dialogueEntityId}/{_menuTopics[i].Index}");

                            Color color = selected ? InteractionData.Crime
                                        : visited  ? new Color(165, 150, 130)
                                                   : _inkColor;
                            _spriteBatch.DrawString(_dialogueFont,
                                (selected ? "> " : "  ") + label,
                                pos, color, 0f, Vector2.Zero, menuScale, SpriteEffects.None, 0f);
                            pos.Y += lineH;
                        }

                        string menuHint   = _pointer.LastWasTouch
                            ? "Tap a choice"
                            : "[ W/S ] choose   [ Enter ] act";
                        var    menuHintSz = _dialogueFont.MeasureString(menuHint) * dialogueScale;
                        _spriteBatch.DrawString(_dialogueFont, menuHint,
                            new Vector2(boxRect.Right  - PAD_X - menuHintSz.X,
                                        boxRect.Bottom - PAD_Y * 0.6f - menuHintSz.Y),
                            hintColor, 0f, Vector2.Zero, dialogueScale, SpriteEffects.None, 0f);
                    }
                    else
                    {
                        int totalCharsOnPage = _dialoguePages[_currentDialoguePage]
                            .Replace("[", "").Replace("]", "").Length;
                        bool typingDone = _typewriterTimer >= totalCharsOnPage;

                        DrawRichText(
                            _spriteBatch,
                            _dialogueFont,
                            _dialoguePages[_currentDialoguePage],
                            _currentKeywords,
                            new Vector2(textX, boxRect.Y + PAD_Y),
                            textMaxWidth,
                            (int)_typewriterTimer,
                            dialogueScale);

                        bool isLastPage  = _currentDialoguePage >= _dialoguePages.Length - 1;
                        bool opensMenu   = isLastPage && _currentInteraction.Topics.Length > 0;
                        string hint      = _pointer.LastWasTouch
                            ? (typingDone
                                ? (opensMenu ? "Tap ..." : isLastPage ? "Tap to dismiss" : "Tap to continue")
                                : "Tap to skip")
                            : (typingDone
                                ? (opensMenu ? "[ Enter ] ..." : isLastPage ? "[ Enter ] to dismiss" : "[ Enter ] to continue")
                                : "[ Enter ] to skip");
                        var    hintSize = _dialogueFont.MeasureString(hint) * dialogueScale;
                        _spriteBatch.DrawString(
                            _dialogueFont,
                            hint,
                            new Vector2(
                                boxRect.Right  - PAD_X - hintSize.X,
                                boxRect.Bottom - PAD_Y * 0.6f - hintSize.Y),
                            hintColor,
                            0f, Vector2.Zero, dialogueScale, SpriteEffects.None, 0f);
                    }
                    _spriteBatch.End();
                }

                // ════════════════════════════════════════════════════════════════
                // PASS 7 — NOTEBOOK UI  (hidden while dialogue is open)
                // ════════════════════════════════════════════════════════════════
                if (!_isDialogueActive)
                {
                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                    {
                        // SOLVE reflects room progress: pulsing glow once every clue
                        // in the room is found (playtest: players left rooms without
                        // solving), muted once the room's board is done. Confrontation
                        // clues (gated-topic-only) don't hold the pulse back - they
                        // unlock after the solve.
                        bool roomSolved = _roomSolvedStates.TryGetValue(_currentRoomId, out var rs) && rs;
                        var (roomFound, roomTotal) = _notebook.GetRoomClueCounts(
                            _currentRoomId, id => !_ungatedClueIds.Contains(id));
                        // Casebook buttons (Game1.UiStyle.cs): cream = idle, gold glow =
                        // "ready" (every clue found), green = solved, red = the finale.
                        if (roomSolved)
                        {
                            DrawUiButton(_solveButtonRect, UiGreen, "Solved", maxLabelScale: 0.7f);
                        }
                        else if (roomTotal > 0 && roomFound > 0 &&
                                 _notebook.AreRoomCluesFound(_currentRoomId,
                                     id => _ungatedClueIds.Contains(id)))
                        {
                            DrawUiButton(_solveButtonRect, UiYellow, "Solve", glow: true, maxLabelScale: 0.75f);
                        }
                        else
                        {
                            DrawUiButton(_solveButtonRect, UiCream, "Solve", maxLabelScale: 0.75f);
                        }

                        // FINAL SOLVE - locked until every room is solved AND every
                        // confrontation (requiresSolve topic) has been heard.
                        if (AllRoomsSolved && AllConfrontationsHeard)
                        {
                            DrawUiButton(_finalSolveButtonRect, UiRed, "Final Solve",
                                glow: true, maxLabelScale: 0.62f);
                        }
                        else if (AllRoomsSolved)
                        {
                            DrawUiButton(_finalSolveButtonRect, UiCream,
                                $"Final Solve\n{HeardConfrontationCount()}/{_confrontationTopics.Count} confronted",
                                enabled: false, maxLabelScale: 0.55f);
                        }
                        else
                        {
                            DrawUiButton(_finalSolveButtonRect, UiCream,
                                $"Final Solve\n{SolvedRoomCount()}/{_caseRooms.Count} rooms solved",
                                enabled: false, maxLabelScale: 0.55f);
                        }
                    }

                    // MENU: touch stand-in for Escape from the room (autosave + menu).
                    if (!_isDeductionBoardOpen)
                        DrawUiButton(_menuButtonRect, UiCream, "Menu", maxLabelScale: 0.55f);

                    // Bottom-center prompt: a tap selection explains the second tap;
                    // otherwise the doorway prompt for the zone the cat stands in.
                    string? bottomHint = _isDeductionBoardOpen ? null : TapSelectionHint();
                    if (bottomHint == null && _activeTransferZone != null)
                        bottomHint = _pointer.LastWasTouch ? "Tap the door to go through"
                                                           : "[ Enter ] to enter";
                    if (bottomHint != null)
                        DrawNotePill(bottomHint, SCREEN_HEIGHT - 28);

                    // Clue counters: current room progress + case-wide total.
                    // Room basis = clues whose roomId is this room, matching the
                    // journal's Investigation overview.
                    if (!_isDeductionBoardOpen)
                    {
                        var (roomFound, roomTotal) = _notebook.GetRoomClueCounts(
                            _currentRoomId, id => !_ungatedClueIds.Contains(id));

                        // The tutorial's guide card carries the clue count itself.
                        if (_currentCaseId == TUTORIAL_CASE)
                            DrawTutorialGuide();   // Game1.Tutorial.cs
                        else
                            DrawRoomCard(ToDisplayName(_currentRoomId), roomFound, roomTotal,
                                         _notebook.UnlockedClues.Count, _notebook.TotalClueCount);
                    }

                    // Transient toast — bottom-center, fades out at the end.
                    if (_toastTimer > 0f)
                    {
                        float alpha = Math.Min(1f, _toastTimer / 0.5f);
                        DrawNotePill(_toastMessage, SCREEN_HEIGHT - 110, alpha, scale: 0.66f);
                    }

                    _spriteBatch.End();
                }

                // ════════════════════════════════════════════════════════════════
                // PASS 8 — DEDUCTION BOARD  (hidden while dialogue is open)
                // ════════════════════════════════════════════════════════════════
                if (_isDeductionBoardOpen && !_isDialogueActive && !_journalOnNotesPage)
                {
                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

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

                        // ── LEFT PAGE: header — names the room being solved (the
                        // right page has the tab art; the left page needs its own
                        // anchor). Ink text only, no baked art.
                        {
                            bool headSolved = !_isFinalSolveMode &&
                                _roomSolvedStates.TryGetValue(_currentRoomId, out var hs) && hs;
                            string headTitle = _isFinalSolveMode
                                ? (_gatheringActive ? "The Gathering" : "The Final Solve")
                                : ToDisplayName(_currentRoomId);
                            string headSub = _isFinalSolveMode
                                               ? (_gatheringActive && CurrentGatheringStep is { } hs2
                                                   ? hs2.Title : "What really happened?")
                                           : headSolved        ? "Solved!"
                                           :                     "What happened here?";
                            float headScale = 1.05f * jsy;
                            _spriteBatch.DrawString(_dialogueFont, headTitle,
                                new Vector2(leftPageArea.X, J(160, jsy)), _inkColor,
                                0f, Vector2.Zero, headScale, SpriteEffects.None, 0f);
                            _spriteBatch.DrawString(_dialogueFont, headSub,
                                new Vector2(leftPageArea.X,
                                    J(160, jsy) + _dialogueFont.LineSpacing * headScale + 6 * jsy),
                                headSolved ? new Color(30, 130, 50) : _inkColor * 0.75f,
                                0f, Vector2.Zero, 0.68f * jsy, SpriteEffects.None, 0f);

                            // Tutorial: how a board works, in ink at the foot of the page.
                            if (_currentCaseId == TUTORIAL_CASE && !headSolved)
                            {
                                string how = _pointer.LastWasTouch
                                    ? "How it works:\n1. Tap a clue on the right (the tabs sort them by color).\n" +
                                      "2. Tap a gap in the sentence to put it there.\n3. Tap Submit."
                                    : "How it works:\n1. Click a clue on the right (the tabs sort them by color).\n" +
                                      "2. Click a gap in the sentence - or Insert - to put it there.\n3. Click Submit.";
                                if (_isFinalSolveMode)
                                    how += "\n\nThe Final Solve uses the clues of every room you solved.";
                                float howScale = 0.56f * jsy;
                                float howH     = _dialogueFont.MeasureString(how).Y * howScale;
                                _spriteBatch.DrawString(_dialogueFont, how,
                                    new Vector2(leftPageArea.X, J(1010, jsy) - howH),
                                    _inkColor * 0.7f, 0f, Vector2.Zero, howScale, SpriteEffects.None, 0f);
                            }
                        }

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
                                // Empty slot = faint tab-tinted paper; filled = the chip itself.
                                if (slot.SelectedClueId != null)
                                    DrawChip(slotRect, Color.Lerp(slotColor, UiCream, 0.35f), 0.8f, lifted: false);
                                else
                                    DrawChip(slotRect, slotColor * 0.32f, 0.45f, lifted: false);
                                float tX = slotRect.X + (slotRect.Width  - textW) * 0.5f;
                                float tY = slotRect.Y + (slotRect.Height - _dialogueFont.LineSpacing * jt) * 0.5f;
                                _spriteBatch.DrawString(_dialogueFont, label, new Vector2(tX, tY), _inkColor,
                                    0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                                slot.Bounds = slotRect;
                                lx += slotW + 8;
                            }
                        }

                        // (Investigation checklist + case clue total moved to the
                        // CASE NOTES page — Pass 8b — to keep this spread clean.)

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
                            ? (_gatheringActive && CurrentGatheringStep is { Bank.Count: > 0 } gStep
                                ? _notebook.UnlockedClues.FindAll(c => gStep.Bank.Contains(c.Id))
                                : _notebook.GetMacroClues())
                            : _notebook.GetLocalBankClues(_currentRoomId);
                        var filteredWB = sourceClues.FindAll(c => c.Category == _activeTab);
                        // The WHERE/WHEN tab is the timeline: sort its chips by the
                        // timestamp in their names (untimed chips keep discovery
                        // order, after the timed ones).
                        if (_activeTab == ClueCategory.WhereWhen)
                            SortCluesByTime(filteredWB);

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
                                // Word-bank chip: pale tab paper; the selected one is full
                                // colour, lifted, with a soft gold halo.
                                DrawChip(tagR,
                                    sel ? _tabColors[(int)_activeTab]
                                        : Color.Lerp(_tabColors[(int)_activeTab], UiCream, 0.6f),
                                    sel ? 0.9f : 0.5f, lifted: sel);
                                float nameX = tagR.X + (tagR.Width  - textSz.X) * 0.5f;
                                float nameY = tagR.Y + (tagR.Height - textSz.Y) * 0.5f;
                                _spriteBatch.DrawString(_dialogueFont, cl.Name,
                                    new Vector2(nameX, nameY),
                                    _inkColor,
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
                                DrawUiButton(_journalPrevPageRect, UiCream, "<");
                            if (_wordBankPage < _wordBankPageCount)
                                DrawUiButton(_journalNextPageRect, UiCream, ">");

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
                                Color.Lerp(_tabColors[(int)_selectedWordBankClue.Category], UiCream, 0.35f),
                                "Insert", maxLabelScale: jt);
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
                            DrawUiButton(_journalClearRect,  UiCream, "Clear",  maxLabelScale: jt);
                            DrawUiButton(_journalSubmitRect, UiGreen, "Submit", maxLabelScale: jt);
                        }

                        // ── Page turn (forward to Case Notes) ─────────────────
                        DrawUiButton(_journalTurnPageRect, UiCream,
                            "Case Notes >", maxLabelScale: jt);

                        // ── Close button (X) ──────────────────────────────────
                        DrawUiButton(_journalCloseRect, UiRed, "X", maxLabelScale: 0.6f);
                    }

                    _spriteBatch.End();
                }

                // ════════════════════════════════════════════════════════════════
                // PASS 8b — CASE NOTES (casebook page 2): the investigation score
                // card. Room checklist, case totals, and solved-deduction recaps
                // live here so the deduction spread itself stays uncluttered.
                // ════════════════════════════════════════════════════════════════
                if (_isDeductionBoardOpen && !_isDialogueActive && _journalOnNotesPage)
                {
                    _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                    _spriteBatch.Draw(_notesBgTex,
                        new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT),
                        Color.White);

                    float jsx = (float)SCREEN_WIDTH  / 2020f;
                    float jsy = (float)SCREEN_HEIGHT / 1136f;
                    int J(float v, float s) => (int)(v * s);
                    var leftPageArea  = new Rectangle(J(250,  jsx), J(200, jsy), J(650, jsx), J(790, jsy));
                    var rightPageArea = new Rectangle(J(1060, jsx), J(200, jsy), J(740, jsx), J(790, jsy));

                    float jt        = 0.80f * jsy;   // body text
                    float jti       = 0.66f * jsy;   // recap sentences (densest content)
                    float headScale = 1.05f * jsy;
                    float lineH     = _dialogueFont.LineSpacing * jt + 12 * jsy;

                    // ── LEFT PAGE: the investigation checklist ────────────────
                    float oy = leftPageArea.Y;
                    _spriteBatch.DrawString(_dialogueFont, "The Investigation",
                        new Vector2(leftPageArea.X, oy), _inkColor,
                        0f, Vector2.Zero, headScale, SpriteEffects.None, 0f);
                    oy += _dialogueFont.LineSpacing * headScale + 16 * jsy;

                    _spriteBatch.DrawString(_dialogueFont,
                        $"Case clues: {_notebook.UnlockedClues.Count}/{_notebook.TotalClueCount}",
                        new Vector2(leftPageArea.X, oy), _inkColor * 0.8f,
                        0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                    oy += lineH;

                    if (_confrontationTopics.Count > 0)
                    {
                        _spriteBatch.DrawString(_dialogueFont,
                            $"Suspects confronted: {HeardConfrontationCount()}/{_confrontationTopics.Count}",
                            new Vector2(leftPageArea.X, oy), _inkColor * 0.8f,
                            0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                        oy += lineH;
                    }
                    oy += lineH * 0.5f;

                    foreach (var room in _caseRooms)
                    {
                        bool solved = _roomSolvedStates.TryGetValue(room, out var s) && s;
                        var (found, total) = _notebook.GetRoomClueCounts(
                            room, id => !_ungatedClueIds.Contains(id));
                        _spriteBatch.DrawString(_dialogueFont,
                            $"{(solved ? "[x]" : "[  ]")} {ToDisplayName(room)}  -  clues {found}/{total}",
                            new Vector2(leftPageArea.X, oy),
                            solved ? new Color(30, 130, 50)
                                   : (found == total ? _inkColor : Color.Gray),
                            0f, Vector2.Zero, jt, SpriteEffects.None, 0f);
                        oy += _dialogueFont.LineSpacing * jt + 10 * jsy;
                    }

                    // ── LEFT PAGE: the evening, assembling itself ─────────────
                    // Every found clue whose name carries a timestamp, in
                    // chronological order - the payoff for collecting timed
                    // clues is watching the timeline fill in.
                    {
                        var timed = new List<(int Min, Clue Clue)>();
                        foreach (var cl in _notebook.UnlockedClues)
                            if (TryParseClueTime(cl.Name, out var tMin))
                                timed.Add((tMin, cl));
                        if (timed.Count > 0)
                        {
                            timed.Sort((a, b) => a.Min.CompareTo(b.Min));
                            oy += 6 * jsy;
                            _spriteBatch.DrawString(_dialogueFont, "The Evening",
                                new Vector2(leftPageArea.X, oy), _inkColor,
                                0f, Vector2.Zero, headScale * 0.75f, SpriteEffects.None, 0f);
                            oy += _dialogueFont.LineSpacing * headScale * 0.75f + 8 * jsy;

                            // Shrink until the whole list fits the remaining page.
                            float availH = leftPageArea.Bottom - oy;
                            float ts     = jti;
                            while (ts > 0.30f &&
                                   timed.Count * (_dialogueFont.LineSpacing * ts + 4 * jsy) > availH)
                                ts *= 0.92f;
                            foreach (var (_, cl) in timed)
                            {
                                _spriteBatch.DrawString(_dialogueFont, "* " + cl.Name,
                                    new Vector2(leftPageArea.X + 14 * jsx, oy), _inkColor * 0.85f,
                                    0f, Vector2.Zero, ts, SpriteEffects.None, 0f);
                                oy += _dialogueFont.LineSpacing * ts + 4 * jsy;
                            }
                        }
                    }

                    // ── RIGHT PAGE: solved-deduction recaps ───────────────────
                    float ry = rightPageArea.Y;
                    _spriteBatch.DrawString(_dialogueFont, "Deductions so far",
                        new Vector2(rightPageArea.X, ry), _inkColor,
                        0f, Vector2.Zero, headScale, SpriteEffects.None, 0f);
                    ry += _dialogueFont.LineSpacing * headScale + 16 * jsy;

                    var recaps = new List<(string Room, string Sentence)>();
                    foreach (var room in _caseRooms)
                        if (_roomSolvedSentences.TryGetValue(room, out var sentence))
                            recaps.Add((room, sentence));

                    if (recaps.Count == 0)
                    {
                        DrawWrappedString(_spriteBatch, _dialogueFont,
                            "Nothing pinned down yet. Solve a room's deduction and it lands on this page.",
                            new Vector2(rightPageArea.X, ry), rightPageArea.Width,
                            _dialogueFont.LineSpacing * jt + 4, Color.Gray, jt);
                    }
                    else
                    {
                        // Auto-fit: shrink the recaps until all solved sentences fit
                        // on the page (7 rooms of prose is the worst case). Room
                        // headers shrink proportionally so they don't eat the space
                        // the sentences need.
                        float availH    = rightPageArea.Bottom - ry;
                        float fitScale  = jti;
                        float headScale2 = jt;
                        for (int tries = 0; tries < 6; tries++)
                        {
                            headScale2 = Math.Min(jt, fitScale * 1.25f);
                            float headerH = _dialogueFont.LineSpacing * headScale2 + 4;
                            float h = 0f;
                            foreach (var (_, sentence) in recaps)
                                h += headerH
                                   + MeasureWrappedHeight(_dialogueFont, $"\"{sentence}\"",
                                         rightPageArea.Width,
                                         _dialogueFont.LineSpacing * fitScale + 4, fitScale)
                                   + 10 * jsy;
                            if (h <= availH) break;
                            fitScale *= 0.88f;
                        }

                        foreach (var (room, sentence) in recaps)
                        {
                            _spriteBatch.DrawString(_dialogueFont, ToDisplayName(room),
                                new Vector2(rightPageArea.X, ry), new Color(30, 130, 50),
                                0f, Vector2.Zero, headScale2, SpriteEffects.None, 0f);
                            ry += _dialogueFont.LineSpacing * headScale2 + 4;
                            ry  = DrawWrappedString(_spriteBatch, _dialogueFont,
                                $"\"{sentence}\"",
                                new Vector2(rightPageArea.X, ry), rightPageArea.Width,
                                _dialogueFont.LineSpacing * fitScale + 4, _inkColor, fitScale);
                            ry += 10 * jsy;
                        }
                    }

                    // ── Page turn (back to the board) + close ─────────────────
                    DrawUiButton(_journalTurnBackRect, UiCream,
                        "< Board", maxLabelScale: jt);
                    DrawUiButton(_journalCloseRect, UiRed, "X", maxLabelScale: 0.6f);

                    _spriteBatch.End();
                }
            } // end Playing

            // ════════════════════════════════════════════════════════════════
            // PASS 9 — CRT OVERLAY (optional). Drawn into the render target so
            // it covers every state and appears in screenshots.
            // ════════════════════════════════════════════════════════════════
            if (_settings.CrtEnabled)
                _crt.Draw(_spriteBatch, SCREEN_WIDTH, SCREEN_HEIGHT);

            // ── Blit render target to the OS window at display scale ─────────
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);

#if BLAZORGL
            // Letterbox into the browser canvas, whatever size it currently is.
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque,
                SamplerState.LinearClamp);
            _spriteBatch.Draw(_renderTarget, WasmScale.LetterboxRect(), Color.White);
            _spriteBatch.End();
#else
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            _spriteBatch.Draw(
                _renderTarget,
                new Rectangle(0, 0, (int)(SCREEN_WIDTH * DISPLAY_SCALE), (int)(SCREEN_HEIGHT * DISPLAY_SCALE)),
                Color.White);
            _spriteBatch.End();
#endif

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
            // Queued; Update() promotes the next message once the current one expires.
            _toastQueue.Enqueue(message);
        }

        // ── Menu screens ───────────────────────────────────────────────────────

        private void DrawCaseIntro()
        {
            GraphicsDevice.Clear(new Color(8, 8, 10));

            // Poster art has straight alpha (PremultiplyAlpha=False in the .mgcb).
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            // Poster letterboxed on the left, full height minus a margin.
            float posterRight = 40f;
            if (_posterTex != null)
            {
                float ph = SCREEN_HEIGHT - 40f;
                float pw = _posterTex.Width * (ph / _posterTex.Height);
                _spriteBatch.Draw(_posterTex,
                    new Rectangle(40, 20, (int)pw, (int)ph), Color.White);
                posterRight = 40f + pw;
            }

            // Typewriter narration on the right.
            float textX     = posterRight + 80f;
            float maxWidth  = SCREEN_WIDTH - textX - 60f;
            DrawRichText(_spriteBatch, _dialogueFont,
                _introPages[_introPageIndex], Array.Empty<Keyword>(),
                new Vector2(textX, 120f), maxWidth,
                maxChars: Math.Max(0, (int)_typewriterTimer), scale: 0.8f,
                color: new Color(225, 220, 205));

            // Page dots + controls footer.
            string pager  = $"{_introPageIndex + 1}/{_introPages.Length}";
            string footer = "Enter - continue   Esc - skip";
            float  fScale = 0.62f;
            var    fSz    = _dialogueFont.MeasureString(footer) * fScale;
            _spriteBatch.DrawString(_dialogueFont, footer,
                new Vector2(SCREEN_WIDTH - fSz.X - 40f, SCREEN_HEIGHT - fSz.Y - 24f),
                new Color(140, 140, 150), 0f, Vector2.Zero, fScale, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_dialogueFont, pager,
                new Vector2(textX, SCREEN_HEIGHT - fSz.Y - 24f),
                new Color(110, 110, 120), 0f, Vector2.Zero, fScale, SpriteEffects.None, 0f);

            _spriteBatch.End();
        }

        private void DrawEndScene()
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            var   beat      = _endBeats[_endBeatIndex];
            var   textColor = new Color(225, 220, 205);
            float textScale = 0.85f;
            float maxWidth  = Math.Min(1000f, SCREEN_WIDTH - 200f);
            float textX     = (SCREEN_WIDTH - maxWidth) * 0.5f;

            if (beat.IsCard)
            {
                // Title card: every line centered, first line big and gold.
                string[] lines = beat.Text.Split('\n');
                float blockH = 0f;
                bool  first  = true;
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
                    float lscale = first ? 2.0f : 0.9f;
                    var   lsz    = _dialogueFont.MeasureString(line) * lscale;
                    var   lpos   = new Vector2((SCREEN_WIDTH - lsz.X) * 0.5f, cy);
                    if (first)
                        _spriteBatch.DrawString(_dialogueFont, line, lpos + new Vector2(4, 4),
                            Color.Black * 0.8f, 0f, Vector2.Zero, lscale, SpriteEffects.None, 0f);
                    _spriteBatch.DrawString(_dialogueFont, line, lpos,
                        first ? Color.Gold : textColor,
                        0f, Vector2.Zero, lscale, SpriteEffects.None, 0f);
                    cy += _dialogueFont.LineSpacing * lscale;
                    first = false;
                }

                string cardFooter = "Enter - menu";
                var    cfSz       = _dialogueFont.MeasureString(cardFooter) * 0.62f;
                _spriteBatch.DrawString(_dialogueFont, cardFooter,
                    new Vector2(SCREEN_WIDTH - cfSz.X - 40f, SCREEN_HEIGHT - cfSz.Y - 24f),
                    new Color(140, 140, 150), 0f, Vector2.Zero, 0.62f, SpriteEffects.None, 0f);

                _spriteBatch.End();
                return;
            }

            float textTop;
            if (beat.ShowRudebeak && _rudebeakTex != null)
            {
                // Image beat: Rudebeak centered up top, speaker label in the gap,
                // caption below.
                float ih = 420f;
                float iw = _rudebeakTex.Width * (ih / _rudebeakTex.Height);
                _spriteBatch.Draw(_rudebeakTex,
                    new Rectangle((int)((SCREEN_WIDTH - iw) * 0.5f), 30, (int)iw, (int)ih),
                    Color.White);
                textTop = 30f + ih + 30f;
                if (beat.Speaker.Length > 0)
                    textTop += _dialogueFont.LineSpacing * 0.75f + 20f;
            }
            else
            {
                // Text beat: vertically centered (label + text as one block).
                float textH  = MeasureRichTextHeight(_dialogueFont, beat.Text, maxWidth, textScale);
                float labelH = beat.Speaker.Length > 0 ? _dialogueFont.LineSpacing * 0.75f + 30f : 0f;
                textTop = Math.Max(80f, (SCREEN_HEIGHT - textH - labelH) * 0.5f + labelH);
            }

            if (beat.Speaker.Length > 0)
            {
                float lScale = 0.75f;
                var   lSz    = _dialogueFont.MeasureString(beat.Speaker) * lScale;
                _spriteBatch.DrawString(_dialogueFont, beat.Speaker,
                    new Vector2((SCREEN_WIDTH - lSz.X) * 0.5f, textTop - lSz.Y - 30f),
                    Color.Gold, 0f, Vector2.Zero, lScale, SpriteEffects.None, 0f);
            }

            DrawRichText(_spriteBatch, _dialogueFont, beat.Text, Array.Empty<Keyword>(),
                new Vector2(textX, textTop), maxWidth,
                maxChars: (int)_typewriterTimer, scale: textScale, color: textColor);

            string footer = _endBeatIndex < _endBeats.Length - 1 ? "Enter" : "Enter - menu";
            float  fScale = 0.62f;
            var    fSz    = _dialogueFont.MeasureString(footer) * fScale;
            _spriteBatch.DrawString(_dialogueFont, footer,
                new Vector2(SCREEN_WIDTH - fSz.X - 40f, SCREEN_HEIGHT - fSz.Y - 24f),
                new Color(140, 140, 150), 0f, Vector2.Zero, fScale, SpriteEffects.None, 0f);

            _spriteBatch.End();
        }

        /// <summary>
        /// Code-drawn UI button: drop shadow, fill, lighter border, hover highlight,
        /// and a label auto-scaled to fit inside the rect.
        /// </summary>
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
        /// <summary>
        /// Doorway arrow heading. An explicit ArrowDirection on the zone (e.g. the
        /// entrance stairs: side doorway, but you walk UP) beats the heuristic
        /// (direction from the zone's position vs the room center).
        /// </summary>
        private void ArrowHeading(TransferZone zone, out bool horizontal, out bool pointsNegative)
        {
            float ndx = (zone.TriggerRect.Center.X - SCREEN_WIDTH  * 0.5f) / SCREEN_WIDTH;
            float ndy = (zone.TriggerRect.Center.Y - SCREEN_HEIGHT * 0.5f) / SCREEN_HEIGHT;
            switch (zone.ArrowDirection.ToLowerInvariant())
            {
                case "up":    horizontal = false; pointsNegative = true;  break;
                case "down":  horizontal = false; pointsNegative = false; break;
                case "left":  horizontal = true;  pointsNegative = true;  break;
                case "right": horizontal = true;  pointsNegative = false; break;
                default:
                    horizontal     = Math.Abs(ndx) > Math.Abs(ndy);
                    pointsNegative = horizontal ? ndx < 0 : ndy < 0;
                    break;
            }
        }

        /// <summary>Resting (un-pulsed, un-bobbed) screen rect of a doorway arrow - the tap target.</summary>
        private Rectangle DoorArrowRect(TransferZone zone)
        {
            const float ARROW_DRAW_W = 64f;   // keep in sync with Pass 4b
            float scale = ARROW_DRAW_W / _arrowTex.Width;
            ArrowHeading(zone, out bool horizontal, out _);
            float halfY = (horizontal ? _arrowTex.Height : _arrowTex.Width)  * scale * 0.5f;
            float halfX = (horizontal ? _arrowTex.Width  : _arrowTex.Height) * scale * 0.5f;
            var c = new Vector2(zone.TriggerRect.Center.X, zone.TriggerRect.Y - 8f - halfY);
            c.X = MathHelper.Clamp(c.X, halfX + 6f, SCREEN_WIDTH  - halfX - 6f);
            c.Y = MathHelper.Clamp(c.Y, halfY + 6f, SCREEN_HEIGHT - halfY - 6f);
            return new Rectangle((int)(c.X - halfX), (int)(c.Y - halfY), (int)(halfX * 2), (int)(halfY * 2));
        }

        private void UpdateLayout()
        {
            float sx = (float)SCREEN_WIDTH  / 2020f;
            float sy = (float)SCREEN_HEIGHT / 1136f;
            int S(float v) => (int)v;

            // HUD (cozy kit): wide tab-style pills, bottom-right stack + top-right Menu.
            _solveButtonRect      = new Rectangle(S((2020 - 290) * sx), S((1136 - 140) * sy), S(250 * sx), S(100 * sy));
            _finalSolveButtonRect = new Rectangle(S((2020 - 290) * sx), S((1136 - 262) * sy), S(250 * sx), S(96 * sy));
            _menuButtonRect       = new Rectangle(S((2020 - 190) * sx), S(28 * sy), S(160 * sx), S(70 * sy));

            // Tab bar image is 800×150 (four 200px humps), drawn scaled at _tabImagePos —
            // hotspots mirror those humps exactly in the same reference space.
            _tabImagePos = new Vector2(1050 * sx, 100 * sy);
            _tabHotspots = new Rectangle[4];
            for (int i = 0; i < 4; i++)
                _tabHotspots[i] = new Rectangle(
                    S((1050 + i * 200) * sx), S(100 * sy), S(200 * sx), S(150 * sy));

            // Main menu: centered column of buttons; settings BACK shares the column.
            _mainMenuButtonRects = new Rectangle[_mainMenuLabels.Length];
            for (int i = 0; i < _mainMenuButtonRects.Length; i++)
                _mainMenuButtonRects[i] = new Rectangle(
                    S(700 * sx), S((460 + i * 122) * sy), S(620 * sx), S(96 * sy));
            _settingsBackRect = new Rectangle(S(700 * sx), S(920 * sy), S(620 * sx), S(96 * sy));

            // Settings rows: labels left, controls right (2020x1136 reference space).
            _settingsVolMinusRect = new Rectangle(S(1000 * sx), S(450 * sy), S(70 * sx), S(80 * sy));
            _settingsVolCellRects = new Rectangle[10];
            for (int i = 0; i < 10; i++)
                _settingsVolCellRects[i] = new Rectangle(
                    S((1090 + i * 62) * sx), S(450 * sy), S(56 * sx), S(80 * sy));
            _settingsVolPlusRect   = new Rectangle(S(1716 * sx), S(450 * sy), S(70 * sx), S(80 * sy));
            _settingsCrtToggleRect = new Rectangle(S(1090 * sx), S(610 * sy), S(180 * sx), S(80 * sy));

            // Casebook menu (Game1.MenuScreens.cs) overrides the rects above with
            // right-page positions once the spread art is loaded.
            LayoutMenuScreens();

            _journalPrevPageRect = new Rectangle(S(1050 * sx), S(692 * sy), S(90 * sx), S(35 * sy));
            _journalNextPageRect = new Rectangle(S(1710 * sx), S(692 * sy), S(90 * sx), S(35 * sy));
            // Top-right of the inspector paper, level with the clue name —
            // keeps the button clear of the wrapped description text.
            _journalInsertRect   = new Rectangle(S(1610 * sx), S(800 * sy), S(140 * sx), S(50 * sy));
            _journalSubmitRect   = new Rectangle(S(1400 * sx), S(1040 * sy), S(270 * sx), S(70 * sy));
            _journalClearRect    = new Rectangle(S(1200 * sx), S(1040 * sy), S(170 * sx), S(70 * sy));
            _journalCloseRect    = new Rectangle(S(1930 * sx), S(30  * sy), S(60  * sx), S(60 * sy));

            // Page-turn buttons live in the book's bottom corners (page-corner metaphor):
            // forward on the right page, back on the left page.
            _journalTurnPageRect = new Rectangle(S(1700 * sx), S(1040 * sy), S(180 * sx), S(70 * sy));
            _journalTurnBackRect = new Rectangle(S(250  * sx), S(1040 * sy), S(180 * sx), S(70 * sy));
        }

        // ── Screenshot helper ──────────────────────────────────────────────────
#if !BLAZORGL
        private void SaveScreenshot()
        {
            // Navigate three levels up from the binary folder to the project root.
            string dir = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "debug_output"));
            Directory.CreateDirectory(dir);
            string suffix = _screenshotView.Length > 0 ? $"_{_screenshotView.Replace(':', '_')}" : "";
            string path = Path.Combine(dir, $"{_screenshotCase}_{_screenshotRoom}{suffix}.png");
            // The game blits the render target with BlendState.Opaque, so its alpha
            // channel never shows on screen - but translucent overlays lower it,
            // and a raw PNG would show those areas see-through (grey). Flatten.
            var pixels = new Color[_renderTarget.Width * _renderTarget.Height];
            _renderTarget.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++) pixels[i].A = 255;
            using var flat = new Texture2D(GraphicsDevice, _renderTarget.Width, _renderTarget.Height);
            flat.SetData(pixels);
            using var stream = File.Create(path);
            flat.SaveAsPng(stream, flat.Width, flat.Height);
            Console.WriteLine($"[Screenshot] Saved → {Path.GetFullPath(path)}");
        }
#endif

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

        /// <summary>
        /// Height DrawRichText would occupy: same tokenizer ('\n' breaks, space
        /// handling, wrap at maxWidth), no drawing. Brackets are stripped the way
        /// ParseSpans removes them, so keyword spans measure at their visible width.
        /// (MeasureWrappedHeight can't be used here - it ignores '\n'.)
        /// </summary>
        private float MeasureRichTextHeight(SpriteFont font, string text, float maxWidth, float scale)
        {
            string plain = text.Replace("[", "").Replace("]", "");
            float x = 0f, y = 0f;
            float lineH = font.LineSpacing * scale;
            int pos = 0;
            while (pos < plain.Length)
            {
                if (plain[pos] == '\n') { x = 0f; y += lineH; pos++; continue; }
                bool isSpace = plain[pos] == ' ';
                int  start   = pos;
                while (pos < plain.Length && plain[pos] != '\n' &&
                       (plain[pos] == ' ') == isSpace)
                    pos++;
                float tokenW = font.MeasureString(plain[start..pos]).X * scale;
                if (isSpace)
                {
                    if (x > 0f) x += tokenW;
                }
                else
                {
                    if (x > 0f && x + tokenW > maxWidth) { x = 0f; y += lineH; }
                    x += tokenW;
                }
            }
            return y + lineH;
        }

        /// <summary>
        /// Splits a line into sentences at ./!/? boundaries (trailing quotes kept),
        /// never inside a [keyword] span.
        /// </summary>
        private static List<string> SplitSentences(string line)
        {
            var parts = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '[') depth++;
                else if (c == ']') depth = Math.Max(0, depth - 1);
                else if (depth == 0 && (c == '.' || c == '!' || c == '?'))
                {
                    int j = i + 1;
                    while (j < line.Length && (line[j] == '\'' || line[j] == '"')) j++;
                    if (j < line.Length && line[j] == ' ')
                    {
                        parts.Add(line[start..j]);
                        start = j + 1;
                        i = j;
                    }
                }
            }
            if (start < line.Length)
                parts.Add(line[start..]);
            return parts;
        }

        /// <summary>
        /// Builds the dialogue page list: author '|' breaks first, then any page
        /// that would overflow the box's text area is auto-split further - at '\n'
        /// line boundaries, then at sentence boundaries (never inside a [keyword]).
        /// </summary>
        private string[] PaginateDialogue(string rawText, bool hasPortrait)
        {
            var box = DialogueBoxRect;
            float maxWidth = box.Width - DIALOGUE_PAD_X * 2
                             - (hasPortrait ? PORTRAIT_MAX_W + PORTRAIT_TEXT_GAP : 0);
            float hintH     = _dialogueFont.LineSpacing * DIALOGUE_TEXT_SCALE;
            float maxHeight = box.Height - DIALOGUE_PAD_Y
                              - DIALOGUE_PAD_Y * 0.6f - hintH - 8f;

            bool Fits(string s) =>
                MeasureRichTextHeight(_dialogueFont, s, maxWidth, DIALOGUE_TEXT_SCALE) <= maxHeight;

            var pages = new List<string>();
            foreach (var page in rawText.Split('|'))
            {
                if (Fits(page)) { pages.Add(page); continue; }

                // Units small enough to pack: lines, or sentences of an oversize line.
                var units = new List<(string Text, string Sep)>();
                var lines = page.Split('\n');
                for (int li = 0; li < lines.Length; li++)
                {
                    string sep = li == 0 ? "" : "\n";
                    if (Fits(lines[li])) { units.Add((lines[li], sep)); continue; }
                    bool first = true;
                    foreach (var sentence in SplitSentences(lines[li]))
                    {
                        units.Add((sentence, first ? sep : " "));
                        first = false;
                    }
                }

                string current = "";
                foreach (var (text, sep) in units)
                {
                    string candidate = current.Length == 0 ? text : current + sep + text;
                    if (current.Length > 0 && !Fits(candidate))
                    {
                        pages.Add(current);
                        current = text;
                    }
                    else
                    {
                        current = candidate;
                    }
                }
                if (current.Length > 0)
                    pages.Add(current);
            }
            return pages.Count > 0 ? pages.ToArray() : new[] { "" };
        }

        /// <summary>
        /// Sets the dialogue portrait from the speaker's own in-world sprite.
        /// Characters (entities with topics) use a face crop; objects show their
        /// full sprite.
        /// </summary>
        private void SetDialoguePortrait(InteractableEntity entity)
        {
            _dialoguePortrait = null;
            if (entity.Data == null || entity.Texture == null)
                return;
            var tex = entity.Texture;
            var crop = entity.Data.PortraitCrop;
            // Character default: middle 60% of the width x top 32% - sprites carry
            // transparent side margins, so a full-width crop leaves the face tiny in
            // the frame. room_config "portraitCrop": [x, y, w, h] (texture fractions)
            // overrides it for poses where the face is not at the top (a sleeping dog).
            bool isCharacter = entity.Data.Topics.Length > 0;
            float dx = isCharacter ? 0.20f : 0f, dy = 0f;
            float dw = isCharacter ? 0.60f : 1f, dh = isCharacter ? 0.32f : 1f;
            float cx = crop?[0] ?? dx, cy = crop?[1] ?? dy;
            float cw = crop?[2] ?? dw, ch = crop?[3] ?? dh;
            _dialoguePortrait       = tex;
            _dialoguePortraitSource = new Rectangle(
                (int)(tex.Width * cx), (int)(tex.Height * cy),
                Math.Max(1, (int)(tex.Width * cw)),
                Math.Max(1, (int)(tex.Height * ch)));
        }

        private void DrawRichText(
            SpriteBatch spriteBatch,
            SpriteFont  font,
            string      text,
            Keyword[]   keywords,
            Vector2     origin,
            float       maxWidth,
            int         maxChars = int.MaxValue,
            float       scale    = 1f,
            Color?      color    = null)
        {
            Color textColor  = color ?? _inkColor;   // dark ink unless a scene overrides
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
                    // Explicit '\n' forces a line break (bulleted lists, radio logs).
                    if (spanText[pos] == '\n')
                    {
                        x  = origin.X;
                        y += lineH;
                        pos++;
                        continue;
                    }
                    bool isSpace = spanText[pos] == ' ';
                    int  start   = pos;
                    while (pos < spanText.Length && spanText[pos] != '\n' &&
                           (spanText[pos] == ' ') == isSpace)
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
                        spriteBatch.DrawString(font, drawToken, new Vector2(x, y), textColor,
                            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                        x          += tokenW;
                        charsDrawn += token.Length;
                    }
                }
            }
        }
    }
}
