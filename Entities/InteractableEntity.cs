using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CatDetective.Entities
{
    /// <summary>
    /// A named, Y-sorted interactable object derived from a Tiled "Interactables" object.
    /// Position is the bottom-center floor contact point derived from the Tiled rect.
    /// </summary>
    public class InteractableEntity : GameObject
    {
        public string Id { get; }

        /// <summary>Raw Tiled rect used for overlap/collision detection.</summary>
        public Rectangle TriggerZone { get; }

        /// <summary>Dialogue and keyword data; null if this object has no dialogue entry.</summary>
        public InteractionData? Data { get; set; }

        public InteractableEntity(string id, Rectangle triggerZone, Texture2D? sprite, Vector2 position)
        {
            Id          = id;
            TriggerZone = triggerZone;
            Texture     = sprite;
            Position    = position;           // bottom-center floor contact point
            LayerDepth  = CalculateLayerDepth();
        }

        // ── GameObject override (non-highlighted) ──────────────────────────────
        public override void Draw(SpriteBatch spriteBatch)
            => Draw(spriteBatch, isHighlighted: false, totalSeconds: 0.0);

        /// <param name="isHighlighted">True when the cat overlaps this entity's TriggerZone.</param>
        /// <param name="totalSeconds">gameTime.TotalGameTime.TotalSeconds — drives the pulse.</param>
        public void Draw(SpriteBatch spriteBatch, bool isHighlighted, double totalSeconds)
        {
            if (Texture == null) return;

            var tint = Color.White;
            if (isHighlighted)
            {
                float pulse = (float)(Math.Sin(totalSeconds * 5.0) * 0.5 + 0.5);
                tint = Color.Lerp(Color.White, new Color(200, 255, 200), pulse * 0.4f);
            }

            float    scale    = Data?.Scale ?? 1.0f;
            Vector2  origin   = Data == null ? new Vector2(Texture.Width * 0.5f, Texture.Height)
                                             : CalcOrigin(Data.Align, Texture.Width, Texture.Height);
            Vector2  drawPos  = Position;
            if (Data != null)
                drawPos += new Vector2(Data.OffsetX, Data.OffsetY);

            spriteBatch.Draw(
                Texture,
                position:        drawPos,
                sourceRectangle: null,
                color:           tint,
                rotation:        0f,
                origin:          origin,
                scale:           new Vector2(scale),
                effects:         SpriteEffects.None,
                layerDepth:      LayerDepth);
        }

        private static Vector2 CalcOrigin(string align, int w, int h) => align switch
        {
            "Center"       => new Vector2(w * 0.5f, h * 0.5f),
            "TopLeft"      => Vector2.Zero,
            _              => new Vector2(w * 0.5f, h),   // "BottomCenter" (default)
        };
    }
}
