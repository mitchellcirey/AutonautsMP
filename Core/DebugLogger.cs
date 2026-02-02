using BepInEx.Logging;

namespace AutonautsMP.Core
{
    /// <summary>
    /// Centralized logging helper for the AutonautsMP mod.
    /// Wraps BepInEx ManualLogSource for consistent log formatting.
    /// </summary>
    internal static class DebugLogger
    {
        private static ManualLogSource? _logger;
        
        /// <summary>
        /// Initialize the logger with the BepInEx log source.
        /// Must be called once during plugin startup.
        /// </summary>
        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Log an informational message.
        /// </summary>
        public static void Info(string message)
        {
            _logger?.LogInfo(message);
        }
        
        /// <summary>
        /// Log a warning message.
        /// </summary>
        public static void Warning(string message)
        {
            _logger?.LogWarning(message);
        }
        
        /// <summary>
        /// Log an error message.
        /// </summary>
        public static void Error(string message)
        {
            _logger?.LogError(message);
        }
        
        /// <summary>
        /// Log a debug message (only visible with debug logging enabled).
        /// </summary>
        public static void Debug(string message)
        {
            _logger?.LogDebug(message);
        }
        
        /// <summary>
        /// Log a message with a specific log level.
        /// </summary>
        public static void Log(LogLevel level, string message)
        {
            _logger?.Log(level, message);
        }
    }
}
