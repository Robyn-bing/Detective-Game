using UnityEngine;
using UnityEngine.UI;
namespace DetectiveGame.Recall
{
    public sealed partial class RecallRuntimeUI
    {
        [Header("Scene UI — edit Canvas and period template")]
        [SerializeField] private bool useSceneUI;
        [SerializeField] private RectTransform periodContent;
        [SerializeField] private Image periodTemplate;
        [SerializeField] private Color selectedPeriodColor = new Color(.08f, .55f, .64f, .92f);
        [SerializeField] private float selectionHeaderAndFooterHeight = 92f;

        private void BindSceneUI() { }

        private void ShowScenePeriods(RecallObjectData data, int selectedIndex)
        {
            for (int i = 0; i < data.AvailablePeriodCount; i++)
            {
                RecallPeriod period = data.GetPeriod(data.GetAvailablePeriodIndex(i));
                Image entry = Instantiate(periodTemplate, periodContent);
                entry.name = "Period " + (i + 1);
                entry.gameObject.SetActive(true);
                entry.GetComponentInChildren<Text>(true).text = $"[{i + 1}]  {period.TimeRange}    {period.Label}";
                periodRects.Add(entry.rectTransform);
                periodBackgrounds.Add(entry);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(periodContent);
            selectionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                LayoutUtility.GetPreferredHeight(periodContent) + selectionHeaderAndFooterHeight);
            UpdatePeriodSelection(selectedIndex);
            if (selectionAnimation != null) StopCoroutine(selectionAnimation);
            selectionAnimation = StartCoroutine(AnimatePeriodSelectionOpen());
        }
    }
}
