using CatDetective.Entities;
using CatDetective.Systems;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatDetective.Map
{
    // ── JSON data models (internal — only MapParser layer sees these) ─────────

    internal sealed class LevelConfigData
    {
        [JsonPropertyName("clues")]
        public List<ClueConfigData> Clues { get; set; } = new();

        [JsonPropertyName("interactables")]
        public List<InteractableConfigData> Interactables { get; set; } = new();

        [JsonPropertyName("deductionSlots")]
        public List<DeductionSlotConfigData> DeductionSlots { get; set; } = new();
    }

    internal sealed class ClueConfigData
    {
        [JsonPropertyName("id")]          public string Id          { get; set; } = "";
        [JsonPropertyName("category")]    public string Category    { get; set; } = "";
        [JsonPropertyName("displayText")] public string DisplayText { get; set; } = "";
    }

    internal sealed class InteractableConfigData
    {
        [JsonPropertyName("id")]       public string                  Id       { get; set; } = "";
        [JsonPropertyName("text")]     public string                  Text     { get; set; } = "";
        [JsonPropertyName("keywords")] public List<KeywordConfigData> Keywords { get; set; } = new();
    }

    internal sealed class KeywordConfigData
    {
        [JsonPropertyName("displayText")] public string DisplayText { get; set; } = "";
        [JsonPropertyName("id")]          public string Id          { get; set; } = "";
        /// <summary>"plot", "crime", or "misc"</summary>
        [JsonPropertyName("color")]       public string Color       { get; set; } = "misc";
    }

    internal sealed class DeductionSlotConfigData
    {
        [JsonPropertyName("category")]      public string Category      { get; set; } = "";
        [JsonPropertyName("correctClueId")] public string CorrectClueId { get; set; } = "";
        [JsonPropertyName("x")]             public int    X             { get; set; }
        [JsonPropertyName("y")]             public int    Y             { get; set; }
        [JsonPropertyName("width")]         public int    Width         { get; set; }
        [JsonPropertyName("height")]        public int    Height        { get; set; }
    }

    // ── Parsed result returned to Game1 ──────────────────────────────────────

    /// <summary>All level data loaded from <c>level_config.json</c>.</summary>
    public sealed class LevelConfig
    {
        /// <summary>Master clue database for <see cref="NotebookManager"/>.</summary>
        public Dictionary<string, Clue> Clues { get; }

        /// <summary>Dialogue + keyword data keyed by Tiled object name.</summary>
        public Dictionary<string, InteractionData> Interactables { get; }

        /// <summary>Correct-answer slots for <see cref="DeductionManager"/>.</summary>
        public List<DeductionSlot> DeductionSlots { get; }

        public LevelConfig(
            Dictionary<string, Clue>            clues,
            Dictionary<string, InteractionData> interactables,
            List<DeductionSlot>                 deductionSlots)
        {
            Clues          = clues;
            Interactables  = interactables;
            DeductionSlots = deductionSlots;
        }
    }

    // ── Parser ────────────────────────────────────────────────────────────────

    public static class LevelConfigParser
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Loads and deserializes <paramref name="jsonPath"/> into a <see cref="LevelConfig"/>.
        /// Throws <see cref="InvalidOperationException"/> if the file is missing or malformed.
        /// </summary>
        public static LevelConfig Load(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                throw new InvalidOperationException(
                    $"[LevelConfigParser] Level config not found: '{jsonPath}'");

            var raw = JsonSerializer.Deserialize<LevelConfigData>(
                File.ReadAllText(jsonPath), _jsonOptions)
                ?? throw new InvalidOperationException(
                    $"[LevelConfigParser] Failed to parse: '{jsonPath}'");

            return new LevelConfig(
                ParseClues(raw.Clues),
                ParseInteractables(raw.Interactables),
                ParseDeductionSlots(raw.DeductionSlots));
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static Dictionary<string, Clue> ParseClues(List<ClueConfigData> raw)
        {
            var dict = new Dictionary<string, Clue>(raw.Count);
            foreach (var c in raw)
                dict[c.Id] = new Clue(c.Id, ParseCategory(c.Category), c.DisplayText);
            return dict;
        }

        private static Dictionary<string, InteractionData> ParseInteractables(
            List<InteractableConfigData> raw)
        {
            var dict = new Dictionary<string, InteractionData>(raw.Count);
            foreach (var i in raw)
            {
                var keywords = new Keyword[i.Keywords.Count];
                for (int k = 0; k < i.Keywords.Count; k++)
                {
                    var kw = i.Keywords[k];
                    keywords[k] = new Keyword(kw.DisplayText, kw.Id, ParseColor(kw.Color));
                }
                dict[i.Id] = new InteractionData(i.Text, keywords);
            }
            return dict;
        }

        private static List<DeductionSlot> ParseDeductionSlots(List<DeductionSlotConfigData> raw)
        {
            var list = new List<DeductionSlot>(raw.Count);
            foreach (var s in raw)
                list.Add(new DeductionSlot(
                    ParseCategory(s.Category),
                    s.CorrectClueId,
                    new Rectangle(s.X, s.Y, s.Width, s.Height)));
            return list;
        }

        private static ClueCategory ParseCategory(string value) => value switch
        {
            "Who"       => ClueCategory.Who,
            "What"      => ClueCategory.What,
            "Why"       => ClueCategory.Why,
            "WhereWhen" => ClueCategory.WhereWhen,
            _ => throw new InvalidOperationException($"Unknown clue category: '{value}'")
        };

        private static Microsoft.Xna.Framework.Color ParseColor(string value) => value switch
        {
            "plot"  => InteractionData.Plot,
            "crime" => InteractionData.Crime,
            "misc"  => InteractionData.Misc,
            _ => Microsoft.Xna.Framework.Color.White
        };
    }
}
