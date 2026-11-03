using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CatDetective
{
    /// <summary>
    /// The "cozy casebook" UI kit: every button, HUD card, toast and prompt is
    /// drawn from these helpers so the game has ONE visual language - the one the
    /// painted art already speaks:
    ///   - BUTTONS look like the category tabs (ui_tab.png): soft rounded pastel
    ///     paper, a brown ink outline, ink lettering, a small drop shadow.
    ///   - PANELS (room card, toasts, prompts) are cut from the dialogue-box
    ///     parchment (ui_dialogue_box.png) as a nine-slice, so they stretch to any
    ///     size without distorting the border, with a paw-free centre patch.
    /// No new art files: the rounded shapes are generated into small textures at
    /// LoadContent with SetData (works on WebGL; only GetData is unavailable there),
    /// stored PREMULTIPLIED for the AlphaBlend UI passes.
    /// </summary>
    public partial class Game1
    {
        // ── Palette (sampled from the tab art; keep in the same family) ───────
        private static readonly Color UiCream  = new(246, 236, 208);
        private static readonly Color UiYellow = new(240, 214, 118);
        private static readonly Color UiGreen  = new(170, 200, 140);
        private static readonly Color UiPurple = new(200, 174, 216);
        private static readonly Color UiOrange = new(232, 156, 108);
        private static readonly Color UiRed    = new(222, 128, 110);
        private static readonly Color UiSlate  = new(214, 202, 178);   // warm "locked" paper
        private static readonly Color UiInk    = new(66, 46, 30);
        private static readonly Color UiGlow   = new(255, 206, 90);

        private const int ROUND_SIZE   = 64;
        private const int ROUND_RADIUS = 20;
        private const int SOFT_SIZE    = 96;
        private const int SOFT_INSET   = 16;

        private Texture2D _uiRound   = null!;   // filled rounded rect, baked top-light shading
        private Texture2D _uiOutline = null!;   // ink ring of the same shape
        private Texture2D _uiSoft    = null!;   // blurred rounded rect: shadows + glows
        private float     _uiTime;              // seconds, for pulses (set at the top of Draw)

        // Parchment nine-slice geometry inside ui_dialogue_box.png (art bbox 50,44 - 1358,582).
        private static readonly Rectangle ParchOuter  = new(50, 44, 1308, 538);
        private const int                 PARCH_EDGE  = 64;
        // Paw-free patch between the two paw rows; stretched as the panel centre.
        private static readonly Rectangle ParchCenter = new(140, 215, 1100, 180);

        // ── Texture generation ────────────────────────────────────────────────

        private void CreateUiTextures()
        {
            _uiRound   = MakeShape(ROUND_SIZE, 0, ROUND_RADIUS, (d, y) =>
            {
                float a     = Math.Clamp(0.5f - d, 0f, 1f);
                float shade = MathHelper.Lerp(1.0f, 0.86f, y / (float)ROUND_SIZE);   // lit from above
                return (a, shade);
            });
            _uiOutline = MakeShape(ROUND_SIZE, 0, ROUND_RADIUS, (d, _) =>
            {
                const float W = 3f;   // ring width at 1:1; scaled with the corner
                float a = Math.Clamp(0.5f - d, 0f, 1f) * Math.Clamp(0.5f + d + W, 0f, 1f);
                return (a, 1f);
            });
            _uiSoft = MakeShape(SOFT_SIZE, SOFT_INSET, ROUND_RADIUS, (d, _) =>
            {
                float t = Math.Clamp((d + 4f) / (SOFT_INSET + 4f), 0f, 1f);   // 1 inside -> 0 at edge
                float a = 1f - t * t * (3f - 2f * t);
                return (a, 1f);
            });
        }

        /// <summary>
        /// size x size texture of a rounded rect inset by <paramref name="inset"/>
        /// with corner radius r. fn(signedDistance, y) -> (alpha, grey value).
        /// </summary>
        private Texture2D MakeShape(int size, int inset, int r, Func<float, int, (float A, float V)> fn)
        {
            var px = new Color[size * size];
            float lo = inset + r, hi = size - inset - r;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px0 = x + 0.5f, py0 = y + 0.5f;
                    float qx = Math.Max(Math.Max(lo - px0, px0 - hi), 0f);
                    float qy = Math.Max(Math.Max(lo - py0, py0 - hi), 0f);
                    float d  = MathF.Sqrt(qx * qx + qy * qy) - r;   // <0 inside
                    if (qx == 0f && qy == 0f)
                        d = -Math.Min(Math.Min(px0 - inset, size - inset - px0),
                                      Math.Min(py0 - inset, size - inset - py0));
                    var (a, v) = fn(d, y);
                    byte c = (byte)(255 * v * a), al = (byte)(255 * a);   // premultiplied
                    px[y * size + x] = new Color(c, c, c, al);
                }
            var tex = new Texture2D(GraphicsDevice, size, size);
            tex.SetData(px);
            return tex;
        }

        // ── Primitives ────────────────────────────────────────────────────────

        /// <summary>Nine-slice draw: corners keep their shape, edges/centre stretch.</summary>
        private void DrawNineSlice(Texture2D tex, Rectangle src, int srcEdge, Rectangle dst,
                                   int dstEdge, Color color, Rectangle? centerSrc = null)
        {
            dstEdge = Math.Max(1, Math.Min(dstEdge, Math.Min(dst.Width, dst.Height) / 2));
            int[] sx = { src.X, src.X + srcEdge, src.Right - srcEdge, src.Right };
            int[] sy = { src.Y, src.Y + srcEdge, src.Bottom - srcEdge, src.Bottom };
            int[] dx = { dst.X, dst.X + dstEdge, dst.Right - dstEdge, dst.Right };
            int[] dy = { dst.Y, dst.Y + dstEdge, dst.Bottom - dstEdge, dst.Bottom };
            for (int j = 0; j < 3; j++)
                for (int i = 0; i < 3; i++)
                {
                    var d = new Rectangle(dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]);
                    if (d.Width <= 0 || d.Height <= 0) continue;
                    var s = (i == 1 && j == 1 && centerSrc.HasValue)
                        ? centerSrc.Value
                        : new Rectangle(sx[i], sy[j], sx[i + 1] - sx[i], sy[j + 1] - sy[j]);
                    _spriteBatch.Draw(tex, d, s, color);
                }
        }

        private int CornerFor(Rectangle r) => Math.Clamp(Math.Min(r.Width, r.Height) / 3, 8, 22);

        /// <summary>Soft blurred halo around a rect (shadow when black, glow when gold).</summary>
        private void DrawSoft(Rectangle r, int spread, Color color)
        {
            var g = r;
            g.Inflate(spread, spread);
            int edge = CornerFor(r) + spread;
            DrawNineSlice(_uiSoft, new Rectangle(0, 0, SOFT_SIZE, SOFT_SIZE),
                SOFT_INSET + ROUND_RADIUS, g, edge, color);
        }

        /// <summary>Parchment panel cut from the dialogue box art.</summary>
        private void DrawParchment(Rectangle r, float alpha = 1f, int edge = 22)
        {
            var shadow = r;
            shadow.Offset(0, 3);
            DrawSoft(shadow, 6, Color.Black * (0.30f * alpha));
            DrawNineSlice(_dialogueBoxTex, ParchOuter, PARCH_EDGE, r, edge,
                Color.White * alpha, ParchCenter);
        }

        // ── Buttons ───────────────────────────────────────────────────────────

        /// <summary>
        /// Tab-style button. <paramref name="fill"/> is a palette colour (UiCream,
        /// UiGreen...). A '\n' in the label starts a smaller sub-line (status,
        /// e.g. "Final Solve\n3/7 rooms"). <paramref name="glow"/> adds a pulsing
        /// gold halo ("ready" cue, same language as the doorway arrow);
        /// <paramref name="selected"/> a steady one (keyboard focus).
        /// </summary>
        private void DrawUiButton(Rectangle rect, Color fill, string label,
                                  bool enabled = true, bool glow = false, bool selected = false,
                                  float maxLabelScale = 0.85f)
        {
            bool hover = enabled && !_pointer.LastWasTouch && rect.Contains(_pointerGamePos);
            var  body  = rect;
            if (hover || selected) body.Offset(0, -2);   // lifts toward the finger/cursor

            Color paper = enabled ? fill : Color.Lerp(fill, UiSlate, 0.6f);
            if (hover) paper = Color.Lerp(paper, Color.White, 0.25f);

            int corner = CornerFor(body);

            if (glow)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(_uiTime * 4f);
                DrawSoft(body, 10 + (int)(6 * pulse), UiGlow * (0.45f + 0.4f * pulse));
            }
            else if (selected)
            {
                DrawSoft(body, 10, UiGlow * 0.55f);
            }

            var shadow = body;
            shadow.Offset(0, hover || selected ? 5 : 3);
            DrawSoft(shadow, 4, Color.Black * (enabled ? 0.35f : 0.18f));

            var roundSrc = new Rectangle(0, 0, ROUND_SIZE, ROUND_SIZE);
            DrawNineSlice(_uiRound,   roundSrc, ROUND_RADIUS + 2, body, corner, paper);
            DrawNineSlice(_uiOutline, roundSrc, ROUND_RADIUS + 2, body, corner,
                UiInk * (enabled ? 0.85f : 0.4f));

            // Label: first line in ink, further lines smaller (status text).
            string[] lines = label.Split('\n');
            float    lh    = _dialogueFont.LineSpacing;
            float    w0    = 1f, blockW = 0f, blockH = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                float k = i == 0 ? 1f : 0.62f;
                blockW = Math.Max(blockW, _dialogueFont.MeasureString(lines[i]).X * k);
                blockH += lh * k * (i == 0 ? 1f : 0.95f);
                if (i == 0) w0 = k;
            }
            float pad   = Math.Max(10f, corner * 0.6f);
            float scale = Math.Min(maxLabelScale, Math.Min(
                (body.Width  - pad * 2) / Math.Max(blockW, 1f),
                (body.Height - 8f)      / Math.Max(blockH, 1f)));
            Color ink = enabled ? UiInk : UiInk * 0.5f;
            float y   = body.Y + (body.Height - blockH * scale) * 0.5f;
            for (int i = 0; i < lines.Length; i++)
            {
                float k  = i == 0 ? 1f : 0.62f;
                float s  = scale * k;
                var   sz = _dialogueFont.MeasureString(lines[i]) * s;
                _spriteBatch.DrawString(_dialogueFont, lines[i],
                    new Vector2(body.X + (body.Width - sz.X) * 0.5f, y),
                    i == 0 ? ink : ink * 0.75f, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
                y += lh * s * (i == 0 ? 1f : 0.95f);
            }
        }

        /// <summary>
        /// Small rounded paper chip (word-bank clues, board slots): same shape and
        /// ink outline as the buttons, no label handling, a light shadow on paper.
        /// </summary>
        private void DrawChip(Rectangle r, Color fill, float outlineAlpha, bool lifted)
        {
            var body = r;
            if (lifted)
            {
                body.Offset(0, -2);
                DrawSoft(body, 6, UiGlow * 0.5f);
            }
            var shadow = body;
            shadow.Offset(0, lifted ? 3 : 2);
            DrawSoft(shadow, 2, Color.Black * 0.15f);
            int corner = Math.Clamp(r.Height / 3, 6, 12);
            var src    = new Rectangle(0, 0, ROUND_SIZE, ROUND_SIZE);
            DrawNineSlice(_uiRound,   src, ROUND_RADIUS + 2, body, corner, fill);
            DrawNineSlice(_uiOutline, src, ROUND_RADIUS + 2, body, corner, UiInk * outlineAlpha);
        }

        // ── HUD pieces ────────────────────────────────────────────────────────

        /// <summary>
        /// Top-left case card: room name, this room's clue progress (bar), and the
        /// case total. Parchment, ink, small - it labels the scene without covering it.
        /// </summary>
        private void DrawRoomCard(string roomName, int found, int total, int caseFound, int caseTotal)
        {
            const float nameScale  = 0.62f;
            const float lineScale  = 0.46f;
            string clueLine = $"Clues {found}/{total}";
            string caseLine = $"Case {caseFound}/{caseTotal}";

            var nameSz = _dialogueFont.MeasureString(roomName) * nameScale;
            var clueSz = _dialogueFont.MeasureString(clueLine) * lineScale;
            var caseSz = _dialogueFont.MeasureString(caseLine) * lineScale;
            const float barW = 90f, gap = 12f;

            float innerW = Math.Max(nameSz.X, clueSz.X + gap + barW + gap + caseSz.X);
            const int padX = 26, padY = 16;
            var card = new Rectangle(14, 10, (int)innerW + padX * 2,
                                     (int)(nameSz.Y + clueSz.Y) + padY * 2 - 2);
            DrawParchment(card);

            float x = card.X + padX, y = card.Y + padY - 2;
            _spriteBatch.DrawString(_dialogueFont, roomName, new Vector2(x, y), UiInk,
                0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);
            y += nameSz.Y - 2;

            _spriteBatch.DrawString(_dialogueFont, clueLine, new Vector2(x, y), UiInk * 0.8f,
                0f, Vector2.Zero, lineScale, SpriteEffects.None, 0f);

            // Progress bar: rounded pastel track, green fill (gold once complete).
            var track = new Rectangle((int)(x + clueSz.X + gap), (int)(y + clueSz.Y * 0.5f - 6), (int)barW, 12);
            var src   = new Rectangle(0, 0, ROUND_SIZE, ROUND_SIZE);
            DrawNineSlice(_uiRound, src, ROUND_RADIUS + 2, track, 6, UiSlate * 0.9f);
            if (total > 0 && found > 0)
            {
                var fillR = track;
                fillR.Width = Math.Max(12, (int)(track.Width * Math.Min(1f, found / (float)total)));
                DrawNineSlice(_uiRound, src, ROUND_RADIUS + 2, fillR, 6, found >= total ? UiYellow : UiGreen);
            }
            DrawNineSlice(_uiOutline, src, ROUND_RADIUS + 2, track, 6, UiInk * 0.55f);

            _spriteBatch.DrawString(_dialogueFont, caseLine,
                new Vector2(track.Right + gap, y), UiInk * 0.55f,
                0f, Vector2.Zero, lineScale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// A short parchment note, centred horizontally with its bottom at
        /// <paramref name="bottomY"/>: the bottom prompt ("Tap again to...") and toasts.
        /// </summary>
        private void DrawNotePill(string text, float bottomY, float alpha = 1f, float scale = 0.62f)
        {
            var sz   = _dialogueFont.MeasureString(text) * scale;
            const int padX = 28, padY = 12;
            var r = new Rectangle((int)((SCREEN_WIDTH - sz.X) * 0.5f) - padX,
                                  (int)(bottomY - sz.Y) - padY * 2,
                                  (int)sz.X + padX * 2, (int)sz.Y + padY * 2);
            DrawParchment(r, alpha, edge: 16);
            _spriteBatch.DrawString(_dialogueFont, text,
                new Vector2(r.X + padX, r.Y + padY + 1), UiInk * alpha,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
