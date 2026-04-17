using System.Collections.Generic;
using CatDetective.Entities;

namespace CatDetective.Systems
{
    /// <summary>
    /// Owns the master clue database and tracks which clues the player has found.
    /// Call <see cref="UnlockClue"/> with a keyword ID whenever an interaction fires.
    /// </summary>
    public sealed class NotebookManager
    {
        // ── Master database — every clue that can ever be found ───────────────
        private static readonly Dictionary<string, Clue> _database = new()
        {
            // Who
            // (no suspects yet — placeholder so the Who tab isn't empty forever)

            // What
            { "stolen_pet",     new Clue("stolen_pet",     ClueCategory.What,      "Rudebeak the macaw was stolen") },
            { "fish_tacos",     new Clue("fish_tacos",     ClueCategory.What,      "Leftover Fish Tacos in the takeout bag") },

            // Why
            // (motives TBD — placeholder)

            // WhereWhen
            { "saturday",                    new Clue("saturday",                    ClueCategory.WhereWhen, "'Pirate Cove 3' premiere is Saturday") },
            { "sunday",                      new Clue("sunday",                      ClueCategory.WhereWhen, "Someone's birthday falls on Sunday") },
            { "thursday",                    new Clue("thursday",                    ClueCategory.WhereWhen, "Detective has dentist on Thursday") },
            { "friday",                      new Clue("friday",                      ClueCategory.WhereWhen, "Friday is marked with a question mark") },
            { "the_day_before_the_premiere", new Clue("the_day_before_the_premiere", ClueCategory.WhereWhen, "Manager needed Lance at the house the day before the premiere") },
            { "downtown_office",             new Clue("downtown_office",             ClueCategory.WhereWhen, "Takeout was delivered to the Downtown Office") },
            { "malibu_mansion",              new Clue("malibu_mansion",              ClueCategory.WhereWhen, "VIP parking pass for a Malibu Mansion in the trash") },
        };

        public List<Clue> UnlockedClues { get; } = new();

        /// <summary>
        /// Adds the clue with <paramref name="clueId"/> to <see cref="UnlockedClues"/>
        /// if it exists in the master database and has not been found yet.
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
