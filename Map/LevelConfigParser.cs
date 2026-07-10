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

    internal sealed class CaseConfigData
    {
        [JsonPropertyName("clues")]
        public List<ClueConfigData> Clues { get; set; } = new();

        [JsonPropertyName("finalSolveSentence")]
        public string DeductionSentence { get; set; } = "";

        /// <summary>Answer clue ids for the final sentence's slots, in slot order.</summary>
        [JsonPropertyName("finalSolveClueIds")]
        public List<string> FinalSolveClueIds { get; set; } = new();

        /// <summary>All room ids in the case, in display order. Empty fails safe (final solve stays locked).</summary>
        [JsonPropertyName("rooms")]
        public List<string> Rooms { get; set; } = new();
    }

    internal sealed class RoomConfigData
    {
        [JsonPropertyName("props")]
        public List<PropConfigData> Props { get; set; } = new();

        [JsonPropertyName("interactables")]
        public List<InteractableConfigData> Interactables { get; set; } = new();

        [JsonPropertyName("localDeductionSentence")]
        public string LocalDeductionSentence { get; set; } = "";

        /// <summary>Answer clue ids for the local sentence's slots, in slot order.</summary>
        [JsonPropertyName("localDeductionClueIds")]
        public List<string> LocalDeductionClueIds { get; set; } = new();
    }

    internal sealed class ClueConfigData
    {
        [JsonPropertyName("id")]                   public string Id                   { get; set; } = "";
        [JsonPropertyName("category")]             public string Category             { get; set; } = "";
        [JsonPropertyName("name")]                 public string Name                 { get; set; } = "";
        [JsonPropertyName("context")]              public string Context              { get; set; } = "";
        [JsonPropertyName("inspectorDescription")] public string InspectorDescription { get; set; } = "";
        [JsonPropertyName("roomId")]               public string RoomId               { get; set; } = "";
        [JsonPropertyName("isMacroClue")]          public bool   IsMacroClue          { get; set; } = false;
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
        /// <summary>Fallback sprite content path (e.g. "Shared/placeholder_object").</summary>
        [JsonPropertyName("texture")]  public string                  Texture  { get; set; } = "";
    }

    internal sealed class KeywordConfigData
    {
        [JsonPropertyName("displayText")] public string DisplayText { get; set; } = "";
        [JsonPropertyName("id")]          public string Id          { get; set; } = "";
        /// <summary>"plot", "crime", or "misc"</summary>
        [JsonPropertyName("color")]       public string Color       { get; set; } = "misc";
    }

    // ── Parsed results returned to Game1 ─────────────────────────────────────

    /// <summary>Case-level data: global clue database and the macro deduction sentence.</summary>
    public sealed class CaseConfig
    {
        /// <summary>Master clue database for <see cref="NotebookManager"/>.</summary>
        public Dictionary<string, Clue> Clues { get; }

        /// <summary>Sentence template for the final macro solve (e.g. "[WHO] stole the [WHAT]").</summary>
        public string DeductionSentence { get; }

        /// <summary>Answer clue ids for the final sentence's slots, in slot order.</summary>
        public IReadOnlyList<string> FinalSolveClueIds { get; }

        /// <summary>All room ids in the case, in display order.</summary>
        public IReadOnlyList<string> Rooms { get; }

        public CaseConfig(Dictionary<string, Clue> clues, string deductionSentence,
            IReadOnlyList<string> finalSolveClueIds, IReadOnlyList<string> rooms)
        {
            Clues             = clues;
            DeductionSentence = deductionSentence;
            FinalSolveClueIds = finalSolveClueIds;
            Rooms             = rooms;
        }
    }

    /// <summary>Room-level data: props, interactables, and an optional local deduction sentence.</summary>
    public sealed class RoomConfig
    {
        /// <summary>Foreground prop definitions (texture, sortY, trigger name).</summary>
        public List<PropConfigData> Props { get; }

        /// <summary>Dialogue + keyword data keyed by Tiled object name.</summary>
        public Dictionary<string, InteractionData> Interactables { get; }

        /// <summary>Optional sentence template for a room-local sub-puzzle.</summary>
        public string LocalDeductionSentence { get; }

        /// <summary>Answer clue ids for the local sentence's slots, in slot order.</summary>
        public IReadOnlyList<string> LocalDeductionClueIds { get; }

        public RoomConfig(
            List<PropConfigData>                props,
            Dictionary<string, InteractionData> interactables,
            string                              localDeductionSentence,
            IReadOnlyList<string>               localDeductionClueIds)
        {
            Props                  = props;
            Interactables          = interactables;
            LocalDeductionSentence = localDeductionSentence;
            LocalDeductionClueIds  = localDeductionClueIds;
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
        /// Loads case-level data (clues, macro deduction sentence) from <paramref name="jsonPath"/>.
        /// </summary>
        public static CaseConfig LoadCase(string jsonPath)
        {
            var raw = Deserialize<CaseConfigData>(jsonPath);

            return new CaseConfig(
                ParseClues(raw.Clues),
                raw.DeductionSentence,
                raw.FinalSolveClueIds,
                raw.Rooms);
        }

        /// <summary>
        /// Loads room-level data (props, interactables, local deduction sentence) from <paramref name="jsonPath"/>.
        /// </summary>
        public static RoomConfig LoadRoom(string jsonPath)
        {
            var raw = Deserialize<RoomConfigData>(jsonPath);

            return new RoomConfig(
                raw.Props,
                ParseInteractables(raw.Interactables),
                raw.LocalDeductionSentence,
                raw.LocalDeductionClueIds);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static T Deserialize<T>(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                throw new InvalidOperationException(
                    $"[LevelConfigParser] Config not found: '{jsonPath}'");

            return JsonSerializer.Deserialize<T>(
                File.ReadAllText(jsonPath), _jsonOptions)
                ?? throw new InvalidOperationException(
                    $"[LevelConfigParser] Failed to parse: '{jsonPath}'");
        }

        private static Dictionary<string, Clue> ParseClues(List<ClueConfigData> raw)
        {
            var dict = new Dictionary<string, Clue>(raw.Count);
            foreach (var c in raw)
                dict[c.Id] = new Clue(c.Id, ParseCategory(c.Category), c.Name, c.Context,
                    c.InspectorDescription, c.RoomId, c.IsMacroClue);
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
                    i.Scale, i.Align, i.OffsetX, i.OffsetY, i.Texture);
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
