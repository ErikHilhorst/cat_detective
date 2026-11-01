using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatDetective.Systems
{
    // ══════════════════════════════════════════════════════════════════════════
    // case_script.json - the case's framing: a scripted cold open / roll call
    // (intro), the Poirot-style gathering that replaces the one-shot final board
    // (gathering), and the epilogue (ending). Runtime-read like room_config.json;
    // also read by tools/story_sim (the headless text playthrough), so keep the
    // schema in sync with tools/story_sim/sim.py.
    //
    // Beats are "comic panels" in the Ace Attorney sense: a background, an
    // optional big character stand-in (their in-world sprite), a dialogue box,
    // and cheap code-drawn punctuation (stamp words, shake, flash, news chyron,
    // cast lineup) - dynamic scenes without new art.
    // ══════════════════════════════════════════════════════════════════════════

    public sealed class ScriptBeat
    {
        /// <summary>Background id (key into backgrounds). Omitted = keep the previous beat's.</summary>
        [JsonPropertyName("bg")]      public string?  Bg      { get; set; }
        /// <summary>Cast id drawn as the big stand-in. Omitted/"" = no character.</summary>
        [JsonPropertyName("who")]     public string?  Who     { get; set; }
        /// <summary>Stand-in position: left | center | right (default center).</summary>
        [JsonPropertyName("pos")]     public string?  Pos     { get; set; }
        /// <summary>Name label. Omitted = the cast member's name (or none for narration).</summary>
        [JsonPropertyName("speaker")] public string?  Speaker { get; set; }
        [JsonPropertyName("text")]    public string   Text    { get; set; } = "";
        /// <summary>Big slam-in word ("BRAWK!", "POUNCE!") shown before the text types.</summary>
        [JsonPropertyName("stamp")]   public string?  Stamp   { get; set; }
        /// <summary>"shake", "flash" - any combination.</summary>
        [JsonPropertyName("fx")]      public List<string> Fx  { get; set; } = new();
        /// <summary>News lower-third banner text.</summary>
        [JsonPropertyName("chyron")]  public string?  Chyron  { get; set; }
        /// <summary>Cast ids drawn as a small row of faces (who = highlighted).</summary>
        [JsonPropertyName("lineup")]  public List<string>? Lineup { get; set; }
        /// <summary>Centered title card instead of a dialogue box.</summary>
        [JsonPropertyName("card")]    public bool     Card    { get; set; }
        /// <summary>Background dim 0..1 (default 0.35).</summary>
        [JsonPropertyName("dim")]     public float?   Dim     { get; set; }
    }

    public sealed class ScriptCastMember
    {
        [JsonPropertyName("name")]   public string Name   { get; set; } = "";
        [JsonPropertyName("sprite")] public string Sprite { get; set; } = "";
    }

    public sealed class ScriptBackground
    {
        /// <summary>Content paths drawn in order, each stretched to cover the canvas.</summary>
        [JsonPropertyName("layers")] public List<string> Layers { get; set; } = new();
        /// <summary>"cover" (default) or "contain" (letterboxed, e.g. the poster).</summary>
        [JsonPropertyName("fit")]    public string Fit { get; set; } = "cover";
        /// <summary>Code-drawn fallback when a layer is missing: "night" | "black".</summary>
        [JsonPropertyName("fallback")] public string Fallback { get; set; } = "black";
    }

    public sealed class GatheringStep
    {
        [JsonPropertyName("id")]       public string Id    { get; set; } = "";
        /// <summary>Board header ("The Squawk at Eight").</summary>
        [JsonPropertyName("title")]    public string Title { get; set; } = "";
        [JsonPropertyName("intro")]    public List<ScriptBeat> Intro   { get; set; } = new();
        /// <summary>Mad-libs sentence; same [slot] scheme as every other board.</summary>
        [JsonPropertyName("sentence")] public string Sentence { get; set; } = "";
        [JsonPropertyName("answers")]  public List<string> Answers { get; set; } = new();
        /// <summary>Curated word bank (Golden Idol style). Empty = every found macro clue.</summary>
        [JsonPropertyName("bank")]     public List<string> Bank    { get; set; } = new();
        /// <summary>Suspect's taunt on a wrong submit; prefixed to the "N/M fit" count.</summary>
        [JsonPropertyName("wrongLine")] public string WrongLine { get; set; } = "";
        [JsonPropertyName("success")]  public List<ScriptBeat> Success { get; set; } = new();
    }

    public sealed class GatheringScript
    {
        [JsonPropertyName("intro")] public List<ScriptBeat>   Intro { get; set; } = new();
        [JsonPropertyName("steps")] public List<GatheringStep> Steps { get; set; } = new();
    }

    public sealed class CaseScript
    {
        [JsonPropertyName("cast")]        public Dictionary<string, ScriptCastMember> Cast        { get; set; } = new();
        [JsonPropertyName("backgrounds")] public Dictionary<string, ScriptBackground> Backgrounds { get; set; } = new();
        [JsonPropertyName("intro")]       public List<ScriptBeat> Intro  { get; set; } = new();
        [JsonPropertyName("gathering")]   public GatheringScript? Gathering { get; set; }
        [JsonPropertyName("ending")]      public List<ScriptBeat> Ending { get; set; } = new();

        /// <summary>Every answer + bank clue id across all gathering steps.</summary>
        public IEnumerable<string> GatheringClueIds()
        {
            if (Gathering == null) yield break;
            foreach (var s in Gathering.Steps)
            {
                foreach (var id in s.Answers) yield return id;
                foreach (var id in s.Bank)    yield return id;
            }
        }
    }

    public static class CaseScriptParser
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>
        /// Loads Levels/&lt;caseId&gt;/case_script.json, or null when the case has
        /// none (or it fails to parse - logged, never fatal). Desktop prefers the
        /// source file so script edits apply without a rebuild.
        /// </summary>
        public static CaseScript? Load(string contentRoot, string caseId)
        {
            string path = Path.Combine(contentRoot, "Levels", caseId, "case_script.json");
#if !BLAZORGL
            string source = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "Content", "Levels", caseId, "case_script.json"));
            if (File.Exists(source)) path = source;
#endif
            if (!GameFile.Exists(path)) return null;
            try
            {
                return JsonSerializer.Deserialize<CaseScript>(GameFile.ReadAllText(path), _jsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CaseScript] {caseId}: failed to parse - {ex.Message}");
                return null;
            }
        }
    }
}
