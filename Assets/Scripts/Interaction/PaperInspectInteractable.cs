using System.Collections.Generic;
using DetectiveGame.Input;
using DetectiveGame.Player;
using DetectiveGame.Recall;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PaperInspectInteractable : WorldInteractable
    {
        [SerializeField] private Transform viewAnchor;
        [SerializeField] private GameObject exitHint;
        [SerializeField, Range(15, 100)] private float fieldOfView = 40;
        [SerializeField, Min(0.01f)] private float entrySeconds = 0.9f, exitSeconds = 0.65f;
        [SerializeField] private AnimationCurve cameraProgress = AnimationCurve.EaseInOut(0, 0, 1, 1);
        private enum ViewState { Idle, Entering, Viewing, Exiting }
        private ViewState state;
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private Camera viewCamera;
        private CinemachineBrain brain;
        private FullBodyViewController body;
        private ThirdPersonController movement;
        private StarterAssetsInputs inputs;
        private GameInputModeController modes;
        private bool brainEnabled, bodyLocked;
        private Vector3 savedPosition, fromPosition;
        private Quaternion savedRotation, fromRotation;
        private float savedFov, savedNear, fromFov, elapsed;
        public bool IsInspecting => state != ViewState.Idle;
        public bool IsViewing => state == ViewState.Viewing;
        public Transform ViewAnchor => viewAnchor;
        public override string PromptText => "【E 查看】";
        public override bool IsInteractionAvailable => base.IsInteractionAvailable && !IsInspecting && viewAnchor != null &&
            (GameInputModeController.Current == null || GameInputModeController.Current.IsGameplay);

        public override void Interact(Transform interactor)
        {
            if (!IsInteractionAvailable || interactor == null) return;
            var player = interactor.GetComponentInParent<PlayerInput>();
            if (player == null) player = interactor.GetComponentInChildren<PlayerInput>();
            viewCamera = Camera.main;
            if (player == null || viewCamera == null) return;
            inputs = player.GetComponent<StarterAssetsInputs>();
            modes = GameInputModeController.GetOrCreate(player, inputs);
            if (!modes.TryEnter(this, GameInputMode.InspectObject)) return;
            savedPosition = viewCamera.transform.position; savedRotation = viewCamera.transform.rotation;
            savedFov = viewCamera.fieldOfView; savedNear = viewCamera.nearClipPlane;
            brain = viewCamera.GetComponent<CinemachineBrain>(); brainEnabled = brain != null && brain.enabled;
            if (brain != null) brain.enabled = false;
            body = player.GetComponent<FullBodyViewController>(); bodyLocked = body != null && body.InteractionLocked;
            if (body != null) body.SetInteractionLock(true);
            movement = player.GetComponent<ThirdPersonController>();
            Suspend(movement); Suspend(player.GetComponent<PlayerInteractor>()); Suspend(player.GetComponent<RecallInteractor>());
            Suspend(player.GetComponent<PlayerDoorInteractionController>()); Suspend(player.GetComponent<BasicRigidBodyPush>());
            if (movement != null) movement.ResetMotionAfterCinematic();
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
                if (r.enabled) { hidden.Add(r); r.enabled = false; }
            viewCamera.nearClipPlane = Mathf.Min(savedNear, 0.03f);
            if (exitHint != null) exitHint.SetActive(true);
            Begin(ViewState.Entering);
        }

        private void Update()
        {
            if (!IsInspecting) return;
            if (viewCamera == null || viewAnchor == null) { Restore(); return; }
            bool cancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame ||
                Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (cancel && state != ViewState.Exiting) { ExitInspect(); return; }
            if (state == ViewState.Viewing) return;
            elapsed += Time.unscaledDeltaTime;
            bool entering = state == ViewState.Entering;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, entering ? entrySeconds : exitSeconds));
            FixedViewCameraTransition.Apply(viewCamera, fromPosition, fromRotation, fromFov,
                entering ? viewAnchor.position : savedPosition, entering ? viewAnchor.rotation : savedRotation,
                entering ? fieldOfView : savedFov, FixedViewCameraTransition.Progress(cameraProgress, t));
            if (t >= 1) { if (entering) state = ViewState.Viewing; else Restore(); }
        }

        public void ExitInspect()
        {
            if (!IsInspecting || state == ViewState.Exiting) return;
            if (viewCamera == null) { Restore(); return; }
            if (body != null && body.IsFirstPerson && body.FirstPersonCamera != null)
            {
                savedPosition = body.FirstPersonCamera.transform.position;
                savedRotation = body.FirstPersonCamera.transform.rotation;
                savedFov = body.FirstPersonCamera.Lens.FieldOfView;
            }
            Begin(ViewState.Exiting);
        }

        private void Begin(ViewState next)
        {
            state = next; elapsed = 0; fromPosition = viewCamera.transform.position;
            fromRotation = viewCamera.transform.rotation; fromFov = viewCamera.fieldOfView;
        }
        private void Suspend(Behaviour b) { if (b != null && b.enabled) { suspended.Add(b); b.enabled = false; } }
        private void Restore()
        {
            if (!IsInspecting) return;
            if (viewCamera != null)
            {
                viewCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
                viewCamera.fieldOfView = savedFov; viewCamera.nearClipPlane = savedNear;
            }
            if (brain != null) brain.enabled = brainEnabled;
            foreach (var r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
            if (inputs != null) { inputs.MoveInput(Vector2.zero); inputs.LookInput(Vector2.zero); inputs.JumpInput(false); inputs.SprintInput(false); }
            if (movement != null) movement.ResetMotionAfterCinematic();
            if (body != null) body.SetInteractionLock(bodyLocked);
            foreach (var b in suspended) if (b != null) b.enabled = true;
            suspended.Clear();
            if (modes != null) modes.Exit(this);
            if (exitHint != null) exitHint.SetActive(false);
            state = ViewState.Idle;
        }
        protected override void OnDisable() { base.OnDisable(); Restore(); }

    }
}
