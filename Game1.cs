using CatDetective.Entities;
using CatDetective.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.IO;

namespace CatDetective
{
    /// <summary>
    /// Root game class for the Cat Detective prototype.
    ///
    /// SCREEN RESOLUTION
    ///   1072 × 1136 — matches the art assets exactly so no scaling artefacts occur.
    ///
    /// RENDER PIPELINE (in order each frame):
    ///
    ///   Pass 1 — Base Background  (AlphaBlend, Deferred)
    ///     bg_base.png drawn at (0,0).
    ///
    ///   Pass 2 — Blob Shadow  (AlphaBlend, Deferred)
    ///     shadow_blob.png drawn under the cat's feet BEFORE the cat sprite,
    ///     so it composites underneath.
    ///
    ///   Pass 3 — Y-Sorted Entities  (NonPremultiplied, FrontToBack)
    ///     Cat and props drawn together, sorted by floor-Y depth.
    ///     Props are full-room overlays (same size as background) drawn at (0,0);
    ///     their LayerDepth is derived from a logical sortY, not their draw position.
    ///     Prop alpha lerps to 0.4 when the cat walks behind them.
    ///
    ///   Pass 4 — Lighting / Sunbeams  (Additive, Deferred)
    ///     mask_sunbeams.png drawn at (0,0). Additive blend washes out the scene
    ///     naturally where beams hit — no shaders needed (GDD "El Mariachi" trick).
    /// </summary>
    public class Game1 : Game
    {
        // ── Internal resolution — matches bg_base.png / prop overlay dimensions ─
        private const int   SCREEN_WIDTH   = 1072;
        private const int   SCREEN_HEIGHT  = 1136;

        // ── Display scale — shrinks the OS window while keeping proportions ────
        // Change this one constant to resize the window (e.g. 0.5 for half size).
        private const float DISPLAY_SCALE  = 0.6f;
        private static readonly int WINDOW_WIDTH  = (int)(SCREEN_WIDTH  * DISPLAY_SCALE);
        private static readonly int WINDOW_HEIGHT = (int)(SCREEN_HEIGHT * DISPLAY_SCALE);

        // ── MonoGame core ──────────────────────────────────────────────────────
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch     _spriteBatch   = null!;
        private RenderTarget2D  _renderTarget  = null!;

        // ── Scene textures ─────────────────────────────────────────────────────
        private Texture2D _bgBase       = null!;
        private Texture2D _sunbeamsMask = null!;

        // ── Entities ───────────────────────────────────────────────────────────
        private Cat  _cat     = null!;
        private Prop _desk    = null!;
        private Prop _cabinet = null!;

        // ── World data from Tiled ──────────────────────────────────────────────
        private List<Rectangle> _solidBoundaries = new();

        // ── Debug overlay ──────────────────────────────────────────────────────
        private Texture2D    _debugPixel  = null!;
        private bool         _showDebug   = true;   // F1 toggles
        private KeyboardState _prevKbState;

