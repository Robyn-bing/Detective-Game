using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace DetectiveGame.Player
{
    /// <summary>Switches the existing Starter Assets character between full-body first and third person.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ThirdPersonController), typeof(StarterAssetsInputs), typeof(PlayerInput))]
    public sealed class FullBodyViewController : MonoBehaviour
    {
        [Header("View Mode")]
        [SerializeField, Tooltip("Checked: full-body first person. Unchecked: original third person. Works during Play Mode.")]
        private bool useFirstPerson;

        [Header("Scene References")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private CinemachineVirtualCameraBase thirdPersonCamera;
        [SerializeField] private CinemachineCamera firstPersonCamera;
        [SerializeField] private SkinnedMeshRenderer fullBodyRenderer;
        [SerializeField, Tooltip("Generated headless copy. The original FBX is never modified.")]
        private Mesh firstPersonBodyMesh;

        [Header("First Person Lens")]
        [SerializeField, Min(0.1f)] private float eyeHeight = 1.66f;
        [SerializeField, Tooltip("Offset from the root at eye height. Slightly ahead of the chest so looking down sees its outside surface.")]
        private float forwardOffset = 0.22f;
        [SerializeField, Range(40f, 100f)] private float fieldOfView = 75f;
        [SerializeField, Range(0.01f, 0.1f)] private float nearClipPlane = 0.04f;
        [SerializeField, Range(0f, 89f)] private float lookUpLimit = 80f;
        [SerializeField, Range(0f, 89f)] private float lookDownLimit = 85f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 1f;
        [SerializeField, Min(0f)] private float gamepadLookSpeed = 120f;

        [Header("Camera Collision")]
        [SerializeField, Tooltip("World geometry only; the player's own layer is automatically excluded.")]
        private LayerMask cameraCollisionLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.01f)] private float cameraClearance = 0.1f;

        private ThirdPersonController movement;
        private StarterAssetsInputs input;
        private PlayerInput playerInput;
        private Animator animator;
        private SkinnedMeshRenderer firstPersonBody;
        private ShadowCastingMode originalShadows;
        private AnimatorCullingMode originalCulling;
        private bool originalRendererEnabled;
        private bool initialized;
        private bool modeApplied;
        private bool appliedFirstPerson;
        private float yaw;
        private float pitch;

        public bool UseFirstPerson
        {
            get => useFirstPerson;
            set => useFirstPerson = value;
        }

        public bool IsFirstPerson => initialized && modeApplied && appliedFirstPerson;

        private void Awake()
        {
            movement = GetComponent<ThirdPersonController>();
            input = GetComponent<StarterAssetsInputs>();
            playerInput = GetComponent<PlayerInput>();
            animator = GetComponent<Animator>();
            if (mainCamera == null) mainCamera = Camera.main;

            if (mainCamera == null || thirdPersonCamera == null || firstPersonCamera == null ||
                fullBodyRenderer == null || firstPersonBodyMesh == null)
            {
                Debug.LogError("Full Body View Controller needs both cameras, the full body renderer and its generated body mesh.", this);
                enabled = false;
                return;
            }

            originalShadows = fullBodyRenderer.shadowCastingMode;
            originalRendererEnabled = fullBodyRenderer.enabled;
            if (animator != null) originalCulling = animator.cullingMode;
            CreateBodyRenderer();
            initialized = true;
        }

        private void Start() => ApplyMode(true);

        private void Update()
        {
            if (!initialized) return;
            if (!modeApplied || useFirstPerson != appliedFirstPerson) ApplyMode(false);
            if (!appliedFirstPerson) return;

            if (!movement.LockCameraPosition && input.cursorInputForLook)
            {
                // Input System mouse delta is already per frame. Stick deflection is degrees/second.
                float multiplier = playerInput.currentControlScheme == "KeyboardMouse"
                    ? mouseSensitivity : gamepadLookSpeed * Time.deltaTime;
                yaw = Mathf.Repeat(yaw + input.look.x * multiplier, 360f);
                pitch += input.look.y * multiplier;
            }
            pitch = Mathf.Clamp(pitch, -lookUpLimit, lookDownLimit);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void LateUpdate()
        {
            if (!IsFirstPerson) return;
            UpdateFirstPersonCamera();
            for (int i = 0; i < firstPersonBodyMesh.blendShapeCount; i++)
                firstPersonBody.SetBlendShapeWeight(i, fullBodyRenderer.GetBlendShapeWeight(i));
        }

        private void ApplyMode(bool starting)
        {
            if (!initialized) return;
            if (useFirstPerson)
            {
                // At spawn use the character's facing, not a potentially stale editor camera transform.
                yaw = starting ? transform.eulerAngles.y : mainCamera.transform.eulerAngles.y;
                pitch = starting ? 0f : Mathf.DeltaAngle(0f, mainCamera.transform.eulerAngles.x);
                pitch = Mathf.Clamp(pitch, -lookUpLimit, lookDownLimit);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                firstPersonCamera.Priority = thirdPersonCamera.Priority.Value + 1;
                UpdateFirstPersonCamera();
                firstPersonCamera.PreviousStateIsValid = false;
                fullBodyRenderer.enabled = true;
                // Keep the complete silhouette in shadows, including the head.
                fullBodyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                firstPersonBody.enabled = true;
                if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            else
            {
                if (modeApplied && appliedFirstPerson)
                    movement.SetThirdPersonLook(yaw, pitch);
                thirdPersonCamera.PreviousStateIsValid = false;
                RestoreBody();
            }
            firstPersonCamera.enabled = useFirstPerson;
            movement.SetFirstPersonMovement(useFirstPerson);
            appliedFirstPerson = useFirstPerson;
            modeApplied = true;
        }

        private void UpdateFirstPersonCamera()
        {
            // Root-relative height deliberately ignores animated head translation and rotation.
            Vector3 anchor = transform.TransformPoint(new Vector3(0f, eyeHeight, 0f));
            Vector3 offset = transform.TransformVector(new Vector3(0f, 0f, forwardOffset));
            float distance = offset.magnitude;
            int mask = cameraCollisionLayers.value & ~(1 << gameObject.layer);
            if (distance > 0.001f && Physics.SphereCast(anchor, cameraClearance, offset / distance,
                    out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
                offset = offset.normalized * Mathf.Max(0f, hit.distance - 0.01f);

            firstPersonCamera.transform.SetPositionAndRotation(anchor + offset, Quaternion.Euler(pitch, yaw, 0f));
            var lens = firstPersonCamera.Lens;
            lens.FieldOfView = fieldOfView;
            lens.NearClipPlane = nearClipPlane;
            firstPersonCamera.Lens = lens;
        }

        private void CreateBodyRenderer()
        {
            var body = new GameObject("FirstPersonBody (Runtime)");
            body.layer = fullBodyRenderer.gameObject.layer;
            body.transform.SetParent(fullBodyRenderer.transform, false);
            firstPersonBody = body.AddComponent<SkinnedMeshRenderer>();
            firstPersonBody.sharedMesh = firstPersonBodyMesh;
            firstPersonBody.sharedMaterials = fullBodyRenderer.sharedMaterials;
            firstPersonBody.bones = fullBodyRenderer.bones;
            firstPersonBody.rootBone = fullBodyRenderer.rootBone;
            firstPersonBody.localBounds = fullBodyRenderer.localBounds;
            firstPersonBody.quality = fullBodyRenderer.quality;
            firstPersonBody.updateWhenOffscreen = true;
            firstPersonBody.shadowCastingMode = ShadowCastingMode.Off;
            firstPersonBody.receiveShadows = fullBodyRenderer.receiveShadows;
            firstPersonBody.lightProbeUsage = fullBodyRenderer.lightProbeUsage;
            firstPersonBody.reflectionProbeUsage = fullBodyRenderer.reflectionProbeUsage;
            firstPersonBody.probeAnchor = fullBodyRenderer.probeAnchor;
            firstPersonBody.enabled = false;
        }

        private void RestoreBody()
        {
            if (firstPersonBody != null) firstPersonBody.enabled = false;
            if (fullBodyRenderer != null)
            {
                fullBodyRenderer.enabled = originalRendererEnabled;
                fullBodyRenderer.shadowCastingMode = originalShadows;
            }
            if (animator != null) animator.cullingMode = originalCulling;
        }

        private void OnDisable()
        {
            if (!initialized) return;
            if (appliedFirstPerson && movement != null) movement.SetThirdPersonLook(yaw, pitch);
            if (movement != null) movement.SetFirstPersonMovement(false);
            if (firstPersonCamera != null) firstPersonCamera.enabled = false;
            RestoreBody();
            modeApplied = false;
        }

        private void OnDestroy()
        {
            if (firstPersonBody != null) Destroy(firstPersonBody.gameObject);
        }
    }
}
