using CatDetective.Entities;
using CatDetective.Map;
using CatDetective.Systems;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace CatDetective
{
    /// <summary>
    /// Tap / click controls for the room (phones, tablets, and the desktop mouse).
    ///
    /// TAP TO GO, TAP AGAIN TO ACT:
    ///   - Tap floor             -> the cat walks there (nearest REACHABLE spot
    ///                              when the tap lands on furniture or a cut-off
    ///                              patch of floor). Clears any selection.
    ///   - Tap a person/object   -> SELECT it (gold pulsing outline) and walk to
    ///                              the walk-nearest spot in reach. Nothing opens.
    ///   - Tap a door arrow/zone -> SELECT the door (its arrow turns gold) and
    ///                              walk into the doorway. Nothing loads.
    ///   - Tap the selected thing again -> act: opens the dialogue / goes through
    ///                              the door, immediately if in reach, otherwise
    ///                              as soon as the cat arrives.
    /// Movement keys cancel the path and the selection; Enter keeps working.
    ///
    /// Stuck handling: if the cat barely moves for STUCK_SECONDS while walking a
    /// path it replans once, then gives up and stands still. Rooms where that
    /// happens get their Collisions rects simplified in Tiled - no per-room code.
    /// </summary>
    public partial class Game1
    {
        private const float STUCK_SECONDS     = 0.4f;
        private const float STUCK_MIN_PROGRESS = 6f;     // px within STUCK_SECONDS
        private const int   MIN_TAP_TARGET     = 72;     // game px; small props get a fat finger target

        private readonly PointerInput _pointer = new();
        private Point          _pointerGamePos;
        private NavGrid?       _navGrid;
        private readonly List<Vector2> _navPath = new();

        // Current destination, kept for the stuck replan.
        private Vector2?   _navGoalPoint;
        private Rectangle? _navGoalZone;

        private InteractableEntity? _tapEntity;        // tap-selected person/object
        private TransferZone?       _tapDoor;          // tap-selected doorway
        private bool                _tapActOnArrival;  // second tap given while walking

        private float   _stuckTimer;
        private Vector2 _stuckAnchor;
        private bool    _stuckReplanned;

        // Dialogue taps, consumed by the dialogue branch of Update.
        private bool _dialogueTapAdvance;
        private int  _tapTopicIndex = -1;

        // HUD buttons that stand in for Escape.
        private Rectangle _menuButtonRect;
        private Rectangle DialogueCloseRect
        {
            get
            {
                var box = DialogueBoxRect;
                return new Rectangle(box.Right - DIALOGUE_PAD_X - 20, box.Y + DIALOGUE_PAD_Y - 78, 64, 64);
            }
        }

        private void ClearTapState()
        {
            _navPath.Clear();
            _navGoalPoint    = null;
            _navGoalZone     = null;
            _tapEntity       = null;
            _tapDoor         = null;
            _tapActOnArrival = false;
            _stuckTimer      = 0f;
            _stuckReplanned  = false;
            if (_cat != null) _cat.SteerTarget = null;
        }

        // ── World taps ────────────────────────────────────────────────────────

        private void HandleWorldTap(Point p)
        {
            if (_navGrid == null || _cat == null) return;
            var tap = new Vector2(p.X, p.Y);

            // Priority: door arrow (explicit target) > person/object sprite >
            // door trigger zone > floor.
            TransferZone?       door   = HitDoorArrow(tap);
            InteractableEntity? entity = door == null ? HitInteractable(tap) : null;
            if (door == null && entity == null) door = HitDoorZone(tap);

            if (entity != null)
            {
                if (ReferenceEquals(entity, _tapEntity))
                {
                    // Second tap: act now if in reach (the Update branch opens it
                    // this frame), otherwise once the cat gets there.
                    _tapActOnArrival = true;
                    return;
                }
                ClearTapState();
                _tapEntity = entity;
                if (!_cat.CollisionBox.Intersects(entity.InteractZone))
                    PlanToZone(entity.InteractZone);
                return;
            }

            if (door != null)
            {
                if (ReferenceEquals(door, _tapDoor))
                {
                    _tapActOnArrival = true;
                    return;
                }
                ClearTapState();
                _tapDoor = door;
                if (!_cat.CollisionBox.Intersects(door.TriggerRect))
                    PlanToZone(door.TriggerRect);
                return;
            }

            ClearTapState();
            PlanToPoint(tap);
        }

        private void PlanToPoint(Vector2 target)
        {
            _navGoalPoint = target;
            _navGoalZone  = null;
            ReplacePath(_navGrid!.PathTo(_cat.Position, target));
        }

        private void PlanToZone(Rectangle zone)
        {
            _navGoalZone  = zone;
            _navGoalPoint = null;
            // Probe slightly narrower than the narrowest feet box, so the real
            // box (any facing, int-truncated) is sure to overlap on arrival.
            var path = _navGrid!.PathToZone(_cat.Position, zone, Math.Max(4, _cat.ProbeHalfWidth - 2));
            // Unreachable zone (verify_level.py should flag it): get as close as possible.
            path ??= _navGrid.PathTo(_cat.Position, zone.Center.ToVector2());
            ReplacePath(path);
        }

        private void ReplacePath(List<Vector2>? path)
        {
            _navPath.Clear();
            if (path != null) _navPath.AddRange(path);
            _stuckTimer  = 0f;
            _stuckAnchor = _cat.Position;
        }

        private void FollowTapPath()
        {
            // Corners are passed loosely; the FINAL waypoint is landed exactly
            // (the cat steers onto it), so arrival checks see the planned box.
            while (_navPath.Count > 0 &&
                   Vector2.Distance(_cat.Position, _navPath[0]) < (_navPath.Count > 1 ? 1.5f : 0.1f))
                _navPath.RemoveAt(0);
            _cat.SteerTarget = _navPath.Count > 0 ? _navPath[0] : null;
        }

        private void UpdateTapStuck(float dt)
        {
            if (_navPath.Count == 0) { _stuckTimer = 0f; return; }

            if (Vector2.Distance(_cat.Position, _stuckAnchor) > STUCK_MIN_PROGRESS)
            {
                _stuckAnchor = _cat.Position;
                _stuckTimer  = 0f;
                return;
            }

            _stuckTimer += dt;
            if (_stuckTimer < STUCK_SECONDS) return;

            if (!_stuckReplanned)
            {
                _stuckReplanned = true;
                if (_navGoalZone.HasValue)       PlanToZone(_navGoalZone.Value);
                else if (_navGoalPoint.HasValue) PlanToPoint(_navGoalPoint.Value);
                Console.WriteLine($"[TapNav] stuck in {_currentRoomId} at " +
                                  $"({_cat.Position.X:0},{_cat.Position.Y:0}) - replanned.");
            }
            else
            {
                // Give up: stand still, keep the selection (a tap retries).
#if !BLAZORGL
                _navGiveUps++;
#endif
                Console.WriteLine($"[TapNav] stuck in {_currentRoomId} at " +
                                  $"({_cat.Position.X:0},{_cat.Position.Y:0}) - stopped. " +
                                  "Simplify the Collisions rects here in Tiled.");
                _navPath.Clear();
                _cat.SteerTarget = null;
                _stuckTimer      = 0f;
                _stuckReplanned  = false;
                _tapActOnArrival = false;
            }
        }

        // ── Hit tests ─────────────────────────────────────────────────────────

        private static Rectangle AtLeast(Rectangle r, int min)
        {
            int growX = Math.Max(0, (min - r.Width)  / 2);
            int growY = Math.Max(0, (min - r.Height) / 2);
            r.Inflate(growX, growY);
            return r;
        }

        private TransferZone? HitDoorArrow(Vector2 p)
        {
            foreach (var zone in _transferZones)
                if (AtLeast(DoorArrowRect(zone), 96).Contains(p)) return zone;
            return null;
        }

        private TransferZone? HitDoorZone(Vector2 p)
        {
            foreach (var zone in _transferZones)
                if (zone.TriggerRect.Contains(p)) return zone;
            return null;
        }

        /// <summary>
        /// Frontmost interactable whose sprite (grown to a finger-sized target)
        /// covers the tap. Characters beat objects on overlap. Objects hidden
        /// behind an opaque walk-behind prop (the speaker behind the couch, the
        /// registry behind the console) only become tappable once the prop fades
        /// - same discovery rule as walking up to them with the keyboard.
        /// </summary>
        private InteractableEntity? HitInteractable(Vector2 p)
        {
            InteractableEntity? best = null;
            foreach (var e in _interactables)
            {
                if (e.Data == null) continue;
                if (!AtLeast(e.SpriteBounds, MIN_TAP_TARGET).Contains(p)) continue;
                if (IsCoveredByProp(e, p)) continue;
                if (best == null) { best = e; continue; }

                bool eChar = e.Data.Topics.Length > 0, bChar = best.Data!.Topics.Length > 0;
                if (eChar != bChar) { if (eChar) best = e; continue; }
                if (e.LayerDepth > best.LayerDepth) best = e;
            }
            return best;
        }

        /// <summary>
        /// A walk-behind prop that sorts in front of the entity is opaque at the
        /// tap point (the couch over the hidden speaker, the console over the
        /// registry). Faded props never cover: once the cat is behind the couch,
        /// the speaker is fair game.
        /// </summary>
        private bool IsCoveredByProp(InteractableEntity e, Vector2 p)
        {
            foreach (var prop in _foregroundProps)
                if (prop.SortDepth > e.LayerDepth && prop.CoversPoint(p)) return true;
            return false;
        }

        /// <summary>Share of the entity's tap target a tap can actually select (navtest report).</summary>
        private float TappableShare(InteractableEntity e)
        {
            var r = AtLeast(e.SpriteBounds, MIN_TAP_TARGET);
            int hit = 0, total = 0;
            for (int y = r.Top; y < r.Bottom; y += 4)
                for (int x = r.Left; x < r.Right; x += 4)
                {
                    total++;
                    if (!IsCoveredByProp(e, new Vector2(x, y))) hit++;
                }
            return total == 0 ? 1f : hit / (float)total;
        }

        // ── Dialogue taps ─────────────────────────────────────────────────────

        private void HandleDialogueTap(Point p)
        {
            if (DialogueCloseRect.Contains(p))
            {
                _isDialogueActive = false;
                _isTopicMenuOpen  = false;
                return;
            }
            if (_isTopicMenuOpen)
            {
                for (int i = 0; i <= _menuTopics.Count; i++)
                    if (TopicRowRect(i).Contains(p)) { _tapTopicIndex = i; return; }
                return;   // a stray tap never picks a topic
            }
            _dialogueTapAdvance = true;
        }

        private const float TOPIC_MENU_SCALE = 0.66f;

        /// <summary>Text x where dialogue / menu text starts (portrait shifts it right).</summary>
        private float DialogueTextX =>
            DialogueBoxRect.X + DIALOGUE_PAD_X +
            (_dialoguePortrait != null ? PORTRAIT_MAX_W + PORTRAIT_TEXT_GAP : 0);

        private float TopicLineHeight => _dialogueFont.LineSpacing * TOPIC_MENU_SCALE + 2f;

        /// <summary>Top-left of topic row i (row 0 follows the "considers" header).</summary>
        private Vector2 TopicRowPos(int i)
        {
            var box = DialogueBoxRect;
            return new Vector2(DialogueTextX,
                box.Y + DIALOGUE_PAD_Y - 20 + TopicLineHeight + 8f + i * TopicLineHeight);
        }

        /// <summary>Tap target for topic row i: the full text width, one line tall.</summary>
        private Rectangle TopicRowRect(int i)
        {
            var pos = TopicRowPos(i);
            var box = DialogueBoxRect;
            return new Rectangle((int)pos.X - 10, (int)pos.Y,
                (int)(box.Right - DIALOGUE_PAD_X - pos.X + 10), (int)TopicLineHeight);
        }

        // ── Headless walk test (--screenshot ui navtest [caseId]) ─────────────

#if !BLAZORGL
        private int _navGiveUps;   // bumped by UpdateTapStuck's "stopped" branch

        /// <summary>
        /// Drives the REAL follower + Cat.Update + MoveWithCollision at 60 fps,
        /// in every room of the case: from every spawn point to every
        /// interactable and doorway (arrival = feet box overlaps the zone, the
        /// same test the game uses), plus random floor taps from spawn_default
        /// (arrival = cat ends on its planned final waypoint). Each failure
        /// prints room + coordinates, so the Collisions rects can be fixed in
        /// Tiled. Exits after printing.
        /// </summary>
        private void RunNavTest(string caseId)
        {
            LoadCase(caseId);
            int checks = 0, failures = 0;
            var rng = new Random(1234);   // deterministic floor taps
            var dtTime = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60.0));
            const float DT = 1f / 60f;

            // Walk until arrival/give-up; true = arrived.
            bool Simulate(Func<bool> arrived, out Vector2 endPos)
            {
                for (int step = 0; step < 60 * 30; step++)
                {
                    if (arrived()) { endPos = _cat.Position; return true; }
                    if (_navPath.Count == 0) break;
                    FollowTapPath();
                    _cat.Update(dtTime);
                    _cat.MoveWithCollision(DT, _solidBoundaries);
                    UpdateTapStuck(DT);
                }
                endPos = _cat.Position;
                return arrived();
            }

            foreach (var room in _caseRooms)
            {
                string mapPath = System.IO.Path.Combine(
                    Content.RootDirectory, "Levels", caseId, room, "room_map.json");
                var spawns = new List<string>();
                using (var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(mapPath)))
                    foreach (var layer in doc.RootElement.GetProperty("layers").EnumerateArray())
                        if (layer.TryGetProperty("name", out var n) && n.GetString() == "Spawn")
                            foreach (var obj in layer.GetProperty("objects").EnumerateArray())
                                spawns.Add(obj.GetProperty("name").GetString() ?? "");

                int roomFail = 0, roomChecks = 0;

                // How much of each object a walk-behind prop covers (0% = only
                // tappable once the cat is behind the prop - the hidden ones).
                LoadRoom(room, "spawn_default");
                foreach (var prop in _foregroundProps) prop.Update(new GameTime());   // settle alpha
                foreach (var e in _interactables)
                {
                    if (e.Data == null) continue;
                    float share = TappableShare(e);
                    if (share < 0.999f)
                        Console.WriteLine($"[NavTest] {room}: '{e.Id}' tappable area {share:P0} " +
                                          "(rest covered by a walk-behind prop until it fades)");
                }

                foreach (var spawn in spawns)
                {
                    LoadRoom(room, spawn);
                    int targetCount = _interactables.Count + _transferZones.Count;
                    for (int t = 0; t < targetCount; t++)
                    {
                        LoadRoom(room, spawn);   // fresh cat at the spawn
                        Rectangle zone;
                        string    label;
                        if (t < _interactables.Count)
                        {
                            var e = _interactables[t];
                            if (e.Data == null) continue;
                            zone  = e.InteractZone;
                            label = e.Id;
                        }
                        else
                        {
                            var d = _transferZones[t - _interactables.Count];
                            // The spawn's own return door is right there; skip
                            // trivially-in-zone cases (they never path).
                            zone  = d.TriggerRect;
                            label = $"door->{d.TargetRoom}";
                        }

                        roomChecks++;
                        if (_cat.CollisionBox.Intersects(zone)) continue;
                        _navGiveUps = 0;
                        PlanToZone(zone);
                        bool ok = Simulate(() => _cat.CollisionBox.Intersects(zone), out var end);
                        if (!ok)
                        {
                            roomFail++;
                            Console.WriteLine($"[NavTest] FAIL {room} {spawn} -> {label}: " +
                                $"stopped at ({end.X:0},{end.Y:0}), zone {zone}" +
                                (_navGiveUps > 0 ? " (stuck)" : " (no path reaches it)"));
                        }
                    }
                }

                // Random floor taps from the default spawn.
                for (int i = 0; i < 150; i++)
                {
                    LoadRoom(room, "spawn_default");
                    var tap = new Vector2(rng.Next(0, SCREEN_WIDTH), rng.Next(0, SCREEN_HEIGHT));
                    _navGiveUps = 0;
                    PlanToPoint(tap);
                    if (_navPath.Count == 0) continue;   // already there / nowhere to go
                    Vector2 goal = _navPath[^1];
                    roomChecks++;
                    bool ok = Simulate(() => Vector2.Distance(_cat.Position, goal) < 3f, out var end);
                    if (!ok)
                    {
                        roomFail++;
                        Console.WriteLine($"[NavTest] FAIL {room} floor tap ({tap.X:0},{tap.Y:0}) -> " +
                            $"goal ({goal.X:0},{goal.Y:0}): stopped at ({end.X:0},{end.Y:0})" +
                            (_navGiveUps > 0 ? " (stuck)" : ""));
                    }
                }

                checks   += roomChecks;
                failures += roomFail;
                Console.WriteLine($"[NavTest] {room}: {roomChecks - roomFail}/{roomChecks} ok");
            }
            Console.WriteLine($"[NavTest] {caseId}: {checks - failures}/{checks} walks arrived, {failures} failed.");
        }
#endif

        // ── Hints ─────────────────────────────────────────────────────────────

        /// <summary>Bottom-center prompt while something is tap-selected, else null.</summary>
        private string? TapSelectionHint()
        {
            if (_tapEntity?.Data != null)
            {
                string name = ResolveDisplayName(_tapEntity.Data, _tapEntity.Id);
                string verb = _tapEntity.Data.Topics.Length > 0 ? "talk to" : "inspect";
                return _tapActOnArrival ? $"Padding over to {name}..."
                                        : $"Tap again to {verb} {name}";
            }
            if (_tapDoor != null)
            {
                return _tapActOnArrival ? "Padding over..."
                                        : "Tap the door again to go through";
            }
            return null;
        }
    }
}
