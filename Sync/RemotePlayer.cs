using UnityEngine;
using AutonautsMP.Core;

namespace AutonautsMP.Sync
{
    /// <summary>
    /// Represents a remote player in the game world.
    /// Creates and manages a visual "ghost" representation.
    /// </summary>
    public class RemotePlayer
    {
        /// <summary>
        /// The network player ID.
        /// </summary>
        public int PlayerId { get; private set; }

        /// <summary>
        /// The player's display name.
        /// </summary>
        public string PlayerName { get; private set; }

        /// <summary>
        /// The GameObject representing this remote player.
        /// </summary>
        public GameObject Ghost { get; private set; }

        /// <summary>
        /// Current interpolated position.
        /// </summary>
        public Vector3 CurrentPosition { get; private set; }

        /// <summary>
        /// Current interpolated rotation (Y axis).
        /// </summary>
        public float CurrentRotation { get; private set; }

        /// <summary>
        /// Target position to interpolate towards.
        /// </summary>
        public Vector3 TargetPosition { get; private set; }

        /// <summary>
        /// Target rotation to interpolate towards.
        /// </summary>
        public float TargetRotation { get; private set; }

        /// <summary>
        /// Time of last position update.
        /// </summary>
        public float LastUpdateTime { get; private set; }

        // Interpolation settings
        private const float INTERPOLATION_SPEED = 15f;
        private const float ROTATION_SPEED = 360f; // Degrees per second
        private const float TELEPORT_THRESHOLD = 10f; // Teleport if distance > this

        // Ghost visual settings
        private static readonly Color GhostColor = new Color(0.3f, 0.7f, 1f, 0.7f); // Semi-transparent blue

        public RemotePlayer(int playerId, string playerName)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            LastUpdateTime = Time.realtimeSinceStartup;

            CreateGhost();
        }

        /// <summary>
        /// Create the visual ghost representation.
        /// </summary>
        private void CreateGhost()
        {
            // Try to clone the local player's appearance
            GameObject template = FindPlayerTemplate();

            if (template != null)
            {
                Ghost = Object.Instantiate(template);
                Ghost.name = $"RemotePlayer_{PlayerId}_{PlayerName}";
                
                // Disable any AI/input components
                DisablePlayerComponents(Ghost);
                
                // Make it visually distinct
                ApplyGhostMaterial(Ghost);
                
                DebugLogger.Info($"Created remote player ghost from template: {Ghost.name}");
            }
            else
            {
                // Create a simple capsule placeholder
                Ghost = CreatePlaceholderGhost();
                DebugLogger.Info($"Created placeholder ghost for remote player: {PlayerName}");
            }

            // Add name label
            AddNameLabel();
        }

        /// <summary>
        /// Try to find a player template to clone.
        /// </summary>
        private GameObject FindPlayerTemplate()
        {
            // Look for existing player/farmer object
            var playerNames = new[] { "Farmer", "Player", "LocalPlayer", "FarmerPlayer" };
            
            foreach (var name in playerNames)
            {
                var obj = GameObject.Find(name);
                if (obj != null && obj.activeInHierarchy)
                {
                    return obj;
                }
            }

            // Try tagged player
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
            {
                return tagged;
            }

            return null;
        }

