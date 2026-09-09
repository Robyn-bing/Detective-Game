using DetectiveGame.Interaction;
using Ink.UnityIntegration;
using UnityEngine;

namespace DetectiveGame.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class DialogueInteractable : WorldInteractable
    {
        [Header("Dialogue")]
        [SerializeField] private InkFile conversation;
        [SerializeField] private DialogueController dialogueController;
        [SerializeField] private string promptText = "[E] 对话";

        public override string PromptText => promptText;
        public override bool IsInteractionAvailable => base.IsInteractionAvailable &&
                                                       dialogueController != null &&
                                                       dialogueController.CanBeginDialogue;

        private void Awake()
        {
            if (dialogueController == null) dialogueController = FindAnyObjectByType<DialogueController>();
        }

        public override void Interact(Transform interactor)
        {
            if (!IsInteractionAvailable) return;
            dialogueController.BeginDialogue(conversation);
        }
    }
}
