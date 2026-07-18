using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatDetective.Systems
{
    /// <summary>Single-slot save game: everything needed to resume a case.</summary>
    public sealed class SaveData
    {
        [JsonPropertyName("version")]             public int    Version { get; set; } = 1;
        [JsonPropertyName("caseId")]              public string CaseId  { get; set; } = "";
        [JsonPropertyName("roomId")]              public string RoomId  { get; set; } = "";
        [JsonPropertyName("unlockedClueIds")]     public List<string> UnlockedClueIds { get; set; } = new();
        [JsonPropertyName("roomSolvedStates")]    public Dictionary<string, bool>   RoomSolvedStates    { get; set; } = new();
        [JsonPropertyName("roomSolvedSentences")] public Dictionary<string, string> RoomSolvedSentences { get; set; } = new();
        [JsonPropertyName("visitedTopics")]       public List<string> VisitedTopics   { get; set; } = new();
        [JsonPropertyName("firedGateToasts")]     public List<string> FiredGateToasts { get; set; } = new();
        [JsonPropertyName("savedAtUtc")]          public string SavedAtUtc { get; set; } = "";
    }

    /// <summary>User preferences, persisted separately from the save game.</summary>
    public sealed class SettingsData
    {
        [JsonPropertyName("version")]     public int   Version     { get; set; } = 1;
        [JsonPropertyName("musicVolume")] public float MusicVolume { get; set; } = 0.5f;
        [JsonPropertyName("crtEnabled")]  public bool  CrtEnabled  { get; set; } = false;
    }

    /// <summary>
    /// File-based persistence for the save slot and settings, isolated here so the
    /// eventual KNI/WASM port only has to swap these bodies (e.g. localStorage).
    /// Every operation is failure-tolerant: IO errors log and fall back to
    /// defaults instead of crashing the game.
    /// </summary>
    public static class SaveSystem
    {
        private static string Dir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatDetective");

        private static string SavePath     => Path.Combine(Dir, "save.json");
        private static string SettingsPath => Path.Combine(Dir, "settings.json");

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
        };

        public static bool SaveExists()
        {
            try { return File.Exists(SavePath); }
            catch { return false; }
        }

        public static SaveData? LoadGame()
        {
            try
            {
                if (!File.Exists(SavePath)) return null;
                return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SavePath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSystem] Failed to load save: {ex.Message}");
                return null;
            }
        }

        public static bool SaveGame(SaveData data)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(SavePath, JsonSerializer.Serialize(data, _jsonOptions));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSystem] Failed to write save: {ex.Message}");
                return false;
            }
        }

        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(SavePath))
                    File.Delete(SavePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSystem] Failed to delete save: {ex.Message}");
            }
        }

        public static SettingsData LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new SettingsData();
                return JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(SettingsPath))
                       ?? new SettingsData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSystem] Failed to load settings: {ex.Message}");
                return new SettingsData();
            }
        }

        public static void SaveSettings(SettingsData settings)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, _jsonOptions));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSystem] Failed to write settings: {ex.Message}");
            }
        }
    }
}
