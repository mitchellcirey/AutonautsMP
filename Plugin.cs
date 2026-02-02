using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace AutonautsMP
{
    [BepInPlugin("com.autonautsmp.mod", "AutonautsMP", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin? Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo("AutonautsMP loaded");
            var go = new GameObject("AutonautsMP");
            DontDestroyOnLoad(go);
            go.AddComponent<MultiplayerUI>();
        }

        public void Log(string message)
        {
            Logger.LogInfo(message);
        }
    }

    public class MultiplayerUI : MonoBehaviour
    {
        private bool _show = true;
        private string _ip = "127.0.0.1";
        private string _port = "9050";
        private string _status = "Not connected";
        private Vector2 _errorScroll;

        // Network loaded via reflection
        private object? _network;
        private MethodInfo? _hostMethod;
        private MethodInfo? _joinMethod;
        private MethodInfo? _disconnectMethod;
        private MethodInfo? _updateMethod;
        private MethodInfo? _getStatusMethod;
        private bool _triedLoad;
        private string? _loadError;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
                _show = !_show;

            // Poll network
            if (_network != null && _updateMethod != null)
            {
                try { _updateMethod.Invoke(_network, null); } catch { }
            }
        }

        private void OnGUI()
        {
            // MP button always visible
            if (GUI.Button(new Rect(Screen.width - 70, 10, 60, 40), "MP"))
                _show = !_show;

            if (!_show) return;

            float x = 20, y = 20, w = 400, h = 350;
            GUI.Box(new Rect(x, y, w, h), "AutonautsMP");

            // Update status from network
            if (_network != null && _getStatusMethod != null)
            {
                try { _status = (string)_getStatusMethod.Invoke(_network, null)!; } catch { }
            }

            // Status
            GUI.Label(new Rect(x + 10, y + 25, w - 20, 20), "Status: " + _status);

            // Host button
            if (GUI.Button(new Rect(x + 10, y + 55, w - 20, 30), "Host Game"))
            {
                if (LoadNetwork())
                {
                    try { _hostMethod?.Invoke(_network, new object[] { int.Parse(_port) }); }
                    catch (Exception e) { _status = "Error: " + e.Message; }
                }
            }

            // IP/Port input
            GUI.Label(new Rect(x + 10, y + 95, 25, 20), "IP:");
            _ip = GUI.TextField(new Rect(x + 35, y + 95, 200, 25), _ip);
            GUI.Label(new Rect(x + 250, y + 95, 35, 20), "Port:");
            _port = GUI.TextField(new Rect(x + 290, y + 95, 80, 25), _port);

            // Join button
            if (GUI.Button(new Rect(x + 10, y + 130, w - 20, 30), "Join Game"))
            {
                if (LoadNetwork())
                {
                    try { _joinMethod?.Invoke(_network, new object[] { _ip, int.Parse(_port) }); }
                    catch (Exception e) { _status = "Error: " + e.Message; }
                }
            }

            // Disconnect button
            if (GUI.Button(new Rect(x + 10, y + 170, w - 20, 30), "Disconnect"))
            {
                if (_network != null && _disconnectMethod != null)
                {
                    try { _disconnectMethod.Invoke(_network, null); } catch { }
                }
                _status = "Disconnected";
            }

            // Error display with scroll
            if (_loadError != null)
            {
                GUI.Label(new Rect(x + 10, y + 210, w - 20, 20), "Error:");
                _errorScroll = GUI.BeginScrollView(
                    new Rect(x + 10, y + 230, w - 20, 100),
                    _errorScroll,
                    new Rect(0, 0, w - 40, 200)
                );
                GUI.Label(new Rect(0, 0, w - 40, 200), _loadError);
                GUI.EndScrollView();
            }
        }

        private static string? _libDir;
        private static Assembly? _liteNetLib;

        private bool LoadNetwork()
        {
            if (_network != null) return true;
            if (_triedLoad) return false;
            _triedLoad = true;

            try
            {
                // Network DLLs in AppData
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _libDir = Path.Combine(appData, "AutonautsMP");
                string networkDll = Path.Combine(_libDir, "AutonautsMP.Network.dll");
                string liteNetDll = Path.Combine(_libDir, "LiteNetLib.dll");

                if (!File.Exists(networkDll) || !File.Exists(liteNetDll))
                {
                    _loadError = "DLLs not found in:\n" + _libDir;
                    return false;
                }

                // Add resolver to help CLR find LiteNetLib
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

                // Load LiteNetLib first
                _liteNetLib = Assembly.LoadFrom(liteNetDll);

                // Load network assembly
                var asm = Assembly.LoadFrom(networkDll);
                var bridgeType = asm.GetType("AutonautsMP.Network.NetworkBridge");

                if (bridgeType == null)
                {
                    _loadError = "NetworkBridge type not found";
                    return false;
                }

                _network = Activator.CreateInstance(bridgeType);
                _hostMethod = bridgeType.GetMethod("StartHost");
                _joinMethod = bridgeType.GetMethod("Connect");
                _disconnectMethod = bridgeType.GetMethod("Disconnect");
                _updateMethod = bridgeType.GetMethod("Update");
                _getStatusMethod = bridgeType.GetMethod("GetStatus");

                _status = "Network loaded";
                return true;
            }
            catch (Exception e)
            {
                _loadError = "Load failed: " + e.Message;
                if (e.InnerException != null)
                    _loadError += "\nInner: " + e.InnerException.Message;
                return false;
            }
        }

        private static Assembly? OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Help CLR find LiteNetLib
            if (args.Name.StartsWith("LiteNetLib"))
                return _liteNetLib;

            // Try to load from our lib directory
            if (_libDir == null) return null;
            string name = new AssemblyName(args.Name).Name;
            string path = Path.Combine(_libDir, name + ".dll");
            if (File.Exists(path))
                return Assembly.LoadFrom(path);

            return null;
        }
    }
}
