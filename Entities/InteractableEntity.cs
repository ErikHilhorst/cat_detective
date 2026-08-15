using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

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

        /// <summary>
        /// The rect the interaction/highlight check uses: TriggerZone grown by
        /// Data.InteractPadding on every side. Placement, Y-sort, and drawing
        /// still use TriggerZone alone.
        /// </summary>
        public Rectangle InteractZone
        {
            get
            {
                int pad = Data?.InteractPadding ?? 0;
                if (pad <= 0) return TriggerZone;
                var r = TriggerZone;
                r.Inflate(pad, pad);
                return r;
            }
        }

        public InteractableEntity(string id, Rectangle triggerZone, Texture2D? sprite, Vector2 position)
        {
            Id          = id;
            TriggerZone = triggerZone;
            Texture     = sprite;
            Position    = position;           // bottom-center floor contact point
            LayerDepth  = CalculateLayerDepth();
        }

        /// <summary>
        /// Pins the sprite in front of everything in Pass 3 (data.AlwaysOnTop):
        /// hero objects the cat must never occlude, e.g. the living room birdcage.
        /// </summary>
        public void ForceForeground() => LayerDepth = 1f;

        // ── GameObject override (non-highlighted) ──────────────────────────────
        public override void Draw(SpriteBatch spriteBatch)
            => Draw(spriteBatch, isHighlighted: false, totalSeconds: 0.0);

        /// <param name="isHighlighted">True when the cat overlaps this entity's TriggerZone.</param>
        public void Draw(SpriteBatch spriteBatch, bool isHighlighted, double totalSeconds)
        {
            if (Texture == null) return;

            float   scale   = Data?.Scale ?? 1.0f;
            Vector2 origin  = Data == null ? new Vector2(Texture.Width * 0.5f, Texture.Height)
                                           : CalcOrigin(Data.Align, Texture.Width, Texture.Height);
            Vector2 drawPos = Position;
            if (Data != null)
                drawPos += new Vector2(Data.OffsetX, Data.OffsetY);

            if (isHighlighted)
            {
                scale   *= 1.03f;
                drawPos  = new Vector2(drawPos.X, drawPos.Y - 4f);

                Texture2D silhouette = GetWhiteSilhouette(Texture);
                float outlineDepth = Math.Max(0f, LayerDepth - 0.0001f);
                Vector2[] offsets = [new(-2, 0), new(2, 0), new(0, -2), new(0, 2)];
                foreach (var off in offsets)
                {
                    spriteBatch.Draw(
                        silhouette,
                        position:        drawPos + off,
                        sourceRectangle: null,
                        color:           Color.White,
                        rotation:        0f,
                        origin:          origin,
                        scale:           new Vector2(scale),
                        effects:         SpriteEffects.None,
                        layerDepth:      outlineDepth);
                }
            }

            spriteBatch.Draw(
                Texture,
                position:        drawPos,
                sourceRectangle: null,
                color:           Color.White,
                rotation:        0f,
                origin:          origin,
                scale:           new Vector2(scale),
                effects:         SpriteEffects.None,
                layerDepth:      LayerDepth);
        }

        // A tint via SpriteBatch's color parameter can only darken the sprite's own
        // pixels, so a true solid-white outline needs a white copy of the texture.
        // Built once per texture on first highlight, cached for the app lifetime.
        private static readonly Dictionary<Texture2D, Texture2D> _silhouetteCache = new();

#if BLAZORGL
        // WebGL cannot GetData() from a content texture, so the silhouette is
        // built on the GPU instead: clear a render target to alpha-0 white, then
        // copy only the texture's ALPHA channel into it (color write mask).
        // Game1.LoadRoom prewarms every interactable texture outside the draw
        // pass, since this rebinds the render target.
        private static readonly BlendState _alphaOnlyBlend = new()
        {
            ColorWriteChannels    = ColorWriteChannels.Alpha,
            AlphaSourceBlend      = Blend.One,
            AlphaDestinationBlend = Blend.Zero,
        };

        public static void PrewarmSilhouette(SpriteBatch spriteBatch, Texture2D texture)
        {
            if (_silhouetteCache.ContainsKey(texture))
                return;

            var gd = spriteBatch.GraphicsDevice;
            var rt = new RenderTarget2D(gd, texture.Width, texture.Height);
            gd.SetRenderTarget(rt);
            gd.Clear(new Color(255, 255, 255, 0));
            spriteBatch.Begin(SpriteSortMode.Deferred, _alphaOnlyBlend);
            spriteBatch.Draw(texture, Vector2.Zero, Color.White);
            spriteBatch.End();
            gd.SetRenderTarget(null);
            _silhouetteCache[texture] = rt;
        }

        private static Texture2D GetWhiteSilhouette(Texture2D texture)
        {
            // Fallback: an unprewarmed texture highlights without the white
            // outline copy rather than crashing on a WebGL readback.
            return _silhouetteCache.TryGetValue(texture, out var cached)
                ? cached : texture;
        }
#else
        private static Texture2D GetWhiteSilhouette(Texture2D texture)
        {
            if (_silhouetteCache.TryGetValue(texture, out var cached))
                return cached;

            var pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color((byte)255, (byte)255, (byte)255, pixels[i].A);

            var silhouette = new Texture2D(texture.GraphicsDevice, texture.Width, texture.Height);
            silhouette.SetData(pixels);
            _silhouetteCache[texture] = silhouette;
            return silhouette;
        }
#endif

        private static Vector2 CalcOrigin(string align, int w, int h) => align switch
        {
            "Center"       => new Vector2(w * 0.5f, h * 0.5f),
            "TopLeft"      => Vector2.Zero,
            _              => new Vector2(w * 0.5f, h),   // "BottomCenter" (default)
        };
    }
}
