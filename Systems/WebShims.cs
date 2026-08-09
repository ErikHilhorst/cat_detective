#if BLAZORGL
using System;
using Microsoft.JSInterop;
using Microsoft.Xna.Framework;

namespace CatDetective.Systems
{
    /// <summary>
    /// localStorage-backed persistence for the web build. Initialized once from
    /// the Blazor host page before the game is constructed; SaveSystem routes
    /// its bodies here under BLAZORGL.
    /// </summary>
    public static class BrowserStorage
    {
        private static IJSInProcessRuntime? _js;

        public static void Initialize(IJSRuntime runtime) =>
            _js = runtime as IJSInProcessRuntime;

        public static string? GetItem(string key) =>
            _js?.Invoke<string?>("localStorage.getItem", key);

        public static void SetItem(string key, string value) =>
            _js?.InvokeVoid("localStorage.setItem", key, value);

        public static void RemoveItem(string key) =>
            _js?.InvokeVoid("localStorage.removeItem", key);
    }

    /// <summary>
    /// Music via HTMLAudioElement (window.gameAudio* helpers in index.html).
    /// Bypasses the XNA Song pipeline entirely - the browser streams the raw
    /// mp3 from wwwroot and handles looping natively. Playback must not start
    /// before the first user gesture (browser autoplay policy).
    /// </summary>
    public static class BrowserAudio
    {
        private static IJSInProcessRuntime? _js;

        public static void Initialize(IJSRuntime runtime) =>
            _js = runtime as IJSInProcessRuntime;

        public static void Play(string id, string url, bool loop, float volume) =>
            _js?.InvokeVoid("gameAudioPlay", id, url, loop, volume);

        public static void Stop(string id) =>
            _js?.InvokeVoid("gameAudioStop", id);

        public static void Volume(string id, float volume) =>
            _js?.InvokeVoid("gameAudioVolume", id, volume);
    }

    /// <summary>
    /// In the browser the backbuffer is the canvas (window-sized), while the
    /// game renders into a room-sized render target. This maps between the two:
    /// the RT is letterbox-blitted into the canvas, and raw mouse coordinates
    /// are inverse-transformed back into game space.
    /// </summary>
    public static class WasmScale
    {
        // Snapshotted at the top of Draw, before the RT is bound.
        public static int WindowWidth;
        public static int WindowHeight;

        // Virtual canvas size; follows SetCanvas (rooms differ per background).
        public static int GameW = 1456;
        public static int GameH = 816;

        private static float Scale =>
            Math.Min(WindowWidth / (float)GameW, WindowHeight / (float)GameH);

        public static Rectangle LetterboxRect()
        {
            if (WindowWidth <= 0 || WindowHeight <= 0)
                return new Rectangle(0, 0, GameW, GameH);
            float scale = Scale;
            int w = (int)(GameW * scale);
            int h = (int)(GameH * scale);
            return new Rectangle((WindowWidth - w) / 2, (WindowHeight - h) / 2, w, h);
        }

        public static Point ToGame(int rawX, int rawY)
        {
            if (WindowWidth <= 0 || WindowHeight <= 0)
                return new Point(rawX, rawY);
            float scale = Scale;
            float ox = (WindowWidth  - GameW * scale) / 2f;
            float oy = (WindowHeight - GameH * scale) / 2f;
            return new Point((int)((rawX - ox) / scale), (int)((rawY - oy) / scale));
        }
    }
}
#endif
