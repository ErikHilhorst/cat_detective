using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatDetective.Map
{
    // ─────────────────────────────────────────────────────────────────────────
    // JSON data models for scenes_config.json
    // ─────────────────────────────────────────────────────────────────────────

    internal sealed class SceneConfigData
    {
        [JsonPropertyName("scenes")]
        public Dictionary<string, SceneSettings> Scenes { get; set; } = new();
    }

    internal sealed class SceneSettings
    {
        [JsonPropertyName("ambientColor")]
        public string AmbientColor { get; set; } = "#000000";
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads scenes_config.json and returns per-scene settings.
    ///
    /// Usage from Game1.LoadContent():
    /// <code>
    ///   string configPath = Path.Combine(Content.RootDirectory, "scenes_config.json");
    ///   _ambientColor = SceneConfigParser.GetAmbientColor(configPath, "detective_office");
    /// </code>
    /// </summary>
    public static class SceneConfigParser
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Returns the list of scene IDs defined in scenes_config.json.
        /// Returns an empty list if the file is missing or malformed.
        /// </summary>
        public static List<string> GetAvailableScenes(string configPath)
        {
            if (!File.Exists(configPath))
                return new List<string>();

            try
            {
                var data = JsonSerializer.Deserialize<SceneConfigData>(
                    File.ReadAllText(configPath), _jsonOptions);
                return data != null ? new List<string>(data.Scenes.Keys) : new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SceneConfigParser] ERROR reading config file: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Returns the ambient background <see cref="Color"/> for the given scene ID.
        /// Falls back to <see cref="Color.Black"/> if the file or scene is missing.
        /// </summary>
        public static Color GetAmbientColor(string configPath, string sceneId)
        {
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"[SceneConfigParser] WARNING: '{configPath}' not found. " +
                                  "Defaulting to Black.");
                return Color.Black;
            }

            string json;
            try
            {
                json = File.ReadAllText(configPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SceneConfigParser] ERROR reading config file: {ex.Message}");
                return Color.Black;
            }

            var data = JsonSerializer.Deserialize<SceneConfigData>(json, _jsonOptions);
            if (data == null || !data.Scenes.TryGetValue(sceneId, out var settings))
            {
                Console.WriteLine($"[SceneConfigParser] WARNING: scene '{sceneId}' not found. " +
                                  "Defaulting to Black.");
                return Color.Black;
            }

            return ParseHexColor(settings.AmbientColor);
        }

        /// <summary>
        /// Converts a CSS-style hex string (<c>"#RRGGBB"</c> or <c>"#RRGGBBAA"</c>)
        /// into a MonoGame <see cref="Color"/>.
        /// </summary>
        private static Color ParseHexColor(string hex)
        {
            hex = hex.TrimStart('#');

            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                byte a = hex.Length >= 8 ? Convert.ToByte(hex.Substring(6, 2), 16) : (byte)255;
                return new Color(r, g, b, a);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SceneConfigParser] WARNING: could not parse color '{hex}': " +
                                  $"{ex.Message}. Defaulting to Black.");
                return Color.Black;
            }
        }
    }
}
