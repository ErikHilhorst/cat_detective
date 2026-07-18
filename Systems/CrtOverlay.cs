using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CatDetective.Systems
{
    /// <summary>
    /// Shader-free CRT look: a wrap-tiled scanline texture plus a soft radial
    /// vignette, drawn INTO the render target as the final pass (Pass 9) so it
    /// applies to every game state, scales with the canvas, and shows up in
    /// screenshot captures.
    ///
    /// Deliberately not an Effect/.fx: the project has no shader toolchain, and a
    /// custom effect would have to compile for both DesktopGL and the eventual
    /// KNI/WASM target. Two textured quads are portable everywhere.
    /// </summary>
    public sealed class CrtOverlay
    {
        private readonly Texture2D _scanTex;
        private readonly Texture2D _vignetteTex;

        public CrtOverlay(GraphicsDevice graphicsDevice)
        {
            // 1x3: two transparent rows, one dark row -> every third canvas pixel
            // row darkens when wrap-tiled at 1:1.
            _scanTex = new Texture2D(graphicsDevice, 1, 3);
            _scanTex.SetData(new[]
            {
                Color.Transparent,
                Color.Transparent,
                new Color(0, 0, 0, 70),
            });

            // Small radial vignette, stretched to the canvas at draw time.
            const int VW = 512, VH = 288;
            var pixels = new Color[VW * VH];
            for (int y = 0; y < VH; y++)
            {
                for (int x = 0; x < VW; x++)
                {
                    float nx = (x / (float)(VW - 1)) * 2f - 1f;   // -1 .. 1
                    float ny = (y / (float)(VH - 1)) * 2f - 1f;
                    float t  = Math.Clamp((float)Math.Sqrt(nx * nx + ny * ny) / 1.41421356f, 0f, 1f);
                    byte  a  = (byte)(90f * t * t);
                    pixels[y * VW + x] = new Color((byte)0, (byte)0, (byte)0, a);
                }
            }
            _vignetteTex = new Texture2D(graphicsDevice, VW, VH);
            _vignetteTex.SetData(pixels);
        }

        public void Draw(SpriteBatch spriteBatch, int width, int height)
        {
            // PointWrap + a source rect the size of the canvas tiles the 1x3
            // scanline strip across the whole frame.
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap);
            spriteBatch.Draw(_scanTex,
                new Rectangle(0, 0, width, height),
                new Rectangle(0, 0, width, height),
                Color.White);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            spriteBatch.Draw(_vignetteTex, new Rectangle(0, 0, width, height), Color.White);
            spriteBatch.End();
        }
    }
}
