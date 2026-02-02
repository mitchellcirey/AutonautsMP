using System;
using System.Reflection;
using AutonautsMP.Core;

namespace AutonautsMP.Network
{
    /// <summary>
    /// Helper class to interact with Steamworks API.
    /// Uses reflection to access Steam if available (game already loads Steamworks).
    /// Supports multiple Steamworks implementations: Steamworks.NET, Facepunch.Steamworks, etc.
    /// </summary>
    internal static class SteamHelper
    {
        private static bool _initialized = false;
        private static bool _steamAvailable = false;
        private static string _cachedName = null;
        private static Func<string> _getNameFunc = null;
        
        /// <summary>
        /// Try to initialize Steam access via reflection.
        /// Searches for multiple Steamworks implementations.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            
            DebugLogger.Info("SteamHelper: Searching for Steam integration...");
            
            try
            {
                // Log all loaded assemblies for debugging
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                DebugLogger.Info($"SteamHelper: Found {assemblies.Length} loaded assemblies");
                
                foreach (var assembly in assemblies)
                {
                    string asmName = assembly.GetName().Name.ToLowerInvariant();
                    
                    // Log assemblies that might be Steam-related
                    if (asmName.Contains("steam") || asmName.Contains("facepunch"))
                    {
                        DebugLogger.Info($"SteamHelper: Found potential Steam assembly: {assembly.GetName().Name}");
                    }
                    
                    // Try Steamworks.NET pattern
                    if (TrySteamworksNet(assembly)) return;
                    
                    // Try Facepunch.Steamworks pattern
                    if (TryFacepunchSteamworks(assembly)) return;
                    
                    // Try any assembly with Steam types
                    if (asmName.Contains("steam") || assembly.FullName.ToLowerInvariant().Contains("steam"))
                    {
                        if (TryGenericSteamTypes(assembly)) return;
                    }
                }
                
                // Try to find in Assembly-CSharp (game's main assembly)
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetName().Name == "Assembly-CSharp")
                    {
                        DebugLogger.Info("SteamHelper: Searching in Assembly-CSharp...");
                        if (TryGenericSteamTypes(assembly)) return;
                    }
                }
                
