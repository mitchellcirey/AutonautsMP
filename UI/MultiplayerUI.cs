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
        private const float PANEL_HEIGHT = 480f;
        
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
            
            // Always show the connection HUD when connected (and main UI is closed)
            if (!_showWindow && NetworkManager.Instance.IsConnected)
            {
                DrawConnectionHUD();
            }
            
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
            
            // Block clicks from going through the button
            Event e = Event.current;
            bool mouseOverButton = buttonRect.Contains(e.mousePosition);
            
            if (mouseOverButton && e.type == EventType.MouseDown && e.button == 0)
            {
                ToggleWindow();
                e.Use();
                return;
            }
            
            // Consume any mouse events over the button area
            if (mouseOverButton && e.isMouse)
            {
                e.Use();
            }
            
            // Draw button with custom style
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = _primaryColor;
            
            GUI.Button(buttonRect, "MP", _buttonStyle);
            
            GUI.backgroundColor = originalColor;
        }
        
        private void DrawConnectionHUD()
        {
            // Get all players
            var allPlayers = NetworkManager.Instance.GetAllPlayers();
            int playerCount = allPlayers.Count;
            
            // Calculate HUD size based on player count (show up to 4 players)
            int displayCount = System.Math.Min(playerCount, 4);
            float hudWidth = 220f;
            float baseHeight = 45f;
            float playerRowHeight = 18f;
            float hudHeight = baseHeight + (displayCount * playerRowHeight);
            if (playerCount > 4) hudHeight += 14f; // Extra row for "and X more"
            
            float hudX = (Screen.width - hudWidth) / 2f;
            float hudY = 8f;
            
            Rect hudRect = new Rect(hudX, hudY, hudWidth, hudHeight);
            
            // Block all mouse events on HUD
            Event e = Event.current;
            if (hudRect.Contains(e.mousePosition) && e.isMouse)
            {
                e.Use();
            }
            
            // Semi-transparent background
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(hudRect, _panelTexture);
            
            // Top border accent
            GUI.color = _secondaryColor;
            GUI.DrawTexture(new Rect(hudX, hudY, hudWidth, 2), _accentTexture);
            GUI.color = Color.white;
            
            // Content
            float padding = 10f;
            float y = hudY + 8f;
            float contentWidth = hudWidth - (padding * 2);
            
            // Header row: Status and player count
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _secondaryColor }
            };
            
            string status = NetworkManager.Instance.IsHost ? "HOSTING" : "CONNECTED";
            GUI.Label(new Rect(hudX + padding, y, 80, 16), status, headerStyle);
            
            // Player count on right
            GUIStyle countStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white }
            };
            
            string countText = playerCount == 1 ? "1 Player" : $"{playerCount} Players";
            GUI.Label(new Rect(hudX + padding, y, contentWidth, 16), countText, countStyle);
            y += 22f;
            
            // Separator line
            GUI.color = new Color(1, 1, 1, 0.2f);
            GUI.DrawTexture(new Rect(hudX + padding, y, contentWidth, 1), _panelTexture);
            GUI.color = Color.white;
            y += 6f;
            
            // Player list with names and ping
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            
            GUIStyle pingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.6f, 0.6f, 0.65f) }
            };
            
            GUIStyle hostTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _accentColor }
            };
            
            for (int i = 0; i < displayCount; i++)
            {
                var player = allPlayers[i];
                
                // Name (truncate if too long)
                string displayName = player.Name;
                if (displayName.Length > 16)
                {
                    displayName = displayName.Substring(0, 14) + "..";
                }
                
                // Add host tag
                if (player.IsHost)
                {
                    GUI.Label(new Rect(hudX + padding, y, 40, playerRowHeight), "[HOST]", hostTagStyle);
                    GUI.Label(new Rect(hudX + padding + 42, y, contentWidth - 80, playerRowHeight), displayName, nameStyle);
                }
                else
                {
                    GUI.Label(new Rect(hudX + padding, y, contentWidth - 50, playerRowHeight), displayName, nameStyle);
                }
                
                // Ping (don't show for host, show for clients)
                if (!player.IsHost || !NetworkManager.Instance.IsHost)
                {
                    int ping = player.IsHost ? 0 : player.Ping;
                    
                    // Color code ping
                    Color pingColor;
                    if (ping < 50) pingColor = _secondaryColor;      // Green - good
                    else if (ping < 100) pingColor = _accentColor;   // Orange - okay
                    else pingColor = _dangerColor;                    // Red - bad
                    
                    pingStyle.normal.textColor = pingColor;
                    
                    string pingText = ping > 0 ? $"{ping}ms" : "--";
                    GUI.Label(new Rect(hudX + padding, y, contentWidth, playerRowHeight), pingText, pingStyle);
                }
                
                y += playerRowHeight;
            }
            
            // Show "and X more" if there are more players
            if (playerCount > 4)
            {
                GUIStyle moreStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Italic,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.55f) }
                };
                
                GUI.Label(new Rect(hudX + padding, y, contentWidth, 14), $"and {playerCount - 4} more...", moreStyle);
            }
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
                var allPlayers = NetworkManager.Instance.GetAllPlayers();
                int playerCount = allPlayers.Count;
                
                // Header
                string headerText = NetworkManager.Instance.IsHost ? "HOSTING" : "CONNECTED";
                GUI.Label(new Rect(padding, y, contentWidth / 2, 20), headerText, _labelStyle);
                
                // Player count on right
                GUIStyle rightAlignLabel = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(padding, y, contentWidth, 20), $"{playerCount} Players", rightAlignLabel);
                y += 30;
                
                // Player list box
                float listHeight = System.Math.Min(playerCount, 5) * 24f + 16f;
                GUI.DrawTexture(new Rect(padding, y, contentWidth, listHeight), _inputTexture);
                
                // Player rows
                float rowY = y + 8f;
                GUIStyle playerNameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
                GUIStyle pingDisplayStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleRight
                };
                GUIStyle hostTag = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = _accentColor }
                };
                
                int displayMax = System.Math.Min(playerCount, 5);
                for (int i = 0; i < displayMax; i++)
                {
                    var player = allPlayers[i];
                    
                    // Name with host tag
                    string displayName = player.Name;
                    if (displayName.Length > 18) displayName = displayName.Substring(0, 16) + "..";
                    
                    if (player.IsHost)
                    {
                        GUI.Label(new Rect(padding + 8, rowY, 45, 20), "[HOST]", hostTag);
                        GUI.Label(new Rect(padding + 55, rowY, contentWidth - 120, 20), displayName, playerNameStyle);
                    }
                    else
                    {
                        GUI.Label(new Rect(padding + 8, rowY, contentWidth - 80, 20), displayName, playerNameStyle);
                    }
                    
                    // Ping (color coded)
                    if (!player.IsHost || !NetworkManager.Instance.IsHost)
                    {
                        int ping = player.Ping;
                        Color pingColor = ping < 50 ? _secondaryColor : (ping < 100 ? _accentColor : _dangerColor);
                        pingDisplayStyle.normal.textColor = pingColor;
                        string pingText = ping > 0 ? $"{ping}ms" : "--";
                        GUI.Label(new Rect(padding + 8, rowY, contentWidth - 16, 20), pingText, pingDisplayStyle);
                    }
                    
                    rowY += 24f;
                }
                
                if (playerCount > 5)
                {
                    GUIStyle moreStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 10,
                        fontStyle = FontStyle.Italic,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(0.5f, 0.5f, 0.55f) }
                    };
                    GUI.Label(new Rect(padding, rowY, contentWidth, 16), $"+ {playerCount - 5} more players", moreStyle);
                }
                
                y += listHeight + 15;
                
                // Server info (if hosting)
                if (NetworkManager.Instance.IsHost)
                {
                    GUI.Label(new Rect(padding, y, contentWidth, 18), 
                        $"Port: {_port} - Share your IP to let friends join!", _subtitleStyle);
                    y += 25;
                }
                else
                {
                    // Show our ping to server
                    int clientPing = NetworkManager.Instance.ClientPing;
                    Color pingColor = clientPing < 50 ? _secondaryColor : (clientPing < 100 ? _accentColor : _dangerColor);
                    GUIStyle pingInfoStyle = new GUIStyle(_subtitleStyle) { normal = { textColor = pingColor } };
                    string pingInfo = clientPing > 0 ? $"Your ping: {clientPing}ms" : "Measuring ping...";
                    GUI.Label(new Rect(padding, y, contentWidth, 18), pingInfo, pingInfoStyle);
                    y += 25;
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
