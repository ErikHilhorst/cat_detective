using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace CatDetective.Entities
{
    /// <summary>
    /// The player-controlled cat character.
    ///
    /// MOVEMENT
    ///   Isometric perspective means the Y-axis appears foreshortened.
    ///   We compensate by running Y movement at half the X speed so the
    ///   cat doesn't slide unnaturally fast toward or away from the camera.
    ///     X_SPEED = 100 px/s  (full horizontal speed)
    ///     Y_SPEED =  50 px/s  (half vertical speed)
    ///
    /// ANIMATION
    ///   Two sprite sheets, each with <see cref="_frameCount"/> equally-wide frames:
    ///     spr_cat_walk_down  — used when moving down / forward
    ///     spr_cat_walk_up    — used when moving up / backward
    ///   Left-facing variants are derived at runtime via SpriteEffects.FlipHorizontally;
    ///   no extra textures are needed (matches the GDD animation approach).
    ///
    /// COLLISION BOX
    ///   A thin rectangle at the cat's feet only (~14 px tall), centred on Position.
    ///   This keeps the player from clipping through walls at shoulder height and
    ///   prevents false positives on tall furniture whose trigger zones are above.
    /// </summary>
    public class Cat : GameObject
    {
        // ── Movement constants ─────────────────────────────────────────────────
        private const float X_SPEED = 150f;
        private const float Y_SPEED =  75f;

        // ── Collision box ──────────────────────────────────────────────────────
        private const int   FEET_BOX_HEIGHT = 14;   // px, covers only the paws on the floor

        // ── Sprite scale ───────────────────────────────────────────────────────
        private const float SPRITE_SCALE = 0.30f;   // placeholder art is oversized

        // ── Animation ─────────────────────────────────────────────────────────
        private readonly Texture2D _walkDownTexture;
        private readonly Texture2D _walkUpTexture;
        private readonly int       _frameCount;      // Columns in the sprite sheet

        private int   _frameWidth;
        private int   _frameHeight;
        private int   _currentFrame;
        private float _frameTimer;
        private const float FRAME_INTERVAL = 0.12f;  // seconds per frame (~8 fps walk cycle)

        // ── Per-frame state ────────────────────────────────────────────────────
        private Vector2       _velocity;             // pixels/s this frame
        private bool          _isMoving;
        private bool          _facingUp;             // true → use walk-up sheet
        private SpriteEffects _spriteFlip = SpriteEffects.None;

        // ── Shadow ─────────────────────────────────────────────────────────────
        /// <summary>Assign shadow_blob.png after construction.</summary>
        public Texture2D? ShadowTexture { get; set; }

        // ── Constructor ────────────────────────────────────────────────────────
        /// <param name="walkDownTexture">Sprite sheet for down/forward movement.</param>
        /// <param name="walkUpTexture">Sprite sheet for up/backward movement.</param>
        /// <param name="startPosition">Initial bottom-center world position.</param>
        /// <param name="frameCount">Number of animation frames in each sheet.</param>
        public Cat(
            Texture2D walkDownTexture,
            Texture2D walkUpTexture,
            Vector2   startPosition,
            int       frameCount = 4)
        {
            _walkDownTexture = walkDownTexture;
            _walkUpTexture   = walkUpTexture;
            _frameCount      = frameCount;

            Texture      = _walkDownTexture;
            _frameWidth  = Texture.Width / _frameCount;
            _frameHeight = Texture.Height;

            Position = startPosition;
            RefreshCollisionBox();
            LayerDepth = CalculateLayerDepth();
        }

        // ── Update ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Phase 1 of the per-frame update: read input, pick texture/flip,
        /// advance animation timer. Does NOT move the cat — that happens in
        /// <see cref="MoveWithCollision"/> so collision resolution can be applied
        /// cleanly on each axis separately.
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            HandleInput();
            AdvanceAnimation((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        /// <summary>
        /// Phase 2 of the per-frame update: apply <see cref="_velocity"/> to
        /// <see cref="Position"/> while resolving AABB collisions axis by axis.
        ///
        /// Separating X and Y resolution prevents the cat from getting stuck in
        /// corners — it slides smoothly along walls instead.
        /// </summary>
        public void MoveWithCollision(float deltaTime, List<Rectangle> solidBoundaries)
        {
            // ── Try X axis ────────────────────────────────────────────────────
            Position = new Vector2(Position.X + _velocity.X * deltaTime, Position.Y);
            RefreshCollisionBox();
            foreach (var wall in solidBoundaries)
            {
                if (CollisionBox.Intersects(wall))
                {
                    // Revert X and stop, but Y can still proceed below.
                    Position = new Vector2(Position.X - _velocity.X * deltaTime, Position.Y);
                    RefreshCollisionBox();
                    break;
                }
            }

            // ── Try Y axis ────────────────────────────────────────────────────
            Position = new Vector2(Position.X, Position.Y + _velocity.Y * deltaTime);
            RefreshCollisionBox();
            foreach (var wall in solidBoundaries)
            {
                if (CollisionBox.Intersects(wall))
                {
                    Position = new Vector2(Position.X, Position.Y - _velocity.Y * deltaTime);
                    RefreshCollisionBox();
                    break;
                }
            }

            // Update depth AFTER the final resolved position is known.
            LayerDepth = CalculateLayerDepth();
        }

        // ── Draw ───────────────────────────────────────────────────────────────

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (Texture == null) return;

            // Source rectangle selects the current animation frame from the sheet.
            var sourceRect = new Rectangle(
                _currentFrame * _frameWidth, 0,
                _frameWidth, _frameHeight);

            // When using a sourceRectangle, 'origin' is in SOURCE rect space,
            // so bottom-center of the FRAME, not the full texture.
            var frameOrigin = new Vector2(_frameWidth * 0.5f, _frameHeight);

            spriteBatch.Draw(
                Texture,
                Position,
                sourceRect,
                Color.White,
                rotation:   0f,
                origin:     frameOrigin,
                scale:      new Vector2(SPRITE_SCALE),
                effects:    _spriteFlip,
                layerDepth: LayerDepth);
        }

        /// <summary>
        /// Draws the blob shadow beneath the cat's feet.
        /// Must be called inside a separate SpriteBatch pass with AlphaBlend
        /// (Pass 2 in the render pipeline) so it composites under the cat.
        /// </summary>
        public void DrawShadow(SpriteBatch spriteBatch)
        {
            if (ShadowTexture == null) return;

            var shadowOrigin = new Vector2(ShadowTexture.Width * 0.5f, ShadowTexture.Height * 0.5f);
            // Offset slightly down so the oval sits visually under the paws.
            var shadowPos = Position + new Vector2(0f, -5f);

            // shadow_blob.png is 800 x 447. Scale it down to ~100 x 40 px so it
            // sits naturally under the cat's paws without dominating the scene.
            spriteBatch.Draw(
                ShadowTexture,
                shadowPos,
                sourceRectangle: null,
                Color.White * 0.55f,
                rotation: 0f,
                origin:   shadowOrigin,
                scale:    new Vector2(0.13f, 0.09f),   // ≈ 104 x 40 px at runtime
                effects:  SpriteEffects.None,
                layerDepth: 0f);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void HandleInput()
        {
            var kb = Keyboard.GetState();

            bool up    = kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up);
            bool down  = kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down);
            bool left  = kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left);
            bool right = kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right);

            _velocity = Vector2.Zero;
            if (left)  _velocity.X -= X_SPEED;
            if (right) _velocity.X += X_SPEED;
            if (up)    _velocity.Y -= Y_SPEED;
            if (down)  _velocity.Y += Y_SPEED;

            _isMoving = _velocity != Vector2.Zero;

            if (_isMoving)
            {
                // Net vertical direction decides which sprite sheet to use.
                // "up && !down" handles the case where both are held (no change).
                _facingUp = up && !down;

                // The two placeholder sheets have opposite default orientations:
                //   walkUp   → naturally faces right; flip when going left.
                //   walkDown → naturally faces left;  flip when going right.
                bool facingLeft = left && !right;
                _spriteFlip = _facingUp
                    ? (facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
                    : (facingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
            }

            // Swap texture based on current facing.
            // Re-derive frame dimensions because the two sheets may have different heights.
            Texture      = _facingUp ? _walkUpTexture : _walkDownTexture;
            _frameWidth  = Texture.Width / _frameCount;
            _frameHeight = Texture.Height;
        }

        private void AdvanceAnimation(float deltaTime)
        {
            if (!_isMoving)
            {
                // Snap to the idle frame (frame 0) when standing still.
                _currentFrame = 0;
                _frameTimer   = 0f;
                return;
            }

            _frameTimer += deltaTime;
            if (_frameTimer >= FRAME_INTERVAL)
            {
                _frameTimer  -= FRAME_INTERVAL;
                _currentFrame = (_currentFrame + 1) % _frameCount;
            }
        }

        /// <summary>
        /// Rebuilds the feet-only collision box centred on <see cref="Position"/>.
        /// Because Position is the bottom-center, the box spans:
        ///   X: [Position.X - frameWidth/2 .. Position.X + frameWidth/2]
        ///   Y: [Position.Y - FEET_BOX_HEIGHT .. Position.Y]
        /// </summary>
        private void RefreshCollisionBox()
        {
            CollisionBox = new Rectangle(
                (int)(Position.X - (_frameWidth * SPRITE_SCALE) * 0.25f),
                (int)(Position.Y - FEET_BOX_HEIGHT),
                (int)(_frameWidth * SPRITE_SCALE * 0.5f),
                FEET_BOX_HEIGHT);
        }
    }
}
