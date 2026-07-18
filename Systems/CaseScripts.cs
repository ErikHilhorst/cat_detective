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
        private static readonly Dictionary<string, string[]> _intros = new()
        {
            ["malibu_mansion"] = new[]
            {
                "MALIBU, CALIFORNIA. TUESDAY. 8:42 PM.\n\n" +
                "The radio said a macaw was missing from the Vale mansion.\n" +
                "The radio said the police had it handled.\n\n" +
                "Dikkie had heard the police say that before.",

                "He finished his sardine, stretched once, and walked out " +
                "into the evening like a rumor with whiskers.\n\n" +
                "Somewhere up the coast, a bird named Rudebeak was not " +
                "where a bird named Rudebeak should be.\n\n" +
                "The cat took the case. Nobody asked him to. Nobody ever does.",
            },
        };

        // ── End scenes (one beat per screen) ──────────────────────────────────
        private static readonly Dictionary<string, EndSceneBeat[]> _endScenes = new()
        {
            ["malibu_mansion"] = new[]
            {
                new EndSceneBeat("D. MARSH, THE SOUND GUY",
                    "Forty-seven takes. You ever listen to forty-seven takes of " +
                    "one word while a bird screams like a fire alarm through " +
                    "every single one?\n\n" +
                    "The cases were soundproof. He liked it in there. It was " +
                    "quiet. For the first time all shoot, it was QUIET.\n\n" +
                    "... I was going to bring him back. Probably."),

                new EndSceneBeat("OFFICER REYES, TO THE CAMERAS",
                    "Solid police work, plain and simple. We followed the " +
                    "evidence - the manifest, the speaker, the cable. Textbook. " +
                    "The department thanks the department.\n\n" +
                    "No further questions."),

                new EndSceneBeat("",
                    "On the way to the squad car, Reyes stopped by the garden " +
                    "wall. Looked left. Looked right. Then looked down.\n\n" +
                    "'You're all right, cat. You're all right.'\n\n" +
                    "Dikkie blinked, slowly. From a cat, that is a medal."),

                new EndSceneBeat("RUDEBEAK",
                    "Rudebeak came home the next morning, salt-ruffled and " +
                    "unrepentant. He has opinions about everything that " +
                    "happened.\n\n" +
                    "'FORTY-SEVEN TAKES! BRAWK!'",
                    ShowRudebeak: true),

                new EndSceneBeat("",
                    "CASE CLOSED\n\nThe Missing Macaw\n\nDikkie will return.",
                    IsCard: true),
            },

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
