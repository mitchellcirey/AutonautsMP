namespace AutonautsMP.Core
{
    /// <summary>
    /// Developer/debug settings. Edit these values directly to enable/disable features.
    /// </summary>
    public static class DevSettings
    {
        // ============================================
        // VERSION - Update this for new releases
        // ============================================
        
        public const string Version = "1.0.1";

        // ============================================
        // MASTER TOGGLE
        // ============================================
        
        /// <summary>
        /// Master toggle for all dev/debug features.
        /// Set to false to disable ALL debug features at once.
        /// </summary>
        public const bool DevModeEnabled = true;

        // ============================================
        // INDIVIDUAL FEATURE TOGGLES
        // (Only active when DevModeEnabled is true)
        // ============================================

        /// <summary>
        /// Show the network debug overlay (top-left stats panel).
        /// </summary>
        public const bool ShowNetworkOverlay = true;

        /// <summary>
        /// Show the test sync UI (Send Test Packet button and counter).
        /// </summary>
        public const bool ShowTestSyncUI = true;

        /// <summary>
        /// Open debug console window when hosting.
        /// </summary>
        public const bool OpenConsoleOnHost = true;

        /// <summary>
        /// Log packets to the debug console.
        /// </summary>
        public const bool LogPackets = true;

        // ============================================
        // HELPER METHOD
        // ============================================

        /// <summary>
        /// Check if a specific feature should be active.
        /// Returns true only if DevMode is enabled AND the specific feature is enabled.
        /// </summary>
        public static bool IsFeatureEnabled(DevFeature feature)
        {
            if (!DevModeEnabled) return false;

            return feature switch
            {
                DevFeature.NetworkOverlay => ShowNetworkOverlay,
                DevFeature.TestSyncUI => ShowTestSyncUI,
                DevFeature.ConsoleOnHost => OpenConsoleOnHost,
                DevFeature.PacketLogging => LogPackets,
                _ => false
            };
        }
    }

    /// <summary>
    /// Dev feature flags.
    /// </summary>
    public enum DevFeature
    {
        NetworkOverlay,
        TestSyncUI,
        ConsoleOnHost,
        PacketLogging
    }
}
