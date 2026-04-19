using CatDetective.Entities;
using CatDetective.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatDetective.Map
{
    // ── JSON data models (internal — only MapParser layer sees these) ─────────

    public sealed class PropConfigData
    {
        [JsonPropertyName("id")]          public string Id          { get; set; } = "";
        [JsonPropertyName("texture")]     public string Texture     { get; set; } = "";
        [JsonPropertyName("sortY")]       public float  SortY       { get; set; }
        [JsonPropertyName("triggerName")] public string TriggerName { get; set; } = "";
    }

    internal sealed class LevelConfigData
    {
        [JsonPropertyName("props")]
        public List<PropConfigData> Props { get; set; } = new();

        [JsonPropertyName("clues")]
        public List<ClueConfigData> Clues { get; set; } = new();

        [JsonPropertyName("interactables")]
        public List<InteractableConfigData> Interactables { get; set; } = new();

        [JsonPropertyName("deductionSentence")]
        public string DeductionSentence { get; set; } = "";
    }

    internal sealed class ClueConfigData
    {
        [JsonPropertyName("id")]       public string Id       { get; set; } = "";
        [JsonPropertyName("category")] public string Category { get; set; } = "";
        [JsonPropertyName("name")]     public string Name     { get; set; } = "";
        [JsonPropertyName("context")]  public string Context  { get; set; } = "";
    }

    internal sealed class InteractableConfigData
    {
        [JsonPropertyName("id")]       public string                  Id       { get; set; } = "";
        [JsonPropertyName("text")]     public string                  Text     { get; set; } = "";
        [JsonPropertyName("keywords")] public List<KeywordConfigData> Keywords { get; set; } = new();
        [JsonPropertyName("scale")]    public float                   Scale    { get; set; } = 1.0f;
        [JsonPropertyName("align")]    public string                  Align    { get; set; } = "BottomCenter";
        [JsonPropertyName("offsetX")]  public int                     OffsetX  { get; set; } = 0;
        [JsonPropertyName("offsetY")]  public int                     OffsetY  { get; set; } = 0;
    }

    internal sealed class KeywordConfigData
    {
        [JsonPropertyName("displayText")] public string DisplayText { get; set; } = "";
        [JsonPropertyName("id")]          public string Id          { get; set; } = "";
        /// <summary>"plot", "crime", or "misc"</summary>
        [JsonPropertyName("color")]       public string Color       { get; set; } = "misc";
    }

    // ── Parsed result returned to Game1 ──────────────────────────────────────

    /// <summary>All level data loaded from <c>level_config.json</c>.</summary>
    public sealed class LevelConfig
    {
        /// <summary>Foreground prop definitions (texture, sortY, trigger name).</summary>
        public List<PropConfigData> Props { get; }

        /// <summary>Master clue database for <see cref="NotebookManager"/>.</summary>
        public Dictionary<string, Clue> Clues { get; }

        /// <summary>Dialogue + keyword data keyed by Tiled object name.</summary>
        public Dictionary<string, InteractionData> Interactables { get; }

        /// <summary>Sentence template for <see cref="DeductionManager"/> (e.g. "[WHO] stole the [WHAT]").</summary>
        public string DeductionSentence { get; }

        public LevelConfig(
            List<PropConfigData>                props,
            Dictionary<string, Clue>            clues,
            Dictionary<string, InteractionData> interactables,
            string                              deductionSentence)
        {
            Props             = props;
            Clues             = clues;
            Interactables     = interactables;
            DeductionSentence = deductionSentence;
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
                raw.Props,
                ParseClues(raw.Clues),
                ParseInteractables(raw.Interactables),
                raw.DeductionSentence);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static Dictionary<string, Clue> ParseClues(List<ClueConfigData> raw)
        {
            var dict = new Dictionary<string, Clue>(raw.Count);
            foreach (var c in raw)
                dict[c.Id] = new Clue(c.Id, ParseCategory(c.Category), c.Name, c.Context);
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
                dict[i.Id] = new InteractionData(i.Text, keywords,
                    i.Scale, i.Align, i.OffsetX, i.OffsetY);
            }
            return dict;
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
