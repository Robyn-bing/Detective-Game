using System;
using System.Collections.Generic;
using DetectiveGame.Input;
using DetectiveGame.Player;
using DetectiveGame.Recall;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace DetectiveGame.Interaction
{
    [DisallowMultipleComponent]
    public sealed class TelephoneDialController : WorldInteractable
    {
        public enum DialState { Idle, EnteringDialView, Ready, DraggingDial, DialReturning, Validating, CorrectionMode, Success, Exiting, Completed, Story }
        private enum EditingMode { Normal, Correction }

        [Header("Existing Telephone")]
        [SerializeField] private TelephoneDialRecall dialSettings;
        [SerializeField] private RotaryDialController rotaryDial;
        [SerializeField] private TelephoneDialUI dialUI;
        [SerializeField] private Transform dialViewAnchor;
        [Header("Fixed View (shared recall interpolation)")]
        [SerializeField, Min(0.01f)] private float cameraEntrySeconds = 0.9f;
        [SerializeField, Min(0.01f)] private float cameraExitSeconds = 0.65f;
        [SerializeField, Range(15f, 100f)] private float dialFieldOfView = 40f;
        [SerializeField] private AnimationCurve cameraProgress = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Header("Success / Story Hook")]
        [SerializeField] private AudioSource telephoneAudio;
        [SerializeField] private AudioClip connectedSound;
        [SerializeField] private UnityEvent onDialSuccess = new UnityEvent();

        private readonly int[] digits = { -1, -1, -1, -1, -1, -1 };
        private readonly bool[] errors = new bool[6];
        private readonly bool[] pendingCorrections = new bool[6];
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private EditingMode editingMode;
        private int count, correctionIndex = -1;
        private float transitionElapsed;
        private Camera viewCamera;
        private CinemachineBrain brain;
        private bool savedBrainEnabled;
        private float savedFov, savedNearClip;
        private Vector3 savedCameraPosition, transitionStartPosition;
        private Quaternion savedCameraRotation, transitionStartRotation;
        private float transitionStartFov;
        private GameInputModeController inputModes;
        private FullBodyViewController bodyView;
        private ThirdPersonController movement;
        private StarterAssetsInputs starterInputs;
        private bool previousBodyLock;
        private bool completeAfterExit;
        private bool storyOwnsSuccess;
        public event Action DialViewExited;

        public DialState State { get; private set; } = DialState.Idle;
        public UnityEvent OnDialSuccess => onDialSuccess;
        public string CorrectNumber => dialSettings != null ? dialSettings.PhoneNumber : "";
        public int[] EnteredDigits => (int[])digits.Clone();
        public int CorrectionIndex => correctionIndex;
        public override string PromptText => "【E 拨号】";
        public Transform DialViewAnchor => dialViewAnchor;
        public float DialFieldOfView => dialFieldOfView;
        public override bool IsInteractionAvailable => base.IsInteractionAvailable && State == DialState.Idle &&
            (GameInputModeController.Current == null || GameInputModeController.Current.IsGameplay);

        private void Awake()
        {
            if (dialSettings == null) dialSettings = GetComponent<TelephoneDialRecall>();
            if (rotaryDial == null) rotaryDial = GetComponent<RotaryDialController>();
            if (rotaryDial != null) rotaryDial.ReturnCompleted += OnDialReturned;
        }

        public override void Interact(Transform interactor)
        {
            if (!IsInteractionAvailable || interactor == null) return;
            string error;
            if (dialSettings == null || dialSettings.AngleProfile == null || rotaryDial == null ||
                dialUI == null || dialViewAnchor == null || dialUI.Slots.Length != 6 ||
                CorrectNumber.Length != 6 || !Array.TrueForAll(CorrectNumber.ToCharArray(), c => c >= '0' && c <= '9'))
            { Debug.LogWarning("Telephone requires six digits, a shared angle profile, wheel, UI and DialViewAnchor.", this); return; }
            if (!dialSettings.AngleProfile.IsValid(out error)) { Debug.LogWarning(error, this); return; }

            PlayerInput player = interactor.GetComponentInParent<PlayerInput>();
            if (player == null) player = interactor.GetComponentInChildren<PlayerInput>();
            if (player == null) return;
            viewCamera = Camera.main;
            if (viewCamera == null) return;
            starterInputs = player.GetComponent<StarterAssetsInputs>();
            inputModes = GameInputModeController.GetOrCreate(player, starterInputs);
            if (!inputModes.TryEnter(this, GameInputMode.TelephoneDial)) return;

            bodyView = player.GetComponent<FullBodyViewController>();
            movement = player.GetComponent<ThirdPersonController>();
            previousBodyLock = bodyView != null && bodyView.InteractionLocked;
            if (bodyView != null) bodyView.SetInteractionLock(true);
            suspended.Clear();
            Suspend(movement);
            Suspend(player.GetComponent<PlayerInteractor>());
            Suspend(player.GetComponent<RecallInteractor>());
            Suspend(player.GetComponent<PlayerDoorInteractionController>());
            Suspend(player.GetComponent<BasicRigidBodyPush>());
            // The capsule/root stays in place; no reparenting or CharacterController teleport.
            if (movement != null) movement.ResetMotionAfterCinematic();
            hiddenRenderers.Clear();
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled) { hiddenRenderers.Add(renderer); renderer.enabled = false; }

            savedCameraPosition = viewCamera.transform.position;
            savedCameraRotation = viewCamera.transform.rotation;
            savedFov = viewCamera.fieldOfView;
            savedNearClip = viewCamera.nearClipPlane;
            brain = viewCamera.GetComponent<CinemachineBrain>();
            savedBrainEnabled = brain != null && brain.enabled;
            if (brain != null) brain.enabled = false;
            viewCamera.nearClipPlane = Mathf.Min(savedNearClip, 0.03f);
            for (int i = 0; i < 6; i++) { digits[i] = -1; errors[i] = false; pendingCorrections[i] = false; }
            count = 0; correctionIndex = -1; editingMode = EditingMode.Normal;
            rotaryDial.Cancel();
            dialUI.Show();
            Present();
            dialUI.SetInstruction("按住转盘顺时针拖动，松开拨号   ·   Backspace 删除   ·   Esc 返回");
            BeginTransition(DialState.EnteringDialView);
        }

        private void Update()
        {
            if (State == DialState.Idle || State == DialState.Completed) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && State != DialState.Exiting && State != DialState.Success && State != DialState.Story)
            { ExitDialMode(); return; }
            if (State == DialState.EnteringDialView || State == DialState.Exiting) { UpdateCameraTransition(); return; }
            if (State == DialState.Validating)
            {
                if (dialUI.ErrorFeedbackFinished)
                {
                    correctionIndex = NextPendingCorrection();
                    editingMode = EditingMode.Correction;
                    State = DialState.CorrectionMode;
                    dialUI.SetInstruction("重新拨入高亮位置的数字   ·   Backspace 清空当前位置   ·   Esc 返回");
                    Present();
                }
                return;
            }
            Mouse mouse = Mouse.current;
            if (State == DialState.DraggingDial)
            {
                if (mouse != null) rotaryDial.Drag(viewCamera, mouse.position.ReadValue());
                if (mouse == null || !mouse.leftButton.isPressed)
                { rotaryDial.ReleaseAndReturn(); State = DialState.DialReturning; }
                return;
            }
            if (State != DialState.Ready && State != DialState.CorrectionMode) return;
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame) Backspace();
            if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                rotaryDial.TryBeginDrag(viewCamera, mouse.position.ReadValue())) State = DialState.DraggingDial;
        }

        public void Backspace()
        {
            if (State == DialState.Ready && count > 0) digits[--count] = -1;
            else if (State == DialState.CorrectionMode && correctionIndex >= 0) digits[correctionIndex] = -1;
            else return;
            Present();
        }

        private void OnDialReturned(int digit)
        {
            if (State != DialState.DialReturning) return;
            // Esc wins even if the wheel finishes before this controller Update in the same frame.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { ExitDialMode(); return; }
            State = editingMode == EditingMode.Correction ? DialState.CorrectionMode : DialState.Ready;
            if (digit < 0 || digit > 9) return;
            if (editingMode == EditingMode.Normal)
            {
                if (count >= 6) return;
                digits[count++] = digit;
                Present();
                if (count == 6) ValidateNumber();
            }
            else
            {
                if (correctionIndex < 0) return;
                digits[correctionIndex] = digit;
                pendingCorrections[correctionIndex] = false;
                errors[correctionIndex] = false;
                // Do not reveal whether an individual replacement is correct.
                correctionIndex = NextPendingCorrection();
                Present();
                if (correctionIndex < 0) ValidateNumber();
            }
        }

        private void ValidateNumber()
        {
            State = DialState.Validating;
            correctionIndex = -1;
            int wrongCount = 0;
            for (int i = 0; i < 6; i++)
            {
                errors[i] = digits[i] != CorrectNumber[i] - '0';
                pendingCorrections[i] = errors[i];
                if (errors[i]) wrongCount++;
            }
            Present();
            if (wrongCount > 0)
            {
                dialUI.SetInstruction("号码有误");
                dialUI.PlayErrorFeedback(errors);
                return;
            }
            State = DialState.Success;
            dialUI.SetInstruction("电话接通中……");
            // The story can take over here; call ExitDialMode when returning to gameplay.
            onDialSuccess.Invoke();
        }

        private int NextPendingCorrection()
        {
            for (int i = 0; i < 6; i++) if (pendingCorrections[i]) return i;
            return -1;
        }

        private void Present() => dialUI.Present(digits, errors, correctionIndex);

        public void ExitDialMode()
        {
            if (State == DialState.Idle || State == DialState.Completed || State == DialState.Exiting || storyOwnsSuccess) return;
            rotaryDial.Cancel();
            dialUI.Hide();
            if (telephoneAudio != null) telephoneAudio.Stop();
            // A rendered-camera snapshot can be mid-Cinemachine-blend at entry.
            // Return to the live first-person target before re-enabling its brain.
            if (bodyView != null && bodyView.IsFirstPerson && bodyView.FirstPersonCamera != null)
            {
                var normalCamera = bodyView.FirstPersonCamera;
                savedCameraPosition = normalCamera.transform.position;
                savedCameraRotation = normalCamera.transform.rotation;
                savedFov = normalCamera.Lens.FieldOfView;
            }
            BeginTransition(DialState.Exiting);
        }

        public void BeginStoryPresentation(bool hideDialUI = true)
        {
            if (State != DialState.Success) return;
            storyOwnsSuccess = true;
            if (hideDialUI) { dialUI.Hide(); State = DialState.Story; }
        }

        public void EndStoryPresentation(bool completed)
        {
            storyOwnsSuccess = false;
            completeAfterExit = completed;
            ExitDialMode();
        }

        public void PrepareStoryReturn()
        {
            if (dialUI != null) dialUI.Hide();
            if (bodyView != null && bodyView.IsFirstPerson && bodyView.FirstPersonCamera != null)
            {
                var normal = bodyView.FirstPersonCamera;
                savedCameraPosition = normal.transform.position;
                savedCameraRotation = normal.transform.rotation;
                savedFov = normal.Lens.FieldOfView;
            }
            RestoreView();
        }
        public void FinishStoryReturn(bool completed)
        {
            completeAfterExit = completed;
            storyOwnsSuccess = false;
            RestoreGameplay();
        }
        private void RestoreView()
        {
            if (viewCamera != null)
            {
                viewCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
                viewCamera.fieldOfView = savedFov;
                viewCamera.nearClipPlane = savedNearClip;
            }
            if (brain != null) brain.enabled = savedBrainEnabled;
            foreach (Renderer renderer in hiddenRenderers) if (renderer != null) renderer.enabled = true;
            hiddenRenderers.Clear();
        }

        private void BeginTransition(DialState state)
        {
            State = state;
            transitionElapsed = 0;
            transitionStartPosition = viewCamera.transform.position;
            transitionStartRotation = viewCamera.transform.rotation;
            transitionStartFov = viewCamera.fieldOfView;
        }

        private void UpdateCameraTransition()
        {
            if (viewCamera == null || dialViewAnchor == null) { RestoreGameplay(); return; }
            transitionElapsed += Time.unscaledDeltaTime;
            bool entering = State == DialState.EnteringDialView;
            float duration = Mathf.Max(0.01f, entering ? cameraEntrySeconds : cameraExitSeconds);
            float t = Mathf.Clamp01(transitionElapsed / duration);
            FixedViewCameraTransition.Apply(viewCamera, transitionStartPosition, transitionStartRotation,
                transitionStartFov, entering ? dialViewAnchor.position : savedCameraPosition,
                entering ? dialViewAnchor.rotation : savedCameraRotation,
                entering ? dialFieldOfView : savedFov, FixedViewCameraTransition.Progress(cameraProgress, t));
            if (t >= 1f)
            {
                if (entering) State = DialState.Ready;
                else RestoreGameplay();
            }
        }

        private void Suspend(Behaviour component)
        {
            if (component == null || !component.enabled) return;
            suspended.Add(component);
            component.enabled = false;
        }

        private void RestoreGameplay()
        {
            if (State == DialState.Idle || State == DialState.Completed) return;
            if (rotaryDial != null) rotaryDial.Cancel();
            if (dialUI != null) dialUI.Hide();
            if (telephoneAudio != null) telephoneAudio.Stop();
            RestoreView();
            if (starterInputs != null)
            {
                starterInputs.MoveInput(Vector2.zero); starterInputs.LookInput(Vector2.zero);
                starterInputs.JumpInput(false); starterInputs.SprintInput(false);
            }
            if (movement != null) movement.ResetMotionAfterCinematic();
            if (bodyView != null) bodyView.SetInteractionLock(previousBodyLock);
            foreach (Behaviour component in suspended) if (component != null) component.enabled = true;
            suspended.Clear();
            if (inputModes != null) inputModes.Exit(this);
            State = completeAfterExit ? DialState.Completed : DialState.Idle;
            storyOwnsSuccess = false;
            DialViewExited?.Invoke();
        }

        protected override void OnDisable() { base.OnDisable(); RestoreGameplay(); }
        private void OnDestroy() { if (rotaryDial != null) rotaryDial.ReturnCompleted -= OnDialReturned; }
    }
}


