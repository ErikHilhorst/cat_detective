using System;
using System.Collections.Generic;

namespace CatDetective.Systems
{
    /// <summary>
    /// One screen of the end-scene epilogue. Cards (IsCard) render as centered
    /// title screens - first line big and gold - instead of typewriter prose.
    /// </summary>
    public sealed record EndSceneBeat(
        string Speaker, string Text, bool ShowRudebeak = false, bool IsCard = false);

    /// <summary>
    /// Intro and epilogue copy, keyed by case id. Pure ASCII (the sprite font
    /// covers ASCII 32-126 only); '|' splits an end-scene beat into paragraphs
    /// and '\n' forces a line break, exactly like dialogue text.
    /// </summary>
    public static class CaseScripts
    {
        // ── Case intros (typewriter pages shown beside the poster) ────────────
        // Malibu's intro moved to Content/Levels/malibu_mansion/case_script.json
        // (cold open + news-van roll call, played by Game1.Cutscene.cs). These
        // typewriter pages remain the fallback for cases without a script.
        private static readonly Dictionary<string, string[]> _intros = new();

        // ── End scenes (one beat per screen) ──────────────────────────────────
        private static readonly Dictionary<string, EndSceneBeat[]> _endScenes = new()
        {
            // malibu_mansion: the gathering + ending live in case_script.json.

            ["tutorial"] = new[]
            {
                new EndSceneBeat("",
                    "The white room folds up like a napkin. Somewhere, a " +
                    "sunbeam moves.\n\n" +
                    "Dikkie wakes on the windowsill, already certain of three " +
                    "things: the kitten did it, the wind gets blamed for " +
                    "everything, and the basics never change."),

                new EndSceneBeat("",
                    "WHISKER ACADEMY - BASICS COMPLETE\n\n" +
                    "Dikkie is ready for a real case.",
                    IsCard: true),
            },
        };

        /// <summary>Intro pages for a case; empty when the case has no intro.</summary>
        public static string[] GetIntro(string caseId) =>
            _intros.TryGetValue(caseId, out var pages) ? pages : Array.Empty<string>();

        /// <summary>End-scene beats for a case; a generic card when none is authored.</summary>
        public static EndSceneBeat[] GetEndScene(string caseId) =>
            _endScenes.TryGetValue(caseId, out var beats)
                ? beats
                : new[] { new EndSceneBeat("", "CASE CLOSED") };
    }
}
