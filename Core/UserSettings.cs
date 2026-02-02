using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AutonautsMP.Core
{
    /// <summary>
    /// User settings that persist between game sessions.
    /// Saved as JSON in the BepInEx config folder.
    /// </summary>
    public class UserSettingsData
    {
        public string LastIP = "127.0.0.1";
        public int LastPort = 7777;
        public List<string> RecentServers = new List<string>();
        public int MaxRecentServers = 5;
        
        /// <summary>
        /// Serialize to JSON string.
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"LastIP\": \"{EscapeJson(LastIP)}\",");
            sb.AppendLine($"  \"LastPort\": {LastPort},");
            sb.AppendLine($"  \"MaxRecentServers\": {MaxRecentServers},");
            sb.Append("  \"RecentServers\": [");
            
            for (int i = 0; i < RecentServers.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"\"{EscapeJson(RecentServers[i])}\"");
            }
            
            sb.AppendLine("]");
            sb.AppendLine("}");
            return sb.ToString();
        }
        
        /// <summary>
        /// Parse from JSON string.
        /// </summary>
        public static UserSettingsData FromJson(string json)
        {
            var data = new UserSettingsData();
            
            try
            {
                // Simple JSON parsing for our specific format
                data.LastIP = ExtractStringValue(json, "LastIP") ?? "127.0.0.1";
                data.LastPort = ExtractIntValue(json, "LastPort", 7777);
                data.MaxRecentServers = ExtractIntValue(json, "MaxRecentServers", 5);
                data.RecentServers = ExtractStringArray(json, "RecentServers");
            }
            catch
            {
                // Return default on any parse error
            }
            
            return data;
        }
        
        private static string EscapeJson(string s)
        {
            return s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
        }
        
        private static string ExtractStringValue(string json, string key)
        {
            string pattern = $"\"{key}\"\\s*:\\s*\"";
            int start = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            
            start += pattern.Length - 1; // Move to the opening quote
            int valueStart = start + 1;
            int valueEnd = json.IndexOf('"', valueStart);
            
            if (valueEnd < 0) return null;
            return json.Substring(valueStart, valueEnd - valueStart);
        }
        
        private static int ExtractIntValue(string json, string key, int defaultValue)
        {
            string pattern = $"\"{key}\"\\s*:\\s*";
            int start = json.IndexOf($"\"{key}\"", StringComparison.OrdinalIgnoreCase);
            if (start < 0) return defaultValue;
            
            int colonPos = json.IndexOf(':', start);
            if (colonPos < 0) return defaultValue;
            
            int valueStart = colonPos + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;
            
            int valueEnd = valueStart;
            while (valueEnd < json.Length && (char.IsDigit(json[valueEnd]) || json[valueEnd] == '-'))
                valueEnd++;
            
            if (int.TryParse(json.Substring(valueStart, valueEnd - valueStart), out int result))
                return result;
            
            return defaultValue;
        }
        
        private static List<string> ExtractStringArray(string json, string key)
        {
            var result = new List<string>();
            
            int arrayStart = json.IndexOf($"\"{key}\"", StringComparison.OrdinalIgnoreCase);
            if (arrayStart < 0) return result;
            
            int bracketStart = json.IndexOf('[', arrayStart);
            if (bracketStart < 0) return result;
            
            int bracketEnd = json.IndexOf(']', bracketStart);
            if (bracketEnd < 0) return result;
            
            string arrayContent = json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            
            // Extract quoted strings
            int pos = 0;
            while (pos < arrayContent.Length)
            {
                int quoteStart = arrayContent.IndexOf('"', pos);
                if (quoteStart < 0) break;
                
                int quoteEnd = arrayContent.IndexOf('"', quoteStart + 1);
                if (quoteEnd < 0) break;
                
                result.Add(arrayContent.Substring(quoteStart + 1, quoteEnd - quoteStart - 1));
                pos = quoteEnd + 1;
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// Manages user settings persistence.
    /// </summary>
    internal static class UserSettings
    {
        private static UserSettingsData _data = new UserSettingsData();
        private static string _settingsPath = "";
        private static bool _initialized = false;
        
        /// <summary>
        /// Initialize the settings system.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            
            try
            {
                // Settings file goes in BepInEx/config/AutonautsMP/
                string configPath = BepInEx.Paths.ConfigPath;
                DebugLogger.Info($"UserSettings: BepInEx config path = {configPath}");
                
                string configDir = Path.Combine(configPath, "AutonautsMP");
                DebugLogger.Info($"UserSettings: Target directory = {configDir}");
                
                if (!Directory.Exists(configDir))
                {
                    DebugLogger.Info($"UserSettings: Creating directory...");
                    Directory.CreateDirectory(configDir);
                }
                
                _settingsPath = Path.Combine(configDir, "user.json");
                DebugLogger.Info($"UserSettings: Settings file = {_settingsPath}");
                
                Load();
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"UserSettings: Failed to initialize: {ex}");
            }
        }
        
        /// <summary>
        /// Load settings from disk.
        /// </summary>
        private static void Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    DebugLogger.Info($"UserSettings: Loading existing file...");
                    string json = File.ReadAllText(_settingsPath);
                    _data = UserSettingsData.FromJson(json);
                    DebugLogger.Info($"UserSettings: Loaded - Last server: {_data.LastIP}:{_data.LastPort}");
                }
                else
                {
                    DebugLogger.Info($"UserSettings: File doesn't exist, creating default...");
                    _data = new UserSettingsData();
                    Save(); // Create default file
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"UserSettings: Failed to load: {ex}");
                _data = new UserSettingsData();
            }
        }
        
        /// <summary>
        /// Save settings to disk.
        /// </summary>
        public static void Save()
        {
            try
            {
                if (string.IsNullOrEmpty(_settingsPath))
                {
                    DebugLogger.Warning("UserSettings: Cannot save - path not set");
                    return;
                }
                
                string json = _data.ToJson();
                DebugLogger.Info($"UserSettings: Saving to {_settingsPath}");
                File.WriteAllText(_settingsPath, json);
                DebugLogger.Info("UserSettings: Saved successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"UserSettings: Failed to save: {ex}");
            }
        }
        
        /// <summary>
        /// Get the last used IP address.
        /// </summary>
        public static string LastIP
        {
            get
            {
                Initialize();
                return _data?.LastIP ?? "127.0.0.1";
            }
            set
            {
                Initialize();
                if (_data != null)
                {
                    _data.LastIP = value;
                }
            }
        }
        
        /// <summary>
        /// Get the last used port.
        /// </summary>
        public static int LastPort
        {
            get
            {
                Initialize();
                return _data?.LastPort ?? 7777;
            }
            set
            {
                Initialize();
                if (_data != null)
                {
                    _data.LastPort = value;
                }
            }
        }
        
        /// <summary>
        /// Get list of recent servers (IP:Port format).
        /// </summary>
        public static List<string> RecentServers
        {
            get
            {
                Initialize();
                return _data?.RecentServers ?? new List<string>();
            }
        }
        
        /// <summary>
        /// Add a server to the recent servers list.
        /// </summary>
        public static void AddRecentServer(string ip, int port)
        {
            Initialize();
            if (_data == null) return;
            
            string server = $"{ip}:{port}";
            
            // Remove if already exists (we'll add it to the top)
            _data.RecentServers.Remove(server);
            
            // Add to beginning of list
            _data.RecentServers.Insert(0, server);
            
            // Trim list to max size
            while (_data.RecentServers.Count > _data.MaxRecentServers)
            {
                _data.RecentServers.RemoveAt(_data.RecentServers.Count - 1);
            }
            
            // Update last used
            _data.LastIP = ip;
            _data.LastPort = port;
            
            Save();
        }
        
        /// <summary>
        /// Clear all recent servers.
        /// </summary>
        public static void ClearRecentServers()
        {
            Initialize();
            if (_data != null)
            {
                _data.RecentServers.Clear();
                Save();
            }
        }
    }
}
