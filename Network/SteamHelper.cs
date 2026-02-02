using System;
using System.Reflection;
using AutonautsMP.Core;

namespace AutonautsMP.Network
{
    /// <summary>
    /// Helper class to interact with Steamworks API.
    /// Uses reflection to access Steam if available (game already loads Steamworks).
    /// </summary>
    internal static class SteamHelper
    {
        private static bool _initialized = false;
        private static bool _steamAvailable = false;
        private static Type _steamFriendsType;
        private static MethodInfo _getPersonaNameMethod;
        
        /// <summary>
        /// Try to initialize Steam access via reflection.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            
            try
            {
                // Try to find Steamworks.NET assembly
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "com.rlabrecque.steamworks.net" || 
                        assembly.GetName().Name == "Steamworks.NET" ||
                        assembly.FullName.Contains("Steamworks"))
                    {
                        // Look for SteamFriends class
                        _steamFriendsType = assembly.GetType("Steamworks.SteamFriends");
                        if (_steamFriendsType != null)
                        {
                            _getPersonaNameMethod = _steamFriendsType.GetMethod("GetPersonaName", 
                                BindingFlags.Public | BindingFlags.Static);
                            
                            if (_getPersonaNameMethod != null)
                            {
                                _steamAvailable = true;
                                DebugLogger.Info("Steam integration initialized successfully");
                                return;
                            }
                        }
                    }
                }
                
                // Also try direct type lookup
                var steamType = Type.GetType("Steamworks.SteamFriends, com.rlabrecque.steamworks.net") ??
                               Type.GetType("Steamworks.SteamFriends, Steamworks.NET") ??
                               Type.GetType("Steamworks.SteamFriends, Assembly-CSharp");
                               
                if (steamType != null)
                {
                    _steamFriendsType = steamType;
                    _getPersonaNameMethod = steamType.GetMethod("GetPersonaName", 
                        BindingFlags.Public | BindingFlags.Static);
                    
                    if (_getPersonaNameMethod != null)
                    {
                        _steamAvailable = true;
                        DebugLogger.Info("Steam integration initialized (direct lookup)");
                        return;
                    }
                }
                
                DebugLogger.Warning("Steam not available - using fallback names");
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"Failed to initialize Steam: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get the local player's Steam persona name.
        /// Returns a fallback name if Steam is not available.
        /// </summary>
        public static string GetLocalPlayerName()
        {
            Initialize();
            
            if (_steamAvailable && _getPersonaNameMethod != null)
            {
                try
                {
                    var name = _getPersonaNameMethod.Invoke(null, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        DebugLogger.Info($"Got Steam name: {name}");
                        return name;
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"Failed to get Steam name: {ex.Message}");
                }
            }
            
            // Fallback: use system username
            string fallback = Environment.UserName;
            if (string.IsNullOrEmpty(fallback))
            {
                fallback = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
            }
            
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
