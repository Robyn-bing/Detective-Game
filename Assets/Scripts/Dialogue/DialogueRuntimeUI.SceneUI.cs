using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DetectiveGame.Dialogue
{
    public sealed partial class DialogueRuntimeUI
    {
        [Header("Scene UI — edit the referenced Canvas and templates")]
        [SerializeField] private bool useSceneUI;
        [SerializeField] private Button notebookButton;
        [SerializeField] private Button advanceButton;
        [SerializeField] private Button boardCloseButton;
        [SerializeField] private Button logCloseButton;
        [SerializeField] private Button choiceTemplate;
        [SerializeField] private Color notebookSelectedColor = new Color(.66f, .12f, .1f, 1f);
        [SerializeField] private Color notebookNormalColor = new Color(.025f, .025f, .03f, .94f);
        private Color dialogueNormalColor;

        private void BindSceneUI()
        {
            dialogueNormalColor = dialogueText.color;
            notebookButton.onClick.AddListener(() => controller?.ToggleInvestigationWheel());
            dialogueLogButtonObject.GetComponent<Button>().onClick.AddListener(() => controller?.OpenDialogueLog());
            advanceButton.onClick.AddListener(() => controller?.AdvanceStory());
            boardCloseButton.onClick.AddListener(() => controller?.CloseBoard());
            logCloseButton.onClick.AddListener(() => controller?.CloseDialogueLog());
            if (testimonyTab != null) testimonyTab.onClick.AddListener(() => controller?.ShowNotebookPage(false));
            if (evidenceTab != null) evidenceTab.onClick.AddListener(() => controller?.ShowNotebookPage(true));
            wheelRoot.GetComponent<RadialMenuPointerHandler>().Configure(ConfirmWheelSelection);
            foreach (WheelItemView item in wheelItems)
            {
                item.BasePosition = item.Content.anchoredPosition;
                item.Angle = Mathf.Atan2(item.BasePosition.y, item.BasePosition.x) * Mathf.Rad2Deg;
                item.Label = item.Caption.text;
            }
            wheelRoot.SetActive(false);
            dialogueRoot.SetActive(false);
            choiceRoot.SetActive(false);
            boardRoot.SetActive(false);
            logRoot.SetActive(false);
            toastRoot.SetActive(false);
            pauseHoldRoot.SetActive(false);
            dialogueLogButtonObject.SetActive(false);
            if (notebookNavigation != null) notebookNavigation.SetActive(false);
        }

        private void ShowSceneChoices(IReadOnlyList<DialogueChoiceOption> options, Action<int> selected)
        {
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                Button button = Instantiate(choiceTemplate, choiceContainer);
                button.gameObject.SetActive(true);
                button.onClick.AddListener(() => { if (options[index].Interactable) selected(index); });
                choiceObjects.Add(button.gameObject);
            }
            RefreshChoiceOptions(options);
            LayoutRebuilder.ForceRebuildLayoutImmediate(choiceContainer);
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            for (int i = 0; i < options.Count; i++)
                if (options[i].Interactable) { EventSystem.current.SetSelectedGameObject(choiceObjects[i]); break; }
        }
    }
}
