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
        }

        /// <summary>
        /// Returns unlocked clues that belong to <paramref name="roomId"/>
        /// and are not macro clues (i.e. they feed a room sub-puzzle).
        /// </summary>
        public List<Clue> GetCluesForRoom(string roomId) =>
            UnlockedClues.FindAll(c => c.RoomId == roomId && !c.IsMacroClue);

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
