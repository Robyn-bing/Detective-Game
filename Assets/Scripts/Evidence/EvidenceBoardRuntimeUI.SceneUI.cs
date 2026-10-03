using UnityEngine;
using UnityEngine.UI;
namespace DetectiveGame.Evidence
{
    public sealed partial class EvidenceBoardRuntimeUI
    {
        [Header("Scene UI — editable Canvas and dynamic item templates")]
        [SerializeField] private bool useSceneUI;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button playButton;
        [SerializeField] private EvidenceCardView cardTemplate;
        [SerializeField] private EvidenceMomentView momentTemplate;
        [SerializeField] private Button optionTemplate;
        [SerializeField] private GameObject markerTemplate;

        private void BindSceneUI()
        {
            closeButton.onClick.AddListener(() => controller?.Close());
            playButton.onClick.AddListener(TogglePreviewPlayback);
            timelineSlider.onValueChanged.AddListener(OnTimelineChanged);
            previewImage.texture = previewTexture;
            boardRoot.SetActive(false);
            toastRoot.SetActive(false);
        }

        private void RefreshSceneEvidenceCards()
        {
            ClearObjects(evidenceCards);
            if (service == null) return;
            foreach (EvidenceDefinition evidence in service.DiscoveredEvidence)
            {
                if (evidence == null) continue;
                EvidenceCardView card = Instantiate(cardTemplate, evidenceContent);
                card.gameObject.SetActive(true);
                card.label.text = evidence.DisplayName;
                card.icon.sprite = evidence.Icon;
                card.icon.gameObject.SetActive(evidence.Icon != null);
                if (evidence == selectedEvidence)
                {
                    ColorBlock colors = card.button.colors;
                    colors.normalColor = card.selectedColor;
                    card.button.colors = colors;
                }
                card.button.onClick.AddListener(() => SelectEvidence(evidence));
                evidenceCards.Add(card.gameObject);
            }
        }

        private void RefreshSceneDeductionRows()
        {
            ClearObjects(deductionRows);
            if (selectedEvidence == null) return;
            for (int i = 0; i < selectedEvidence.MomentCount; i++)
            {
                EvidenceMomentDefinition moment = selectedEvidence.GetMoment(i);
                if (moment == null) continue;
                EvidenceMomentView row = Instantiate(momentTemplate, deductionContent);
                row.gameObject.SetActive(true);
                deductionRows.Add(row.gameObject);
                row.fact.text = $"<b>[{moment.TimeLabel}]</b>  {moment.ObservedFact}";
                EvidenceChoiceDefinition selected = service.GetSelectedChoice(selectedEvidence, moment);
                if (selected != null)
                {
                    row.selection.text = selected.Text;
                    row.dropZone.GetComponent<Image>().color = row.selectedColor;
                }
                row.dropZone.Configure(this, moment.Id);
                for (int n = 0; n < moment.ChoiceCount; n++)
                {
                    EvidenceChoiceDefinition choice = moment.GetChoice(n);
                    if (choice == null) continue;
                    Button option = Instantiate(optionTemplate, row.options);
                    option.gameObject.SetActive(true);
                    option.GetComponentInChildren<Text>(true).text = $"{(char)('A' + n)}. {choice.Text}";
                    option.GetComponent<EvidenceChoiceDragHandler>().Configure(this, canvasRect, moment.Id, choice.Id);
                }
            }
        }
    }
}
