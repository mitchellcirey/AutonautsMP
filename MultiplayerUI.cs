using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AutonautsMP
{
    /// <summary>
    /// In-game multiplayer UI. Loads network assembly ONLY when user clicks Host/Join.
    /// This class has ZERO references to LiteNetLib or network types.
    /// </summary>
    public class MultiplayerUI : MonoBehaviour
    {
        private bool _showWindow = true;
        private string _ip = "127.0.0.1";
        private string _port = "9050";
        private string _status = "Not connected";
        private Rect _windowRect = new Rect(20, 20, 300, 200);

        // Network state - loaded dynamically, stored as object to avoid type references
        private object? _networkBridge;
        private MethodInfo? _hostMethod;
        private MethodInfo? _joinMethod;
        private MethodInfo? _disconnectMethod;
        private MethodInfo? _updateMethod;
        private MethodInfo? _getStatusMethod;
        private bool _networkLoaded;
        private bool _networkLoadFailed;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
                _showWindow = !_showWindow;

            // Call network Update if loaded
            if (_networkBridge != null && _updateMethod != null)
            {
                try { _updateMethod.Invoke(_networkBridge, null); }
                catch { }
            }
        }

        private void OnGUI()
        {
            // MP toggle button - always visible
            GUI.depth = -1000;
            if (GUI.Button(new Rect(Screen.width - 70, 10, 60, 40), "MP"))
                _showWindow = !_showWindow;

            if (!_showWindow) return;

            _windowRect = GUI.Window(12345, _windowRect, DrawWindow, "AutonautsMP");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            // Status
            GUILayout.Label("Status: " + _status);
            GUILayout.Space(10);

            // Get updated status from network if loaded
            if (_networkBridge != null && _getStatusMethod != null)
            {
                try { _status = (string)_getStatusMethod.Invoke(_networkBridge, null)!; }
                catch { }
            }

            // Host button
            if (GUILayout.Button("Host Game"))
            {
                if (EnsureNetworkLoaded() && _hostMethod != null)
                {
                    try
                    {
                        int p = int.Parse(_port);
                        _hostMethod.Invoke(_networkBridge, new object[] { p });
                        _status = "Hosting on port " + p;
                    }
                    catch (Exception ex) { _status = "Host failed: " + ex.Message; }
                }
            }

            GUILayout.Space(5);

            // Join section
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP:", GUILayout.Width(25));
            _ip = GUILayout.TextField(_ip, GUILayout.Width(120));
            GUILayout.Label("Port:", GUILayout.Width(35));
            _port = GUILayout.TextField(_port, GUILayout.Width(50));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Join Game"))
            {
                if (EnsureNetworkLoaded() && _joinMethod != null)
                {
                    try
                    {
                        int p = int.Parse(_port);
                        _joinMethod.Invoke(_networkBridge, new object[] { _ip, p });
                        _status = "Connecting to " + _ip + ":" + p;
                    }
                    catch (Exception ex) { _status = "Join failed: " + ex.Message; }
                }
            }

            GUILayout.Space(5);

            // Disconnect
            if (GUILayout.Button("Disconnect"))
            {
                if (_networkBridge != null && _disconnectMethod != null)
                {
                    try { _disconnectMethod.Invoke(_networkBridge, null); }
                    catch { }
                    _status = "Disconnected";
                }
            }

            GUILayout.FlexibleSpace();

            if (_networkLoadFailed)
                GUILayout.Label("<color=red>Network DLL not found!</color>");

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        /// <summary>
        /// Loads AutonautsMP.Network.dll on first use. Returns true if loaded successfully.
        /// </summary>
        private bool EnsureNetworkLoaded()
        {
            if (_networkLoaded) return _networkBridge != null;
            if (_networkLoadFailed) return false;

            _networkLoaded = true;

            try
            {
                // Find the network DLL in the libs subfolder (keeps it away from BepInEx scanning)
                string myDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string networkDll = Path.Combine(myDir, "libs", "AutonautsMP.Network.dll");

                if (!File.Exists(networkDll))
                {
                    Debug.LogError("[AutonautsMP] Network DLL not found at: " + networkDll);
                    _networkLoadFailed = true;
                    _status = "AutonautsMP.Network.dll not found!";
                    return false;
                }

                // Load the assembly
                Assembly networkAsm = Assembly.LoadFrom(networkDll);
                
                // Find the NetworkBridge type
                Type? bridgeType = networkAsm.GetType("AutonautsMP.Network.NetworkBridge");
                if (bridgeType == null)
                {
                    Debug.LogError("[AutonautsMP] NetworkBridge type not found in network DLL");
                    _networkLoadFailed = true;
                    _status = "NetworkBridge type not found!";
                    return false;
                }

                // Create instance
                _networkBridge = Activator.CreateInstance(bridgeType);

                // Get methods
                _hostMethod = bridgeType.GetMethod("StartHost");
                _joinMethod = bridgeType.GetMethod("Connect");
                _disconnectMethod = bridgeType.GetMethod("Disconnect");
                _updateMethod = bridgeType.GetMethod("Update");
                _getStatusMethod = bridgeType.GetMethod("GetStatus");

                Debug.Log("[AutonautsMP] Network assembly loaded successfully!");
                _status = "Network loaded";
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[AutonautsMP] Failed to load network assembly: " + ex);
                _networkLoadFailed = true;
                _status = "Load failed: " + ex.Message;
                return false;
            }
        }
    }
}
