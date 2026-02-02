using UnityEngine;
using AutonautsMP.Core;

namespace AutonautsMP.UI
{
    /// <summary>
    /// Main multiplayer UI panel using Unity IMGUI.
    /// Provides host/join interface for future networking features.
    /// Designed for easy extension when network layer is implemented.
    /// </summary>
    internal class MultiplayerUI : MonoBehaviour
    {
        // Window state
        private bool _showWindow = false;
        private Rect _windowRect;
        private int _windowId;
        
        // UI field values
        private string _ipAddress = "";
        private string _port = "";
        private string _statusText = "Ready";
        
        // Styles (initialized in OnGUI for proper Unity lifecycle)
        private GUIStyle? _debugTextStyle;
        private GUIStyle? _titleStyle;
        private GUIStyle? _statusStyle;
        private bool _stylesInitialized = false;
        
        private void Awake()
        {
            // Generate unique window ID
            _windowId = GetInstanceID();
            
            // Initialize window rect from config
            _windowRect = new Rect(
                ModConfig.WindowX,
                ModConfig.WindowY,
                ModConfig.WindowWidth,
                ModConfig.WindowHeight
            );
            
            // Initialize field defaults from config
            _ipAddress = ModConfig.DefaultIP;
            _port = ModConfig.DefaultPort.ToString();
            
            DebugLogger.Info("MultiplayerUI component initialized");
        }
        
        private void Update()
        {
            // Check for toggle key press
            if (Input.GetKeyDown(ModConfig.ToggleKey))
            {
                ToggleWindow();
            }
        }
        
        private void OnGUI()
        {
            // Initialize styles if needed (must be done in OnGUI)
            InitializeStyles();
            
            // Always show debug text to confirm mod is loaded
            DrawDebugOverlay();
            
            // Always show toggle button in top-right corner
            DrawToggleButton();
            
            // Draw main window if visible
            if (_showWindow)
            {
                _windowRect = GUI.Window(_windowId, _windowRect, DrawWindowContents, "");
                
                // Save window position when dragged
                ModConfig.SaveWindowPosition((int)_windowRect.x, (int)_windowRect.y);
            }
        }
        
        /// <summary>
        /// Initialize GUI styles. Must be called from OnGUI.
        /// </summary>
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;
            
            _debugTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.green }
            };
            
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.cyan }
            };
            
            _stylesInitialized = true;
        }
        
        /// <summary>
        /// Draw the "Multiplayer Mod Loaded" debug overlay.
        /// Always visible in top-left corner.
        /// </summary>
        private void DrawDebugOverlay()
        {
            GUI.Label(
                new Rect(10, 10, 250, 25),
                "AutonautsMP Loaded Successfully",
                _debugTextStyle
            );
        }
        
        /// <summary>
        /// Draw the toggle button in top-right corner.
        /// </summary>
        private void DrawToggleButton()
        {
            float buttonWidth = 60;
            float buttonHeight = 40;
            float margin = 10;
            
            Rect buttonRect = new Rect(
                Screen.width - buttonWidth - margin,
                margin,
                buttonWidth,
                buttonHeight
            );
            
            if (GUI.Button(buttonRect, "MP"))
            {
                ToggleWindow();
            }
        }
        
        /// <summary>
        /// Draw the main window contents.
        /// </summary>
        private void DrawWindowContents(int windowId)
        {
            float padding = 10;
            float y = padding;
            float contentWidth = _windowRect.width - (padding * 2);
            
            // Title
            GUI.Label(
                new Rect(padding, y, contentWidth, 30),
                "AutonautsMP",
                _titleStyle
            );
            y += 35;
            
            // Separator line
            GUI.Box(new Rect(padding, y, contentWidth, 2), "");
            y += 10;
            
            // Status label
            GUI.Label(
                new Rect(padding, y, contentWidth, 20),
                $"Status: {_statusText}",
                _statusStyle
            );
            y += 30;
            
            // IP Address field
            GUI.Label(new Rect(padding, y, 80, 20), "IP Address:");
            _ipAddress = GUI.TextField(new Rect(padding + 85, y, contentWidth - 85, 22), _ipAddress);
            y += 30;
            
            // Port field
            GUI.Label(new Rect(padding, y, 80, 20), "Port:");
            _port = GUI.TextField(new Rect(padding + 85, y, 80, 22), _port);
            y += 35;
            
            // Host Game button
            if (GUI.Button(new Rect(padding, y, contentWidth, 35), "Host Game"))
            {
                OnHostGameClicked();
            }
            y += 45;
            
            // Join Game button
            if (GUI.Button(new Rect(padding, y, contentWidth, 35), "Join Game"))
            {
                OnJoinGameClicked();
            }
            y += 55;
            
            // Close button at bottom
            if (GUI.Button(new Rect(padding, _windowRect.height - 45, contentWidth, 30), "Close"))
            {
                OnCloseClicked();
            }
            
            // Make window draggable
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 30));
        }
        
        /// <summary>
        /// Toggle the window visibility.
        /// </summary>
        public void ToggleWindow()
        {
            _showWindow = !_showWindow;
            
            if (_showWindow)
            {
                DebugLogger.Info("Multiplayer UI opened");
            }
            else
            {
                DebugLogger.Info("Multiplayer UI closed");
            }
        }
        
        /// <summary>
        /// Called when Host Game button is clicked.
        /// Logs debug message. Network implementation will be added later.
        /// </summary>
        private void OnHostGameClicked()
        {
            DebugLogger.Info($"Host Game button clicked (Port: {_port})");
            _statusText = "Host clicked - networking not implemented";
            
            // TODO: Future network implementation
            // NetworkService.Instance?.StartHost(int.Parse(_port));
        }
        
        /// <summary>
        /// Called when Join Game button is clicked.
        /// Logs debug message. Network implementation will be added later.
        /// </summary>
        private void OnJoinGameClicked()
        {
            DebugLogger.Info($"Join Game button clicked (IP: {_ipAddress}, Port: {_port})");
            _statusText = "Join clicked - networking not implemented";
            
            // TODO: Future network implementation
            // NetworkService.Instance?.JoinGame(_ipAddress, int.Parse(_port));
        }
        
        /// <summary>
        /// Called when Close button is clicked.
        /// </summary>
        private void OnCloseClicked()
        {
            DebugLogger.Info("Close button clicked");
            _showWindow = false;
        }
        
        /// <summary>
        /// Update the status text displayed in the UI.
        /// Can be called by network layer to show connection status.
        /// </summary>
        public void SetStatus(string status)
        {
            _statusText = status;
            DebugLogger.Debug($"Status updated: {status}");
        }
        
        /// <summary>
        /// Check if the UI window is currently visible.
        /// </summary>
        public bool IsWindowVisible => _showWindow;
    }
}
