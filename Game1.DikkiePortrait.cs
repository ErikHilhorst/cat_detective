using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CatDetective
{
    /// <summary>
    /// Dikkie in the dialogue box. Speakers get a big portrait on the left, but the
    /// player's own cat was only ever seen walking - so every conversation also
    /// shows Dikkie perched on the box's top border (right side, left of the X),
    /// facing the speaker.
    ///
    /// The pose is rolled once per conversation: the main pose most of the time,
    /// the others as an occasional variation. Art: Content/Shared/dikkie/ (sources
    /// in "art trials/dikkie", trimmed + mirrored to face left, 420 px max side).
    /// </summary>
    public partial class Game1
    {
        /// <summary>Pose table; weights are percentages (sum 100).</summary>
        private static readonly (string Asset, int Weight)[] DikkiePoses =
        {
            ("Shared/dikkie/dikkie_main",    40),   // common: the "default" Dikkie
            ("Shared/dikkie/dikkie_crouch",  10),   // uncommon
            ("Shared/dikkie/dikkie_sit_paw", 10),
            ("Shared/dikkie/dikkie_reach",   10),
            ("Shared/dikkie/dikkie_scratch", 10),
            ("Shared/dikkie/dikkie_belly",   10),
            ("Shared/dikkie/dikkie_stretch", 10),
        };

        private const float DIKKIE_HEIGHT    = 165f;  // drawn height (canvas px)
        private const float DIKKIE_MAX_WIDTH = 240f;  // caps the wide poses (belly, stretch)

        private readonly Texture2D?[] _dikkieTex = new Texture2D?[DikkiePoses.Length];
        private readonly Random _dikkieRng = new();
        private int   _dikkiePose    = -1;
        private float _dikkieShownAt = -1f;   // game seconds; -1 = start on next draw

        private void LoadDikkiePortraits()
        {
            for (int i = 0; i < DikkiePoses.Length; i++)
            {
                try { _dikkieTex[i] = Content.Load<Texture2D>(DikkiePoses[i].Asset); }
                catch { _dikkieTex[i] = null; }
            }
        }

        /// <summary>Rolls Dikkie's pose for a new conversation.</summary>
        private void RollDikkiePose()
        {
            _dikkieShownAt = -1f;
            int roll = _dikkieRng.Next(100);
            for (int i = 0; i < DikkiePoses.Length; i++)
            {
                roll -= DikkiePoses[i].Weight;
                if (roll < 0) { _dikkiePose = i; return; }
            }
            _dikkiePose = 0;
        }

        /// <summary>Draws Dikkie on the open dialogue box (after Pass 6, own batch).</summary>
        private void DrawDikkiePortrait(Rectangle box, float now)
        {
            if (_dikkiePose < 0) RollDikkiePose();
            var tex = _dikkieTex[_dikkiePose];
            if (tex == null) return;
            if (_dikkieShownAt < 0f) _dikkieShownAt = now;

            float t = now - _dikkieShownAt;
            // Pop-in (0.3 s ease-out-back) + a slow breathing squash.
            float k       = MathHelper.Clamp(t / 0.3f, 0f, 1f);
            float pop     = 1f + 2.2f * MathF.Pow(k - 1f, 3) + 1.2f * MathF.Pow(k - 1f, 2);
            float breathe = 0.012f * MathF.Sin(t * 2.1f);

            float s     = Math.Min(DIKKIE_HEIGHT / tex.Height, DIKKIE_MAX_WIDTH / tex.Width);
            float feetX = DialogueCloseRect.X - 24f - tex.Width * s * 0.5f;
            float feetY = box.Y + 44f;   // sits on the box art's top border

            var scale = new Vector2(s * (1f + breathe * 0.4f), s * (1f + breathe)) * pop;
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            _spriteBatch.Draw(tex, new Vector2(feetX, feetY), null, Color.White, 0f,
                new Vector2(tex.Width * 0.5f, tex.Height), scale, SpriteEffects.None, 0f);
            _spriteBatch.End();
        }
    }
}
