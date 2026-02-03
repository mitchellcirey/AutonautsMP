using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutonautsMP.Core
{
    /// <summary>
    /// Detects whether the player is currently in a loaded game session.
    /// Used to restrict hosting to only when in-game.
    /// </summary>
    public static class GameStateDetector
    {
        /// <summary>
        /// Whether the player is currently in a loaded game (not in menus).
        /// </summary>
        public static bool IsInGame { get; private set; } = false;

        /// <summary>
        /// The name of the current scene.
        /// </summary>
        public static string CurrentScene { get; private set; } = "";

        /// <summary>
        /// Event fired when game state changes (enters or exits game).
        /// </summary>
        public static event Action<bool> OnGameStateChanged;

        // Known menu scene names (will be detected automatically too)
        private static readonly string[] MenuSceneNames = new[]
        {
            "MainMenu",
            "Menu",
            "Title",
            "TitleScreen",
            "Splash",
            "Loading"
        };

        // Known game scene names
        private static readonly string[] GameSceneNames = new[]
        {
            "Main",
            "Game",
            "World",
            "Gameplay"
        };

        private static bool _initialized = false;

        /// <summary>
        /// Initialize the game state detector.
        /// Call this once at startup.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Subscribe to scene events
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            // Check current scene state
            var currentScene = SceneManager.GetActiveScene();
            CurrentScene = currentScene.name;
            UpdateGameState(currentScene.name);

            DebugLogger.Info($"GameStateDetector initialized - Current scene: {CurrentScene}, IsInGame: {IsInGame}");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CurrentScene = scene.name;
            DebugLogger.Info($"Scene loaded: {scene.name} (mode: {mode})");
            UpdateGameState(scene.name);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            DebugLogger.Info($"Scene unloaded: {scene.name}");
        }

        private static void UpdateGameState(string sceneName)
        {
            bool wasInGame = IsInGame;

            // Check if this is a menu scene
            bool isMenuScene = false;
            foreach (var menuName in MenuSceneNames)
            {
                if (sceneName.IndexOf(menuName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isMenuScene = true;
                    break;
                }
            }

            // Check if this is a known game scene
            bool isGameScene = false;
            foreach (var gameName in GameSceneNames)
            {
                if (sceneName.IndexOf(gameName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isGameScene = true;
                    break;
                }
            }

            // Determine if we're in game:
            // - Explicitly a game scene, OR
            // - Not a menu scene (assume game if we can't tell)
            if (isGameScene)
            {
                IsInGame = true;
            }
            else if (isMenuScene)
            {
                IsInGame = false;
            }
            else
            {
                // Unknown scene - try to detect by checking for game objects
                IsInGame = TryDetectGameState();
            }

            if (wasInGame != IsInGame)
            {
                DebugLogger.Info($"Game state changed: IsInGame = {IsInGame}");
                OnGameStateChanged?.Invoke(IsInGame);
            }
        }

        /// <summary>
        /// Try to detect game state by checking for common game objects.
        /// </summary>
        private static bool TryDetectGameState()
        {
            // Look for common game manager objects that exist in gameplay
            // These are typical names used in Unity games
            var gameManagerNames = new[]
            {
                "GameManager",
                "WorldManager", 
                "TileManager",
                "GameStateManager",
                "PlayModeManager"
            };

            foreach (var name in gameManagerNames)
            {
                var obj = GameObject.Find(name);
                if (obj != null && obj.activeInHierarchy)
                {
                    DebugLogger.Debug($"Detected game state via GameObject: {name}");
                    return true;
                }
            }

            // Also check for Farmer/Player object
            var farmer = GameObject.Find("Farmer");
            var player = GameObject.Find("Player");
            if ((farmer != null && farmer.activeInHierarchy) || 
                (player != null && player.activeInHierarchy))
            {
                DebugLogger.Debug("Detected game state via Farmer/Player object");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Manually set the game state. Called by Harmony patches.
        /// </summary>
        public static void SetInGame(bool inGame)
        {
            if (IsInGame != inGame)
            {
                IsInGame = inGame;
                DebugLogger.Info($"Game state set via patch: IsInGame = {IsInGame}");
                OnGameStateChanged?.Invoke(IsInGame);
            }
        }

        /// <summary>
        /// Force a re-check of the current game state.
        /// Useful when patches can't be applied.
        /// </summary>
        public static void RefreshState()
        {
            var scene = SceneManager.GetActiveScene();
            CurrentScene = scene.name;
            UpdateGameState(scene.name);
        }
    }
}
