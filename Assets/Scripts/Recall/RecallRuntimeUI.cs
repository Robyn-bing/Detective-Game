using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DetectiveGame.Recall
{
    [DisallowMultipleComponent]
    public sealed class RecallRuntimeUI : MonoBehaviour
    {
        private readonly List<RectTransform> periodRects = new List<RectTransform>();
        private readonly List<Image> periodBackgrounds = new List<Image>();
        private Font font;
        private GameObject canvasObject;
        private GameObject promptPanel;
        private RectTransform promptRect;
        private Text promptText;
        private RecallableObject promptTarget;
        private Camera promptCamera;
        private GameObject recallPanel;
        private GameObject selectionPanel;
        private Text selectionTitle;
        private Text titleText;
        private Text statusText;
        private Text startTimeText;
        private Text endTimeText;
        private Text currentTimeText;
        private Text instructionText;
        private RectTransform timelineTrack;
        private RectTransform timelineFill;
        private RectTransform timelineHandle;

        private void Awake()
        {
            promptCamera = Camera.main;
            BuildUI();
            HideRecall();
            SetWorldPrompt(null);
        }

        private void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
            if (font != null) Destroy(font);
        }

        private void LateUpdate()
        {
            if (promptTarget == null || promptCamera == null || !promptPanel.activeSelf) return;
            Vector3 screen = promptCamera.WorldToScreenPoint(promptTarget.InteractionAnchor.position);
            if (screen.z <= 0f)
            {
                promptPanel.SetActive(false);
                return;
            }
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                promptRect.localPosition = local + promptTarget.PromptScreenOffset;
        }

        public void SetWorldPrompt(RecallableObject recallable)
        {
            if (promptPanel == null) return;
            promptTarget = recallable;
            bool visible = promptTarget != null;
            promptPanel.SetActive(visible);
            if (visible) promptText.text = "[R] 回溯";
        }

        public void ShowPeriodSelection(RecallObjectData data, int selectedIndex)
        {
            recallPanel.SetActive(true);
            selectionPanel.SetActive(true);
            ClearPeriodEntries();
            selectionTitle.text = $"选择 {data.DisplayName} 的回溯时间";
            int count = data.AvailablePeriodCount;
            float height = Mathf.Min(90f, 420f / Mathf.Max(1, count));
            for (int available = 0; available < count; available++)
            {
                RecallPeriod period = data.GetPeriod(data.GetAvailablePeriodIndex(available));
                GameObject entry = CreateUIObject($"Period {available + 1}", selectionPanel.transform);
                RectTransform rect = entry.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(620f, height - 8f);
                rect.anchoredPosition = new Vector2(0f, (count - 1) * height * 0.5f - available * height - 15f);
                Image background = entry.AddComponent<Image>();
                background.raycastTarget = false;
                Text label = CreateText(entry.transform, period.Label, 25, TextAnchor.MiddleCenter, Color.white);
                Stretch(label.rectTransform, 20f, 10f, 20f, 10f);
                label.text = $"{period.StartTime} — {period.EndTime}    {period.Label}";
                periodRects.Add(rect);
                periodBackgrounds.Add(background);
            }
            UpdatePeriodSelection(selectedIndex);
        }

        public void UpdatePeriodSelection(int selectedIndex)
        {
            for (int i = 0; i < periodBackgrounds.Count; i++)
                periodBackgrounds[i].color = i == selectedIndex
                    ? new Color(0.08f, 0.55f, 0.64f, 0.92f)
                    : new Color(0.1f, 0.08f, 0.065f, 0.9f);
        }

        public int GetPeriodAtScreenPosition(Vector2 screenPosition)
        {
            for (int i = 0; i < periodRects.Count; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(periodRects[i], screenPosition)) return i;
            return -1;
        }

        public void ShowPlayback(RecallObjectData data, RecallPeriod period)
        {
            recallPanel.SetActive(true);
            selectionPanel.SetActive(false);
            titleText.text = data.DisplayName;
            startTimeText.text = period.StartTime;
            endTimeText.text = period.EndTime;
            instructionText.text = "空格：播放 / 暂停    拖动时间轴：查看状态    Esc：退出回溯";
        }

        public void UpdatePlayback(RecallSessionController.SessionState state, RecallPeriod period, float normalizedTime)
        {
            if (period == null) return;
            statusText.text = state switch
            {
                RecallSessionController.SessionState.TransitioningIn => "镜头转换与回溯中……",
                RecallSessionController.SessionState.TransitioningOut => "正在返回现场……",
                RecallSessionController.SessionState.Playing => "播放中",
                _ => normalizedTime <= 0.001f ? "已回到最初状态" : "已暂停"
            };
            currentTimeText.text = period.GetTime(normalizedTime);
            SetTimelineVisual(normalizedTime);
        }

        public bool IsTimelinePoint(Vector2 screenPosition) => timelineTrack != null &&
            RectTransformUtility.RectangleContainsScreenPoint(timelineTrack, screenPosition);

        public float GetTimelineValue(Vector2 screenPosition)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(timelineTrack, screenPosition, null, out Vector2 local)) return 0f;
            return Mathf.Clamp01((local.x - timelineTrack.rect.xMin) / timelineTrack.rect.width);
        }

        public void HideRecall()
        {
            if (recallPanel != null) recallPanel.SetActive(false);
            if (selectionPanel != null) selectionPanel.SetActive(false);
        }

        private void SetTimelineVisual(float normalizedTime)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            timelineFill.anchorMax = new Vector2(normalizedTime, 1f);
            timelineFill.offsetMax = Vector2.zero;
            timelineHandle.anchorMin = timelineHandle.anchorMax = new Vector2(normalizedTime, 0.5f);
            timelineHandle.anchoredPosition = Vector2.zero;
        }

        private void BuildUI()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 32);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("Recall UI (Runtime)");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            promptPanel = CreatePanel("World Prompt", canvasObject.transform, new Color(0.025f, 0.025f, 0.025f, 0.88f));
            promptRect = promptPanel.GetComponent<RectTransform>();
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0f, 0.5f);
            promptRect.sizeDelta = new Vector2(175f, 48f);
            promptText = CreateText(promptPanel.transform, string.Empty, 26, TextAnchor.MiddleCenter, Color.white);
            Stretch(promptText.rectTransform, 12f, 4f, 12f, 4f);

            recallPanel = CreatePanel("Recall Screen", canvasObject.transform, new Color(0.035f, 0.022f, 0.014f, 0.08f));
            Stretch(recallPanel.GetComponent<RectTransform>());
            titleText = CreateText(recallPanel.transform, string.Empty, 42, TextAnchor.UpperCenter, Color.white);
            Anchor(titleText.rectTransform, new Vector2(0.15f, 0.82f), new Vector2(0.85f, 0.94f));
            statusText = CreateText(recallPanel.transform, string.Empty, 30, TextAnchor.MiddleLeft, new Color(0.65f, 0.9f, 0.95f));
            Anchor(statusText.rectTransform, new Vector2(0.12f, 0.18f), new Vector2(0.5f, 0.25f));
            currentTimeText = CreateText(recallPanel.transform, string.Empty, 26, TextAnchor.MiddleCenter, Color.white);
            Anchor(currentTimeText.rectTransform, new Vector2(0.42f, 0.1f), new Vector2(0.58f, 0.16f));
            startTimeText = CreateText(recallPanel.transform, string.Empty, 27, TextAnchor.UpperLeft, Color.white);
            Anchor(startTimeText.rectTransform, new Vector2(0.065f, 0.045f), new Vector2(0.225f, 0.105f));
            endTimeText = CreateText(recallPanel.transform, string.Empty, 27, TextAnchor.UpperRight, Color.white);
            Anchor(endTimeText.rectTransform, new Vector2(0.775f, 0.045f), new Vector2(0.935f, 0.105f));
            instructionText = CreateText(recallPanel.transform, string.Empty, 19, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.78f));
            Anchor(instructionText.rectTransform, new Vector2(0.2f, 0.0f), new Vector2(0.8f, 0.05f));

            GameObject track = CreatePanel("Timeline Track", recallPanel.transform, new Color(0.75f, 0.75f, 0.72f, 0.72f));
            timelineTrack = track.GetComponent<RectTransform>();
            Anchor(timelineTrack, new Vector2(0.12f, 0.105f), new Vector2(0.88f, 0.118f));
            GameObject fill = CreatePanel("Timeline Fill", track.transform, new Color(0.15f, 0.78f, 0.9f, 0.95f));
            timelineFill = fill.GetComponent<RectTransform>();
            timelineFill.anchorMin = Vector2.zero;
            timelineFill.anchorMax = new Vector2(0f, 1f);
            timelineFill.offsetMin = timelineFill.offsetMax = Vector2.zero;
            GameObject handle = CreatePanel("Timeline Handle", track.transform, Color.white);
            timelineHandle = handle.GetComponent<RectTransform>();
            timelineHandle.anchorMin = timelineHandle.anchorMax = new Vector2(0f, 0.5f);
            timelineHandle.sizeDelta = new Vector2(18f, 38f);
            timelineHandle.anchoredPosition = Vector2.zero;

            selectionPanel = CreatePanel("Period Selection", canvasObject.transform, new Color(0.035f, 0.022f, 0.014f, 0.35f));
            Stretch(selectionPanel.GetComponent<RectTransform>());
            selectionTitle = CreateText(selectionPanel.transform, string.Empty, 38, TextAnchor.MiddleCenter, Color.white);
            Anchor(selectionTitle.rectTransform, new Vector2(0.15f, 0.75f), new Vector2(0.85f, 0.9f));
            Text hint = CreateText(selectionPanel.transform, "← → 选择    Enter 确认    Esc 退出", 21, TextAnchor.MiddleCenter, new Color(0.76f, 0.76f, 0.74f));
            Anchor(hint.rectTransform, new Vector2(0.2f, 0.08f), new Vector2(0.8f, 0.15f));
        }

        private void ClearPeriodEntries()
        {
            foreach (RectTransform rect in periodRects)
                if (rect != null) Destroy(rect.gameObject);
            periodRects.Clear();
            periodBackgrounds.Clear();
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = CreateUIObject(name, parent);
            Image image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private Text CreateText(Transform parent, string value, int size, TextAnchor alignment, Color color)
        {
            GameObject textObject = CreateUIObject("Text", parent);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
