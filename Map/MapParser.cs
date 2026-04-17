using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatDetective.Map
{
    // ─────────────────────────────────────────────────────────────────────────
    // Minimal Tiled JSON data models
    //
    // We only care about "objectgroup" layers that contain simple rectangle
    // objects.  Tile layers and tilesets are intentionally ignored — the GDD
    // uses pre-rendered background PNGs, not tile-based rendering.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Root of the Tiled .json export.</summary>
    internal sealed class TiledMapData
    {
        [JsonPropertyName("layers")]
        public List<TiledLayer> Layers { get; set; } = new();
    }

    /// <summary>One layer inside the map (we only process type == "objectgroup").</summary>
    internal sealed class TiledLayer
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("objects")]
        public List<TiledObject> Objects { get; set; } = new();
    }

    /// <summary>
    /// One rectangle object inside an objectgroup layer.
    /// Tiled stores X/Y as float (supports sub-pixel placement in the editor).
    /// </summary>
    internal sealed class TiledObject
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("width")]
        public float Width { get; set; }

        [JsonPropertyName("height")]
        public float Height { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a Tiled JSON export and extracts:
    ///   • Solid collision rectangles from the layer named <c>Collisions</c>.
    ///   • Named trigger rectangles from the layer named <c>Triggers</c>.
    ///
    /// Usage from Game1.LoadContent():
    /// <code>
    ///   MapParser.Parse(
    ///       Path.Combine(Content.RootDirectory, "room_map.json"),
    ///       out solidBoundaries,
    ///       out triggers);
    /// </code>
    /// </summary>
    public static class MapParser
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Parses <paramref name="jsonPath"/> and populates the three output lists.
        /// If the file is missing the method logs a warning and returns empty lists
        /// so the game can still run without a map (useful during early prototyping).
        /// </summary>
        /// <param name="jsonPath">Absolute or relative path to the Tiled JSON file.</param>
        /// <param name="solidBoundaries">
        ///   Output: all rectangles from the "Collisions" layer.
        ///   Feed these into <c>Cat.MoveWithCollision()</c> each frame.
        /// </param>
        /// <param name="triggers">
        ///   Output: all (name, rect) pairs from the "Triggers" layer.
        ///   Match names to props ("desk_fade", "cabinet_fade") in LoadContent().
        /// </param>
        /// <param name="interactables">
        ///   Output: all (name, rect) pairs from the "Interactables" layer.
        ///   Match names to <c>_interactionDatabase</c> keys in Game1.
        /// </param>
        public static void Parse(
            string jsonPath,
            out List<Rectangle> solidBoundaries,
            out List<(string Name, Rectangle Rect)> triggers,
            out List<(string Name, Rectangle Rect)> interactables)
        {
            solidBoundaries = new List<Rectangle>();
            triggers        = new List<(string, Rectangle)>();
            interactables   = new List<(string, Rectangle)>();

            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"[MapParser] WARNING: '{jsonPath}' not found. " +
                                  "No collision or trigger data loaded.");
                return;
            }

            string json;
            try
            {
                json = File.ReadAllText(jsonPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MapParser] ERROR reading map file: {ex.Message}");
                return;
            }

            var mapData = JsonSerializer.Deserialize<TiledMapData>(json, _jsonOptions);
            if (mapData == null) return;

            foreach (var layer in mapData.Layers)
            {
                // Skip tile layers and any non-object layers
                if (layer.Type != "objectgroup") continue;

                switch (layer.Name)
                {
                    case "Collisions":
                        foreach (var obj in layer.Objects)
                            solidBoundaries.Add(ToRect(obj));
                        break;

                    case "Triggers":
                        foreach (var obj in layer.Objects)
                            triggers.Add((obj.Name, ToRect(obj)));
                        break;

                    case "Interactables":
                        foreach (var obj in layer.Objects)
                            interactables.Add((obj.Name, ToRect(obj)));
                        break;
                }
            }

            Console.WriteLine(
                $"[MapParser] Loaded {solidBoundaries.Count} collision(s), " +
                $"{triggers.Count} trigger(s), " +
                $"{interactables.Count} interactable(s) from '{jsonPath}'.");
        }

        private static Rectangle ToRect(TiledObject obj) =>
            new Rectangle((int)obj.X, (int)obj.Y, (int)obj.Width, (int)obj.Height);
    }
}
