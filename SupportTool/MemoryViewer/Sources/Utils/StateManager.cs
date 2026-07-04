using System;
using System.IO;
using System.Text.Json;
using MemoryViewer.Sources.Models;

namespace MemoryViewer.Sources.Utils
{
    /// <summary>Lưu và load AppState ra/từ file JSON trong %AppData%.</summary>
    public static class StateManager
    {
        private static readonly string _stateDir  = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MemoryViewer");

        private static readonly string _statePath = Path.Combine(_stateDir, "state.json");

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented = true,
            Converters    = { }
        };

        public static void Save(AppState state)
        {
            try
            {
                Directory.CreateDirectory(_stateDir);
                string json = JsonSerializer.Serialize(state, _jsonOpts);
                File.WriteAllText(_statePath, json);
            }
            catch (Exception ex)
            {
                // Non-critical – log to Debug output and continue
                System.Diagnostics.Debug.WriteLine($"[StateManager] Save failed: {ex.Message}");
            }
        }

        public static AppState Load()
        {
            try
            {
                if (!File.Exists(_statePath)) return new AppState();
                string json = File.ReadAllText(_statePath);
                return JsonSerializer.Deserialize<AppState>(json, _jsonOpts) ?? new AppState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StateManager] Load failed: {ex.Message}");
                return new AppState();
            }
        }
    }
}
