using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace AutonautsMP
{
    /// <summary>
    /// Handles synchronization of the local farmer's position and
    /// rendering of remote farmer ghosts.
    /// </summary>
    public class FarmerSync : MonoBehaviour
    {
        public static FarmerSync? Instance { get; private set; }

        // Local farmer tracking
        private object? _localFarmer;
        private Type? _farmerType;
        private PropertyInfo? _positionProperty;
        private FieldInfo? _transformField;
        private Transform? _localFarmerTransform;
        
        // Position sending
        private float _lastSendTime;
        private const float SendInterval = 0.1f; // 10Hz
        private Vector3 _lastSentPosition;
        private float _lastSentRotation;

        // Remote farmers
        private readonly Dictionary<int, RemoteFarmer> _remoteFarmers = new Dictionary<int, RemoteFarmer>();

        // Network reference (set by Plugin)
        private object? _network;
        private MethodInfo? _sendPositionMethod;
        private MethodInfo? _getPlayersMethod;
        private MethodInfo? _isConnectedMethod;
        private MethodInfo? _isHostMethod;
        private int _localPlayerId;

        // Harmony instance
        private Harmony? _harmony;

        private void Awake()
        {
            Instance = this;
            Plugin.Instance?.Log("FarmerSync initialized");
        }

        private void Start()
        {
            // Try to find farmer types via reflection
            FindFarmerType();
            
            // Apply Harmony patches
            ApplyPatches();
        }

        private void FindFarmerType()
        {
            try
            {
                // Look for the Farmer class in the game assembly
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "Assembly-CSharp")
                    {
                        // Try to find Farmer type
                        _farmerType = asm.GetType("Farmer");
                        if (_farmerType != null)
                        {
                            Plugin.Instance?.Log("Found Farmer type: " + _farmerType.FullName);
                            
                            // Find useful properties/fields
                            _transformField = _farmerType.GetField("m_ModelRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            if (_transformField == null)
                            {
                                // Try to find transform another way - check base class
                                var baseType = _farmerType.BaseType;
                                while (baseType != null)
                                {
                                    _transformField = baseType.GetField("m_ModelRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                                    if (_transformField != null) break;
                                    baseType = baseType.BaseType;
                                }
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log("Error finding Farmer type: " + ex.Message);
            }
        }

        private void ApplyPatches()
        {
            try
            {
                _harmony = new Harmony("com.autonautsmp.farmersync");
                // Patches will be applied as needed when we understand the game better
                Plugin.Instance?.Log("Harmony patches ready");
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log("Error applying Harmony patches: " + ex.Message);
            }
        }

        public void SetNetwork(object network, MethodInfo sendPosition, MethodInfo getPlayers, MethodInfo isConnected, MethodInfo isHost)
        {
            _network = network;
            _sendPositionMethod = sendPosition;
            _getPlayersMethod = getPlayers;
            _isConnectedMethod = isConnected;
            _isHostMethod = isHost;
        }

        public void SetLocalPlayerId(int id)
        {
            _localPlayerId = id;
        }

        private void Update()
        {
            // Try to find local farmer if not found
            if (_localFarmerTransform == null)
            {
                TryFindLocalFarmer();
            }

            // Send position if connected
            if (_localFarmerTransform != null && _network != null && _sendPositionMethod != null)
            {
                bool isConnected = false;
                try
                {
                    isConnected = _isConnectedMethod != null && (bool)_isConnectedMethod.Invoke(_network, null)!;
                }
                catch { }

                if (isConnected && Time.time - _lastSendTime >= SendInterval)
                {
                    SendLocalPosition();
                    _lastSendTime = Time.time;
                }
            }

            // Update remote farmers
            UpdateRemoteFarmers();
        }

        private void TryFindLocalFarmer()
        {
            try
            {
                // Method 1: Find by type name
                if (_farmerType != null)
                {
                    var farmers = FindObjectsOfType(_farmerType);
                    if (farmers.Length > 0)
                    {
                        _localFarmer = farmers[0];
                        
                        // Get the Transform component
                        if (_localFarmer is Component comp)
                        {
                            _localFarmerTransform = comp.transform;
                            Plugin.Instance?.Log("Found local farmer via type!");
                        }
                    }
                }

                // Method 2: Find by GameObject name pattern
                if (_localFarmerTransform == null)
                {
                    var allObjects = FindObjectsOfType<Transform>();
                    foreach (var t in allObjects)
                    {
                        if (t.name.Contains("Farmer") && t.gameObject.activeInHierarchy)
                        {
                            // Check if this looks like the player farmer
                            var go = t.gameObject;
                            // Look for components that indicate this is the player
                            if (go.GetComponent<Animator>() != null || go.GetComponentInChildren<Animator>() != null)
                            {
                                _localFarmerTransform = t;
                                Plugin.Instance?.Log("Found local farmer via name search: " + t.name);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log("Error finding farmer: " + ex.Message);
            }
        }

        private void SendLocalPosition()
        {
            if (_localFarmerTransform == null || _sendPositionMethod == null || _network == null) return;

            Vector3 pos = _localFarmerTransform.position;
            float rot = _localFarmerTransform.eulerAngles.y;

            // Only send if changed significantly
            if (Vector3.Distance(pos, _lastSentPosition) > 0.01f || Mathf.Abs(rot - _lastSentRotation) > 1f)
            {
                try
                {
                    _sendPositionMethod.Invoke(_network, new object[] { pos.x, pos.y, pos.z, rot, (byte)0 });
                    _lastSentPosition = pos;
                    _lastSentRotation = rot;
                }
                catch (Exception ex)
                {
                    Plugin.Instance?.Log("Error sending position: " + ex.Message);
                }
            }
        }

        private void UpdateRemoteFarmers()
        {
            if (_network == null || _getPlayersMethod == null) return;

            try
            {
                var players = (object[]?)_getPlayersMethod.Invoke(_network, null);
                if (players == null) return;

                // Update or create remote farmer for each player
                foreach (var playerObj in players)
                {
                    if (playerObj == null) continue;

                    var playerType = playerObj.GetType();
                    var idField = playerType.GetField("Id");
                    var nameField = playerType.GetField("Name");
                    var xField = playerType.GetField("X");
                    var yField = playerType.GetField("Y");
                    var zField = playerType.GetField("Z");
                    var rotField = playerType.GetField("Rotation");

                    if (idField == null) continue;

                    int playerId = (int)idField.GetValue(playerObj)!;

                    // Skip self
                    if (playerId == _localPlayerId) continue;

                    string name = nameField != null ? (string)nameField.GetValue(playerObj)! : "Player";
                    float x = xField != null ? (float)xField.GetValue(playerObj)! : 0;
                    float y = yField != null ? (float)yField.GetValue(playerObj)! : 0;
                    float z = zField != null ? (float)zField.GetValue(playerObj)! : 0;
                    float rot = rotField != null ? (float)rotField.GetValue(playerObj)! : 0;

                    // Skip if no valid position yet
                    if (x == 0 && y == 0 && z == 0) continue;

                    // Create or update remote farmer
                    if (!_remoteFarmers.TryGetValue(playerId, out var remoteFarmer))
                    {
                        remoteFarmer = CreateRemoteFarmer(playerId, name);
                        _remoteFarmers[playerId] = remoteFarmer;
                    }

                    // Update position with interpolation
                    remoteFarmer.UpdatePosition(x, y, z, rot);
                    remoteFarmer.SetName(name);
                }

                // Remove disconnected players
                var toRemove = new List<int>();
                foreach (var kvp in _remoteFarmers)
                {
                    bool found = false;
                    foreach (var playerObj in players)
                    {
                        if (playerObj == null) continue;
                        var idField = playerObj.GetType().GetField("Id");
                        if (idField != null && (int)idField.GetValue(playerObj)! == kvp.Key)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) toRemove.Add(kvp.Key);
                }
                foreach (var id in toRemove)
                {
                    if (_remoteFarmers.TryGetValue(id, out var rf))
                    {
                        rf.Destroy();
                        _remoteFarmers.Remove(id);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log("Error updating remote farmers: " + ex.Message);
            }
        }

        private RemoteFarmer CreateRemoteFarmer(int playerId, string name)
        {
            Plugin.Instance?.Log($"Creating remote farmer for player {playerId}: {name}");
            return new RemoteFarmer(playerId, name, _localFarmerTransform);
        }

        private void OnDestroy()
        {
            // Clean up remote farmers
            foreach (var rf in _remoteFarmers.Values)
            {
                rf.Destroy();
            }
            _remoteFarmers.Clear();

            // Clean up Harmony
            _harmony?.UnpatchSelf();
        }
    }

    /// <summary>
    /// Represents a remote player's farmer ghost
    /// </summary>
    public class RemoteFarmer
    {
        public int PlayerId { get; }
        public string Name { get; private set; }

        private GameObject? _ghost;
        private Transform? _transform;
        private Vector3 _targetPosition;
        private float _targetRotation;
        private TextMesh? _nameLabel;

        public RemoteFarmer(int playerId, string name, Transform? templateFarmer)
        {
            PlayerId = playerId;
            Name = name;

            CreateGhost(templateFarmer);
        }

        private void CreateGhost(Transform? template)
        {
            // Create a simple visual representation
            // In a full implementation, we'd clone the farmer prefab
            _ghost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _ghost.name = $"RemoteFarmer_{PlayerId}";
            
            // Remove collider so it doesn't interfere with gameplay
            var collider = _ghost.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            // Scale to roughly human size
            _ghost.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            // Color it differently to distinguish from local player
            var renderer = _ghost.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.2f, 0.6f, 1f, 0.8f); // Light blue
            }

            _transform = _ghost.transform;

            // Add name label above head
            CreateNameLabel();

            Plugin.Instance?.Log($"Created ghost for remote farmer {PlayerId}");
        }

        private void CreateNameLabel()
        {
            if (_ghost == null) return;

            var labelGo = new GameObject($"NameLabel_{PlayerId}");
            labelGo.transform.SetParent(_ghost.transform);
            labelGo.transform.localPosition = new Vector3(0, 1.5f, 0);
            labelGo.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

            _nameLabel = labelGo.AddComponent<TextMesh>();
            _nameLabel.text = Name;
            _nameLabel.fontSize = 48;
            _nameLabel.alignment = TextAlignment.Center;
            _nameLabel.anchor = TextAnchor.MiddleCenter;
            _nameLabel.color = Color.white;
            _nameLabel.characterSize = 0.1f;

            // Make it billboard (always face camera)
            labelGo.AddComponent<BillboardText>();
        }

        public void UpdatePosition(float x, float y, float z, float rotation)
        {
            _targetPosition = new Vector3(x, y, z);
            _targetRotation = rotation;

            if (_transform != null)
            {
                // Smooth interpolation
                _transform.position = Vector3.Lerp(_transform.position, _targetPosition, Time.deltaTime * 10f);
                
                var currentRot = _transform.eulerAngles;
                currentRot.y = Mathf.LerpAngle(currentRot.y, _targetRotation, Time.deltaTime * 10f);
                _transform.eulerAngles = currentRot;
            }
        }

        public void SetName(string name)
        {
            Name = name;
            if (_nameLabel != null)
            {
                _nameLabel.text = name;
            }
        }

        public void Destroy()
        {
            if (_ghost != null)
            {
                UnityEngine.Object.Destroy(_ghost);
                _ghost = null;
            }
        }
    }

    /// <summary>
    /// Makes text always face the camera
    /// </summary>
    public class BillboardText : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
                    cam.transform.rotation * Vector3.up);
            }
        }
    }
}
