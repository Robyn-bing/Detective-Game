using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Input
{
    public enum GameInputMode
    {
        Gameplay,
        InvestigationWheel,
        FullScreenPanel,
        Dialogue,
        Recall
    }

    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameInputModeController : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private StarterAssetsInputs starterInputs;

        private Object owner;
        private bool previousPlayerInputActive;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        public static GameInputModeController Current { get; private set; }
        public GameInputMode Mode { get; private set; } = GameInputMode.Gameplay;
        public bool IsGameplay => owner == null;

        public static GameInputModeController GetOrCreate(
            PlayerInput preferredPlayerInput = null,
            StarterAssetsInputs preferredStarterInputs = null)
        {
            if (Current != null)
            {
                Current.Configure(preferredPlayerInput, preferredStarterInputs);
                return Current;
            }

            GameInputModeController existing = FindAnyObjectByType<GameInputModeController>();
            if (existing != null)
            {
                existing.Configure(preferredPlayerInput, preferredStarterInputs);
                return existing;
            }

            GameObject host = preferredPlayerInput != null
                ? preferredPlayerInput.gameObject
                : new GameObject("Game Input Mode Controller (Runtime)");
            GameInputModeController created = host.GetComponent<GameInputModeController>();
            if (created == null) created = host.AddComponent<GameInputModeController>();
            created.Configure(preferredPlayerInput, preferredStarterInputs);
            return created;
        }

        private void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            if (playerInput == null) playerInput = FindAnyObjectByType<PlayerInput>();
            if (starterInputs == null && playerInput != null)
                starterInputs = playerInput.GetComponent<StarterAssetsInputs>();
            if (starterInputs == null) starterInputs = FindAnyObjectByType<StarterAssetsInputs>();
        }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            ForceRestore();
            if (Current == this) Current = null;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public bool TryEnter(Object requester, GameInputMode requestedMode)
        {
            if (requester == null || requestedMode == GameInputMode.Gameplay) return false;
            if (owner != null && owner != requester) return false;

            if (owner == null)
            {
                owner = requester;
                CaptureAndFreezeGameplay();
            }

            Mode = requestedMode;
            return true;
        }

        public bool TrySetMode(Object requester, GameInputMode requestedMode)
        {
            if (owner != requester || requestedMode == GameInputMode.Gameplay) return false;
            Mode = requestedMode;
            return true;
        }

        public void Exit(Object requester)
        {
            if (owner != requester) return;
            RestoreGameplay();
        }

        public void ForceExit(Object requester)
        {
            if (owner != requester) return;
            RestoreGameplay();
        }

        public bool IsOwnedBy(Object requester) => owner == requester;

        private void Configure(PlayerInput preferredPlayerInput, StarterAssetsInputs preferredStarterInputs)
        {
            if (playerInput == null && preferredPlayerInput != null) playerInput = preferredPlayerInput;
            if (starterInputs == null && preferredStarterInputs != null) starterInputs = preferredStarterInputs;
        }

        private void CaptureAndFreezeGameplay()
        {
            previousPlayerInputActive = playerInput != null && playerInput.inputIsActive;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;

            if (starterInputs != null)
            {
                starterInputs.MoveInput(Vector2.zero);
                starterInputs.LookInput(Vector2.zero);
                starterInputs.JumpInput(false);
                starterInputs.SprintInput(false);
            }

            if (playerInput != null && playerInput.isActiveAndEnabled) playerInput.DeactivateInput();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreGameplay()
        {
            owner = null;
            Mode = GameInputMode.Gameplay;
            if (playerInput != null && previousPlayerInputActive && playerInput.isActiveAndEnabled)
                playerInput.ActivateInput();
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private void ForceRestore()
        {
            if (owner == null) return;
            RestoreGameplay();
        }
    }
}
