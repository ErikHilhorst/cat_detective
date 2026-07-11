using System.Collections.Generic;
using CatDetective.Entities;

namespace CatDetective.Systems
{
    /// <summary>
    /// Owns the master clue database and tracks which clues the player has found.
    /// The database is populated from <c>case_config.json</c> at case load time
    /// and persists across room transitions.
    /// </summary>
    public sealed class NotebookManager
    {
        private readonly Dictionary<string, Clue> _database;

        public List<Clue> UnlockedClues { get; } = new();

        /// <summary>
        /// Fired once per clue, the moment it is first unlocked - regardless of
        /// the unlock path (keyword, topic, or room-solve auto-unlock).
        /// Game1 uses it for the gate-unlock toast.
        /// </summary>
        public System.Action<string>? OnClueUnlocked { get; set; }

        public NotebookManager(Dictionary<string, Clue> database)
        {
            _database = database;
        }

        /// <summary>
        /// Adds the clue with <paramref name="clueId"/> to <see cref="UnlockedClues"/>
        /// if it exists in the database and has not been found yet.
        /// Silently ignores unknown or duplicate IDs.
        /// </summary>
        public void UnlockClue(string clueId)
        {
            if (!_database.TryGetValue(clueId, out var clue)) return;
            if (UnlockedClues.Exists(c => c.Id == clueId))    return;
            UnlockedClues.Add(clue);
            OnClueUnlocked?.Invoke(clueId);
        }

        /// <summary>True if the clue has already been found (gates dialogue topics).</summary>
        public bool IsUnlocked(string clueId) =>
            UnlockedClues.Exists(c => c.Id == clueId);

        /// <summary>Total number of clues in the case database (for the case-wide counter).</summary>
        public int TotalClueCount => _database.Count;

        /// <summary>Database lookup regardless of unlock state; null if unknown.</summary>
        public Clue? GetClue(string clueId) =>
            _database.TryGetValue(clueId, out var clue) ? clue : null;

        /// <summary>Found/total clue counts for one room (for per-room progress displays).</summary>
        public (int Found, int Total) GetRoomClueCounts(string roomId)
        {
            int total = 0;
            foreach (var clue in _database.Values)
                if (clue.RoomId == roomId) total++;
            int found = UnlockedClues.FindAll(c => c.RoomId == roomId).Count;
            return (found, total);
        }

        /// <summary>
        /// Returns all unlocked clues that belong to <paramref name="roomId"/> — macro and
        /// micro. The room puzzle answers are macro clues unlocked via keywords, so they
        /// must appear in the local word bank.
        /// </summary>
        public List<Clue> GetCluesForRoom(string roomId) =>
            UnlockedClues.FindAll(c => c.RoomId == roomId);

        /// <summary>
        /// Returns unlocked clues that feed the final case-level deduction
        /// regardless of which room they were found in.
        /// </summary>
        public List<Clue> GetMacroClues() =>
            UnlockedClues.FindAll(c => c.IsMacroClue);

        /// <summary>
        /// Silently unlocks every macro clue in the database that belongs to
        /// <paramref name="roomId"/>. Called when the player solves a room's local puzzle.
        /// </summary>
        public void UnlockMacroCluesForRoom(string roomId)
        {
            foreach (var clue in _database.Values)
            {
                if (clue.RoomId == roomId && clue.IsMacroClue)
                    UnlockClue(clue.Id);
            }
        }
    }
}
