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

        // ── Visual overrides (applied in InteractableEntity.Draw, never to Position/LayerDepth) ──
        public float  Scale   { get; }   // default 1.0
        public string Align   { get; }   // "BottomCenter" | "Center" | "TopLeft"
        public int    OffsetX { get; }
        public int    OffsetY { get; }

        public InteractionData(string text, Keyword[] keywords,
            float scale = 1.0f, string align = "BottomCenter",
            int offsetX = 0, int offsetY = 0)
        {
            Text     = text;
            Keywords = keywords;
            Scale    = scale;
            Align    = align;
            OffsetX  = offsetX;
            OffsetY  = offsetY;
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
