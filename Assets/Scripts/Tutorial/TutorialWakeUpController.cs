using System.Collections.Generic;
using DetectiveGame.Input;
using DetectiveGame.Player;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace DetectiveGame.Tutorial
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class TutorialWakeUpController : MonoBehaviour
    {
        [Header("Player: Animator and capsule must share this root")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private ThirdPersonController movement;
        [SerializeField] private CharacterController capsule;
        [SerializeField] private StarterAssetsInputs inputs;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private FullBodyViewController bodyView;
        [SerializeField, Tooltip("Interaction scripts with independent InputActions must also be suspended.")]
        private Behaviour[] suspendDuringOpening;

        [Header("Subtitles")]
        [SerializeField, TextArea(2, 6)] private string dateSubtitle = "1958年9月9日";
        [SerializeField, TextArea(2, 6)] private string storySubtitle = "没有意外的话，\n今天将是我入职阿什福德侦探事务所的日子。";
        [SerializeField] private GameObject introPanel;
        [FormerlySerializedAs("introText"), SerializeField] private Text dateText;
        [SerializeField] private Text storyText;
        [SerializeField] private CanvasGroup subtitleGroup;
        [SerializeField, Min(0f)] private float blackFadeDuration = 2.25f;
        [SerializeField, Min(0f)] private float dateFadeOutDuration = 1f;
        [SerializeField, Min(0f)] private float storyFadeInDuration = 1.4f;
        [SerializeField, Min(0f)] private float storyFadeOutDuration = 0.8f;

        [Header("Two-stage Timeline timing")]
        [SerializeField, Min(0.05f), Tooltip("Source Timeline seconds at the raised torso pose before turning. No FBX trimming is required.")]
        private float sittingEndTime = 1.8f;
        [SerializeField, Min(0.1f), Tooltip("Seconds to play from source time 0 to Sitting End Time, before Playback Speed scaling.")]
        private float sittingUpDuration = 3.2f;
        [SerializeField, Min(0.1f), Tooltip("Seconds to play from Sitting End Time to the end of Timeline, before Playback Speed scaling.")]
        private float standingUpDuration = 4f;
        [SerializeField, Min(0f), Tooltip("Extra seated reading time before a fresh second click is accepted. Early clicks are ignored, never queued.")]
        private float minimumSitHold = 0.25f;

        [Header("Opening")]
        [SerializeField] private PlayableDirector director;
        [SerializeField] private CinemachineCamera openingCamera;
        [SerializeField] private Camera outputCamera;
        [SerializeField] private CanvasGroup eyelids;
        [SerializeField] private Transform lieAnchor;
        [SerializeField] private Transform sitAnchor;
        [SerializeField] private Transform standAnchor;
        [SerializeField, Min(0.1f)] private float playbackSpeed = 1f;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private UnityEvent onCompleted;

        private GameInputModeController inputMode;
        private RuntimeAnimatorController originalAnimatorController;
        private bool previousMovement, previousCapsule, previousRootMotion;
        private readonly Dictionary<Behaviour, bool> suspendedStates = new();
        public enum OpeningPhase { Inactive, Date, SittingUp, WaitingToStand, StandingUp, Finished }
        private OpeningPhase phase;
        private float phaseElapsed;
        private double timelineTime, timelineDuration, effectiveSitTime;
        private int lastContinueFrame = -1;
        private Font generatedIntroFont;
        private Color dateColor, storyColor;
        private bool running, completing;
        private Vector3 sampledPosition;
        private Quaternion sampledRotation;
        private CinemachineBrain brain;
        private CinemachineBlendDefinition previousBlend;
        private bool hasBlend;

        public bool IsPlaying => running;
        public OpeningPhase Phase => phase;
        public Transform StandAnchor => standAnchor;
        public PlayableDirector Director => director;

        private void Awake()
        {
            if (outputCamera != null) brain = outputCamera.GetComponent<CinemachineBrain>();
            if (openingCamera != null) openingCamera.enabled = false;
            if (introPanel != null) introPanel.SetActive(false);
            if (dateText != null) dateColor = dateText.color;
            if (storyText != null) storyColor = storyText.color;
            PrepareSubtitleFont(dateText);
            PrepareSubtitleFont(storyText);
            if (eyelids != null) eyelids.alpha = playOnStart ? 1f : 0f;
        }

        private void PrepareSubtitleFont(Text label)
        {
            if (label == null || (label.font != null &&
                label.font != Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"))) return;
            if (generatedIntroFont == null)
                generatedIntroFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 32);
            label.font = generatedIntroFont != null ? generatedIntroFont :
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Start() { if (playOnStart) BeginOpening(); }

        public void BeginOpening()
        {
            if (running) return;
            if (playerRoot == null || animator == null || !animator.isHuman ||
                director == null || director.playableAsset == null || openingCamera == null ||
                bodyView == null || bodyView.FirstPersonCamera == null ||
                lieAnchor == null || sitAnchor == null || standAnchor == null ||
                movement == null || capsule == null || inputs == null || playerInput == null || eyelids == null ||
                introPanel == null || dateText == null || storyText == null || subtitleGroup == null)
            {
                if (eyelids != null) eyelids.alpha = 0f;
                Debug.LogError("Tutorial opening needs its player, Timeline, cameras, anchors and two subtitle UI references.", this);
                return;
            }
            timelineDuration = director.duration;
            if (double.IsNaN(timelineDuration) || double.IsInfinity(timelineDuration) || timelineDuration <= 0.1d)
            {
                eyelids.alpha = 0f;
                Debug.LogError("Tutorial opening requires a finite Timeline longer than 0.1 seconds.", this);
                return;
            }
            effectiveSitTime = System.Math.Max(0.05d, System.Math.Min(sittingEndTime, timelineDuration - 0.05d));
            inputMode = GameInputModeController.GetOrCreate(playerInput, inputs);
            if (!inputMode.TryEnter(this, GameInputMode.Cinematic))
            {
                eyelids.alpha = 0f;
                return;
            }
            running = true;
            originalAnimatorController = animator.runtimeAnimatorController;
            previousRootMotion = animator.applyRootMotion;
            previousMovement = movement.enabled;
            previousCapsule = capsule.enabled;
            movement.enabled = false;
            capsule.enabled = false;
            animator.applyRootMotion = false;
            bodyView.SetInteractionLock(true);
            foreach (var behaviour in suspendDuringOpening ?? System.Array.Empty<Behaviour>())
            {
                if (behaviour == null || suspendedStates.ContainsKey(behaviour)) continue;
                suspendedStates.Add(behaviour, behaviour.enabled);
                behaviour.enabled = false;
            }
            ClearInputs();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            sampledPosition = lieAnchor.position;
            sampledRotation = lieAnchor.rotation;
            playerRoot.SetPositionAndRotation(sampledPosition, sampledRotation);
            openingCamera.Lens = bodyView.FirstPersonCamera.Lens;
            openingCamera.Priority = 100;
            openingCamera.enabled = true;
            openingCamera.PreviousStateIsValid = false;
            if (brain != null)
            {
                previousBlend = brain.DefaultBlend;
                hasBlend = true;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            }
            director.stopped += OnTimelineStopped;
            director.time = timelineTime = 0d;
            director.Play();
            // Keep evaluating the graph at zero speed; Pause would let normal Idle overwrite the hold pose.
            for (int i = 0; i < director.playableGraph.GetRootPlayableCount(); i++)
                director.playableGraph.GetRootPlayable(i).SetSpeed(0d);
            director.Evaluate();
            ApplySampledRoot();
            ApplyOutputCamera();
            eyelids.alpha = 1f;
            subtitleGroup.alpha = 1f;
            introPanel.SetActive(true);
            dateText.gameObject.SetActive(true);
            storyText.gameObject.SetActive(true);
            dateText.text = dateSubtitle ?? string.Empty;
            storyText.text = storySubtitle ?? string.Empty;
            SetSubtitleAlpha(1f, 0f);
            SetPhase(OpeningPhase.Date);
            lastContinueFrame = -1;
        }

        // Existing UI/event callers can keep using this entry point; transitional clicks are discarded.
        public void AdvanceIntroduction()
        {
            if (!running || lastContinueFrame == Time.frameCount) return;
            if (phase == OpeningPhase.Date)
            {
                lastContinueFrame = Time.frameCount;
                SetPhase(OpeningPhase.SittingUp);
            }
            else if (phase == OpeningPhase.WaitingToStand && phaseElapsed >= minimumSitHold)
            {
                lastContinueFrame = Time.frameCount;
                SetPhase(OpeningPhase.StandingUp);
            }
        }

        private void SetPhase(OpeningPhase next)
        {
            phase = next;
            phaseElapsed = 0f;
        }

        private static float FadeProgress(float elapsed, float duration) =>
            duration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)) : 1f;

        private void SetSubtitleAlpha(float dateAlpha, float storyAlpha)
        {
            dateText.color = new Color(dateColor.r, dateColor.g, dateColor.b, dateColor.a * dateAlpha);
            storyText.color = new Color(storyColor.r, storyColor.g, storyColor.b, storyColor.a * storyAlpha);
        }

        private void AdvanceTimeline(double target, double rate)
        {
            timelineTime = System.Math.Min(target, timelineTime + Time.deltaTime * rate);
            // Clip ends are exclusive: sample just inside the last frame until unified restoration.
            director.time = System.Math.Min(timelineTime, timelineDuration - 0.00001d);
            director.Evaluate();
        }

        private void Update()
        {
            if (!running) return;
            phaseElapsed += Time.unscaledDeltaTime;
            switch (phase)
            {
                case OpeningPhase.Date:
                case OpeningPhase.WaitingToStand:
                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                        AdvanceIntroduction();
                    break;
                case OpeningPhase.SittingUp:
                    eyelids.alpha = 1f - FadeProgress(phaseElapsed, blackFadeDuration);
                    SetSubtitleAlpha(1f - FadeProgress(phaseElapsed, dateFadeOutDuration),
                        FadeProgress(phaseElapsed, storyFadeInDuration));
                    AdvanceTimeline(effectiveSitTime,
                        effectiveSitTime / Mathf.Max(0.1f, sittingUpDuration) * Mathf.Max(0.1f, playbackSpeed));
                    if (timelineTime >= effectiveSitTime && phaseElapsed >=
                        Mathf.Max(blackFadeDuration, Mathf.Max(dateFadeOutDuration, storyFadeInDuration)))
                        SetPhase(OpeningPhase.WaitingToStand);
                    break;
                case OpeningPhase.StandingUp:
                    subtitleGroup.alpha = 1f - FadeProgress(phaseElapsed, storyFadeOutDuration);
                    if (subtitleGroup.alpha <= 0f) introPanel.SetActive(false);
                    AdvanceTimeline(timelineDuration,
                        (timelineDuration - effectiveSitTime) / Mathf.Max(0.1f, standingUpDuration) * Mathf.Max(0.1f, playbackSpeed));
                    break;
            }
        }

        private void LateUpdate()
        {
            if (!running) return;
            ApplySampledRoot();
            if (phase == OpeningPhase.StandingUp && timelineTime >= timelineDuration &&
                phaseElapsed >= storyFadeOutDuration) CompleteOpening();
        }

        public void SampleMotion(TutorialWakeUpMotionAsset data, float time)
        {
            if (!running || data == null) return;
            float sitting = Mathf.Clamp01(data.lieToSit.Evaluate(time));
            float standing = Mathf.Clamp01(data.sitToStand.Evaluate(time));
            sampledPosition = Vector3.Lerp(Vector3.Lerp(lieAnchor.position, sitAnchor.position, sitting), standAnchor.position, standing);
            // Humanoid bones own the turn; root has a constant yaw throughout this clip.
            sampledRotation = lieAnchor.rotation;
            // The controller owns the introduction fade; Timeline must not close the screen again.
            var keys = data.cameraKeys;
            if (keys == null || keys.Length == 0) return;
            WakeUpCameraKey a = keys[0], b = keys[keys.Length - 1];
            for (int i = 1; i < keys.Length; i++)
                if (time <= keys[i].time) { a = keys[i - 1]; b = keys[i]; break; }
            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.time, b.time, time));
            var anchors = new[] { lieAnchor, sitAnchor, standAnchor };
            Vector3 position = Vector3.Lerp(anchors[a.anchor].position + a.offset, anchors[b.anchor].position + b.offset, w);
            Quaternion rotation = Quaternion.Slerp(Quaternion.Euler(a.rotation), Quaternion.Euler(b.rotation), w);
            openingCamera.transform.SetPositionAndRotation(position, rotation);
            // The last 0.35s converges to the actual gameplay lens and collision-adjusted eye position.
            float handoff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.938f, 1f, time));
            sampledRotation = Quaternion.Slerp(lieAnchor.rotation, standAnchor.rotation, handoff);
            if (handoff > 0f)
            {
                playerRoot.SetPositionAndRotation(standAnchor.position, standAnchor.rotation);
                bodyView.SnapFirstPersonLook(standAnchor.eulerAngles.y);
                var normal = bodyView.FirstPersonCamera;
                openingCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(position, normal.transform.position, handoff),
                    Quaternion.Slerp(rotation, normal.transform.rotation, handoff));
                openingCamera.Lens = normal.Lens;
            }
        }

        private void ApplySampledRoot() => playerRoot.SetPositionAndRotation(sampledPosition, sampledRotation);

        private void ApplyOutputCamera()
        {
            if (outputCamera == null) return;
            outputCamera.transform.SetPositionAndRotation(openingCamera.transform.position, openingCamera.transform.rotation);
            outputCamera.fieldOfView = openingCamera.Lens.FieldOfView;
        }

        private void OnTimelineStopped(PlayableDirector _) { if (!completing) CompleteOpening(); }
        public void SkipOpening()
        {
            if (phase == OpeningPhase.StandingUp) CompleteOpening();
        }

        // Both natural completion and skipping use the same idempotent restoration path.
        public void CompleteOpening()
        {
            if (!running || completing) return;
            completing = true;
            director.stopped -= OnTimelineStopped;
            director.Stop();
            playerRoot.SetPositionAndRotation(standAnchor.position, standAnchor.rotation);
            animator.runtimeAnimatorController = originalAnimatorController;
            animator.applyRootMotion = previousRootMotion;
            animator.Rebind();
            movement.ResetMotionAfterCinematic();
            animator.Update(0f);
            // Rebind/root-motion sampling can touch the root: commit placement afterwards.
            playerRoot.SetPositionAndRotation(standAnchor.position, standAnchor.rotation);
            bodyView.SnapFirstPersonLook(standAnchor.eulerAngles.y);
            var normal = bodyView.FirstPersonCamera;
            openingCamera.transform.SetPositionAndRotation(normal.transform.position, normal.transform.rotation);
            openingCamera.Lens = normal.Lens;
            normal.PreviousStateIsValid = false;
            if (outputCamera != null)
            {
                outputCamera.transform.SetPositionAndRotation(normal.transform.position, normal.transform.rotation);
                outputCamera.fieldOfView = normal.Lens.FieldOfView;
            }
            openingCamera.enabled = false;
            if (hasBlend && brain != null) brain.DefaultBlend = previousBlend;
            hasBlend = false;
            Physics.SyncTransforms();
            capsule.enabled = previousCapsule;
            movement.enabled = previousMovement;
            bodyView.SetInteractionLock(false);
            foreach (var state in suspendedStates) if (state.Key != null) state.Key.enabled = state.Value;
            suspendedStates.Clear();
            inputMode.Exit(this);
            ClearInputs();
            if (eyelids != null) eyelids.alpha = 0f;
            if (introPanel != null) introPanel.SetActive(false);
            phase = OpeningPhase.Finished;
            running = false;
            completing = false;
            onCompleted?.Invoke();
        }

        private void ClearInputs()
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.JumpInput(false);
            inputs.SprintInput(false);
        }

        private void OnDisable()
        {
            if (running) CompleteOpening();
            if (hasBlend && brain != null) brain.DefaultBlend = previousBlend;
            hasBlend = false;
        }

        private void OnDestroy()
        {
            if (generatedIntroFont != null) Destroy(generatedIntroFont);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (lieAnchor != null) Gizmos.DrawWireSphere(lieAnchor.position, 0.1f);
            if (sitAnchor != null) Gizmos.DrawWireSphere(sitAnchor.position, 0.1f);
            if (standAnchor != null) Gizmos.DrawWireSphere(standAnchor.position, 0.1f);
        }
    }
}


