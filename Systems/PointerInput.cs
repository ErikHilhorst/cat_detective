using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace CatDetective.Systems
{
    /// <summary>
    /// One "pointer" for mouse and touch. Every click-driven screen (menus,
    /// board, cutscenes) and tap-to-walk read this instead of Mouse directly.
    ///
    /// Why touch needs its own path: KNI's web host calls preventDefault() on
    /// touchstart/touchend, so the browser never synthesizes mouse events from a
    /// tap - Mouse.GetState() stays silent on phones. Touch comes in through
    /// TouchPanel instead (client/CSS pixels, same space as mouse clientX/Y).
    ///
    /// Taps fire on PRESS (finger down), matching the existing mouse click
    /// semantics. A mouse press within <see cref="MOUSE_SUPPRESS_SECONDS"/> of a
    /// touch is ignored, so a touchscreen that emits both never double-taps
    /// (a double tap would select AND act on an interactable in one touch).
    /// </summary>
    public sealed class PointerInput
    {
        private const float MOUSE_SUPPRESS_SECONDS = 0.6f;

        private MouseState _prevMouse;
        private float      _sinceTouch = float.MaxValue;
        private bool       _touchDown;

        /// <summary>A tap/click began this frame.</summary>
        public bool  Pressed  { get; private set; }
        /// <summary>The pointer was lifted this frame (user-activation point for web audio).</summary>
        public bool  Released { get; private set; }
        /// <summary>Raw (window) coordinates of the last pointer event.</summary>
        public Point RawPosition { get; private set; }
        /// <summary>True when the most recent pointer/keyboard input was touch: UI shows tap hints.</summary>
        public bool  LastWasTouch { get; private set; }

        public void Update(MouseState mouse, KeyboardState kb, float dt)
        {
            Pressed  = false;
            Released = false;
            _sinceTouch += dt;

            // ── Touch ────────────────────────────────────────────────────────
            bool anyTouch = false;
            try
            {
                foreach (var t in TouchPanel.GetState())
                {
                    anyTouch = true;
                    RawPosition = new Point((int)t.Position.X, (int)t.Position.Y);
                    if (t.State == TouchLocationState.Pressed && !_touchDown)
                    {
                        Pressed    = true;
                        _touchDown = true;
                    }
                    else if (t.State == TouchLocationState.Released)
                    {
                        Released   = true;
                        _touchDown = false;
                    }
                    break;   // single-pointer game: the first finger only
                }
            }
            catch (Exception)
            {
                // Platforms without a touch device: mouse only.
            }
            if (!anyTouch) _touchDown = false;
            if (anyTouch)
            {
                _sinceTouch  = 0f;
                LastWasTouch = true;
            }

            // ── Mouse ────────────────────────────────────────────────────────
            bool mousePressed  = mouse.LeftButton == ButtonState.Pressed &&
                                 _prevMouse.LeftButton == ButtonState.Released;
            bool mouseReleased = mouse.LeftButton == ButtonState.Released &&
                                 _prevMouse.LeftButton == ButtonState.Pressed;
            bool mouseMoved    = mouse.X != _prevMouse.X || mouse.Y != _prevMouse.Y;
            _prevMouse = mouse;

            if (_sinceTouch >= MOUSE_SUPPRESS_SECONDS)
            {
                if (mousePressed || mouseReleased || mouseMoved)
                    RawPosition = new Point(mouse.X, mouse.Y);
                if (mousePressed)  { Pressed  = true; LastWasTouch = false; }
                if (mouseReleased) Released = true;
            }

            // Any key press means a keyboard player: show keyboard hints again.
            if (kb.GetPressedKeys().Length > 0)
                LastWasTouch = false;
        }
    }
}