        /// <summary>
        /// Disable AI, input, and other gameplay components on the ghost.
        /// </summary>
        private void DisablePlayerComponents(GameObject ghost)
        {
            // Common component names to disable
            var componentsToDisable = new[]
            {
                "PlayerController", "FarmerController", "AIController",
                "CharacterController", "NavMeshAgent", "Rigidbody",
                "PlayerInput", "InputHandler", "AudioSource",
                "Collider", "BoxCollider", "SphereCollider", "CapsuleCollider"
            };

            var components = ghost.GetComponentsInChildren<Component>(true);
            foreach (var comp in components)
            {
                if (comp == null) continue;
                
                string typeName = comp.GetType().Name;
                foreach (var compName in componentsToDisable)
                {
                    if (typeName.Contains(compName))
                    {
                        // Try to disable via Behaviour
                        if (comp is Behaviour behaviour)
                        {
                            behaviour.enabled = false;
                        }
                        else
                        {
                            // Use reflection to disable colliders and rigidbodies
                            TryDisableComponent(comp);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Try to disable a component using reflection (for physics types we don't have references to).
        /// </summary>
        private void TryDisableComponent(Component comp)
        {
            try
            {
                var type = comp.GetType();
                
                // Try to set "enabled" property (for colliders)
                var enabledProp = type.GetProperty("enabled");
                if (enabledProp != null && enabledProp.CanWrite)
                {
                    enabledProp.SetValue(comp, false);
                    return;
                }

                // Try to set "isKinematic" property (for rigidbodies)
                var kinematicProp = type.GetProperty("isKinematic");
                if (kinematicProp != null && kinematicProp.CanWrite)
                {
                    kinematicProp.SetValue(comp, true);
                }
            }
            catch
            {
                // Silently ignore if we can't disable
            }
        }

        /// <summary>
        /// Apply a ghost-like material to make remote player visually distinct.
        /// </summary>
        private void ApplyGhostMaterial(GameObject ghost)
        {
            var renderers = ghost.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                // Create a semi-transparent material
                foreach (var mat in renderer.materials)
                {
                    if (mat != null)
                    {
                        // Tint the material
                        if (mat.HasProperty("_Color"))
                        {
                            Color originalColor = mat.color;
                            mat.color = Color.Lerp(originalColor, GhostColor, 0.5f);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Create a simple capsule placeholder when template isn't found.
        /// </summary>
        private GameObject CreatePlaceholderGhost()
        {
            var ghost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ghost.name = $"RemotePlayer_{PlayerId}_{PlayerName}";
            
            // Scale to roughly human size
            ghost.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            
            // Disable collider using reflection (to avoid PhysicsModule dependency)
            var components = ghost.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp != null && comp.GetType().Name.Contains("Collider"))
                {
                    TryDisableComponent(comp);
                }
            }

            // Apply ghost material
            var renderer = ghost.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = GhostColor;
            }

            return ghost;
        }

        /// <summary>
        /// Add a floating name label above the ghost.
        /// </summary>
        private void AddNameLabel()
        {
            // Create a simple text mesh for the name
            var labelObj = new GameObject($"NameLabel_{PlayerName}");
            labelObj.transform.SetParent(Ghost.transform);
            labelObj.transform.localPosition = new Vector3(0, 2.5f, 0);
            
            // Add TextMesh component
            var textMesh = labelObj.AddComponent<TextMesh>();
            textMesh.text = PlayerName;
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.1f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
            
            // Make it billboard (face camera) - will need update in Update()
            labelObj.AddComponent<BillboardLabel>();
        }

        /// <summary>
        /// Set the target position and rotation for interpolation.
        /// </summary>
        public void SetTargetPosition(Vector3 position, float rotation)
        {
            TargetPosition = position;
            TargetRotation = rotation;
            LastUpdateTime = Time.realtimeSinceStartup;

            // Teleport if too far away
            if (Ghost != null)
            {
                float distance = Vector3.Distance(CurrentPosition, position);
                if (distance > TELEPORT_THRESHOLD || CurrentPosition == Vector3.zero)
                {
                    CurrentPosition = position;
                    CurrentRotation = rotation;
                    Ghost.transform.position = position;
                    Ghost.transform.rotation = Quaternion.Euler(0, rotation, 0);
                }
            }
        }

        /// <summary>
        /// Update interpolation. Call every frame.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (Ghost == null)
                return;

            // Interpolate position
            CurrentPosition = Vector3.Lerp(CurrentPosition, TargetPosition, deltaTime * INTERPOLATION_SPEED);
            Ghost.transform.position = CurrentPosition;

            // Interpolate rotation
            float rotDelta = Mathf.DeltaAngle(CurrentRotation, TargetRotation);
            float maxRotate = ROTATION_SPEED * deltaTime;
            CurrentRotation += Mathf.Clamp(rotDelta, -maxRotate, maxRotate);
            Ghost.transform.rotation = Quaternion.Euler(0, CurrentRotation, 0);
        }

        /// <summary>
        /// Destroy the ghost GameObject.
        /// </summary>
        public void Destroy()
        {
            if (Ghost != null)
            {
                Object.Destroy(Ghost);
                Ghost = null;
            }
        }
    }

    /// <summary>
    /// Simple component to make text always face the camera.
    /// </summary>
    public class BillboardLabel : MonoBehaviour
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
