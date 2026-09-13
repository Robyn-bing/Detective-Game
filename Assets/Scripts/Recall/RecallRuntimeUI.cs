using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DetectiveGame.Recall
{
    [DisallowMultipleComponent]
    public sealed class RecallRuntimeUI : MonoBehaviour
    {
        [Header("World Period Selection")]
        [SerializeField] private Vector2 periodSelectionOffset = new Vector2(14f, 0f);
        [SerializeField, Range(360f, 620f)] private float periodSelectionWidth = 500f;
        [SerializeField, Range(48f, 82f)] private float periodEntryHeight = 62f;
        [SerializeField, Range(0.05f, 0.35f)] private float periodSelectionOpenSeconds = 0.16f;

        private readonly List<RectTransform> periodRects = new List<RectTransform>();
        private readonly List<Image> periodBackgrounds = new List<Image>();
        private Font font;
        private GameObject canvasObject;
        private RectTransform canvasRect;
        private GameObject promptPanel;
        private RectTransform promptRect;
        private Text promptText;
        private RecallableObject promptTarget;
        private Camera promptCamera;
        private GameObject recallPanel;
        private GameObject selectionPanel;
        private RectTransform selectionRect;
        private CanvasGroup selectionCanvasGroup;
        private Coroutine selectionAnimation;
        private bool worldPromptRequested;
        private bool periodSelectionRequested;
        private GameObject playbackControlsRoot;
        private Text selectionTitle;
        private Text selectionHint;
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
            if (promptTarget == null || promptCamera == null ||
                (!worldPromptRequested && !periodSelectionRequested)) return;
            Vector3 screen = promptCamera.WorldToScreenPoint(promptTarget.InteractionAnchor.position);
            if (screen.z <= 0f)
            {
                promptPanel.SetActive(false);
                selectionPanel.SetActive(false);
                return;
            }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
            {
                Vector2 promptPosition = local + promptTarget.PromptScreenOffset;
                const float margin = 18f;
                promptPosition.x = Mathf.Clamp(promptPosition.x,
                    canvasRect.rect.xMin + margin,
                    canvasRect.rect.xMax - promptRect.rect.width - margin);
                promptPosition.y = Mathf.Clamp(promptPosition.y,
                    canvasRect.rect.yMin + promptRect.rect.height * 0.5f + margin,
                    canvasRect.rect.yMax - promptRect.rect.height * 0.5f - margin);
                promptRect.localPosition = promptPosition;
                promptPanel.SetActive(worldPromptRequested);

                if (!periodSelectionRequested) return;
                float candidateX = promptPosition.x + promptRect.rect.width + periodSelectionOffset.x;
                if (candidateX + selectionRect.rect.width > canvasRect.rect.xMax - margin)
                    candidateX = promptPosition.x - selectionRect.rect.width - periodSelectionOffset.x;
                float selectionX = Mathf.Clamp(candidateX,
                    canvasRect.rect.xMin + margin,
                    canvasRect.rect.xMax - selectionRect.rect.width - margin);
                float selectionY = Mathf.Clamp(promptPosition.y + periodSelectionOffset.y,
                    canvasRect.rect.yMin + selectionRect.rect.height * 0.5f + margin,
                    canvasRect.rect.yMax - selectionRect.rect.height * 0.5f - margin);
                selectionRect.localPosition = new Vector2(selectionX, selectionY);
                selectionPanel.SetActive(true);
            }
        }

        public void SetWorldPrompt(RecallableObject recallable)
        {
            if (promptPanel == null) return;
            worldPromptRequested = recallable != null;
            if (recallable != null) promptTarget = recallable;
            else if (!periodSelectionRequested) promptTarget = null;
            promptPanel.SetActive(worldPromptRequested);
            if (worldPromptRequested) promptText.text = "[R] 回溯";
        }

        public void ShowPeriodSelection(RecallObjectData data, RecallableObject target, int selectedIndex)
        {
            promptTarget = target;
            worldPromptRequested = target != null;
            periodSelectionRequested = true;
            recallPanel.SetActive(false);
            selectionPanel.SetActive(true);
            playbackControlsRoot.SetActive(false);
            ClearPeriodEntries();
            selectionTitle.text = "选择回溯时间";
            int count = data.AvailablePeriodCount;
            float panelHeight = 54f + count * periodEntryHeight + 38f;
            selectionRect.sizeDelta = new Vector2(periodSelectionWidth, panelHeight);
            for (int available = 0; available < count; available++)
            {
                RecallPeriod period = data.GetPeriod(data.GetAvailablePeriodIndex(available));
                GameObject entry = CreateUIObject($"Period {available + 1}", selectionPanel.transform);
                RectTransform rect = entry.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(-20f, periodEntryHeight - 8f);
                rect.anchoredPosition = new Vector2(0f, -48f - available * periodEntryHeight);
                Image background = entry.AddComponent<Image>();
                background.raycastTarget = false;
                Text label = CreateText(entry.transform, period.Label, 21, TextAnchor.MiddleLeft, Color.white);
                Stretch(label.rectTransform, 18f, 6f, 18f, 6f);
                label.text = $"[{available + 1}]  {period.StartTime} — {period.EndTime}    {period.Label}";
                periodRects.Add(rect);
                periodBackgrounds.Add(background);
            }
            RectTransform hintRect = selectionHint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(-20f, 30f);
            hintRect.anchoredPosition = new Vector2(0f, 6f);
            UpdatePeriodSelection(selectedIndex);
            if (selectionAnimation != null) StopCoroutine(selectionAnimation);
            selectionAnimation = StartCoroutine(AnimatePeriodSelectionOpen());
        }

        public void HidePeriodSelection()
        {
            periodSelectionRequested = false;
            if (selectionAnimation != null) StopCoroutine(selectionAnimation);
            selectionAnimation = null;
            if (selectionPanel != null) selectionPanel.SetActive(false);
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
            HidePeriodSelection();
            playbackControlsRoot.SetActive(true);
            titleText.text = data.DisplayName;
            startTimeText.text = period.StartTime;
            endTimeText.text = period.EndTime;
            instructionText.text = "空格：播放 / 暂停    拖动时间轴：查看状态    Esc：退出回溯";
        }

        public void ShowReconstruction(RecallObjectData data)
        {
            recallPanel.SetActive(true);
            HidePeriodSelection();
            playbackControlsRoot.SetActive(false);
            titleText.text = data.DisplayName;
            statusText.text = "正在重构记忆……";
        }

        public void UpdatePlayback(RecallSessionController.SessionState state, RecallPeriod period, float normalizedTime)
        {
            if (period == null) return;
            statusText.text = state switch
            {
                RecallSessionController.SessionState.TransitioningIn => "正在重构记忆……",
                RecallSessionController.SessionState.TransitioningOut => "正在返回现场……",
                RecallSessionController.SessionState.Playing => "播放中",
                _ => normalizedTime <= 0.001f ? "已回到最初状态" : "已暂停"
            };
            if (state == RecallSessionController.SessionState.TransitioningIn ||
                state == RecallSessionController.SessionState.TransitioningOut) return;
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
            HidePeriodSelection();
            if (playbackControlsRoot != null) playbackControlsRoot.SetActive(false);
        }

        private IEnumerator AnimatePeriodSelectionOpen()
        {
            selectionCanvasGroup.alpha = 0f;
            selectionRect.localScale = Vector3.one * 0.92f;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, periodSelectionOpenSeconds);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                selectionCanvasGroup.alpha = eased;
                selectionRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, eased);
                yield return null;
            }
            selectionCanvasGroup.alpha = 1f;
            selectionRect.localScale = Vector3.one;
            selectionAnimation = null;
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

            canvasObject = new GameObject("Recall UI (Runtime)", typeof(RectTransform));
            canvasRect = canvasObject.GetComponent<RectTransform>();
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

            playbackControlsRoot = CreateUIObject("Playback Controls", recallPanel.transform);
            Stretch(playbackControlsRoot.GetComponent<RectTransform>());
            currentTimeText = CreateText(playbackControlsRoot.transform, string.Empty, 26, TextAnchor.MiddleCenter, Color.white);
            Anchor(currentTimeText.rectTransform, new Vector2(0.42f, 0.1f), new Vector2(0.58f, 0.16f));
            startTimeText = CreateText(playbackControlsRoot.transform, string.Empty, 27, TextAnchor.UpperLeft, Color.white);
            Anchor(startTimeText.rectTransform, new Vector2(0.065f, 0.045f), new Vector2(0.225f, 0.105f));
            endTimeText = CreateText(playbackControlsRoot.transform, string.Empty, 27, TextAnchor.UpperRight, Color.white);
            Anchor(endTimeText.rectTransform, new Vector2(0.775f, 0.045f), new Vector2(0.935f, 0.105f));
            instructionText = CreateText(playbackControlsRoot.transform, string.Empty, 19, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.78f));
            Anchor(instructionText.rectTransform, new Vector2(0.2f, 0.0f), new Vector2(0.8f, 0.05f));

            GameObject track = CreatePanel("Timeline Track", playbackControlsRoot.transform, new Color(0.75f, 0.75f, 0.72f, 0.72f));
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

            selectionPanel = CreatePanel("World Period Selection", canvasObject.transform, new Color(0.025f, 0.025f, 0.03f, 0.94f));
            selectionRect = selectionPanel.GetComponent<RectTransform>();
            selectionRect.anchorMin = selectionRect.anchorMax = new Vector2(0.5f, 0.5f);
            selectionRect.pivot = new Vector2(0f, 0.5f);
            selectionRect.sizeDelta = new Vector2(periodSelectionWidth, 220f);
            selectionCanvasGroup = selectionPanel.AddComponent<CanvasGroup>();
            selectionTitle = CreateText(selectionPanel.transform, string.Empty, 24, TextAnchor.MiddleLeft, new Color(0.68f, 0.9f, 1f));
            RectTransform selectionTitleRect = selectionTitle.rectTransform;
            selectionTitleRect.anchorMin = new Vector2(0f, 1f);
            selectionTitleRect.anchorMax = new Vector2(1f, 1f);
            selectionTitleRect.pivot = new Vector2(0.5f, 1f);
            selectionTitleRect.sizeDelta = new Vector2(-24f, 38f);
            selectionTitleRect.anchoredPosition = new Vector2(0f, -7f);
            selectionHint = CreateText(selectionPanel.transform,
                "数字键 / 鼠标选择    ← → + Enter    Esc 取消", 16, TextAnchor.MiddleCenter,
                new Color(0.76f, 0.76f, 0.74f));
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
