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

        [JsonPropertyName("properties")]
        public List<TiledProperty> Properties { get; set; } = new();
    }

    /// <summary>One custom property on a Tiled object.</summary>
    internal sealed class TiledProperty
    {
        [JsonPropertyName("name")]  public string      Name  { get; set; } = "";
        [JsonPropertyName("type")]  public string      Type  { get; set; } = "string";
        [JsonPropertyName("value")] public JsonElement Value { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A doorway or passage that sends the cat to another room when activated.
    /// Populated from the "Transfers" object layer in the Tiled map.
    /// </summary>
    public sealed class TransferZone
    {
        public Rectangle TriggerRect { get; }
        /// <summary>Matches a folder name under <c>Cases/{caseId}/Rooms/</c>.</summary>
        public string    TargetRoom  { get; }
        /// <summary>Name of the spawn-point object in the target room's map.</summary>
        public string    TargetSpawn { get; }

        public TransferZone(Rectangle triggerRect, string targetRoom, string targetSpawn)
        {
            TriggerRect = triggerRect;
            TargetRoom  = targetRoom;
            TargetSpawn = targetSpawn;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a Tiled JSON export and extracts:
    ///   • Solid collision rectangles from the layer named <c>Collisions</c>.
    ///   • Named trigger rectangles from the layer named <c>Triggers</c>.
    ///   • <see cref="InteractableEntity"/> instances from the layer named
    ///     <c>Interactables</c>, auto-loading their sprites from the content
    ///     pipeline and looking up dialogue data from <paramref name="levelData"/>.
    ///   • <see cref="TransferZone"/> instances from the layer named <c>Transfers</c>,
    ///     reading <c>targetRoom</c> and <c>targetSpawn</c> custom properties.
    ///   • The spawn point named <paramref name="targetSpawnName"/> from the
    ///     layer named <c>Spawn</c>.
    /// </summary>
    public static class MapParser
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <param name="jsonPath">Absolute or relative path to the Tiled JSON file.</param>
        /// <param name="content">
        ///   The game's <see cref="ContentManager"/>; used to load interactable sprites.
        /// </param>
        /// <param name="levelId">
        ///   Content path prefix (e.g. "Cases/case01/Rooms/entrance"). Sprites are loaded
        ///   from <c>{levelId}/Interactables/{objectName}</c>.
        /// </param>
        /// <param name="levelData">
        ///   Map from Tiled object name → <see cref="InteractionData"/>.
        /// </param>
        /// <param name="targetSpawnName">
        ///   Name of the spawn-point object to look up in the "Spawn" layer.
        ///   Falls back to (500, 500) if not found.
        /// </param>
        public static void Parse(
            string jsonPath,
            ContentManager content,
            string levelId,
            Dictionary<string, InteractionData> levelData,
            string targetSpawnName,
            out List<Rectangle>                      solidBoundaries,
            out List<(string Name, Rectangle Rect)>  triggers,
            out List<InteractableEntity>             interactables,
            out List<TransferZone>                   transfers,
            out Vector2?                             spawnPoint)
        {
            solidBoundaries = new List<Rectangle>();
            triggers        = new List<(string, Rectangle)>();
            interactables   = new List<InteractableEntity>();
            transfers       = new List<TransferZone>();
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

                    case "Spawn":
                        foreach (var obj in layer.Objects)
                        {
                            if (obj.Name.Equals(targetSpawnName, StringComparison.OrdinalIgnoreCase))
                            {
                                spawnPoint = new Vector2(obj.X, obj.Y);
                                break;
                            }
                        }
                        if (spawnPoint == null)
                            Console.WriteLine(
                                $"[MapParser] WARNING: Spawn '{targetSpawnName}' not found in '{jsonPath}'. " +
                                "Defaulting to (500, 500).");
                        break;

                    case "Transfers":
                        foreach (var obj in layer.Objects)
                        {
                            string targetRoom  = GetStringProperty(obj, "targetRoom");
                            string targetSpawn = GetStringProperty(obj, "targetSpawn");
                            if (string.IsNullOrEmpty(targetRoom))
                            {
                                Console.WriteLine(
                                    $"[MapParser] WARNING: Transfer '{obj.Name}' is missing 'targetRoom' property.");
                                continue;
                            }
                            transfers.Add(new TransferZone(ToRect(obj), targetRoom, targetSpawn));
                        }
                        break;

                    case "Interactables":
                        foreach (var obj in layer.Objects)
                        {
                            Texture2D? sprite    = null;
                            string     assetPath = $"{levelId}/Interactables/{obj.Name}";
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
                                    "Add an entry to the room's interaction database.");

                            interactables.Add(entity);
                        }
                        break;
                }
            }

            Console.WriteLine(
                $"[MapParser] Loaded {solidBoundaries.Count} collision(s), " +
                $"{triggers.Count} trigger(s), " +
                $"{interactables.Count} interactable(s), " +
                $"{transfers.Count} transfer(s) from '{jsonPath}'.");
        }

        private static Rectangle ToRect(TiledObject obj) =>
            new Rectangle((int)obj.X, (int)obj.Y, (int)obj.Width, (int)obj.Height);

        private static string GetStringProperty(TiledObject obj, string name)
        {
            foreach (var p in obj.Properties)
            {
                if (!p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                return p.Value.ValueKind == JsonValueKind.String
                    ? p.Value.GetString() ?? ""
                    : "";
            }
            return "";
        }
    }
}
