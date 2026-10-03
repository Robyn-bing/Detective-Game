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
    public enum DialogueLineType { Dialogue, InnerThought }
    public enum InvestigationMenuOption
    {
        TestimonyBoard,
        EvidenceBoard,
        Save,
        Hint,
        Settings,
        QuickSave,
        ReturnToTitle
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
        [Header("Case Notebook / Pause Menu")]
        [SerializeField] private bool useCaseNotebook;
        [SerializeField, Range(0.2f, 1.2f)] private float pauseHoldDuration = 0.45f;
        public bool UseCaseNotebook => useCaseNotebook;
        private bool notebookEvidence;
        private bool pauseHoldPending;
        private bool pauseEligibleLastFrame;
        private float pauseHoldStarted;
        private bool pauseTimeCaptured;
        private float timeScaleBeforePause;

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
        private UnityEngine.Object dialogueHost;
        private Action<bool> hostedFinished;
        private DialogueLineType currentLineType;
        private int lastAdvanceFrame = -1;
        private UnityEngine.Object choiceHost;
        private IReadOnlyList<DialogueChoiceOption> hostedChoices;
        private Action<int> hostedChoiceSelected;
        private bool presentationBlocked;
        private bool retainHostedFinalLine;
        public void SetPresentationBlocked(bool blocked) => presentationBlocked = blocked;
        public void HidePresentation() => ui.HideDialogue();
        public bool ShowHostedChoices(UnityEngine.Object host, IReadOnlyList<DialogueChoiceOption> options, Action<int> selected)
        {
            if (!CanBeginDialogue || host == null || !inputModes.IsOwnedBy(host)) return false;
            choiceHost = host; hostedChoices = options; hostedChoiceSelected = selected;
            choicesVisible = true;
            ui.ShowChoices(options, SelectChoice, true);
            return true;
        }
        public void RefreshHostedChoices(UnityEngine.Object host)
        {
            if (choiceHost == host) ui.RefreshChoiceOptions(hostedChoices);
        }
        public void CloseHostedChoices(UnityEngine.Object host)
        {
            if (choiceHost != host) return;
            choiceHost = null; hostedChoices = null; hostedChoiceSelected = null;
            choicesVisible = false; ui.HideChoices();
        }
        public bool IsDialogueActive => dialogueActive;
        public bool IsHostedDialogue => dialogueHost != null || choiceHost != null;
        public DialogueLineType CurrentLineType => currentLineType;
        public string CurrentSpeakerId => currentSpeakerId;

        public static DialogueController Current { get; private set; }
        public static bool AnyModalOpen =>
            (Current != null && Current.HasOpenModal) || EvidenceBoardController.AnyOpen;
        public bool HasOpenModal => dialogueActive || wheelOpen || boardOpen || logOpen || choiceHost != null;
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
            ui.ConfigureCaseNotebook(useCaseNotebook);

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
            cancelAction.canceled += OnCancelReleased;
            investigationMenuAction.performed += OnInvestigationMenu;
            advanceAction.Enable();
            cancelAction.Enable();
            investigationMenuAction.Enable();
        }

        private void OnDisable()
        {
            advanceAction.performed -= OnAdvance;
            cancelAction.performed -= OnCancel;
            cancelAction.canceled -= OnCancelReleased;
            ResetPauseHold();
            RestorePauseTime();
            if (boardOpen && notebookEvidence && evidenceBoard != null) evidenceBoard.HideNotebookPage();
            investigationMenuAction.performed -= OnInvestigationMenu;
            advanceAction.Disable();
            cancelAction.Disable();
            investigationMenuAction.Disable();
            var cancelledChoice = hostedChoiceSelected;
            AbortModalState();
            cancelledChoice?.Invoke(-1);
            FinishHostedDialogue(false);
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
            UpdatePauseHold();
            pauseEligibleLastFrame = CanStartPauseHold();
            bool recallActive = recallSession != null && recallSession.IsActive;
            ui.SetPersistentHudVisible(
                !IsHostedDialogue && (inputModes == null || inputModes.IsGameplay || dialogueActive) &&
                !recallActive && !wheelOpen && !boardOpen && !logOpen && !EvidenceBoardController.AnyOpen);

            if (presentationBlocked || (!dialogueActive && choiceHost == null) || !choicesVisible || boardOpen || logOpen || Keyboard.current == null) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectChoice(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectChoice(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectChoice(2);
            else if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectChoice(3);
        }

        public void BeginDialogue(InkFile inkFile)
        {
            TryBeginDialogue(inkFile);
        }

        // The host retains the existing input lock through dialogue, visual fade and camera return.
        public bool BeginHostedDialogue(InkFile inkFile, UnityEngine.Object host, Action<bool> onFinished, bool retainFinalLine = false)
        {
            if (!CanBeginDialogue || host == null || inputModes == null || !inputModes.IsOwnedBy(host)) return false;
            dialogueHost = host;
            retainHostedFinalLine = retainFinalLine;
            hostedFinished = onFinished;
            if (TryBeginDialogue(inkFile)) return true;
            dialogueHost = null;
            hostedFinished = null;
            return false;
        }

        public void AbortHostedDialogue(UnityEngine.Object host)
        {
            if (dialogueHost != host || host == null) return;
            AbortModalState();
            FinishHostedDialogue(false);
        }

        private bool TryBeginDialogue(InkFile inkFile)
        {
            if (!CanBeginDialogue) return false;
            if (inkFile == null || !inkFile.isCompiled)
            {
                Debug.LogError("Dialogue cannot start because its Ink file is missing or has compiler errors.", this);
                return false;
            }

            try
            {
                story = new Story(inkFile.storyJson);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }

            if (!IsHostedDialogue && !FreezeGameplay(GameInputMode.Dialogue))
            {
                story = null;
                return false;
            }

            dialogueLog.Clear();
            currentSpeakerId = InvestigationIds.Characters.Clara;
            currentLineType = DialogueLineType.Dialogue;
            lastAdvanceFrame = -1;
            dialogueActive = true;
            choicesVisible = false;
            ui.ShowDialogue();
            if (IsHostedDialogue) ui.SetPersistentHudVisible(false);
            bool wasBlocked = presentationBlocked;
            presentationBlocked = false;
            AdvanceStory();
            presentationBlocked = wasBlocked;
            return true;
        }

        public void AdvanceStory()
        {
            if (presentationBlocked || !dialogueActive || choicesVisible || wheelOpen || boardOpen || logOpen ||
                EvidenceBoardController.AnyOpen || story == null) return;
            if (lastAdvanceFrame == Time.frameCount) return;
            lastAdvanceFrame = Time.frameCount;

            while (story.canContinue)
            {
                string text = story.Continue().Trim();
                ProcessCurrentTags();
                if (string.IsNullOrWhiteSpace(text)) continue;

                ResolveSpeaker(currentSpeakerId, out string speakerName, out Sprite portrait);
                dialogueLog.Add(new DialogueLogEntry(speakerName + (currentLineType == DialogueLineType.InnerThought ? "（心声）" : ""), text));
                ui.ShowLine(speakerName, portrait, text, currentLineType);
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
            if (presentationBlocked) return;
            if (choiceHost != null)
            {
                if (displayedIndex < 0 || displayedIndex >= hostedChoices.Count || !hostedChoices[displayedIndex].Interactable) return;
                hostedChoiceSelected?.Invoke(displayedIndex);
                return;
            }
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
            if (useCaseNotebook) { ToggleCaseNotebook(); return; }
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
            if (useCaseNotebook && !CanStartPauseHold()) return;
            if (IsHostedDialogue) return;
            if (wheelOpen || wheelClosing || boardOpen || logOpen || EvidenceBoardController.AnyOpen ||
                (recallSession != null && recallSession.IsActive)) return;
            if (dialogueActive)
            {
                if (inputModes == null || !inputModes.TrySetMode(this, GameInputMode.InvestigationWheel)) return;
            }
            else if (!FreezeGameplay(GameInputMode.InvestigationWheel)) return;
            wheelOpen = true;
            if (useCaseNotebook)
            {
                timeScaleBeforePause = Time.timeScale;
                pauseTimeCaptured = true;
                Time.timeScale = 0f;
            }
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
            RestorePauseTime();
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
                InvestigationMenuOption.QuickSave => "快速存档",
                InvestigationMenuOption.ReturnToTitle => "返回标题",
                _ => "该"
            };
            ui.ShowFeatureUnavailable(label);
        }

        public void OpenBoard()
        {
            if (IsHostedDialogue) return;
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
            if (useCaseNotebook)
            {
                ui.ShowNotebookNavigation(true, notebookEvidence);
                if (notebookEvidence) ShowNotebookPage(true);
            }
        }

        public void CloseBoard()
        {
            if (!boardOpen) return;
            boardOpen = false;
            if (useCaseNotebook)
            {
                if (evidenceBoard != null) evidenceBoard.HideNotebookPage();
                ui.ShowNotebookNavigation(false, notebookEvidence);
            }
            ui.HideBoard(dialogueActive);
            if (dialogueActive)
            {
                if (inputModes != null) inputModes.TrySetMode(this, GameInputMode.Dialogue);
            }
            else RestoreGameplay();
        }

        public void OpenDialogueLog()
        {
            if (IsHostedDialogue) return;
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
            currentLineType = DialogueLineType.Dialogue;
            IReadOnlyList<string> tags = story.currentTags;
            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i].Trim();
                const string speakerPrefix = "speaker:";
                if (tag.StartsWith(speakerPrefix, StringComparison.OrdinalIgnoreCase))
                    currentSpeakerId = tag.Substring(speakerPrefix.Length).Trim();
                else if (tag.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                    Enum.TryParse(tag.Substring(5).Trim(), true, out currentLineType);
                else
                    knowledge.ApplyInkTag(tag);
            }
        }

        private void CompleteDialogue()
        {
            dialogueActive = false;
            choicesVisible = false;
            story = null;
            if (IsHostedDialogue) { if (!retainHostedFinalLine) ui.HideDialogue(); FinishHostedDialogue(true); return; }
            ui.HideDialogue();
            knowledge.NotifyDialogueCompleted();
            ui.ShowBoardUpdatedToast();
            RestoreGameplay();
        }

        public void CancelDialogue()
        {
            if (IsHostedDialogue) return;
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
            choiceHost = null; hostedChoices = null; hostedChoiceSelected = null; presentationBlocked = false;
            dialogueActive = false;
            choicesVisible = false;
            wheelOpen = false;
            wheelClosing = false;
            boardOpen = false;
            logOpen = false;
            story = null;

            if (ui == null) return;
            ui.ShowNotebookNavigation(false, notebookEvidence);
            ResetPauseHold();
            RestorePauseTime();
            ui.HideChoices();
            ui.HideDialogue();
            ui.HideInvestigationWheel(false, null, true);
            ui.HideBoard(false);
            ui.HideDialogueLog(false);
        }

        private void RestoreGameplay(bool force = false)
        {
            if (IsHostedDialogue) return;
            if (!force && HasOpenModal) return;
            if (inputModes == null) return;
            if (force) inputModes.ForceExit(this);
            else inputModes.Exit(this);
        }

        private void OnAdvance(InputAction.CallbackContext context) => AdvanceStory();

        private void OnInvestigationMenu(InputAction.CallbackContext context)
        {
            if (useCaseNotebook) { ToggleCaseNotebook(); return; }
            if (logOpen || EvidenceBoardController.AnyOpen) return;
            ToggleInvestigationWheel();
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (useCaseNotebook)
            {
                // Eligibility is captured on key-down: closing a panel must never arm a hold.
                ResetPauseHold();
                if (boardOpen) { CloseBoard(); return; }
                if (pauseEligibleLastFrame && CanStartPauseHold())
                {
                    pauseHoldPending = true;
                    pauseHoldStarted = Time.unscaledTime;
                    ui.SetPauseHoldProgress(0f, true);
                    return;
                }
            }
            if (presentationBlocked) return;
            if (choiceHost != null) { hostedChoiceSelected?.Invoke(-1); return; }
            if (IsHostedDialogue) return;
            if (EvidenceBoardController.AnyOpen) return;
            if (boardOpen) CloseBoard();
            else if (logOpen) CloseDialogueLog();
            else if (wheelOpen) CloseInvestigationWheel();
            else if (dialogueActive) CancelDialogue();
        }

        public void ToggleCaseNotebook()
        {
            if (!useCaseNotebook || wheelOpen || wheelClosing || logOpen || IsHostedDialogue) return;
            ResetPauseHold();
            if (boardOpen) CloseBoard();
            else if (!EvidenceBoardController.AnyOpen) OpenBoard();
        }

        public void ShowNotebookPage(bool evidence)
        {
            if (!useCaseNotebook || !boardOpen) return;
            if (evidence)
            {
                if (evidenceBoard == null || !evidenceBoard.ShowNotebookPage(this, CloseBoard)) return;
                ui.HideBoard(false);
                ui.SetPersistentHudVisible(false);
            }
            else
            {
                if (evidenceBoard != null) evidenceBoard.HideNotebookPage();
                ui.ShowBoard();
            }
            notebookEvidence = evidence;
            ui.ShowNotebookNavigation(true, evidence);
        }

        private bool CanStartPauseHold() => useCaseNotebook && !HasOpenModal &&
            !EvidenceBoardController.AnyOpen && !presentationBlocked && inputModes != null &&
            inputModes.IsGameplay && (recallSession == null || !recallSession.IsActive);

        private void UpdatePauseHold()
        {
            if (!pauseHoldPending) return;
            if (!cancelAction.IsPressed() || !CanStartPauseHold()) { ResetPauseHold(); return; }
            float progress = Mathf.Clamp01((Time.unscaledTime - pauseHoldStarted) / pauseHoldDuration);
            ui.SetPauseHoldProgress(progress, true);
            if (progress < 1f) return;
            ResetPauseHold();
            OpenInvestigationWheel();
        }

        private void OnCancelReleased(InputAction.CallbackContext context) => ResetPauseHold();
        private void OnApplicationFocus(bool focused) { if (!focused) ResetPauseHold(); }
        private void ResetPauseHold()
        {
            pauseHoldPending = false;
            if (ui != null) ui.SetPauseHoldProgress(0f, false);
        }

        private void RestorePauseTime()
        {
            if (!pauseTimeCaptured) return;
            Time.timeScale = timeScaleBeforePause;
            pauseTimeCaptured = false;
        }

        private void FinishHostedDialogue(bool completed)
        {
            var callback = hostedFinished;
            dialogueHost = null;
            hostedFinished = null;
            callback?.Invoke(completed);
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
