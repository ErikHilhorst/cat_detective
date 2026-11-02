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

        /// <summary>
        /// Screen rect the sprite covers (same placement math as Draw, without
        /// the highlight lift) - the tap target. Falls back to the trigger zone
        /// for sprite-less entries.
        /// </summary>
        public Rectangle SpriteBounds
        {
            get
            {
                if (Texture == null) return TriggerZone;
                float   scale  = Data?.Scale ?? 1.0f;
                Vector2 origin = Data == null ? new Vector2(Texture.Width * 0.5f, Texture.Height)
                                              : CalcOrigin(Data.Align, Texture.Width, Texture.Height);
                Vector2 pos    = Position;
                if (Data != null) pos += new Vector2(Data.OffsetX, Data.OffsetY);
                Vector2 topLeft = pos - origin * scale;
                return new Rectangle((int)topLeft.X, (int)topLeft.Y,
                    (int)(Texture.Width * scale), (int)(Texture.Height * scale));
            }
        }

        // ── GameObject override (non-highlighted) ──────────────────────────────
        public override void Draw(SpriteBatch spriteBatch)
            => Draw(spriteBatch, isHighlighted: false, totalSeconds: 0.0);

        /// <param name="isHighlighted">True when the cat overlaps this entity's TriggerZone.</param>
        /// <param name="isSelected">
        /// Tap-selected (first tap of tap-to-act): the outline turns gold and
        /// pulses, so it is obvious a second tap will act on it.
        /// </param>
        public void Draw(SpriteBatch spriteBatch, bool isHighlighted, double totalSeconds,
                         bool isSelected = false)
        {
            if (Texture == null) return;
            isHighlighted |= isSelected;

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
                // White silhouette * tint: gold pulse when tap-selected (same cue
                // language as the SOLVE button / active doorway arrow).
                Color outline = Color.White;
                int   w       = 2;
                if (isSelected)
                {
                    float pulse = 0.5f + 0.5f * (float)Math.Sin(totalSeconds * 6.0);
                    outline = Color.Lerp(new Color(255, 215, 120), new Color(255, 180, 40), pulse);
                    w       = 3;
                }
                Vector2[] offsets = isSelected
                    ? [new(-w, 0), new(w, 0), new(0, -w), new(0, w),
                       new(-w, -w), new(w, -w), new(-w, w), new(w, w)]
                    : [new(-w, 0), new(w, 0), new(0, -w), new(0, w)];
                foreach (var off in offsets)
                {
                    spriteBatch.Draw(
                        silhouette,
                        position:        drawPos + off,
                        sourceRectangle: null,
                        color:           outline,
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
