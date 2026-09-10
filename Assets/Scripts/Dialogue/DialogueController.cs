using System;
using System.Collections.Generic;
using DetectiveGame.Evidence;
using DetectiveGame.Input;
using DetectiveGame.Investigation;
using DetectiveGame.Recall;
using Ink.Runtime;
using Ink.UnityIntegration;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Dialogue
{
    public enum InvestigationMenuOption
    {
        TestimonyBoard,
        EvidenceBoard,
        Save,
        Hint,
        Settings
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(InvestigationKnowledgeService))]
    [RequireComponent(typeof(DialogueRuntimeUI))]
    public sealed class DialogueController : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private InvestigationDatabase investigationDatabase;

        [Header("Scene References")]
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private StarterAssetsInputs starterInputs;
        [SerializeField] private RecallSessionController recallSession;
        [SerializeField] private GameInputModeController inputModes;
        [SerializeField] private EvidenceBoardController evidenceBoard;

        private readonly InputAction advanceAction = new InputAction(
            "Advance Dialogue", InputActionType.Button, "<Keyboard>/space");
        private readonly InputAction cancelAction = new InputAction(
            "Close Investigation UI", InputActionType.Button, "<Keyboard>/escape");
        private readonly InputAction investigationMenuAction = new InputAction(
            "Investigation Menu", InputActionType.Button, "<Keyboard>/tab");
        private readonly List<DialogueLogEntry> dialogueLog = new List<DialogueLogEntry>();

        private InvestigationKnowledgeService knowledge;
        private DialogueRuntimeUI ui;
        private Story story;
        private string currentSpeakerId = InvestigationIds.Characters.Clara;
        private bool dialogueActive;
        private bool choicesVisible;
        private bool wheelOpen;
        private bool wheelClosing;
        private bool boardOpen;
        private bool logOpen;

        public static DialogueController Current { get; private set; }
        public static bool AnyModalOpen =>
            (Current != null && Current.HasOpenModal) || EvidenceBoardController.AnyOpen;
        public bool HasOpenModal => dialogueActive || wheelOpen || boardOpen || logOpen;
        public bool CanBeginDialogue => !HasOpenModal && (recallSession == null || !recallSession.IsActive);

        private void Awake()
        {
            if (playerInput == null) playerInput = FindAnyObjectByType<PlayerInput>();
            if (starterInputs == null) starterInputs = FindAnyObjectByType<StarterAssetsInputs>();
            if (recallSession == null) recallSession = FindAnyObjectByType<RecallSessionController>();
            if (evidenceBoard == null) evidenceBoard = FindAnyObjectByType<EvidenceBoardController>();
            if (inputModes == null) inputModes = GameInputModeController.GetOrCreate(playerInput, starterInputs);

            knowledge = GetComponent<InvestigationKnowledgeService>();
            knowledge.Configure(investigationDatabase);
            ui = GetComponent<DialogueRuntimeUI>();
            ui.Initialize(this, knowledge, investigationDatabase);

            advanceAction.AddBinding("<Keyboard>/enter");
            advanceAction.AddBinding("<Gamepad>/buttonSouth");
            cancelAction.AddBinding("<Gamepad>/buttonEast");
            investigationMenuAction.AddBinding("<Gamepad>/start");
        }

        private void OnEnable()
        {
            Current = this;
            advanceAction.performed += OnAdvance;
            cancelAction.performed += OnCancel;
            investigationMenuAction.performed += OnInvestigationMenu;
            advanceAction.Enable();
            cancelAction.Enable();
            investigationMenuAction.Enable();
        }

        private void OnDisable()
        {
            advanceAction.performed -= OnAdvance;
            cancelAction.performed -= OnCancel;
            investigationMenuAction.performed -= OnInvestigationMenu;
            advanceAction.Disable();
            cancelAction.Disable();
            investigationMenuAction.Disable();
            AbortModalState();
            RestoreGameplay(true);
            if (Current == this) Current = null;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            advanceAction.Dispose();
            cancelAction.Dispose();
            investigationMenuAction.Dispose();
        }

        private void Update()
        {
            bool recallActive = recallSession != null && recallSession.IsActive;
            ui.SetPersistentHudVisible(
                !recallActive && !wheelOpen && !boardOpen && !logOpen && !EvidenceBoardController.AnyOpen);

            if (!dialogueActive || !choicesVisible || boardOpen || logOpen || Keyboard.current == null) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectChoice(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectChoice(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectChoice(2);
            else if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectChoice(3);
        }

        public void BeginDialogue(InkFile inkFile)
        {
            if (!CanBeginDialogue) return;
            if (inkFile == null || !inkFile.isCompiled)
            {
                Debug.LogError("Dialogue cannot start because its Ink file is missing or has compiler errors.", this);
                return;
            }

            try
            {
                story = new Story(inkFile.storyJson);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return;
            }

            if (!FreezeGameplay(GameInputMode.Dialogue))
            {
                story = null;
                return;
            }

            dialogueLog.Clear();
            currentSpeakerId = InvestigationIds.Characters.Clara;
            dialogueActive = true;
            choicesVisible = false;
            ui.ShowDialogue();
            AdvanceStory();
        }

        public void AdvanceStory()
        {
            if (!dialogueActive || choicesVisible || wheelOpen || boardOpen || logOpen ||
                EvidenceBoardController.AnyOpen || story == null) return;

            while (story.canContinue)
            {
                string text = story.Continue().Trim();
                ProcessCurrentTags();
                if (string.IsNullOrWhiteSpace(text)) continue;

                ResolveSpeaker(currentSpeakerId, out string speakerName, out Sprite portrait);
                dialogueLog.Add(new DialogueLogEntry(speakerName, text));
                ui.ShowLine(speakerName, portrait, text);
                return;
            }

            if (story.currentChoices.Count > 0)
            {
                choicesVisible = true;
                var labels = new List<string>(story.currentChoices.Count);
                for (int i = 0; i < story.currentChoices.Count; i++)
                    labels.Add(story.currentChoices[i].text.Trim());
                ui.ShowChoices(labels, SelectChoice);
                return;
            }

            CompleteDialogue();
        }

        public void SelectChoice(int displayedIndex)
        {
            if (!dialogueActive || !choicesVisible || story == null || wheelOpen || boardOpen || logOpen ||
                EvidenceBoardController.AnyOpen) return;
            if (displayedIndex < 0 || displayedIndex >= story.currentChoices.Count) return;

            int inkChoiceIndex = story.currentChoices[displayedIndex].index;
            story.ChooseChoiceIndex(inkChoiceIndex);
            choicesVisible = false;
            ui.HideChoices();
            AdvanceStory();
        }

        public void ToggleBoard()
        {
            if (boardOpen) CloseBoard();
            else OpenBoard();
        }

        public void ToggleInvestigationWheel()
        {
            if (wheelOpen)
            {
                CloseInvestigationWheel();
                return;
            }

            if (boardOpen)
            {
                CloseBoard();
                return;
            }

            OpenInvestigationWheel();
        }

        public void OpenInvestigationWheel()
        {
            if (wheelOpen || wheelClosing || boardOpen || logOpen || EvidenceBoardController.AnyOpen ||
                (recallSession != null && recallSession.IsActive)) return;
            if (dialogueActive)
            {
                if (inputModes == null || !inputModes.TrySetMode(this, GameInputMode.InvestigationWheel)) return;
            }
            else if (!FreezeGameplay(GameInputMode.InvestigationWheel)) return;
            wheelOpen = true;
            ui.ShowInvestigationWheel();
        }

        public void CloseInvestigationWheel()
        {
            if (!wheelOpen || wheelClosing) return;
            wheelClosing = true;
            bool returnToDialogue = dialogueActive;
            ui.HideInvestigationWheel(returnToDialogue, () => FinishClosingInvestigationWheel(returnToDialogue));
        }

        private void FinishClosingInvestigationWheel(bool returnToDialogue)
        {
            wheelOpen = false;
            wheelClosing = false;
            if (returnToDialogue && dialogueActive)
            {
                if (inputModes != null) inputModes.TrySetMode(this, GameInputMode.Dialogue);
                return;
            }

            ui.SetPersistentHudVisible(true);
            RestoreGameplay();
        }

        public void SelectInvestigationMenuOption(InvestigationMenuOption option)
        {
            if (!wheelOpen || wheelClosing) return;
            if (option == InvestigationMenuOption.TestimonyBoard)
            {
                OpenBoard();
                return;
            }

            if (option == InvestigationMenuOption.EvidenceBoard)
            {
                if (evidenceBoard == null) evidenceBoard = FindAnyObjectByType<EvidenceBoardController>();
                bool returnToDialogue = dialogueActive;
                GameInputMode returnMode = returnToDialogue ? GameInputMode.Dialogue : GameInputMode.Gameplay;
                if (evidenceBoard == null || !evidenceBoard.OpenFromInvestigationWheel(
                        this, returnMode,
                        () => ui.HideInvestigationWheel(returnToDialogue, null, true))) return;
                wheelOpen = false;
                wheelClosing = false;
                ui.HideInvestigationWheel(false, null, true);
                return;
            }

            string label = option switch
            {
                InvestigationMenuOption.Save => "存档",
                InvestigationMenuOption.Hint => "提示",
                InvestigationMenuOption.Settings => "设置",
                _ => "该"
            };
            ui.ShowFeatureUnavailable(label);
        }

        public void OpenBoard()
        {
            if (boardOpen || (recallSession != null && recallSession.IsActive)) return;
            if (logOpen) CloseDialogueLog();
            if (wheelOpen)
            {
                if (inputModes == null || !inputModes.TrySetMode(this, GameInputMode.FullScreenPanel)) return;
                wheelOpen = false;
                wheelClosing = false;
                ui.HideInvestigationWheel(false, null, true);
            }
            else if (!dialogueActive && !FreezeGameplay(GameInputMode.FullScreenPanel)) return;
            else if (dialogueActive && (inputModes == null ||
                                       !inputModes.TrySetMode(this, GameInputMode.FullScreenPanel))) return;
            boardOpen = true;
            ui.ShowBoard();
        }

        public void CloseBoard()
        {
            if (!boardOpen) return;
            boardOpen = false;
            ui.HideBoard(dialogueActive);
            if (dialogueActive)
            {
                if (inputModes != null) inputModes.TrySetMode(this, GameInputMode.Dialogue);
            }
            else RestoreGameplay();
        }

        public void OpenDialogueLog()
        {
            if (!dialogueActive || logOpen || boardOpen) return;
            logOpen = true;
            if (inputModes != null) inputModes.TrySetMode(this, GameInputMode.FullScreenPanel);
            ui.ShowDialogueLog(dialogueLog);
        }

        public void CloseDialogueLog()
        {
            if (!logOpen) return;
            logOpen = false;
            ui.HideDialogueLog(dialogueActive);
            if (dialogueActive && inputModes != null) inputModes.TrySetMode(this, GameInputMode.Dialogue);
        }

        private void ProcessCurrentTags()
        {
            IReadOnlyList<string> tags = story.currentTags;
            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i].Trim();
                const string speakerPrefix = "speaker:";
                if (tag.StartsWith(speakerPrefix, StringComparison.OrdinalIgnoreCase))
                    currentSpeakerId = tag.Substring(speakerPrefix.Length).Trim();
                else
                    knowledge.ApplyInkTag(tag);
            }
        }

        private void CompleteDialogue()
        {
            dialogueActive = false;
            choicesVisible = false;
            story = null;
            ui.HideDialogue();
            knowledge.NotifyDialogueCompleted();
            ui.ShowBoardUpdatedToast();
            RestoreGameplay();
        }

        public void CancelDialogue()
        {
            if (!dialogueActive || wheelOpen || boardOpen || logOpen || EvidenceBoardController.AnyOpen) return;
            dialogueActive = false;
            choicesVisible = false;
            story = null;
            ui.HideDialogue();
            RestoreGameplay();
        }

        private void ResolveSpeaker(string characterId, out string speakerName, out Sprite portrait)
        {
            if (investigationDatabase != null &&
                investigationDatabase.TryGetCharacter(characterId, out CharacterDefinition character))
            {
                speakerName = character.DisplayName;
                portrait = character.Portrait;
                return;
            }

            speakerName = characterId;
            portrait = null;
        }

        private bool FreezeGameplay(GameInputMode mode)
        {
            return inputModes != null && inputModes.TryEnter(this, mode);
        }

        private void AbortModalState()
        {
            dialogueActive = false;
            choicesVisible = false;
            wheelOpen = false;
            wheelClosing = false;
            boardOpen = false;
            logOpen = false;
            story = null;

            if (ui == null) return;
            ui.HideChoices();
            ui.HideDialogue();
            ui.HideInvestigationWheel(false, null, true);
            ui.HideBoard(false);
            ui.HideDialogueLog(false);
        }

        private void RestoreGameplay(bool force = false)
        {
            if (!force && HasOpenModal) return;
            if (inputModes == null) return;
            if (force) inputModes.ForceExit(this);
            else inputModes.Exit(this);
        }

        private void OnAdvance(InputAction.CallbackContext context) => AdvanceStory();

        private void OnInvestigationMenu(InputAction.CallbackContext context)
        {
            if (logOpen || EvidenceBoardController.AnyOpen) return;
            ToggleInvestigationWheel();
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (EvidenceBoardController.AnyOpen) return;
            if (boardOpen) CloseBoard();
            else if (logOpen) CloseDialogueLog();
            else if (wheelOpen) CloseInvestigationWheel();
            else if (dialogueActive) CancelDialogue();
        }
    }

    public readonly struct DialogueLogEntry
    {
        public readonly string Speaker;
        public readonly string Text;

        public DialogueLogEntry(string speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }
    }
}
