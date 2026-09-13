using DetectiveGame.Evidence;
using DetectiveGame.Input;
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
        [SerializeField] private GameInputModeController inputModes;
        [SerializeField] private EvidenceBoardService evidenceService;

        [Header("Preview")]
        [SerializeField] private int previewLayer = 2;
        [SerializeField, Min(0.01f)] private float previewNearClip = 0.03f;
        [SerializeField] private Shader dissolveShader;

        private readonly InputAction playPauseAction = new InputAction("Recall Play Pause", InputActionType.Button, "<Keyboard>/space");
        private readonly InputAction cancelAction = new InputAction("Recall Cancel", InputActionType.Button, "<Keyboard>/escape");
        private readonly InputAction previousAction = new InputAction("Previous Recall Period", InputActionType.Button, "<Keyboard>/leftArrow");
        private readonly InputAction nextAction = new InputAction("Next Recall Period", InputActionType.Button, "<Keyboard>/rightArrow");
        private readonly InputAction submitAction = new InputAction("Select Recall Period", InputActionType.Button, "<Keyboard>/enter");
        private readonly InputAction periodDigitAction = new InputAction("Select Recall Period Number", InputActionType.Button);
        private readonly InputAction pointerPositionAction = new InputAction("Recall Pointer Position", InputActionType.Value, "<Pointer>/position");
        private readonly InputAction pointerPressAction = new InputAction("Recall Pointer Press", InputActionType.Button, "<Pointer>/press");

        private CinemachineBrain brain;
        private RecallableObject target;
        private RecallObjectData data;
        private RecallPeriod period;
        private GameObject stage;
        private GameObject preview;
        private float clipTime;
        private int activePeriodIndex = -1;
        private bool evidenceReviewEligible;
        private bool evidenceRecorded;
        private int selectedAvailablePeriod;
        private bool draggingTimeline;
        private float transitionElapsed;
        private Vector3 transitionStartPosition;
        private Quaternion transitionStartRotation;
        private float transitionStartFieldOfView;
        private Vector3 previewCameraPosition;
        private Quaternion previewCameraRotation;
        private RecallDissolveVisual targetDissolveVisual;
        private RecallDissolveVisual previewDissolveVisual;
        private bool entryPreviewActivated;
        private bool exitWorldActivated;
        private bool exitUsesWorldObject;
        private float exitSourceDissolve;
        private float exitSourceDesaturation;
        private Renderer[] hiddenPlayerRenderers;
        private bool[] hiddenPlayerRendererStates;

        private float previousTimeScale;
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
        public int ActivePeriodIndex => activePeriodIndex;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null) brain = mainCamera.GetComponent<CinemachineBrain>();
            if (ui == null) ui = GetComponent<RecallRuntimeUI>();
            if (inputModes == null) inputModes = GameInputModeController.GetOrCreate(playerInput, starterInputs);
            if (evidenceService == null) evidenceService = EvidenceBoardService.GetOrCreate();
            if (dissolveShader == null)
                dissolveShader = Shader.Find("DetectiveGame/Recall Dissolve");
            if (dissolveShader == null)
                Debug.LogError("Recall dissolve shader was not found. Recall transitions will not render correctly.", this);
            for (int number = 1; number <= 9; number++)
            {
                periodDigitAction.AddBinding($"<Keyboard>/{number}");
                periodDigitAction.AddBinding($"<Keyboard>/numpad{number}");
            }
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
            periodDigitAction.performed += OnPeriodDigit;
        }

        private void OnDisable()
        {
            playPauseAction.performed -= OnPlayPause;
            cancelAction.performed -= OnCancel;
            previousAction.performed -= OnPrevious;
            nextAction.performed -= OnNext;
            submitAction.performed -= OnSubmit;
            periodDigitAction.performed -= OnPeriodDigit;
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
            periodDigitAction.Dispose();
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
                if (clipTime >= period.AnimationClip.length)
                {
                    State = SessionState.Paused;
                    TryRecordReviewedEvidence();
                }
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
            if (!FreezeGameplay()) return;
            target = recallable;
            data = target.Data;
            if (data.AvailablePeriodCount == 1)
            {
                ui.SetWorldPrompt(null);
                StartPeriod(data.GetAvailablePeriodIndex(0));
            }
            else
            {
                selectedAvailablePeriod = 0;
                State = SessionState.SelectingPeriod;
                ui.ShowPeriodSelection(data, target, selectedAvailablePeriod);
            }
        }

        public void SelectAvailablePeriod(int availableIndex)
        {
            if (State != SessionState.SelectingPeriod || data == null) return;
            if (availableIndex < 0 || availableIndex >= data.AvailablePeriodCount) return;
            int dataIndex = data.GetAvailablePeriodIndex(availableIndex);
            if (dataIndex >= 0) StartPeriod(dataIndex);
        }

        public bool TrySelectPeriodByNumber(int oneBasedNumber)
        {
            if (State != SessionState.SelectingPeriod || data == null ||
                oneBasedNumber < 1 || oneBasedNumber > data.AvailablePeriodCount) return false;
            SelectAvailablePeriod(oneBasedNumber - 1);
            return true;
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
            if (NormalizedTime >= 0.999f) TryRecordReviewedEvidence();
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
            exitWorldActivated = false;
            exitUsesWorldObject = State == SessionState.TransitioningIn && !entryPreviewActivated;
            RecallDissolveVisual exitSource = exitUsesWorldObject
                ? targetDissolveVisual
                : previewDissolveVisual;
            exitSourceDissolve = exitSource?.DissolveAmount ?? 0f;
            exitSourceDesaturation = exitSource?.Desaturation ?? 0f;
            if (exitUsesWorldObject)
            {
                previewDissolveVisual?.SetVisible(false);
                targetDissolveVisual?.BeginTransition(
                    true, exitSourceDissolve, exitSourceDesaturation);
            }
            else
            {
                previewDissolveVisual?.BeginTransition(
                    true, exitSourceDissolve, exitSourceDesaturation);
                targetDissolveVisual?.BeginTransition(false, 1f, 1f);
            }
            State = SessionState.TransitioningOut;
            if (ui != null) ui.HideRecall();
        }

        private void CompleteExitRecall()
        {
            if (!IsActive) return;
            State = SessionState.Idle;
            draggingTimeline = false;
            ReleaseDissolveVisuals();
            if (stage != null) Destroy(stage);
            stage = null;
            preview = null;
            period = null;
            RestoreCamera();
            RestorePlayerVisuals();
            Time.timeScale = previousTimeScale;
            if (inputModes != null) inputModes.Exit(this);
            if (ui != null) ui.HideRecall();
            data = null;
            target = null;
            activePeriodIndex = -1;
            evidenceReviewEligible = false;
            evidenceRecorded = false;
        }

        private void StartPeriod(int dataIndex)
        {
            ui.HidePeriodSelection();
            ui.SetWorldPrompt(null);
            activePeriodIndex = dataIndex;
            evidenceReviewEligible = false;
            evidenceRecorded = false;
            period = data.GetPeriod(dataIndex);
            if (period == null || period.AnimationClip == null || data.PreviewPrefab == null)
            {
                Debug.LogError($"Recall data '{data.name}' has an incomplete preview period.", data);
                CompleteExitRecall();
                return;
            }
            CreatePreview();
            clipTime = 0f;
            SampleCurrentTime();
            FramePreviewCamera();
            HidePlayerVisuals();
            BeginSynchronizedEntry();
            ui.ShowReconstruction(data);
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

            target.SetInteractionHighlight(false, null);
            targetDissolveVisual = new RecallDissolveVisual(
                target.gameObject, dissolveShader, data.DissolveEdgeColor,
                data.DissolveEdgeWidth, data.DissolveNoiseScale);
            previewDissolveVisual = new RecallDissolveVisual(
                preview, dissolveShader, data.DissolveEdgeColor,
                data.DissolveEdgeWidth, data.DissolveNoiseScale);
            targetDissolveVisual.BeginTransition(true, 0f, 0f);
            previewDissolveVisual.BeginTransition(false, 1f, 1f);

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

        private bool FreezeGameplay()
        {
            if (inputModes == null || !inputModes.TryEnter(this, GameInputMode.Recall)) return false;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            SaveAndTakeOverCamera();
            return true;
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
            entryPreviewActivated = false;
            State = SessionState.TransitioningIn;
        }

        private void UpdateSynchronizedEntry()
        {
            float duration = Mathf.Max(0.1f, period.SynchronizedEntrySeconds);
            transitionElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(transitionElapsed / duration);
            float cameraProgress = EvaluateNormalizedProgress(
                data.CameraEntryProgressCurve, t, t * t * (3f - 2f * t));
            mainCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(transitionStartPosition, previewCameraPosition, cameraProgress),
                Quaternion.Slerp(transitionStartRotation, previewCameraRotation, cameraProgress));
            mainCamera.fieldOfView = Mathf.Lerp(transitionStartFieldOfView, data.PreviewFieldOfView, cameraProgress);
            UpdateEntryReconstruction(t);
            if (t >= 1f)
            {
                mainCamera.transform.SetPositionAndRotation(previewCameraPosition, previewCameraRotation);
                mainCamera.fieldOfView = data.PreviewFieldOfView;
                clipTime = 0f;
                SampleCurrentTime();
                ActivateEntryPreview();
                targetDissolveVisual?.RestoreAppearance(false);
                previewDissolveVisual?.RestoreAppearance(true);
                State = SessionState.Paused;
                evidenceReviewEligible = true;
                ui.ShowPlayback(data, period);
                ui.UpdatePlayback(State, period, 0f);
            }
        }

        private void UpdateEntryReconstruction(float normalizedTime)
        {
            float colorFade = EvaluatePhaseProgress(
                data.ColorFadeCurve, normalizedTime, 0f, data.ColorFadeEnd);
            float dissolveOut = EvaluatePhaseProgress(
                data.DissolveOutCurve, normalizedTime, data.DissolveOutStart, data.DissolveOutEnd);
            targetDissolveVisual?.SetEffect(dissolveOut, colorFade);

            float switchPoint = Mathf.Lerp(data.DissolveOutEnd, data.RevealStart, 0.5f);
            if (normalizedTime >= switchPoint) ActivateEntryPreview();
            if (!entryPreviewActivated) return;

            float reveal = EvaluatePhaseProgress(data.RevealCurve, normalizedTime, data.RevealStart, 1f);
            previewDissolveVisual?.SetEffect(1f - reveal, 1f - reveal);
        }

        private void ActivateEntryPreview()
        {
            if (entryPreviewActivated) return;
            entryPreviewActivated = true;
            targetDissolveVisual?.SetEffect(1f, 1f);
            targetDissolveVisual?.SetVisible(false);
            clipTime = 0f;
            SampleCurrentTime();
            previewDissolveVisual?.BeginTransition(true, 1f, 1f);
        }

        private void TryRecordReviewedEvidence()
        {
            if (!evidenceReviewEligible || evidenceRecorded || target == null || target.Evidence == null ||
                evidenceService == null || activePeriodIndex < 0) return;
            evidenceRecorded = true;
            evidenceService.RecordReviewedEvidence(target.Evidence, data, period.PeriodId);
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

        private static float EvaluatePhaseProgress(
            AnimationCurve curve,
            float normalizedTime,
            float phaseStart,
            float phaseEnd)
        {
            if (phaseEnd <= phaseStart + Mathf.Epsilon)
                return normalizedTime >= phaseEnd ? 1f : 0f;
            float phaseTime = Mathf.InverseLerp(phaseStart, phaseEnd, normalizedTime);
            return EvaluateNormalizedProgress(curve, phaseTime, phaseTime);
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
            UpdateExitReconstruction(t);
            if (t >= 1f) CompleteExitRecall();
        }

        private void UpdateExitReconstruction(float normalizedTime)
        {
            if (exitUsesWorldObject)
            {
                float restore = EvaluatePhaseProgress(data.RevealCurve, normalizedTime, 0f, 1f);
                targetDissolveVisual?.SetEffect(
                    Mathf.Lerp(exitSourceDissolve, 0f, restore),
                    Mathf.Lerp(exitSourceDesaturation, 0f, restore));
                return;
            }

            float colorFade = EvaluatePhaseProgress(
                data.ColorFadeCurve, normalizedTime, 0f, data.ColorFadeEnd);
            float dissolveOut = EvaluatePhaseProgress(
                data.DissolveOutCurve, normalizedTime, data.DissolveOutStart, data.DissolveOutEnd);
            previewDissolveVisual?.SetEffect(
                Mathf.Lerp(exitSourceDissolve, 1f, dissolveOut),
                Mathf.Lerp(exitSourceDesaturation, 1f, colorFade));

            float switchPoint = Mathf.Lerp(data.DissolveOutEnd, data.RevealStart, 0.5f);
            if (normalizedTime >= switchPoint) ActivateExitWorld();
            if (!exitWorldActivated) return;

            float reveal = EvaluatePhaseProgress(data.RevealCurve, normalizedTime, data.RevealStart, 1f);
            targetDissolveVisual?.SetEffect(1f - reveal, 1f - reveal);
        }

        private void ActivateExitWorld()
        {
            if (exitWorldActivated) return;
            exitWorldActivated = true;
            previewDissolveVisual?.SetEffect(1f, 1f);
            previewDissolveVisual?.SetVisible(false);
            targetDissolveVisual?.BeginTransition(true, 1f, 1f);
        }

        private void ReleaseDissolveVisuals()
        {
            previewDissolveVisual?.Dispose(false);
            previewDissolveVisual = null;
            targetDissolveVisual?.Dispose(true);
            targetDissolveVisual = null;
            entryPreviewActivated = false;
            exitWorldActivated = false;
            exitUsesWorldObject = false;
            exitSourceDissolve = 0f;
            exitSourceDesaturation = 0f;
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
            int clicked = ui.GetPeriodAtScreenPosition(pointerPositionAction.ReadValue<Vector2>());
            if (clicked >= 0 && clicked != selectedAvailablePeriod)
            {
                selectedAvailablePeriod = clicked;
                ui.UpdatePeriodSelection(selectedAvailablePeriod);
            }
            if (clicked >= 0 && pointerPressAction.WasPressedThisFrame()) SelectAvailablePeriod(clicked);
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

        private void OnPeriodDigit(InputAction.CallbackContext context)
        {
            if (State != SessionState.SelectingPeriod || context.control == null) return;
            string controlName = context.control.name;
            if (string.IsNullOrEmpty(controlName)) return;
            char digit = controlName[controlName.Length - 1];
            if (digit >= '1' && digit <= '9') TrySelectPeriodByNumber(digit - '0');
        }

        private void MovePeriodSelection(int direction)
        {
            if (State != SessionState.SelectingPeriod || data == null) return;
            selectedAvailablePeriod = (selectedAvailablePeriod + direction + data.AvailablePeriodCount) % data.AvailablePeriodCount;
            ui.UpdatePeriodSelection(selectedAvailablePeriod);
        }

        private void EnableActions(bool enabled)
        {
            InputAction[] actions = { playPauseAction, cancelAction, previousAction, nextAction, submitAction, periodDigitAction, pointerPositionAction, pointerPressAction };
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
