using System;
using DetectiveGame.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace DetectiveGame.Evidence
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EvidenceBoardService))]
    [RequireComponent(typeof(EvidenceBoardRuntimeUI))]
    public sealed class EvidenceBoardController : MonoBehaviour
    {
        [SerializeField] private GameInputModeController inputModes;

        private readonly InputAction closeAction = new InputAction(
            "Close Evidence Board", InputActionType.Button, "<Keyboard>/escape");
        private readonly InputAction playPauseAction = new InputAction(
            "Evidence Preview Play Pause", InputActionType.Button, "<Keyboard>/space");

        private EvidenceBoardService service;
        private EvidenceBoardRuntimeUI ui;
        private Object returnOwner;
        private GameInputMode returnMode = GameInputMode.Gameplay;
        private Action closedCallback;

        public static EvidenceBoardController Current { get; private set; }
        public static bool AnyOpen => Current != null && Current.IsOpen;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            service = GetComponent<EvidenceBoardService>();
            ui = GetComponent<EvidenceBoardRuntimeUI>();
            if (inputModes == null) inputModes = GameInputModeController.GetOrCreate();
            ui.Initialize(this, service);
            closeAction.AddBinding("<Gamepad>/buttonEast");
            playPauseAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void OnEnable()
        {
            Current = this;
            closeAction.performed += OnClose;
            playPauseAction.performed += OnPlayPause;
            closeAction.Enable();
            playPauseAction.Enable();
        }

        private void OnDisable()
        {
            closeAction.performed -= OnClose;
            playPauseAction.performed -= OnPlayPause;
            closeAction.Disable();
            playPauseAction.Disable();
            if (IsOpen) Close();
            if (Current == this) Current = null;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            closeAction.Dispose();
            playPauseAction.Dispose();
        }

        public bool OpenFromInvestigationWheel(
            Object wheelOwner,
            GameInputMode modeAfterClose,
            Action onClosed = null)
        {
            if (IsOpen || inputModes == null ||
                !inputModes.TryTransfer(wheelOwner, this, GameInputMode.FullScreenPanel)) return false;
            returnOwner = modeAfterClose == GameInputMode.Gameplay ? null : wheelOwner;
            returnMode = modeAfterClose;
            closedCallback = onClosed;
            IsOpen = true;
            ui.Show();
            return true;
        }

        public bool OpenDirect()
        {
            if (IsOpen || inputModes == null || !inputModes.TryEnter(this, GameInputMode.FullScreenPanel))
                return false;
            IsOpen = true;
            returnOwner = null;
            returnMode = GameInputMode.Gameplay;
            closedCallback = null;
            ui.Show();
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ui.Hide();
            bool returnedToPreviousOwner = inputModes != null && returnOwner != null &&
                                           inputModes.TryTransfer(this, returnOwner, returnMode);
            if (!returnedToPreviousOwner && inputModes != null) inputModes.Exit(this);
            Action callback = closedCallback;
            returnOwner = null;
            returnMode = GameInputMode.Gameplay;
            closedCallback = null;
            callback?.Invoke();
        }

        private void OnClose(InputAction.CallbackContext context)
        {
            if (IsOpen) Close();
        }

        private void OnPlayPause(InputAction.CallbackContext context)
        {
            if (IsOpen) ui.TogglePreviewPlayback();
        }
    }
}
