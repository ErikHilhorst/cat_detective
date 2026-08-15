using System;
using System.Collections.Generic;
using System.IO;
using CatDetective.Map;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CatDetective.Systems
{
    /// <summary>
    /// Background asset warmer for the shared ContentManager. On the web build
    /// every Content.Load is a blocking XHR + Brotli decode, so a cold LoadRoom
    /// stalls for seconds while it fetches the background plus every prop and
    /// interactable sprite serially. This queues those same asset paths ahead
    /// of time and loads ONE per Pump() call, so each stall hides in a frame
    /// where a hitch is invisible (menus, the case intro, open dialogue/board,
    /// an idle cat). The ContentManager caches by asset path and is never
    /// unloaded, so a warmed room's LoadRoom becomes pure cache hits.
    /// </summary>
    public sealed class RoomPreloader
    {
        private readonly ContentManager  _content;
        private readonly Queue<Action>   _steps       = new();
        private readonly HashSet<string> _queuedRooms = new();
        private readonly HashSet<string> _queuedCases = new();

        public RoomPreloader(ContentManager content) => _content = content;

        private int _stepsRun;

        public bool HasWork => _steps.Count > 0;

        /// <summary>
        /// Queues every room of a case in its rooms-list order. To prioritize a
        /// specific room (e.g. the saved room for CONTINUE), QueueRoom it first.
        /// </summary>
        public void QueueCase(string caseId)
        {
            if (caseId.Length == 0 || !_queuedCases.Add(caseId))
                return;

            // LoadCase also loads the shared sunbeam mask; warm it too.
            _steps.Enqueue(() => _content.Load<Texture2D>("Shared/mask_sunbeams"));

            // Reading case_config is itself a blocking fetch on web, so
            // discovery runs as a queue step instead of inline.
            _steps.Enqueue(() =>
            {
                var cfg = LevelConfigParser.LoadCase(Path.Combine(
                    _content.RootDirectory, "Levels", caseId, "case_config.json"));
                foreach (var roomId in cfg.Rooms)
                    QueueRoom(caseId, roomId);
            });
        }

        /// <summary>Queues one room's background, props, and interactable sprites.</summary>
        public void QueueRoom(string caseId, string roomId)
        {
            if (caseId.Length == 0 || roomId.Length == 0 ||
                !_queuedRooms.Add($"{caseId}/{roomId}"))
                return;

            _steps.Enqueue(() =>
            {
                var cfg = LevelConfigParser.LoadRoom(Path.Combine(
                    _content.RootDirectory, "Levels", caseId, roomId, "room_config.json"));
                string contentBase = $"Levels/{caseId}/{roomId}";

                _steps.Enqueue(() => _content.Load<Texture2D>($"{contentBase}/bg_base"));

                foreach (var prop in cfg.Props)
                {
                    string tex = prop.Texture;
                    _steps.Enqueue(() => _content.Load<Texture2D>($"{contentBase}/{tex}"));
                }

                // Mirrors MapParser's lookup: per-name sprite under
                // Interactables/, else the config's fallback texture path.
                foreach (var (name, data) in cfg.Interactables)
                {
                    string sprite   = $"{contentBase}/Interactables/{name}";
                    string fallback = data.TexturePath;
                    _steps.Enqueue(() =>
                    {
                        try
                        {
                            _content.Load<Texture2D>(sprite);
                        }
                        catch (ContentLoadException)
                        {
                            if (!string.IsNullOrEmpty(fallback))
                                _content.Load<Texture2D>(fallback);
                        }
                    });
                }
            });
        }

        /// <summary>
        /// Runs one queued step (at most one blocking fetch). Call only on
        /// frames where a stall is invisible.
        /// </summary>
        public void Pump()
        {
            if (_steps.Count == 0)
                return;

            try
            {
                _steps.Dequeue()();
            }
            catch (Exception ex)
            {
                // A bad/missing asset must never break the pump; LoadRoom's own
                // fallbacks still apply when the room is actually entered.
                Console.WriteLine($"[Preload] Skipped a step: {ex.Message}");
            }

            _stepsRun++;
            if (_steps.Count == 0)
                Console.WriteLine($"[Preload] Warm-up complete ({_stepsRun} steps).");
        }
    }
}