        // ══════════════════════════════════════════════════════════════════════
        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth  = WINDOW_WIDTH,
                PreferredBackBufferHeight = WINDOW_HEIGHT,
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Initialize()
        {
            GameObject.SetScreenHeight(SCREEN_HEIGHT);
            base.Initialize();
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void LoadContent()
        {
            _spriteBatch  = new SpriteBatch(GraphicsDevice);
            _renderTarget = new RenderTarget2D(GraphicsDevice, SCREEN_WIDTH, SCREEN_HEIGHT);

            // ── Scene textures ───────────────────────────────────────────────
            _bgBase       = Content.Load<Texture2D>("bg_base");
            _sunbeamsMask = Content.Load<Texture2D>("mask_sunbeams");

            // ── Cat ──────────────────────────────────────────────────────────
            // Sprite sheets: 447 × 354 px, 3 frames of 149 × 354 each.
            var walkDown = Content.Load<Texture2D>("spr_cat_walk_down");
            var walkUp   = Content.Load<Texture2D>("spr_cat_walk_up");
            var shadow   = Content.Load<Texture2D>("shadow_blob");

            // Start position: centre-X, lower floor area.
            // Adjust Y once the room art is reviewed in-engine.
            _cat = new Cat(walkDown, walkUp, startPosition: new Vector2(536, 900), frameCount: 1)
            {
                ShadowTexture = shadow
            };

            // ── Props ────────────────────────────────────────────────────────
            // Full-room overlay PNGs: same 1072 × 1136 canvas as bg_base.
            // sortY is the approximate Y of each prop's floor contact point within
            // the image. Tune these values after reviewing the art in-engine.
            var deskTex    = Content.Load<Texture2D>("prop_desk");
            var cabinetTex = Content.Load<Texture2D>("prop_cabinet");

            // ── Debug pixel ─────────────────────────────────────────────────
            _debugPixel = new Texture2D(GraphicsDevice, 1, 1);
            _debugPixel.SetData(new[] { Color.White });

            // ── Parse Tiled map ──────────────────────────────────────────────
            string mapPath = Path.Combine(Content.RootDirectory, "room_map.json");
            MapParser.Parse(mapPath, out _solidBoundaries, out var triggers);

            // ── Wire trigger zones to props ───────────────────────────────────
            var deskTrigger    = new Rectangle(230, 490, 280, 210); // fallback
            var cabinetTrigger = new Rectangle(590, 340, 220, 200); // fallback

            foreach (var (name, rect) in triggers)
            {
                var lower = name.ToLowerInvariant();
                if (lower.Contains("desk"))    deskTrigger    = rect;
                if (lower.Contains("cabinet")) cabinetTrigger = rect;
            }

            // sortY values: approximate Y of each prop's floor contact in the
            // full-room overlay image. Tune after reviewing in-engine.
            _desk    = new Prop(deskTex,    sortY: 750f, deskTrigger);
            _cabinet = new Prop(cabinetTex, sortY: 580f, cabinetTrigger);
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Update(GameTime gameTime)
        {
            var kbState = Keyboard.GetState();
            if (kbState.IsKeyDown(Keys.Escape))
                Exit();

            // Toggle debug overlay with F1 (on key-down, not held)
            if (kbState.IsKeyDown(Keys.F1) && !_prevKbState.IsKeyDown(Keys.F1))
                _showDebug = !_showDebug;
            _prevKbState = kbState;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _cat.Update(gameTime);
            _cat.MoveWithCollision(dt, _solidBoundaries);

            _desk.CheckFadeTrigger(_cat.CollisionBox);
            _cabinet.CheckFadeTrigger(_cat.CollisionBox);
            _desk.Update(gameTime);
            _cabinet.Update(gameTime);

            base.Update(gameTime);
        }

        // ══════════════════════════════════════════════════════════════════════
        protected override void Draw(GameTime gameTime)
        {
            // ── Render all passes into the full-resolution render target ─────
            GraphicsDevice.SetRenderTarget(_renderTarget);
            GraphicsDevice.Clear(Color.Black);

            // ════════════════════════════════════════════════════════════════
            // PASS 1 — BASE BACKGROUND
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _spriteBatch.Draw(_bgBase, Vector2.Zero, Color.White);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 2 — BLOB SHADOW
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _cat.DrawShadow(_spriteBatch);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 3 — Y-SORTED ENTITIES
            // NonPremultiplied required: textures have straight alpha (see .mgcb),
            // and prop alpha modulation must fade cleanly without colour darkening.
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.NonPremultiplied);
            _desk.Draw(_spriteBatch);
            _cabinet.Draw(_spriteBatch);
            _cat.Draw(_spriteBatch);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 4 — LIGHTING / SUNBEAMS  (additive — El Mariachi trick)
            // ════════════════════════════════════════════════════════════════
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
            _spriteBatch.Draw(_sunbeamsMask, Vector2.Zero, Color.White);
            _spriteBatch.End();

            // ════════════════════════════════════════════════════════════════
            // PASS 5 — DEBUG OVERLAY  (F1 to toggle)
            // AlphaBlend + Deferred; drawn last so it sits on top of everything.
            // ════════════════════════════════════════════════════════════════
            if (_showDebug)
            {
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                // Collision walls — red
                foreach (var wall in _solidBoundaries)
                    DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, wall, Color.Red);

                // Cat feet collision box — cyan
                DebugHelper.DrawHollowRect(_spriteBatch, _debugPixel, _cat.CollisionBox, Color.Cyan);

                _spriteBatch.End();
            }

            // ── Blit render target to the OS window at display scale ─────────
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            _spriteBatch.Draw(
                _renderTarget,
                new Rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT),
                Color.White);
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
