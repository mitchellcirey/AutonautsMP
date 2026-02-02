using UnityEngine;
using AutonautsMP.Core;
using AutonautsMP.Network;

namespace AutonautsMP.UI
{
    /// <summary>
    /// Main multiplayer UI panel using Unity IMGUI.
    /// Fullscreen overlay with modern styling.
    /// </summary>
    internal class MultiplayerUI : MonoBehaviour
    {
        // Window state
        private bool _showWindow = false;
        
        // UI field values
        private string _ipAddress = "";
        private string _port = "";
        private string _statusText = "Ready to connect";
        
        // Textures for custom styling
        private Texture2D _overlayTexture;
        private Texture2D _panelTexture;
        private Texture2D _panelBorderTexture;
        private Texture2D _headerTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverTexture;
        private Texture2D _buttonActiveTexture;
        private Texture2D _inputTexture;
        private Texture2D _accentTexture;
        private Texture2D _closeButtonTexture;
        private Texture2D _closeButtonHoverTexture;
        
        // Styles
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _statusConnectedStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _buttonSmallStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _playerCountStyle;
        private GUIStyle _footerStyle;
        private bool _stylesInitialized = false;
        
        // Colors - Autonauts-inspired palette
        private readonly Color _primaryColor = new Color(0.2f, 0.6f, 0.9f);      // Sky blue
        private readonly Color _secondaryColor = new Color(0.3f, 0.8f, 0.4f);    // Green
        private readonly Color _accentColor = new Color(1f, 0.7f, 0.2f);         // Orange/Gold
        private readonly Color _dangerColor = new Color(0.9f, 0.3f, 0.3f);       // Red
        private readonly Color _panelColor = new Color(0.15f, 0.15f, 0.2f, 0.98f);
        private readonly Color _headerColor = new Color(0.1f, 0.1f, 0.15f);
        private readonly Color _inputBgColor = new Color(0.08f, 0.08f, 0.12f);
        
        // Layout constants
        private const float PANEL_WIDTH = 420f;
        private const float PANEL_HEIGHT = 440f;
        
        // Animation
        private float _animationProgress = 0f;
        private float _pulseTime = 0f;
        
        private void Awake()
        {
            // Initialize field defaults from config
            _ipAddress = ModConfig.DefaultIP;
            _port = ModConfig.DefaultPort.ToString();
            
            // Subscribe to network events
            NetworkManager.Instance.OnStateChanged += OnNetworkStateChanged;
            NetworkManager.Instance.OnPlayerConnected += OnPlayerConnected;
            NetworkManager.Instance.OnPlayerDisconnected += OnPlayerDisconnected;
            NetworkManager.Instance.OnError += OnNetworkError;
            
            DebugLogger.Info("MultiplayerUI component initialized");
        }
        
        private void OnDestroy()
        {
            // Cleanup textures
            if (_overlayTexture != null) Destroy(_overlayTexture);
            if (_panelTexture != null) Destroy(_panelTexture);
            if (_panelBorderTexture != null) Destroy(_panelBorderTexture);
            if (_headerTexture != null) Destroy(_headerTexture);
            if (_buttonTexture != null) Destroy(_buttonTexture);
            if (_buttonHoverTexture != null) Destroy(_buttonHoverTexture);
            if (_buttonActiveTexture != null) Destroy(_buttonActiveTexture);
            if (_inputTexture != null) Destroy(_inputTexture);
            if (_accentTexture != null) Destroy(_accentTexture);
            if (_closeButtonTexture != null) Destroy(_closeButtonTexture);
            if (_closeButtonHoverTexture != null) Destroy(_closeButtonHoverTexture);
            
            // Unsubscribe from network events
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnStateChanged -= OnNetworkStateChanged;
                NetworkManager.Instance.OnPlayerConnected -= OnPlayerConnected;
                NetworkManager.Instance.OnPlayerDisconnected -= OnPlayerDisconnected;
                NetworkManager.Instance.OnError -= OnNetworkError;
            }
        }
        
