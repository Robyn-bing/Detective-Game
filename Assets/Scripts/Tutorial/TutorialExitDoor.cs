using DetectiveGame.Dialogue;
using DetectiveGame.Input;
using DetectiveGame.Interaction;
using DetectiveGame.Player;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialExitDoor : WorldInteractable
    {
        public enum ExitState { Idle, Choosing, Leaving }
        [SerializeField] private DialogueController dialogue;
        [SerializeField] private TutorialPhoneCallController phoneProgress;
        [SerializeField] private string stayLabel = "留在房间再看看";
        [SerializeField] private string lockedDestinationLabel = "出发去████████";
        [SerializeField] private string unlockedDestinationLabel = "出发去马洛新月街17号";
        [SerializeField] private string destinationScene = "Test";
        [SerializeField, Min(0f)] private float sceneFadeOutDuration = 0.8f;
        [SerializeField, Min(0f)] private float sceneFadeInDuration = 0.7f;
        private readonly DialogueChoiceOption[] options = { new DialogueChoiceOption("", true), new DialogueChoiceOption("", false) };
        private GameInputModeController input;
        private FullBodyViewController body;
        private ThirdPersonController movement;
        private bool previousBodyLock, previousMovement;
        public ExitState State { get; private set; }
        public bool DestinationUnlocked => phoneProgress != null && phoneProgress.Completed;
        public override string PromptText => "【E 开门】";
        public override bool IsInteractionAvailable => base.IsInteractionAvailable && State == ExitState.Idle &&
            (GameInputModeController.Current == null || GameInputModeController.Current.IsGameplay);
        protected override void OnEnable()
        {
            base.OnEnable();
            if (phoneProgress != null) phoneProgress.OnTutorialPhoneCallCompleted.AddListener(RefreshOptions);
        }
        public override void Interact(Transform interactor)
        {
            if (!IsInteractionAvailable || dialogue == null || !dialogue.CanBeginDialogue || interactor == null) return;
            var player = interactor.GetComponentInParent<PlayerInput>();
            if (player == null) return;
            input = GameInputModeController.GetOrCreate(player, player.GetComponent<StarterAssetsInputs>());
            if (!input.TryEnter(this, GameInputMode.Dialogue)) return;
            body = player.GetComponent<FullBodyViewController>();
            movement = player.GetComponent<ThirdPersonController>();
            previousBodyLock = body != null && body.InteractionLocked;
            previousMovement = movement != null && movement.enabled;
            if (body != null) body.SetInteractionLock(true);
            if (movement != null) { movement.ResetMotionAfterCinematic(); movement.enabled = false; }
            State = ExitState.Choosing;
            RefreshOptions();
            if (!dialogue.ShowHostedChoices(this, options, Choose)) RestoreGameplay();
        }
        private void RefreshOptions()
        {
            options[0].Label = stayLabel;
            options[1].Label = DestinationUnlocked ? unlockedDestinationLabel : lockedDestinationLabel;
            options[1].Interactable = DestinationUnlocked;
            if (dialogue != null && State == ExitState.Choosing) dialogue.RefreshHostedChoices(this);
        }
        private void Choose(int index)
        {
            if (State != ExitState.Choosing) return;
            if (index <= 0) { dialogue.CloseHostedChoices(this); RestoreGameplay(); return; }
            if (index != 1 || !DestinationUnlocked) return;
            if (ScreenFade.Current == null || !Application.CanStreamedLevelBeLoaded(destinationScene))
            { Debug.LogWarning("Destination scene must be enabled in the active Build Profile: " + destinationScene, this); return; }
            State = ExitState.Leaving;
            dialogue.CloseHostedChoices(this);
            if (!ScreenFade.Current.LoadSceneWithFade(destinationScene, sceneFadeOutDuration, sceneFadeInDuration, RestoreGameplay))
                RestoreGameplay();
        }
        private void RestoreGameplay()
        {
            if (State == ExitState.Idle) return;
            if (body != null) body.SetInteractionLock(previousBodyLock);
            if (movement != null) { movement.ResetMotionAfterCinematic(); movement.enabled = previousMovement; }
            if (input != null) input.Exit(this);
            State = ExitState.Idle;
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            if (phoneProgress != null) phoneProgress.OnTutorialPhoneCallCompleted.RemoveListener(RefreshOptions);
            if (dialogue != null) dialogue.CloseHostedChoices(this);
            RestoreGameplay();
        }
    }
}
