using CatDetective.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
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
    ///   • <see cref="InteractableEntity"/> instances from the layer named
    ///     <c>Interactables</c>, auto-loading their sprites from the content
    ///     pipeline and looking up dialogue data from <paramref name="levelData"/>.
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
        /// <param name="content">
        ///   The game's <see cref="ContentManager"/>; used to load interactable sprites.
        /// </param>
        /// <param name="levelId">
        ///   The level folder name (e.g. "detective_office"). Sprites are loaded from
        ///   <c>Levels/{levelId}/Interactables/{objectName}</c>.
        /// </param>
        /// <param name="levelData">
        ///   Map from Tiled object name → <see cref="InteractionData"/>.
        ///   Each matched interactable has its <c>Data</c> property set; unmatched
        ///   objects log a console warning so missing entries are easy to spot.
        /// </param>
        /// <param name="solidBoundaries">
        ///   Output: all rectangles from the "Collisions" layer.
        /// </param>
        /// <param name="triggers">
        ///   Output: all (name, rect) pairs from the "Triggers" layer.
        /// </param>
        /// <param name="interactables">
        ///   Output: fully-populated <see cref="InteractableEntity"/> instances from
        ///   the "Interactables" layer.
        /// </param>
        public static void Parse(
            string jsonPath,
            ContentManager content,
            string levelId,
            Dictionary<string, InteractionData> levelData,
            out List<Rectangle> solidBoundaries,
            out List<(string Name, Rectangle Rect)> triggers,
            out List<InteractableEntity> interactables,
            out Vector2? spawnPoint)
        {
            solidBoundaries = new List<Rectangle>();
            triggers        = new List<(string, Rectangle)>();
            interactables   = new List<InteractableEntity>();
            spawnPoint      = null;

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
                if (layer.Type != "objectgroup") continue;

                foreach (var obj in layer.Objects)
                {
                    if (spawnPoint == null &&
                        obj.Name.Equals("spawn", StringComparison.OrdinalIgnoreCase))
                        spawnPoint = new Vector2(obj.X, obj.Y);
                }

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
                        {
                            Texture2D? sprite    = null;
                            string     assetPath = $"Levels/{levelId}/Interactables/{obj.Name}";
                            try
                            {
                                sprite = content.Load<Texture2D>(assetPath);
                            }
                            catch (ContentLoadException)
                            {
                                Console.WriteLine(
                                    $"[MapParser] No sprite for '{obj.Name}' at '{assetPath}' — " +
                                    "entity will be invisible (trigger zone only).");
                            }

                            // Position = bottom-center floor contact point of the Tiled rect.
                            var position = new Vector2(
                                obj.X + obj.Width  * 0.5f,
                                obj.Y + obj.Height);

                            var entity = new InteractableEntity(obj.Name, ToRect(obj), sprite, position);

                            if (levelData.TryGetValue(obj.Name, out var data))
                                entity.Data = data;
                            else
                                Console.WriteLine(
                                    $"[MapParser] WARNING: No dialogue data for interactable '{obj.Name}'. " +
                                    "Add an entry to the level's interaction database.");

                            interactables.Add(entity);
                        }
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
