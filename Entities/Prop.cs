using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CatDetective.Entities
{
    /// <summary>
    /// A foreground prop (desk, cabinet, etc.) that:
    ///   1. Y-sorts correctly with the cat via <see cref="LayerDepth"/>.
    ///   2. Smoothly fades to semi-transparent when the cat walks behind it.
    ///
    /// FULL-ROOM OVERLAY
    ///   Prop textures are the same pixel dimensions as the background (1072 × 1136).
    ///   The PNG contains the prop painted in the correct isometric position over a
    ///   fully transparent background, so it is always drawn at (0, 0) — no
    ///   position offset needed.
    ///
    ///   Because the draw position is always (0,0), <see cref="LayerDepth"/> is
    ///   calculated from a separate <see cref="_sortY"/> value — the approximate
    ///   Y coordinate (in screen space) of the prop's floor contact point within
    ///   the full-room image.  Tune this in Game1 to control when the cat passes
    ///   in front of or behind the prop.
    ///
    /// FADE TRIGGER ZONE
    ///   An AABB placed in the area visually "behind" the prop (lower screen Y =
    ///   further from camera in isometric view).  When the cat's feet enter this
    ///   zone, <see cref="TargetAlpha"/> drops to 0.4f.
    /// </summary>
    public class Prop : GameObject
    {
        private const float FADE_SPEED = 5f;

        private readonly float _sortY;   // logical floor Y used for depth, not draw position

        // ── Fade state ─────────────────────────────────────────────────────────
        public float CurrentAlpha { get; private set; } = 1.0f;
        public float TargetAlpha  { get; private set; } = 1.0f;

        /// <summary>
        /// AABB that triggers the fade when intersected by the player's CollisionBox.
        /// Populated from the Tiled "Triggers" layer at load time.
        /// </summary>
        public Rectangle FadeTriggerZone { get; set; }

        // ── Constructor ────────────────────────────────────────────────────────
        /// <param name="texture">Full-room overlay texture (same size as bg_base).</param>
        /// <param name="sortY">
        ///   The Y screen coordinate of this prop's floor contact point.
        ///   Used only for depth sorting — the texture always draws at (0, 0).
        ///   A good starting value: roughly the Y of the prop's bottom edge in the image.
        /// </param>
        /// <param name="fadeTriggerZone">AABB that triggers the fade when the cat enters.</param>
        public Prop(Texture2D texture, float sortY, Rectangle fadeTriggerZone)
        {
            Texture          = texture;
            _sortY           = sortY;
            FadeTriggerZone  = fadeTriggerZone;
            Position         = Vector2.Zero;      // draw position is always top-left
            LayerDepth       = CalculateLayerDepth(_sortY);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Call every frame (before Update) with the player's current CollisionBox.
        /// Sets <see cref="TargetAlpha"/> based on whether the player is inside
        /// the fade trigger zone.
        /// </summary>
        public void CheckFadeTrigger(Rectangle playerBounds)
        {
            TargetAlpha = FadeTriggerZone.Intersects(playerBounds) ? 0.4f : 1.0f;
        }

        // ── GameObject overrides ───────────────────────────────────────────────

        public override void Update(GameTime gameTime)
        {
            CurrentAlpha = MathHelper.Lerp(
                CurrentAlpha,
                TargetAlpha,
                (float)gameTime.ElapsedGameTime.TotalSeconds * FADE_SPEED);

            // If the prop is transparent or currently fading, force it to the very front (LayerDepth = 1.0f)
            // so the cat is always drawn behind it and seen through the transparency.
            if (TargetAlpha < 1.0f || CurrentAlpha < 0.99f)
            {
                LayerDepth = 1.0f;
            }
            else
            {
                // Otherwise, sort normally based on its floor contact point.
                LayerDepth = CalculateLayerDepth(_sortY);
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (Texture == null) return;

            // Full-room overlay: draw at (0,0) with no origin offset.
            // The texture's transparent regions show the background underneath;
            // only the painted prop pixels are visible.
            spriteBatch.Draw(
                Texture,
                position:        Vector2.Zero,
                sourceRectangle: null,
                color:           Color.White * CurrentAlpha,
                rotation:        0f,
                origin:          Vector2.Zero,
                scale:           Vector2.One,
                effects:         SpriteEffects.None,
                layerDepth:      LayerDepth);
        }
    }
}