        private void OnNetworkStateChanged(ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.Disconnected:
                    _statusText = "Disconnected";
                    break;
                case ConnectionState.Connecting:
                    _statusText = "Connecting...";
                    break;
                case ConnectionState.Connected:
                    _statusText = "Connected to server!";
                    break;
                case ConnectionState.Hosting:
                    _statusText = $"Server running on port {_port}";
                    break;
            }
        }
        
        private void OnPlayerConnected(int peerId, string address)
        {
            if (NetworkManager.Instance.IsHost)
            {
                _statusText = $"Hosting - {NetworkManager.Instance.ConnectedPlayerCount} player(s) online";
            }
            DebugLogger.Info($"Player connected: {peerId} from {address}");
        }
        
        private void OnPlayerDisconnected(int peerId)
        {
            if (NetworkManager.Instance.IsHost)
            {
                _statusText = $"Hosting - {NetworkManager.Instance.ConnectedPlayerCount} player(s) online";
            }
            DebugLogger.Info($"Player disconnected: {peerId}");
        }
        
        private void OnNetworkError(string error)
        {
            _statusText = $"Error: {error}";
            DebugLogger.Error($"Network error: {error}");
        }
        
        private void Update()
        {
            // Check for toggle key press
            if (Input.GetKeyDown(ModConfig.ToggleKey))
            {
                ToggleWindow();
            }
            
            // Close on Escape
            if (_showWindow && Input.GetKeyDown(KeyCode.Escape))
            {
                _showWindow = false;
            }
            
            // Animation updates
            if (_showWindow && _animationProgress < 1f)
            {
                _animationProgress = Mathf.Min(1f, _animationProgress + Time.unscaledDeltaTime * 5f);
            }
            else if (!_showWindow && _animationProgress > 0f)
            {
                _animationProgress = Mathf.Max(0f, _animationProgress - Time.unscaledDeltaTime * 8f);
            }
            
            _pulseTime += Time.unscaledDeltaTime;
        }
        
        private void OnGUI()
        {
            // Initialize styles if needed (must be done in OnGUI)
            InitializeStyles();
            
            // Always show toggle button (unless fullscreen UI is open)
            if (!_showWindow)
            {
                DrawToggleButton();
            }
            
            // Draw fullscreen UI if visible or animating
            if (_showWindow || _animationProgress > 0.01f)
            {
                DrawFullscreenUI();
            }
        }
        
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;
            
            // Create textures
            _overlayTexture = MakeTexture(2, 2, new Color(0, 0, 0, 0.85f));
            _panelTexture = MakeTexture(2, 2, _panelColor);
            _panelBorderTexture = MakeTexture(2, 2, _primaryColor * 0.6f);
            _headerTexture = MakeTexture(2, 2, _headerColor);
            _buttonTexture = MakeTexture(2, 2, _primaryColor);
            _buttonHoverTexture = MakeTexture(2, 2, _primaryColor * 1.2f);
            _buttonActiveTexture = MakeTexture(2, 2, _primaryColor * 0.8f);
            _inputTexture = MakeTexture(2, 2, _inputBgColor);
            _accentTexture = MakeTexture(2, 2, _accentColor);
            _closeButtonTexture = MakeTexture(2, 2, new Color(0.4f, 0.4f, 0.45f));
            _closeButtonHoverTexture = MakeTexture(2, 2, _dangerColor);
            
            // Title style - large and prominent
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _accentColor }
            };
            
            // Subtitle style
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.7f) }
            };
            
            // Label style
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.8f, 0.8f, 0.85f) }
            };
            
            // Status style
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.75f) }
            };
            
            // Status connected style
            _statusConnectedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _secondaryColor }
            };
            
            // Input style
            _inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 8, 8),
                normal = { background = _inputTexture, textColor = Color.white },
                focused = { background = _inputTexture, textColor = Color.white },
                hover = { background = _inputTexture, textColor = Color.white }
            };
            
            // Button style
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _buttonTexture, textColor = Color.white },
                hover = { background = _buttonHoverTexture, textColor = Color.white },
                active = { background = _buttonActiveTexture, textColor = Color.white }
            };
            
            // Small button style
            _buttonSmallStyle = new GUIStyle(_buttonStyle)
            {
                fontSize = 13
            };
            
            // Close button style
            _closeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = _closeButtonTexture, textColor = Color.white },
                hover = { background = _closeButtonHoverTexture, textColor = Color.white },
                active = { background = _closeButtonHoverTexture, textColor = Color.white }
            };
            
            // Player count style
            _playerCountStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _accentColor }
            };
            
            // Footer style
            _footerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.4f, 0.4f, 0.45f) }
            };
            
            _stylesInitialized = true;
        }
        
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;
            
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
        
        private void DrawToggleButton()
        {
            float buttonWidth = 50;
            float buttonHeight = 50;
            float margin = 15;
            
            Rect buttonRect = new Rect(
                Screen.width - buttonWidth - margin,
                margin,
                buttonWidth,
                buttonHeight
            );
            
            // Draw button with custom style
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = _primaryColor;
            
            if (GUI.Button(buttonRect, "MP", _buttonStyle))
            {
                ToggleWindow();
            }
            
            GUI.backgroundColor = originalColor;
        }
        
        private void DrawFullscreenUI()
        {
            // Apply animation
            float alpha = Mathf.SmoothStep(0, 1, _animationProgress);
            float scale = Mathf.Lerp(0.95f, 1f, _animationProgress);
            
            // Calculate centered panel position with scale animation
            float panelW = PANEL_WIDTH * scale;
            float panelH = PANEL_HEIGHT * scale;
            float panelX = (Screen.width - panelW) / 2f;
            float panelY = (Screen.height - panelH) / 2f;
            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);
            
            // Fullscreen overlay - blocks all input to game
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _overlayTexture);
            
            // Handle clicks - only close if clicking OUTSIDE the panel
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (!panelRect.Contains(e.mousePosition))
                {
                    // Clicked outside panel - close
                    _showWindow = false;
                    e.Use();
                }
            }
            
            // Block all mouse events from reaching the game
            if (e.isMouse)
            {
                // Consume the event so game doesn't receive it
            }
            
            // Draw panel border (glow effect)
            float borderSize = 3;
            GUI.color = new Color(_primaryColor.r, _primaryColor.g, _primaryColor.b, alpha * 0.5f);
            GUI.DrawTexture(new Rect(panelRect.x - borderSize, panelRect.y - borderSize, 
                panelRect.width + borderSize * 2, panelRect.height + borderSize * 2), _panelBorderTexture);
            
            // Draw main panel
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(panelRect, _panelTexture);
            
            // Draw panel contents
            GUILayout.BeginArea(panelRect);
            DrawPanelContents();
            GUILayout.EndArea();
            
            // Reset color
            GUI.color = Color.white;
        }
        
        private void DrawPanelContents()
        {
            float padding = 25f;
            float y = padding;
            float contentWidth = PANEL_WIDTH - (padding * 2);
            
            // Close button (top right)
            if (GUI.Button(new Rect(PANEL_WIDTH - 45, 10, 35, 35), "X", _closeButtonStyle))
            {
                _showWindow = false;
            }
            
            // Title
            GUI.Label(new Rect(padding, y, contentWidth, 45), "AutonautsMP", _titleStyle);
            y += 50;
            
            // Subtitle
            GUI.Label(new Rect(padding, y, contentWidth, 20), "Multiplayer Mod for Autonauts", _subtitleStyle);
            y += 30;
            
            // Accent line
            GUI.DrawTexture(new Rect(padding + 50, y, contentWidth - 100, 2), _accentTexture);
            y += 20;
            
            // Status with pulsing effect when connecting
            bool isConnecting = _statusText.Contains("Connecting") || _statusText.Contains("Starting");
            var statusStyle = NetworkManager.Instance.IsConnected ? _statusConnectedStyle : _statusStyle;
            
            if (isConnecting)
            {
                float pulse = (Mathf.Sin(_pulseTime * 4f) + 1f) / 2f;
                GUI.color = new Color(1, 1, 1, 0.5f + pulse * 0.5f);
            }
            
            GUI.Label(new Rect(padding, y, contentWidth, 25), _statusText, statusStyle);
            GUI.color = Color.white;
            y += 40;
            
            bool isConnected = NetworkManager.Instance.IsConnected;
            
            if (!isConnected)
            {
                // === Not Connected UI ===
                
                // IP Address field
                GUI.Label(new Rect(padding, y, contentWidth, 20), "SERVER IP ADDRESS", _labelStyle);
                y += 25;
                _ipAddress = GUI.TextField(new Rect(padding, y, contentWidth, 38), _ipAddress, _inputStyle);
                y += 50;
                
                // Port field
                GUI.Label(new Rect(padding, y, contentWidth, 20), "PORT", _labelStyle);
                y += 25;
                _port = GUI.TextField(new Rect(padding, y, 120, 38), _port, _inputStyle);
                y += 55;
                
                // Buttons
                GUI.backgroundColor = _secondaryColor;
                if (GUI.Button(new Rect(padding, y, contentWidth, 45), "HOST GAME", _buttonStyle))
                {
                    OnHostGameClicked();
                }
                y += 55;
                
                GUI.backgroundColor = _primaryColor;
                if (GUI.Button(new Rect(padding, y, contentWidth, 45), "JOIN GAME", _buttonStyle))
                {
                    OnJoinGameClicked();
                }
                
                GUI.backgroundColor = Color.white;
            }
            else
            {
                // === Connected UI ===
                
                if (NetworkManager.Instance.IsHost)
                {
                    // Hosting UI
                    GUI.Label(new Rect(padding, y, contentWidth, 25), "PLAYERS ONLINE", _labelStyle);
                    y += 35;
                    
                    // Big player count
                    string playerCount = NetworkManager.Instance.ConnectedPlayerCount.ToString();
                    GUI.Label(new Rect(padding, y, contentWidth, 50), playerCount, _playerCountStyle);
                    y += 70;
                    
                    // Server info box
                    GUI.DrawTexture(new Rect(padding, y, contentWidth, 60), _inputTexture);
                    GUI.Label(new Rect(padding + 10, y + 5, contentWidth - 20, 25), 
                        $"Port: {_port}", _labelStyle);
                    GUI.Label(new Rect(padding + 10, y + 30, contentWidth - 20, 25), 
                        "Share your IP with friends to join!", _subtitleStyle);
                    y += 80;
                }
                else
                {
                    // Client UI
                    GUI.Label(new Rect(padding, y, contentWidth, 25), "CONNECTED TO SERVER", _labelStyle);
                    y += 35;
                    
                    // Server info
                    GUI.DrawTexture(new Rect(padding, y, contentWidth, 50), _inputTexture);
                    GUI.Label(new Rect(padding + 10, y + 15, contentWidth - 20, 25), 
                        $"{_ipAddress}:{_port}", _statusConnectedStyle);
                    y += 70;
                }
                
                // Disconnect button
                GUI.backgroundColor = _dangerColor;
                if (GUI.Button(new Rect(padding, y, contentWidth, 45), "DISCONNECT", _buttonStyle))
                {
                    OnDisconnectClicked();
                }
                GUI.backgroundColor = Color.white;
            }
            
            // Footer
            GUI.Label(new Rect(padding, PANEL_HEIGHT - 35, contentWidth, 20), 
                "Press F10 or ESC to close", _footerStyle);
        }
        
        public void ToggleWindow()
        {
            _showWindow = !_showWindow;
            
            if (_showWindow)
            {
                _animationProgress = 0f;
                DebugLogger.Info("Multiplayer UI opened");
            }
            else
            {
                DebugLogger.Info("Multiplayer UI closed");
            }
        }
        
        private void OnHostGameClicked()
        {
            DebugLogger.Info($"Host Game button clicked (Port: {_port})");
            
            if (!int.TryParse(_port, out int port) || port < 1 || port > 65535)
            {
                _statusText = "Invalid port number (1-65535)";
                return;
            }
            
            _statusText = "Starting server...";
            
            if (NetworkManager.Instance.StartHost(port))
            {
                DebugLogger.Info($"Server started successfully on port {port}");
            }
            else
            {
                _statusText = "Failed to start server";
            }
        }
        
        private void OnJoinGameClicked()
        {
            DebugLogger.Info($"Join Game button clicked (IP: {_ipAddress}, Port: {_port})");
            
            if (string.IsNullOrWhiteSpace(_ipAddress))
            {
                _statusText = "Please enter a server IP address";
                return;
            }
            
            if (!int.TryParse(_port, out int port) || port < 1 || port > 65535)
            {
                _statusText = "Invalid port number (1-65535)";
                return;
            }
            
            _statusText = "Connecting...";
            
            if (NetworkManager.Instance.JoinGame(_ipAddress, port))
            {
                DebugLogger.Info($"Connecting to {_ipAddress}:{port}...");
            }
            else
            {
                _statusText = "Failed to connect";
            }
        }
        
        private void OnDisconnectClicked()
        {
            DebugLogger.Info("Disconnect button clicked");
            NetworkManager.Instance.Disconnect();
            _statusText = "Disconnected";
        }
        
        public void SetStatus(string status)
        {
            _statusText = status;
            DebugLogger.Debug($"Status updated: {status}");
        }
        
        public bool IsWindowVisible => _showWindow;
    }
}
