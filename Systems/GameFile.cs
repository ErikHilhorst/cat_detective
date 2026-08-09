using System.IO;
#if BLAZORGL
using Microsoft.Xna.Framework;
#endif

namespace CatDetective.Systems
{
    /// <summary>
    /// Runtime reads for the content JSON files (room_map / room_config /
    /// case_config / scenes_config). Desktop reads straight from disk; the web
    /// build (BLAZORGL) fetches from wwwroot via TitleContainer, which KNI
    /// implements as a synchronous XHR - so callers stay synchronous on both.
    /// </summary>
    public static class GameFile
    {
        public static bool Exists(string path)
        {
#if BLAZORGL
            try
            {
                using var stream = TitleContainer.OpenStream(Normalize(path));
                return true;
            }
            catch
            {
                return false;
            }
#else
            return File.Exists(path);
#endif
        }

        public static string ReadAllText(string path)
        {
#if BLAZORGL
            using var stream = TitleContainer.OpenStream(Normalize(path));
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
#else
            return File.ReadAllText(path);
#endif
        }

#if BLAZORGL
        // Desktop call sites build paths with Path.Combine; URLs need '/'.
        private static string Normalize(string path) => path.Replace('\\', '/');
#endif
    }
}
