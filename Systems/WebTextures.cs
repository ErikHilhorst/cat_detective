#if BLAZORGL
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CatDetective.Systems
{
    /// <summary>
    /// Web texture streaming. The page's worker (wwwroot/js/gametex*.js)
    /// downloads small jpg/webp copies of every texture (tools/build_web_assets.py)
    /// and decodes them OFF the main thread; this side only copies finished
    /// RGBA into Texture2Ds. Replaces the old path where every Content.Load was a
    /// blocking XHR + JavaScript Brotli decode of raw pixels (seconds per room,
    /// with the whole page frozen - taps lost, no frames drawn).
    ///
    /// Anything not streamed (fonts, or every texture if the manifest is
    /// missing / the browser lacks OffscreenCanvas) still goes through the
    /// regular .xnb loader, so the game always works - just slower.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public static partial class WebTextures
    {
        [JSImport("globalThis.gameTex.state")]      private static partial string JsState();
        [JSImport("globalThis.gameTex.prioritize")] private static partial void   JsPrioritize(string csv);
        [JSImport("globalThis.gameTex.progress")]   private static partial string JsProgress(string csv);
        [JSImport("globalThis.gameTex.readyCount")] private static partial string JsReadyCount(string csv);
        [JSImport("globalThis.gameTex.info")]       private static partial string JsInfo(string name);
        [JSImport("globalThis.gameTex.take")]       private static partial void   JsTake(string name, [JSMarshalAs<JSType.MemoryView>] Span<byte> dest);
        [JSImport("globalThis.gameTex.nextReady")]  private static partial string JsNextReady();
        [JSImport("globalThis.gameTex.drop")]       private static partial void   JsDrop(string name);
        [JSImport("globalThis.gameLoaderHide")]     private static partial void   JsLoaderHide();

        /// <summary>"pending" (manifest still loading), "active", or "none" (use .xnb).</summary>
        public static string State
        {
            get { try { return JsState(); } catch { return "none"; } }
        }

        public static bool Active => State == "active";

        public static void Prioritize(IEnumerable<string> prefixes)
        {
            if (Active) JsPrioritize(string.Join(",", prefixes));
        }

        /// <summary>(handed to the game, total) for assets matching any prefix.</summary>
        public static (int done, int total) Progress(IEnumerable<string> prefixes) =>
            Parse(Active ? JsProgress(string.Join(",", prefixes)) : "0,0");

        /// <summary>(decoded or handed over, total) - used before the game exists.</summary>
        public static (int done, int total) ReadyCount(IEnumerable<string> prefixes) =>
            Parse(Active ? JsReadyCount(string.Join(",", prefixes)) : "0,0");

        public static void HideLoader()
        {
            try { JsLoaderHide(); } catch { }
        }

        private static (int, int) Parse(string s)
        {
            var p = s.Split(',');
            return p.Length == 2 && int.TryParse(p[0], out int d) && int.TryParse(p[1], out int t)
                ? (d, t) : (0, 0);
        }

        /// <summary>Builds a Texture2D from decoded pixels waiting for <paramref name="name"/>, or null.</summary>
        internal static Texture2D? TryTake(GraphicsDevice gd, string name)
        {
            if (!Active) return null;
            string info = JsInfo(name);
            if (info.Length == 0) return null;
            var (w, h) = Parse(info);
            var buf = new byte[w * h * 4];
            JsTake(name, buf);
            var tex = new Texture2D(gd, w, h, false, SurfaceFormat.Color) { Name = name };
            tex.SetData(buf);
            return tex;
        }

        internal static string NextReady() => Active ? JsNextReady() : "";
        internal static void Drop(string name) { if (Active) JsDrop(name); }
    }

    /// <summary>
    /// ContentManager that serves Texture2D loads from <see cref="WebTextures"/>
    /// when the worker has them, else from .xnb (and tells the worker to discard
    /// its copy). <see cref="Pump"/> drains decoded textures into the cache in
    /// small per-frame time slices so later Loads are instant.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public sealed class WebContentManager : ContentManager
    {
        private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);

        public WebContentManager(IServiceProvider services, string root) : base(services, root) { }

        private GraphicsDevice Device =>
            ((IGraphicsDeviceService)ServiceProvider.GetService(typeof(IGraphicsDeviceService))!).GraphicsDevice;

        public override T Load<T>(string assetName)
        {
            if (typeof(T) != typeof(Texture2D))
                return base.Load<T>(assetName);

            string key = assetName.Replace('\\', '/');
            if (_textures.TryGetValue(key, out var cached))
                return (T)(object)cached;

            var tex = WebTextures.TryTake(Device, key);
            if (tex == null)
            {
                if (WebTextures.Active)
                    Console.WriteLine($"[WebTextures] '{key}' not streamed yet - blocking .xnb load");
                tex = base.Load<Texture2D>(assetName);
                WebTextures.Drop(key);
            }
            _textures[key] = tex;
            return (T)(object)tex;
        }

        /// <summary>
        /// Moves decoded textures into the cache until <paramref name="budgetMs"/>
        /// is spent (at least one). Returns how many were taken.
        /// </summary>
        public int Pump(double budgetMs)
        {
            int n = 0;
            long start = Environment.TickCount64;
            while (true)
            {
                string name = WebTextures.NextReady();
                if (name.Length == 0) break;
                if (_textures.ContainsKey(name)) { WebTextures.Drop(name); continue; }
                var tex = WebTextures.TryTake(Device, name);
                if (tex == null) break;
                _textures[name] = tex;
                n++;
                if (Environment.TickCount64 - start >= budgetMs) break;
            }
            return n;
        }
    }
}
#endif
