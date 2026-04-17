using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CatDetective
{
    public static class DebugHelper
    {
        public static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 p1, Vector2 p2, Color color, int thickness = 2)
        {
            float length = Vector2.Distance(p1, p2);
            float angle = (float)Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
            spriteBatch.Draw(pixel, p1, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        public static void DrawHollowRect(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness = 2)
        {
            DrawLine(spriteBatch, pixel, new Vector2(rect.Left,  rect.Top),    new Vector2(rect.Right, rect.Top),    color, thickness);
            DrawLine(spriteBatch, pixel, new Vector2(rect.Right, rect.Top),    new Vector2(rect.Right, rect.Bottom), color, thickness);
            DrawLine(spriteBatch, pixel, new Vector2(rect.Right, rect.Bottom), new Vector2(rect.Left,  rect.Bottom), color, thickness);
            DrawLine(spriteBatch, pixel, new Vector2(rect.Left,  rect.Bottom), new Vector2(rect.Left,  rect.Top),   color, thickness);
        }
    }
}
