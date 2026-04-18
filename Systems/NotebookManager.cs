using System.Collections.Generic;
using CatDetective.Entities;

namespace CatDetective.Systems
{
    /// <summary>
    /// Owns the master clue database and tracks which clues the player has found.
    /// The database is loaded from <c>level_config.json</c> via <see cref="LevelConfig"/>
    /// and passed in at construction time — nothing is hardcoded here.
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
    }
}
