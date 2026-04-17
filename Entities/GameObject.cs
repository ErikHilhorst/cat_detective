using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CatDetective.Entities
{
    /// <summary>
    /// Base class for every visible, positionable entity in the scene.
    ///
    /// PIVOT RULE (CRITICAL):
    ///   All rendering uses the BOTTOM-CENTER of the sprite as its origin/pivot.
    ///   This means <see cref="Position"/> represents the point where the entity
    ///   "touches the floor". Y-sorting then correctly sorts by the floor contact
    ///   point rather than the top-left corner of the texture.
    ///
    /// LAYER DEPTH:
    ///   MonoGame's SpriteSortMode.FrontToBack draws higher depth values in front.
    ///   We normalise Position.Y to [0..1] using the screen height so the cat
    ///   slides naturally in front of or behind props as it moves up or down.
    /// </summary>
    public abstract class GameObject
    {
        // ── Static screen height used by all instances for depth normalisation ──
        private static int _screenHeight = 720;

        /// <summary>Call once from Game1.Initialize() before any entities are created.</summary>
        public static void SetScreenHeight(int height) => _screenHeight = height;

        // ── Core properties ────────────────────────────────────────────────────
        /// <summary>
        /// World position of this entity's BOTTOM-CENTER (floor contact point).
        /// </summary>
        public Vector2 Position { get; set; }

        /// <summary>Current sprite texture (may be swapped per animation state).</summary>
        public Texture2D? Texture { get; set; }

        /// <summary>Axis-aligned bounding box used for collision detection.</summary>
        public Rectangle CollisionBox { get; protected set; }

        /// <summary>
        /// Depth in [0..1]. Higher = rendered in front when using FrontToBack sort.
        /// Updated each frame via <see cref="CalculateLayerDepth"/>.
        /// </summary>
        public float LayerDepth { get; protected set; }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// The bottom-center pivot in TEXTURE space.
        /// For a full-texture draw this is (width/2, height).
        /// Subclasses that draw from a source rectangle should compute their own.
        /// </summary>
        protected Vector2 TextureOrigin =>
            Texture != null
                ? new Vector2(Texture.Width * 0.5f, Texture.Height)
                : Vector2.Zero;

        /// <summary>
        /// Normalises a floor Y-coordinate to a [0..1] depth value.
        /// Pass an explicit <paramref name="sortY"/> when the draw position differs
        /// from the logical floor contact point (e.g. full-room overlay props that
        /// always draw at (0,0) but must sort by where the object stands in the scene).
        /// Defaults to <see cref="Position"/>.Y when not supplied.
        /// </summary>
        protected float CalculateLayerDepth(float? sortY = null) =>
            Math.Clamp((sortY ?? Position.Y) / _screenHeight, 0f, 1f);

        // ── Abstract interface ─────────────────────────────────────────────────
        public virtual void Update(GameTime gameTime) { }
        public abstract void Draw(SpriteBatch spriteBatch);
    }
}