                DebugLogger.Warning("SteamHelper: No Steam integration found - using fallback names");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"SteamHelper: Exception during initialization: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Try Steamworks.NET pattern: Steamworks.SteamFriends.GetPersonaName()
        /// </summary>
        private static bool TrySteamworksNet(Assembly assembly)
        {
            try
            {
                // Try SteamFriends.GetPersonaName() - Steamworks.NET pattern
                var steamFriendsType = assembly.GetType("Steamworks.SteamFriends");
                if (steamFriendsType != null)
                {
                    var method = steamFriendsType.GetMethod("GetPersonaName", 
                        BindingFlags.Public | BindingFlags.Static);
                    
                    if (method != null)
                    {
                        _getNameFunc = () => method.Invoke(null, null) as string;
                        
                        // Test it
                        string testName = _getNameFunc();
                        if (!string.IsNullOrEmpty(testName))
                        {
                            _steamAvailable = true;
                            _cachedName = testName;
                            DebugLogger.Info($"SteamHelper: Steamworks.NET found! Name: {testName}");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"SteamHelper: Steamworks.NET check failed: {ex.Message}");
            }
            return false;
        }
        
        /// <summary>
        /// Try Facepunch.Steamworks pattern: SteamClient.Name
        /// </summary>
        private static bool TryFacepunchSteamworks(Assembly assembly)
        {
            try
            {
                // Try SteamClient.Name - Facepunch.Steamworks pattern
                var steamClientType = assembly.GetType("Steamworks.SteamClient");
                if (steamClientType != null)
                {
                    var nameProperty = steamClientType.GetProperty("Name", 
                        BindingFlags.Public | BindingFlags.Static);
                    
                    if (nameProperty != null)
                    {
                        _getNameFunc = () => nameProperty.GetValue(null) as string;
                        
                        // Test it
                        string testName = _getNameFunc();
                        if (!string.IsNullOrEmpty(testName))
                        {
                            _steamAvailable = true;
                            _cachedName = testName;
                            DebugLogger.Info($"SteamHelper: Facepunch.Steamworks found! Name: {testName}");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"SteamHelper: Facepunch check failed: {ex.Message}");
            }
            return false;
        }
        
        /// <summary>
        /// Try to find any Steam-related types that might give us the player name.
        /// </summary>
        private static bool TryGenericSteamTypes(Assembly assembly)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    string typeName = type.Name.ToLowerInvariant();
                    
                    // Skip if not Steam-related
                    if (!typeName.Contains("steam") && !type.Namespace?.ToLowerInvariant().Contains("steam") == true)
                        continue;
                    
                    // Look for GetPersonaName method
                    var method = type.GetMethod("GetPersonaName", 
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                    if (method != null && method.GetParameters().Length == 0 && method.ReturnType == typeof(string))
                    {
                        if (method.IsStatic)
                        {
                            _getNameFunc = () => method.Invoke(null, null) as string;
                        }
                        else
                        {
                            // Instance method - need to find instance
                            continue;
                        }
                        
                        string testName = _getNameFunc();
                        if (!string.IsNullOrEmpty(testName))
                        {
                            _steamAvailable = true;
                            _cachedName = testName;
                            DebugLogger.Info($"SteamHelper: Found name via {type.FullName}.GetPersonaName(): {testName}");
                            return true;
                        }
                    }
                    
                    // Look for Name property
                    var nameProp = type.GetProperty("Name", 
                        BindingFlags.Public | BindingFlags.Static);
                    if (nameProp != null && nameProp.PropertyType == typeof(string))
                    {
                        _getNameFunc = () => nameProp.GetValue(null) as string;
                        
                        string testName = _getNameFunc();
                        if (!string.IsNullOrEmpty(testName))
                        {
                            _steamAvailable = true;
                            _cachedName = testName;
                            DebugLogger.Info($"SteamHelper: Found name via {type.FullName}.Name: {testName}");
                            return true;
                        }
                    }
                    
                    // Look for PersonaName property
                    var personaNameProp = type.GetProperty("PersonaName", 
                        BindingFlags.Public | BindingFlags.Static);
                    if (personaNameProp != null && personaNameProp.PropertyType == typeof(string))
                    {
                        _getNameFunc = () => personaNameProp.GetValue(null) as string;
                        
                        string testName = _getNameFunc();
                        if (!string.IsNullOrEmpty(testName))
                        {
                            _steamAvailable = true;
                            _cachedName = testName;
                            DebugLogger.Info($"SteamHelper: Found name via {type.FullName}.PersonaName: {testName}");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"SteamHelper: Generic search in {assembly.GetName().Name} failed: {ex.Message}");
            }
            return false;
        }
        
        /// <summary>
        /// Force re-initialization. Call this if Steam wasn't ready during initial load.
        /// </summary>
        public static void Reinitialize()
        {
            _initialized = false;
            _steamAvailable = false;
            _cachedName = null;
            _getNameFunc = null;
            Initialize();
        }
        
        /// <summary>
        /// Get the local player's Steam persona name.
        /// Returns a fallback name if Steam is not available.
        /// </summary>
        public static string GetLocalPlayerName()
        {
            Initialize();
            
            // Return cached name if available
            if (!string.IsNullOrEmpty(_cachedName))
            {
                return _cachedName;
            }
            
            // Try to get fresh name
            if (_steamAvailable && _getNameFunc != null)
            {
                try
                {
                    var name = _getNameFunc();
                    if (!string.IsNullOrEmpty(name))
                    {
                        _cachedName = name;
                        DebugLogger.Info($"SteamHelper: Got Steam name: {name}");
                        return name;
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"SteamHelper: Failed to get Steam name: {ex.Message}");
                }
            }
            
            // Fallback: use system username
            string fallback = Environment.UserName;
            if (string.IsNullOrEmpty(fallback))
            {
                fallback = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
            }
            
            DebugLogger.Info($"SteamHelper: Using fallback name: {fallback}");
            return fallback;
        }
        
        /// <summary>
        /// Check if Steam is available.
        /// </summary>
        public static bool IsSteamAvailable
        {
            get
            {
                Initialize();
                return _steamAvailable;
            }
        }
    }
}
