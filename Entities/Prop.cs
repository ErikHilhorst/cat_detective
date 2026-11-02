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

        /// <summary>Depth the prop sorts at while opaque (LayerDepth jumps to 1 while fading).</summary>
        public float SortDepth => CalculateLayerDepth(_sortY);

        /// <summary>True while the prop is (fading) see-through because the cat is behind it.</summary>
        public bool IsFaded => TargetAlpha < 1.0f || CurrentAlpha < 0.99f;

        // ── Opacity mask (tap hit-testing) ─────────────────────────────────────
        // Quarter-resolution "is this pixel opaque" map of the prop art, built on
        // the GPU (draw into a render target, read the RT back) because WebGL
        // cannot GetData() a content texture. Null = build failed: the prop then
        // never blocks a tap (degrades to "everything tappable").
        private const int MASK_FACTOR = 4;
        private bool[]? _opaque;
        private int     _maskW, _maskH;

        public void BuildOpacityMask(SpriteBatch spriteBatch)
        {
            if (Texture == null || _opaque != null) return;
            var gd = spriteBatch.GraphicsDevice;
            try
            {
                _maskW = System.Math.Max(1, Texture.Width  / MASK_FACTOR);
                _maskH = System.Math.Max(1, Texture.Height / MASK_FACTOR);
                using var rt = new RenderTarget2D(gd, _maskW, _maskH);
                gd.SetRenderTarget(rt);
                gd.Clear(Color.Transparent);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
                spriteBatch.Draw(Texture, new Rectangle(0, 0, _maskW, _maskH), Color.White);
                spriteBatch.End();
                gd.SetRenderTarget(null);

                var px = new Color[_maskW * _maskH];
                rt.GetData(px);
                _opaque = new bool[px.Length];
                for (int i = 0; i < px.Length; i++)
                    _opaque[i] = px[i].A >= 128;
            }
            catch (System.Exception ex)
            {
                gd.SetRenderTarget(null);
                _opaque = null;
                System.Console.WriteLine($"[Prop] opacity mask unavailable: {ex.Message}");
            }
        }

        /// <summary>True when the (opaque, un-faded) prop art covers this screen point.</summary>
        public bool CoversPoint(Vector2 p)
        {
            if (_opaque == null || IsFaded) return false;
            int x = (int)(p.X / MASK_FACTOR), y = (int)(p.Y / MASK_FACTOR);   // drawn at (0,0), scale 1
            if (x < 0 || y < 0 || x >= _maskW || y >= _maskH) return false;
            return _opaque[y * _maskW + x];
        }

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
