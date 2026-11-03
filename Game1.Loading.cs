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
        };

        private Action?   _loadingAction;
        private string[]  _loadingAssets = Array.Empty<string>();
        private GameState _loadingReturnState;
        private string    _loadingLabel = "";
        private int       _loadingDone, _loadingTotal;
        private float     _loadingShown;   // seconds on screen (fade-in)

#if BLAZORGL
        private WebContentManager? WebContent => Content as WebContentManager;
#endif

        /// <summary>
        /// Runs <paramref name="action"/> once every texture whose asset name
        /// starts with one of <paramref name="assets"/> is in memory.
        /// </summary>
        private void WhenAssetsReady(Func<string[]> assets, string label, Action action)
        {
#if BLAZORGL
            if (WebContent != null && WebTextures.Active)
            {
                var list = assets();
                WebTextures.Prioritize(list);
                WebContent.Pump(8);
                var (done, total) = WebTextures.Progress(list);
                if (done < total)
                {
                    _loadingAction      = action;
                    _loadingAssets      = list;
                    _loadingLabel       = label;
                    _loadingDone        = done;
                    _loadingTotal       = total;
                    _loadingShown       = 0f;
                    _loadingReturnState = _currentState;
                    _currentState       = GameState.Loading;
                    return;
                }
            }
#endif
            action();
        }

        private void UpdateLoading(float dt)
        {
            _loadingShown += dt;
#if BLAZORGL
            WebContent?.Pump(30);
            (_loadingDone, _loadingTotal) = WebTextures.Progress(_loadingAssets);
            if (_loadingDone < _loadingTotal) return;
#endif
            var action = _loadingAction;
            _loadingAction = null;
            _currentState  = _loadingReturnState;
            action?.Invoke();
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

        /// <summary>The study scene with a parchment card + progress bar.</summary>
        private void DrawLoadingScreen()
        {
            BeginMenuScene(showPrompt: false);

            float a  = Math.Clamp(_loadingShown / 0.25f, 0f, 1f);
            int   cw = Math.Min(560, SCREEN_WIDTH - 80), ch = 112;
            var card = new Rectangle((SCREEN_WIDTH - cw) / 2, (int)(SCREEN_HEIGHT * 0.49f) - ch / 2, cw, ch);
            DrawParchment(card, a);

            string label = _loadingLabel;
            float  ls    = Math.Min(0.66f, (cw - 60) / _dialogueFont.MeasureString(label).X);
            DrawCentered(label, card.Center.X, card.Y + 16, ls, UiInk * a);

            var bar = new Rectangle(card.X + 40, card.Bottom - 42, cw - 80, 18);
            var src = new Rectangle(0, 0, ROUND_SIZE, ROUND_SIZE);
            DrawNineSlice(_uiRound, src, ROUND_RADIUS + 2, bar, 9, UiSlate * (0.55f * a));
            float p = _loadingTotal > 0 ? _loadingDone / (float)_loadingTotal : 0f;
            if (p > 0f)
            {
                var fill = new Rectangle(bar.X, bar.Y, Math.Max(18, (int)(bar.Width * p)), bar.Height);
                DrawNineSlice(_uiRound, src, ROUND_RADIUS + 2, fill, 9, UiYellow * a);
            }
            DrawNineSlice(_uiOutline, src, ROUND_RADIUS + 2, bar, 9, UiInk * (0.6f * a));

            _spriteBatch.End();
        }
    }
}
