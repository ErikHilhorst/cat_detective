using Microsoft.Xna.Framework;

namespace CatDetective.Entities
{
    /// <summary>
    /// A single bracketed keyword inside a dialogue string.
    /// <para>
    /// <c>DisplayText</c> must match the text inside the <c>[brackets]</c> in
    /// <see cref="InteractionData.Text"/> exactly (case-insensitive).
    /// </para>
    /// </summary>
    public readonly record struct Keyword(
        string DisplayText,
        string Id,
        Color  Color);

    /// <summary>
    /// One selectable entry on a character's interrogation menu.
    /// The prompt is a cat action ("Stare at him. Do not blink."); the text is
    /// the character's monologue in response. The detective never speaks.
    /// </summary>
    public sealed class DialogueTopic
    {
        /// <summary>Menu label: the cat action the player chooses.</summary>
        public string Prompt { get; }

        /// <summary>Response text; pages split on '|', [brackets] colored via <see cref="Keywords"/>.</summary>
        public string Text { get; }

        /// <summary>Keywords in the response; unlocked when the topic is chosen.</summary>
        public Keyword[] Keywords { get; }

        /// <summary>Clue id that must be unlocked before this topic appears. Empty = always shown.</summary>
        public string RequiresClue { get; }

        /// <summary>
        /// Room id whose local deduction board must be solved before this topic
        /// appears - the "confrontation" reward for a room solve. Empty = no solve gate.
        /// Combines with <see cref="RequiresClue"/> as AND when both are set.
        /// </summary>
        public string RequiresSolve { get; }

        public DialogueTopic(string prompt, string text, Keyword[] keywords,
            string requiresClue = "", string requiresSolve = "")
        {
            Prompt        = prompt;
            Text          = text;
            Keywords      = keywords;
            RequiresClue  = requiresClue;
            RequiresSolve = requiresSolve;
        }
    }

    /// <summary>
    /// Holds the content for a single interactable zone.
    /// Keyed by Tiled object name in <c>Game1._interactionDatabase</c>.
    /// </summary>
    public sealed class InteractionData
    {
        /// <summary>
        /// Dialogue text shown in the UI box.
        /// Tokens in [brackets] are rendered in the matching <see cref="Keyword.Color"/>;
        /// unmatched brackets fall back to white.
        /// </summary>
        public string Text { get; }

        /// <summary>Keywords in this dialogue, each with its display text, clue ID, and tint.</summary>
        public Keyword[] Keywords { get; }

        /// <summary>
        /// Interrogation topics offered after <see cref="Text"/> finishes.
        /// Empty = plain object inspection (dialogue closes after the last page).
        /// </summary>
        public DialogueTopic[] Topics { get; }

        /// <summary>
        /// Display name for characters ("Basil the Gardener") - used in the
        /// gate-unlock toast. Empty = derive from the interactable id.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Replacement display name shown once <see cref="RevealNameOnClue"/> is
        /// unlocked ("The Sound Guy" -> "D. Marsh"). Empty = no reveal.
        /// Toast names deliberately keep <see cref="DisplayName"/> (anti-leak rule).
        /// </summary>
        public string RevealName       { get; }
        public string RevealNameOnClue { get; }

        /// <summary>
        /// Alternate intro shown instead of <see cref="Text"/> once its gates are
        /// satisfied, so characters react to investigation progress. The regular
        /// <see cref="Keywords"/> still unlock and highlight - any [bracket] in the
        /// alt text must match one of them. Empty = intro never changes.
        /// Gates combine as AND, like topics.
        /// </summary>
        public string AltText              { get; }
        public string AltTextRequiresClue  { get; }
        public string AltTextRequiresSolve { get; }

        // ── Visual overrides (applied in InteractableEntity.Draw, never to Position/LayerDepth) ──
        public float  Scale   { get; }   // default 1.0
        public string Align   { get; }   // "BottomCenter" | "Center" | "TopLeft"
        public int    OffsetX { get; }
        public int    OffsetY { get; }

        /// <summary>
        /// Fallback content path (e.g. "Shared/placeholder_person") used when no
        /// per-name sprite exists under Interactables/. Empty = no fallback.
        /// </summary>
        public string TexturePath { get; }

        /// <summary>
        /// Optional dialogue-portrait crop override as texture fractions
        /// [x, y, width, height]. Null = the default head crop (x 0.20, y 0,
        /// w 0.60, h 0.32), which suits upright characters; wide or lying poses
        /// (a sleeping dog) set this so the crop lands on the face.
        /// </summary>
        public float[]? PortraitCrop { get; }

        public InteractionData(string text, Keyword[] keywords,
            float scale = 1.0f, string align = "BottomCenter",
            int offsetX = 0, int offsetY = 0, string texturePath = "",
            DialogueTopic[]? topics = null, string displayName = "",
            string revealName = "", string revealNameOnClue = "",
            string altText = "", string altTextRequiresClue = "", string altTextRequiresSolve = "",
            float[]? portraitCrop = null)
        {
            Text                 = text;
            Keywords             = keywords;
            Scale                = scale;
            Align                = align;
            OffsetX              = offsetX;
            OffsetY              = offsetY;
            TexturePath          = texturePath;
            Topics               = topics ?? System.Array.Empty<DialogueTopic>();
            DisplayName          = displayName;
            RevealName           = revealName;
            RevealNameOnClue     = revealNameOnClue;
            AltText              = altText;
            AltTextRequiresClue  = altTextRequiresClue;
            AltTextRequiresSolve = altTextRequiresSolve;
            PortraitCrop         = portraitCrop != null && portraitCrop.Length == 4
                ? portraitCrop : null;
        }

        // ── Shared colour palette ──────────────────────────────────────────────
        /// <summary>Plot / time / location clues — lavender.</summary>
        public static readonly Color Plot  = new Color(180, 120, 255);
        /// <summary>Crime / suspect clues — amber.</summary>
        public static readonly Color Crime = new Color(255, 160,  60);
        /// <summary>Miscellaneous / flavour clues — green.</summary>
        public static readonly Color Misc  = new Color( 80, 200, 100);
    }
}
