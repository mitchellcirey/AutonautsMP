using HarmonyLib;
using AutonautsMP.Core;

namespace AutonautsMP.Patches
{
    /// <summary>
    /// Harmony patch to verify that runtime injection is working.
    /// Patches a safe, always-running Unity method to confirm patching works.
    /// </summary>
    [HarmonyPatch]
    internal static class GameLoopPatch
    {
        // Flag to ensure we only log the confirmation message once
        private static bool _hasLoggedConfirmation = false;
        
        /// <summary>
        /// Patch the Camera.main getter - this is called constantly during gameplay
        /// and is a safe target that won't affect game behavior.
        /// </summary>
        [HarmonyPatch(typeof(UnityEngine.Camera), "get_main")]
        [HarmonyPostfix]
        public static void Camera_Main_Postfix()
        {
            if (_hasLoggedConfirmation) return;
            
            _hasLoggedConfirmation = true;
            DebugLogger.Debug("[PATCH] Game loop patch confirmed working!");
            DebugLogger.Debug("[PATCH] Harmony injection successful - Camera.main getter patched");
        }
        
        /// <summary>
        /// Alternative patch on Time.frameCount getter as a backup verification.
        /// Also constantly called and completely safe to patch.
        /// </summary>
        [HarmonyPatch(typeof(UnityEngine.Time), "get_frameCount")]
        [HarmonyPostfix]
        public static void Time_FrameCount_Postfix()
        {
            // Only log if the camera patch hasn't already logged
            // This provides redundancy in case Camera.main isn't used
            if (_hasLoggedConfirmation) return;
            
            _hasLoggedConfirmation = true;
            DebugLogger.Debug("[PATCH] Game loop patch confirmed working!");
            DebugLogger.Debug("[PATCH] Harmony injection successful - Time.frameCount getter patched");
        }
    }
}
