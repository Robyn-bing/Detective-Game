using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Recall
{
    [DisallowMultipleComponent]
    public sealed class RecallSessionController : MonoBehaviour
    {
        public enum SessionState { Idle, SelectingPeriod, TransitioningIn, Paused, Playing, TransitioningOut }

        [Header("Scene References")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private StarterAssetsInputs starterInputs;
        [SerializeField] private RecallRuntimeUI ui;

        [Header("Preview")]
        [SerializeField] private int previewLayer = 2;
        [SerializeField, Min(0.01f)] private float previewNearClip = 0.03f;

        private readonly InputAction playPauseAction = new InputAction("Recall Play Pause", InputActionType.Button, "<Keyboard>/space");
        private readonly InputAction cancelAction = new InputAction("Recall Cancel", InputActionType.Button, "<Keyboard>/escape");
        private readonly InputAction previousAction = new InputAction("Previous Recall Period", InputActionType.Button, "<Keyboard>/leftArrow");
        private readonly InputAction nextAction = new InputAction("Next Recall Period", InputActionType.Button, "<Keyboard>/rightArrow");
        private readonly InputAction submitAction = new InputAction("Select Recall Period", InputActionType.Button, "<Keyboard>/enter");
        private readonly InputAction pointerPositionAction = new InputAction("Recall Pointer Position", InputActionType.Value, "<Pointer>/position");
        private readonly InputAction pointerPressAction = new InputAction("Recall Pointer Press", InputActionType.Button, "<Pointer>/press");

        private CinemachineBrain brain;
        private RecallableObject target;
        private RecallObjectData data;
        private RecallPeriod period;
        private GameObject stage;
        private GameObject preview;
        private float clipTime;
        private int selectedAvailablePeriod;
        private bool draggingTimeline;
        private float transitionElapsed;
        private Vector3 transitionStartPosition;
        private Quaternion transitionStartRotation;
        private float transitionStartFieldOfView;
        private Vector3 previewCameraPosition;
        private Quaternion previewCameraRotation;
        private Renderer[] hiddenTargetRenderers;
        private bool[] hiddenTargetRendererStates;
        private Renderer[] hiddenPlayerRenderers;
        private bool[] hiddenPlayerRendererStates;

        private float previousTimeScale;
        private bool previousPlayerInputEnabled;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private int previousCullingMask;
        private CameraClearFlags previousClearFlags;
        private Color previousBackground;
        private float previousFieldOfView;
        private float previousNearClip;
        private float previousFarClip;
        private bool previousOrthographic;
        private float previousOrthographicSize;
        private Vector3 previousCameraPosition;
        private Quaternion previousCameraRotation;
        private bool previousBrainEnabled;

        public SessionState State { get; private set; }
        public bool IsActive => State != SessionState.Idle;
        public float NormalizedTime => period?.AnimationClip == null || period.AnimationClip.length <= 0f
            ? 0f : Mathf.Clamp01(clipTime / period.AnimationClip.length);
        public GameObject ActivePreview => preview;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null) brain = mainCamera.GetComponent<CinemachineBrain>();
            if (ui == null) ui = GetComponent<RecallRuntimeUI>();
            State = SessionState.Idle;
        }

        private void OnEnable()
        {
            EnableActions(true);
            playPauseAction.performed += OnPlayPause;
            cancelAction.performed += OnCancel;
            previousAction.performed += OnPrevious;
            nextAction.performed += OnNext;
            submitAction.performed += OnSubmit;
        }

        private void OnDisable()
        {
            playPauseAction.performed -= OnPlayPause;
            cancelAction.performed -= OnCancel;
            previousAction.performed -= OnPrevious;
            nextAction.performed -= OnNext;
            submitAction.performed -= OnSubmit;
            EnableActions(false);
            if (IsActive) CompleteExitRecall();
        }

        private void OnDestroy()
        {
            playPauseAction.Dispose();
            cancelAction.Dispose();
            previousAction.Dispose();
            nextAction.Dispose();
            submitAction.Dispose();
            pointerPositionAction.Dispose();
            pointerPressAction.Dispose();
        }

        private void Update()
        {
            if (!IsActive) return;
            if (State == SessionState.SelectingPeriod)
            {
                HandlePeriodPointer();
                return;
            }

            if (State == SessionState.TransitioningIn)
            {
                UpdateSynchronizedEntry();
                ui.UpdatePlayback(State, period, NormalizedTime);
                return;
            }
            if (State == SessionState.TransitioningOut)
            {
                UpdateCameraReturnTransition();
                return;
            }

            HandleTimelinePointer();
            if (draggingTimeline) return;
            if (State == SessionState.Playing)
            {
                clipTime = Mathf.Min(period.AnimationClip.length, clipTime + Time.unscaledDeltaTime * period.PlaybackSpeed);
                SampleCurrentTime();
                if (clipTime >= period.AnimationClip.length) State = SessionState.Paused;
            }
            ui.UpdatePlayback(State, period, NormalizedTime);
        }

        public void SetWorldPrompt(RecallableObject recallable)
        {
            if (ui != null && !IsActive)
                ui.SetWorldPrompt(recallable);
        }

        public void BeginRecall(RecallableObject recallable)
        {
            if (IsActive || recallable == null || recallable.Data == null || recallable.Data.AvailablePeriodCount == 0) return;
            target = recallable;
            data = target.Data;
            FreezeGameplay();
            ui.SetWorldPrompt(null);
            if (data.AvailablePeriodCount == 1)
                StartPeriod(data.GetAvailablePeriodIndex(0));
            else
            {
                selectedAvailablePeriod = 0;
                State = SessionState.SelectingPeriod;
                ui.ShowPeriodSelection(data, selectedAvailablePeriod);
            }
        }

        public void SelectAvailablePeriod(int availableIndex)
        {
            if (State != SessionState.SelectingPeriod || data == null) return;
            int dataIndex = data.GetAvailablePeriodIndex(Mathf.Clamp(availableIndex, 0, data.AvailablePeriodCount - 1));
            if (dataIndex >= 0) StartPeriod(dataIndex);
        }

        public void TogglePlayback()
        {
            if (State != SessionState.Paused && State != SessionState.Playing) return;
            if (State == SessionState.Playing) State = SessionState.Paused;
            else
            {
                if (clipTime >= period.AnimationClip.length) clipTime = 0f;
                State = SessionState.Playing;
            }
            ui.UpdatePlayback(State, period, NormalizedTime);
        }

        public void SetNormalizedTime(float value)
        {
            if (period?.AnimationClip == null || (State != SessionState.Paused && State != SessionState.Playing)) return;
            State = SessionState.Paused;
            clipTime = Mathf.Clamp01(value) * period.AnimationClip.length;
            SampleCurrentTime();
            ui.UpdatePlayback(State, period, NormalizedTime);
        }

        public void ExitRecall()
        {
            if (!IsActive) return;
            if (State == SessionState.SelectingPeriod)
            {
                CompleteExitRecall();
                return;
            }
            if (State == SessionState.TransitioningOut) return;
            draggingTimeline = false;
            transitionStartPosition = mainCamera.transform.position;
            transitionStartRotation = mainCamera.transform.rotation;
            transitionStartFieldOfView = mainCamera.fieldOfView;
            transitionElapsed = 0f;
            State = SessionState.TransitioningOut;
            if (ui != null) ui.HideRecall();
        }

        private void CompleteExitRecall()
        {
            if (!IsActive) return;
            State = SessionState.Idle;
            draggingTimeline = false;
            if (stage != null) Destroy(stage);
            stage = null;
            preview = null;
            period = null;
            RestoreOriginalTarget();
            RestoreCamera();
            RestorePlayerVisuals();
            Time.timeScale = previousTimeScale;
            if (playerInput != null)
            {
                playerInput.enabled = previousPlayerInputEnabled;
                if (previousPlayerInputEnabled && playerInput.isActiveAndEnabled) playerInput.ActivateInput();
            }
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            if (ui != null) ui.HideRecall();
            data = null;
            target = null;
        }

        private void StartPeriod(int dataIndex)
        {
            period = data.GetPeriod(dataIndex);
            if (period == null || period.AnimationClip == null || data.PreviewPrefab == null)
            {
                Debug.LogError($"Recall data '{data.name}' has an incomplete preview period.", data);
                CompleteExitRecall();
                return;
            }
            CreatePreview();
            clipTime = period.AnimationClip.length;
            SampleCurrentTime();
            FramePreviewCamera();
            HidePlayerVisuals();
            BeginSynchronizedEntry();
            ui.ShowPlayback(data, period);
            ui.UpdatePlayback(State, period, NormalizedTime);
        }

        private void CreatePreview()
        {
            stage = new GameObject("Recall Preview Stage (Runtime)");
            stage.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            preview = Instantiate(data.PreviewPrefab, stage.transform);
            preview.name = data.PreviewPrefab.name;
            preview.transform.localPosition = Vector3.zero;
            preview.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(stage, previewLayer);
            foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in preview.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (Animator animator in preview.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Animation animation in preview.GetComponentsInChildren<Animation>(true)) animation.enabled = false;

            HideOriginalTarget();

            var lightObject = new GameObject("Recall Key Light");
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localPosition = new Vector3(-1.5f, 2.5f, -2f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 4f;
            light.color = new Color(0.82f, 0.9f, 1f);
            light.cullingMask = 1 << previewLayer;
            SetLayerRecursively(lightObject, previewLayer);
        }

        private void FramePreviewCamera()
        {
            float endTime = clipTime;
            Bounds bounds = default;
            const int framingSamples = 24;
            for (int i = 0; i <= framingSamples; i++)
            {
                clipTime = period.AnimationClip.length * i / framingSamples;
                SampleCurrentTime();
                Bounds sampled = GetPreviewBounds();
                if (i == 0) bounds = sampled;
                else bounds.Encapsulate(sampled);
            }
            clipTime = endTime;
            SampleCurrentTime();

            previewCameraRotation = Quaternion.Euler(data.PreviewCameraEuler);
            float verticalExtent = Mathf.Max(bounds.extents.y, bounds.extents.x / Mathf.Max(1f, mainCamera.aspect));
            verticalExtent = Mathf.Max(verticalExtent, bounds.extents.z * 0.55f);
            float distance = verticalExtent / Mathf.Tan(data.PreviewFieldOfView * 0.5f * Mathf.Deg2Rad) * data.FramingPadding;
            distance = Mathf.Max(distance, bounds.extents.magnitude * 1.05f, 0.45f);
            previewCameraPosition = bounds.center - previewCameraRotation * Vector3.forward * distance;
        }

        private Bounds GetPreviewBounds()
        {
            Renderer[] renderers = preview.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(preview.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private void SampleCurrentTime()
        {
            if (preview != null && period?.AnimationClip != null)
                period.AnimationClip.SampleAnimation(preview, Mathf.Clamp(clipTime, 0f, period.AnimationClip.length));
        }

        private void FreezeGameplay()
        {
            previousTimeScale = Time.timeScale;
            previousPlayerInputEnabled = playerInput != null && playerInput.enabled;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            if (starterInputs != null)
            {
                starterInputs.MoveInput(Vector2.zero);
                starterInputs.LookInput(Vector2.zero);
                starterInputs.JumpInput(false);
                starterInputs.SprintInput(false);
            }
            if (playerInput != null) playerInput.DeactivateInput();
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SaveAndTakeOverCamera();
        }

        private void SaveAndTakeOverCamera()
        {
            previousCullingMask = mainCamera.cullingMask;
            previousClearFlags = mainCamera.clearFlags;
            previousBackground = mainCamera.backgroundColor;
            previousFieldOfView = mainCamera.fieldOfView;
            previousNearClip = mainCamera.nearClipPlane;
            previousFarClip = mainCamera.farClipPlane;
            previousOrthographic = mainCamera.orthographic;
            previousOrthographicSize = mainCamera.orthographicSize;
            previousCameraPosition = mainCamera.transform.position;
            previousCameraRotation = mainCamera.transform.rotation;
            previousBrainEnabled = brain != null && brain.enabled;
            if (brain != null) brain.enabled = false;
            mainCamera.cullingMask = previousCullingMask | 1 << previewLayer;
            mainCamera.orthographic = false;
            mainCamera.nearClipPlane = previewNearClip;
            mainCamera.farClipPlane = previousFarClip;
        }

        private void BeginSynchronizedEntry()
        {
            transitionStartPosition = mainCamera.transform.position;
            transitionStartRotation = mainCamera.transform.rotation;
            transitionStartFieldOfView = mainCamera.fieldOfView;
            transitionElapsed = 0f;
            State = SessionState.TransitioningIn;
        }

        private void UpdateSynchronizedEntry()
        {
            float duration = Mathf.Max(0.1f, period.SynchronizedEntrySeconds);
            transitionElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(transitionElapsed / duration);
            float cameraProgress = EvaluateNormalizedProgress(
                data.CameraEntryProgressCurve, t, t * t * (3f - 2f * t));
            float rewindProgress = EvaluateNormalizedProgress(period.RewindProgressCurve, t, t);
            mainCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(transitionStartPosition, previewCameraPosition, cameraProgress),
                Quaternion.Slerp(transitionStartRotation, previewCameraRotation, cameraProgress));
            mainCamera.fieldOfView = Mathf.Lerp(transitionStartFieldOfView, data.PreviewFieldOfView, cameraProgress);
            clipTime = period.AnimationClip.length * (1f - rewindProgress);
            SampleCurrentTime();
            if (t >= 1f)
            {
                mainCamera.transform.SetPositionAndRotation(previewCameraPosition, previewCameraRotation);
                mainCamera.fieldOfView = data.PreviewFieldOfView;
                clipTime = 0f;
                SampleCurrentTime();
                State = SessionState.Paused;
            }
        }

        private static float EvaluateNormalizedProgress(AnimationCurve curve, float normalizedTime, float fallback)
        {
            if (curve == null || curve.length < 2) return Mathf.Clamp01(fallback);
            Keyframe first = curve.keys[0];
            Keyframe last = curve.keys[curve.length - 1];
            float timeRange = last.time - first.time;
            float valueRange = last.value - first.value;
            if (timeRange <= Mathf.Epsilon || Mathf.Abs(valueRange) <= Mathf.Epsilon)
                return Mathf.Clamp01(fallback);

            float curveTime = Mathf.Lerp(first.time, last.time, Mathf.Clamp01(normalizedTime));
            return Mathf.Clamp01((curve.Evaluate(curveTime) - first.value) / valueRange);
        }

        private void UpdateCameraReturnTransition()
        {
            float duration = Mathf.Max(0.05f, data.CameraReturnTransitionSeconds);
            transitionElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(transitionElapsed / duration);
            float smoothT = t * t * (3f - 2f * t);
            mainCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(transitionStartPosition, previousCameraPosition, smoothT),
                Quaternion.Slerp(transitionStartRotation, previousCameraRotation, smoothT));
            mainCamera.fieldOfView = Mathf.Lerp(transitionStartFieldOfView, previousFieldOfView, smoothT);
            if (t >= 1f) CompleteExitRecall();
        }

        private void HideOriginalTarget()
        {
            target.SetInteractionHighlight(false, null);
            hiddenTargetRenderers = target.GetComponentsInChildren<Renderer>(true);
            hiddenTargetRendererStates = new bool[hiddenTargetRenderers.Length];
            for (int i = 0; i < hiddenTargetRenderers.Length; i++)
            {
                hiddenTargetRendererStates[i] = hiddenTargetRenderers[i].enabled;
                hiddenTargetRenderers[i].enabled = false;
            }
        }

        private void RestoreOriginalTarget()
        {
            if (hiddenTargetRenderers != null && hiddenTargetRendererStates != null)
            {
                int count = Mathf.Min(hiddenTargetRenderers.Length, hiddenTargetRendererStates.Length);
                for (int i = 0; i < count; i++)
                    if (hiddenTargetRenderers[i] != null) hiddenTargetRenderers[i].enabled = hiddenTargetRendererStates[i];
            }
            hiddenTargetRenderers = null;
            hiddenTargetRendererStates = null;
        }

        private void HidePlayerVisuals()
        {
            if (playerInput == null) return;
            hiddenPlayerRenderers = playerInput.GetComponentsInChildren<Renderer>(true);
            hiddenPlayerRendererStates = new bool[hiddenPlayerRenderers.Length];
            for (int i = 0; i < hiddenPlayerRenderers.Length; i++)
            {
                hiddenPlayerRendererStates[i] = hiddenPlayerRenderers[i].enabled;
                hiddenPlayerRenderers[i].enabled = false;
            }
        }

        private void RestorePlayerVisuals()
        {
            if (hiddenPlayerRenderers != null && hiddenPlayerRendererStates != null)
            {
                int count = Mathf.Min(hiddenPlayerRenderers.Length, hiddenPlayerRendererStates.Length);
                for (int i = 0; i < count; i++)
                    if (hiddenPlayerRenderers[i] != null) hiddenPlayerRenderers[i].enabled = hiddenPlayerRendererStates[i];
            }
            hiddenPlayerRenderers = null;
            hiddenPlayerRendererStates = null;
        }

        private void RestoreCamera()
        {
            if (mainCamera == null) return;
            mainCamera.cullingMask = previousCullingMask;
            mainCamera.clearFlags = previousClearFlags;
            mainCamera.backgroundColor = previousBackground;
            mainCamera.fieldOfView = previousFieldOfView;
            mainCamera.nearClipPlane = previousNearClip;
            mainCamera.farClipPlane = previousFarClip;
            mainCamera.orthographic = previousOrthographic;
            mainCamera.orthographicSize = previousOrthographicSize;
            mainCamera.transform.SetPositionAndRotation(previousCameraPosition, previousCameraRotation);
            if (brain != null) brain.enabled = previousBrainEnabled;
        }

        private void HandlePeriodPointer()
        {
            if (!pointerPressAction.WasPressedThisFrame()) return;
            int clicked = ui.GetPeriodAtScreenPosition(pointerPositionAction.ReadValue<Vector2>());
            if (clicked >= 0) SelectAvailablePeriod(clicked);
        }

        private void HandleTimelinePointer()
        {
            bool pressed = pointerPressAction.IsPressed();
            Vector2 pointer = pointerPositionAction.ReadValue<Vector2>();
            if (pointerPressAction.WasPressedThisFrame() && ui.IsTimelinePoint(pointer)) draggingTimeline = true;
            if (draggingTimeline && pressed) SetNormalizedTime(ui.GetTimelineValue(pointer));
            if (draggingTimeline && !pressed) draggingTimeline = false;
        }

        private void OnPlayPause(InputAction.CallbackContext context) => TogglePlayback();
        private void OnCancel(InputAction.CallbackContext context) { if (IsActive) ExitRecall(); }
        private void OnPrevious(InputAction.CallbackContext context) => MovePeriodSelection(-1);
        private void OnNext(InputAction.CallbackContext context) => MovePeriodSelection(1);
        private void OnSubmit(InputAction.CallbackContext context) => SelectAvailablePeriod(selectedAvailablePeriod);

        private void MovePeriodSelection(int direction)
        {
            if (State != SessionState.SelectingPeriod || data == null) return;
            selectedAvailablePeriod = (selectedAvailablePeriod + direction + data.AvailablePeriodCount) % data.AvailablePeriodCount;
            ui.UpdatePeriodSelection(selectedAvailablePeriod);
        }

        private void EnableActions(bool enabled)
        {
            InputAction[] actions = { playPauseAction, cancelAction, previousAction, nextAction, submitAction, pointerPositionAction, pointerPressAction };
            foreach (InputAction action in actions)
            {
                if (enabled) action.Enable(); else action.Disable();
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
