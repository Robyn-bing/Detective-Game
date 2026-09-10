using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DetectiveGame.Investigation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DetectiveGame.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class DialogueRuntimeUI : MonoBehaviour
    {
        [Header("Investigation Wheel")]
        [SerializeField, Range(0f, 8f)] private float backgroundBlurRadius = 3.5f;
        [SerializeField] private Color backgroundBlurTint = new Color(0.12f, 0.16f, 0.2f, 0.72f);
        [SerializeField, Range(220f, 380f)] private float wheelRadius = 295f;
        [SerializeField, Range(70f, 180f)] private float wheelInnerRadius = 118f;
        [SerializeField, Range(0f, 12f)] private float wheelSegmentGap = 5f;
        [SerializeField, Range(50f, 180f)] private float wheelCenterDeadZone = 105f;
        [SerializeField, Range(0.1f, 0.5f)] private float wheelOpenDuration = 0.24f;
        [SerializeField, Range(0.08f, 0.4f)] private float wheelCloseDuration = 0.16f;
        [SerializeField, Range(0.4f, 0.95f)] private float wheelStartScale = 0.72f;
        [SerializeField, Range(0.7f, 1f)] private float wheelCloseScale = 0.87f;
        [SerializeField, Range(0f, 0.08f)] private float wheelSegmentStagger = 0.025f;
        [SerializeField, Range(0f, 40f)] private float wheelHoverOffset = 14f;
        [SerializeField, Range(1f, 1.2f)] private float wheelHoverScale = 1.08f;
        [SerializeField, Range(0.03f, 0.3f)] private float wheelHoverDuration = 0.1f;
        [SerializeField] private AnimationCurve wheelOpenScaleCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.72f, 1.07f), new Keyframe(1f, 1f));
        [SerializeField] private AnimationCurve wheelCloseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private static readonly Color InkBlack = new Color(0.025f, 0.025f, 0.03f, 0.94f);
        private static readonly Color Paper = new Color(0.93f, 0.91f, 0.84f, 1f);
        private static readonly Color Accent = new Color(0.66f, 0.12f, 0.1f, 1f);
        private static readonly Color Muted = new Color(0.42f, 0.42f, 0.44f, 1f);

        private readonly List<GameObject> choiceObjects = new List<GameObject>();
        private readonly List<GameObject> ownedObjects = new List<GameObject>();
        private readonly List<WheelItemView> wheelItems = new List<WheelItemView>();

        private DialogueController controller;
        private InvestigationKnowledgeService knowledge;
        private InvestigationDatabase database;
        private Font font;
        private bool ownsFont;
        private GameObject canvasObject;
        private GameObject persistentHud;
        private GameObject dialogueLogButtonObject;
        private GameObject wheelRoot;
        private CanvasGroup wheelCanvasGroup;
        private RectTransform wheelContainer;
        private Text wheelCenterText;
        private Coroutine wheelTransition;
        private bool wheelInteractive;
        private bool wheelClosing;
        private int selectedWheelIndex = -1;
        private GameObject dialogueRoot;
        private GameObject choiceRoot;
        private RectTransform choiceContainer;
        private GameObject boardRoot;
        private GameObject logRoot;
        private GameObject toastRoot;
        private Text toastText;
        private Material blurMaterial;
        private Text speakerText;
        private Text dialogueText;
        private Image portraitImage;
        private Text portraitPlaceholder;
        private Text logText;
        private Text edwardNode;
        private Text margaretNode;
        private Text jamesNode;
        private Text peterNode;
        private Text relationshipEvidence;
        private Text actionEvidence;
        private Text motiveEvidence;
        private Text timePeriodEvidence;
        private GameObject spouseConnection;
        private GameObject partnerConnection;
        private GameObject superiorConnection;
        private GameObject spouseLabel;
        private GameObject partnerLabel;
        private GameObject superiorLabel;
        private bool dialogueVisible;
        private bool choicesVisible;
        private float toastRemaining;

        private sealed class WheelItemView
        {
            public InvestigationMenuOption Option;
            public string Label;
            public float Angle;
            public RadialMenuSegmentGraphic Segment;
            public RectTransform Content;
            public CanvasGroup ContentCanvasGroup;
            public Text Icon;
            public Text Caption;
            public Vector2 BasePosition;
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();
        }

        private void Update()
        {
            if (wheelRoot != null && wheelRoot.activeSelf) UpdateInvestigationWheel();

            if (toastRemaining > 0f)
            {
                toastRemaining -= Time.unscaledDeltaTime;
                if (toastRemaining <= 0f && toastRoot != null) toastRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (knowledge != null) knowledge.KnowledgeChanged -= RefreshBoard;
            if (canvasObject != null) Destroy(canvasObject);
            if (blurMaterial != null) Destroy(blurMaterial);
            if (ownsFont && font != null) Destroy(font);
        }

        public void Initialize(
            DialogueController owner,
            InvestigationKnowledgeService knowledgeService,
            InvestigationDatabase investigationDatabase)
        {
            controller = owner;
            knowledge = knowledgeService;
            database = investigationDatabase;
            if (knowledge != null)
            {
                knowledge.KnowledgeChanged -= RefreshBoard;
                knowledge.KnowledgeChanged += RefreshBoard;
            }
            RefreshBoard();
        }

        public void SetPersistentHudVisible(bool visible)
        {
            if (persistentHud != null && persistentHud.activeSelf != visible)
                persistentHud.SetActive(visible);
        }

        public void ShowInvestigationWheel()
        {
            persistentHud.SetActive(false);
            dialogueRoot.SetActive(false);
            choiceRoot.SetActive(false);
            boardRoot.SetActive(false);
            logRoot.SetActive(false);
            wheelRoot.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            BeginWheelTransition(AnimateWheelOpen());
        }

        public void HideInvestigationWheel(
            bool returnToDialogue = false,
            Action onHidden = null,
            bool immediate = false)
        {
            if (wheelRoot == null)
            {
                onHidden?.Invoke();
                return;
            }

            if (immediate || !wheelRoot.activeSelf)
            {
                StopWheelTransition();
                FinishHidingWheel(returnToDialogue, onHidden);
                return;
            }

            BeginWheelTransition(AnimateWheelClose(returnToDialogue, onHidden));
        }

        public void ShowDialogue()
        {
            persistentHud.SetActive(true);
            dialogueVisible = true;
            choicesVisible = false;
            dialogueRoot.SetActive(true);
            choiceRoot.SetActive(false);
            boardRoot.SetActive(false);
            logRoot.SetActive(false);
            dialogueLogButtonObject.SetActive(true);
        }

        public void HideDialogue()
        {
            dialogueVisible = false;
            choicesVisible = false;
            dialogueRoot.SetActive(false);
            choiceRoot.SetActive(false);
            dialogueLogButtonObject.SetActive(false);
            ClearChoices();
        }

        public void ShowLine(string speakerName, Sprite portrait, string text)
        {
            speakerText.text = speakerName;
            dialogueText.text = text;
            portraitImage.sprite = portrait;
            portraitImage.color = portrait == null ? new Color(0.14f, 0.14f, 0.16f, 1f) : Color.white;
            portraitPlaceholder.gameObject.SetActive(portrait == null);
        }

        public void ShowChoices(IReadOnlyList<string> labels, Action<int> onSelected)
        {
            ClearChoices();
            choicesVisible = true;
            choiceRoot.SetActive(dialogueVisible);

            float totalHeight = labels.Count * 82f + Mathf.Max(0, labels.Count - 1) * 18f;
            choiceContainer.sizeDelta = new Vector2(900f, totalHeight);
            for (int i = 0; i < labels.Count; i++)
            {
                int capturedIndex = i;
                Button button = CreateButton(
                    choiceContainer,
                    $"Choice {i + 1}",
                    $"[{i + 1}]  {labels[i]}",
                    new Vector2(900f, 82f),
                    Paper,
                    Color.black,
                    30);
                LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 82f;
                button.onClick.AddListener(() => onSelected(capturedIndex));
                choiceObjects.Add(button.gameObject);
            }

            if (choiceObjects.Count > 0 && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(choiceObjects[0]);
        }

        public void HideChoices()
        {
            choicesVisible = false;
            choiceRoot.SetActive(false);
            ClearChoices();
        }

        public void ShowBoard()
        {
            persistentHud.SetActive(false);
            RefreshBoard();
            dialogueRoot.SetActive(false);
            choiceRoot.SetActive(false);
            logRoot.SetActive(false);
            boardRoot.SetActive(true);
        }

        public void HideBoard(bool returnToDialogue)
        {
            boardRoot.SetActive(false);
            persistentHud.SetActive(true);
            if (!returnToDialogue) return;
            dialogueRoot.SetActive(dialogueVisible);
            choiceRoot.SetActive(dialogueVisible && choicesVisible);
        }

        public void ShowDialogueLog(IReadOnlyList<DialogueLogEntry> entries)
        {
            persistentHud.SetActive(false);
            var builder = new StringBuilder();
            int start = Mathf.Max(0, entries.Count - 24);
            for (int i = start; i < entries.Count; i++)
            {
                builder.Append("【").Append(entries[i].Speaker).Append("】\n")
                    .Append(entries[i].Text).Append("\n\n");
            }
            logText.text = builder.Length == 0 ? "暂无对话记录" : builder.ToString();
            dialogueRoot.SetActive(false);
            choiceRoot.SetActive(false);
            logRoot.SetActive(true);
        }

        public void HideDialogueLog(bool returnToDialogue)
        {
            logRoot.SetActive(false);
            persistentHud.SetActive(true);
            if (!returnToDialogue) return;
            dialogueRoot.SetActive(dialogueVisible);
            choiceRoot.SetActive(dialogueVisible && choicesVisible);
        }

        public void ShowBoardUpdatedToast()
        {
            toastText.text = "证言板已更新";
            toastRemaining = 3f;
            toastRoot.SetActive(true);
        }

        public void ShowFeatureUnavailable(string featureName)
        {
            toastText.text = $"{featureName}功能正在开发中";
            toastRemaining = 2.25f;
            toastRoot.SetActive(true);
        }

        private void BuildUI()
        {
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 32);
            ownsFont = font != null;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("Investigation UI (Runtime)", typeof(RectTransform));
            ownedObjects.Add(canvasObject);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4700;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            BuildPersistentHud(canvasObject.transform);
            BuildInvestigationWheel(canvasObject.transform);
            BuildDialogue(canvasObject.transform);
            BuildChoices(canvasObject.transform);
            BuildBoard(canvasObject.transform);
            BuildDialogueLog(canvasObject.transform);
            BuildToast(canvasObject.transform);
        }

        private void BuildPersistentHud(Transform parent)
        {
            persistentHud = CreateRectObject("Persistent HUD", parent).gameObject;
            RectTransform hudRect = persistentHud.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(1f, 1f);
            hudRect.anchorMax = new Vector2(1f, 1f);
            hudRect.pivot = new Vector2(1f, 1f);
            hudRect.anchoredPosition = new Vector2(-24f, -24f);
            hudRect.sizeDelta = new Vector2(430f, 70f);
            HorizontalLayoutGroup layout = persistentHud.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            Button logButton = CreateHudButton(hudRect, "对话\n日志");
            dialogueLogButtonObject = logButton.gameObject;
            logButton.onClick.AddListener(() => controller?.OpenDialogueLog());

            GameObject hintPanel = CreatePanel(hudRect, "Investigation Menu Button",
                new Color(0.035f, 0.035f, 0.045f, 0.82f));
            hintPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 64f);
            Image hintBackground = hintPanel.GetComponent<Image>();
            hintBackground.raycastTarget = true;
            Button investigationButton = hintPanel.AddComponent<Button>();
            investigationButton.targetGraphic = hintBackground;
            ColorBlock hintColors = investigationButton.colors;
            hintColors.normalColor = hintBackground.color;
            hintColors.highlightedColor = Color.Lerp(hintBackground.color, Paper, 0.18f);
            hintColors.pressedColor = Color.Lerp(hintBackground.color, Color.black, 0.2f);
            hintColors.selectedColor = hintColors.highlightedColor;
            investigationButton.colors = hintColors;
            investigationButton.onClick.AddListener(() => controller?.ToggleInvestigationWheel());
            LayoutElement hintLayout = hintPanel.AddComponent<LayoutElement>();
            hintLayout.preferredWidth = 280f;
            hintLayout.preferredHeight = 64f;
            Text hint = CreateText(hintPanel.transform, "Hint", "[Tab]  调查菜单", 24,
                TextAnchor.MiddleCenter, Paper);
            StretchFull(hint.rectTransform, 8f);
            dialogueLogButtonObject.SetActive(false);
        }

        private void BuildInvestigationWheel(Transform parent)
        {
            wheelRoot = CreatePanel(parent, "Investigation Wheel", new Color(0.015f, 0.02f, 0.028f, 0.78f));
            StretchFull(wheelRoot.GetComponent<RectTransform>());
            wheelCanvasGroup = wheelRoot.AddComponent<CanvasGroup>();
            wheelRoot.AddComponent<RadialMenuPointerHandler>().Configure(ConfirmWheelSelection);

            Shader blurShader = Shader.Find("DetectiveGame/UI/InvestigationBackgroundBlur");
            if (blurShader != null)
            {
                blurMaterial = new Material(blurShader) { name = "Investigation Background Blur (Runtime)" };
                blurMaterial.SetFloat("_BlurRadius", backgroundBlurRadius);
                blurMaterial.SetColor("_Tint", backgroundBlurTint);
                wheelRoot.GetComponent<Image>().material = blurMaterial;
            }

            wheelContainer = CreateRectObject("Wheel Container", wheelRoot.transform);
            SetRect(wheelContainer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.one * (wheelRadius * 2f + 40f), new Vector2(0.5f, 0.5f));

            CreateRadialGraphic(wheelContainer, "Outer Rim", wheelRadius + 3f, wheelRadius + 7f,
                0f, 360f, 64, new Color(0.52f, 0.78f, 0.9f, 0.65f));
            CreateRadialGraphic(wheelContainer, "Inner Rim", wheelInnerRadius - 5f, wheelInnerRadius - 1f,
                0f, 360f, 48, new Color(0.42f, 0.62f, 0.72f, 0.55f));

            InvestigationMenuOption[] options =
            {
                InvestigationMenuOption.TestimonyBoard,
                InvestigationMenuOption.EvidenceBoard,
                InvestigationMenuOption.Save,
                InvestigationMenuOption.Hint,
                InvestigationMenuOption.Settings
            };
            string[] labels = { "证言板", "证据板", "存档", "提示", "设置" };
            string[] icons = { "证", "据", "存", "?", "设" };
            float[] angles = { 90f, 18f, -54f, -126f, 162f };
            float contentRadius = Mathf.Lerp(wheelInnerRadius, wheelRadius, 0.56f);
            float sectorSpan = 72f - wheelSegmentGap;
            for (int i = 0; i < options.Length; i++)
            {
                float radians = angles[i] * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                RadialMenuSegmentGraphic segment = CreateRadialGraphic(
                    wheelContainer, $"Segment {labels[i]}", wheelInnerRadius, wheelRadius,
                    angles[i], sectorSpan, 18, WheelNormalColor);

                RectTransform content = CreateRectObject($"Content {labels[i]}", wheelContainer);
                SetRect(content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    direction * contentRadius, new Vector2(150f, 128f), new Vector2(0.5f, 0.5f));
                CanvasGroup contentCanvasGroup = content.gameObject.AddComponent<CanvasGroup>();

                RectTransform badge = CreateRectObject("Icon Badge", content);
                SetRect(badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 18f), new Vector2(76f, 76f), new Vector2(0.5f, 0.5f));
                RadialMenuSegmentGraphic badgeGraphic = badge.gameObject.AddComponent<RadialMenuSegmentGraphic>();
                badgeGraphic.Configure(0f, 38f, 0f, 360f, 32,
                    new Color(0.025f, 0.035f, 0.045f, 0.96f));

                Text icon = CreateText(badge, "Icon", icons[i], 38, TextAnchor.MiddleCenter, Paper);
                StretchFull(icon.rectTransform, 4f);
                Text caption = CreateText(content, "Caption", labels[i], 25, TextAnchor.MiddleCenter, Paper);
                SetRect(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 2f), new Vector2(150f, 38f), new Vector2(0.5f, 0f));

                wheelItems.Add(new WheelItemView
                {
                    Option = options[i],
                    Label = labels[i],
                    Angle = angles[i],
                    Segment = segment,
                    Content = content,
                    ContentCanvasGroup = contentCanvasGroup,
                    Icon = icon,
                    Caption = caption,
                    BasePosition = direction * contentRadius
                });
            }

            RadialMenuSegmentGraphic center = CreateRadialGraphic(
                wheelContainer, "Wheel Center", 0f, wheelInnerRadius - 12f,
                0f, 360f, 48, new Color(0.018f, 0.025f, 0.035f, 0.985f));
            center.transform.SetAsLastSibling();
            wheelCenterText = CreateText(center.transform, "Title",
                "调查菜单\n<size=18>移动鼠标选择</size>", 31, TextAnchor.MiddleCenter, Paper);
            StretchFull(wheelCenterText.rectTransform, 18f);

            wheelCanvasGroup.alpha = 0f;
            wheelRoot.SetActive(false);
        }

        private static readonly Color WheelNormalColor = new Color(0.055f, 0.07f, 0.09f, 0.97f);
        private static readonly Color WheelSelectedColor = new Color(0.12f, 0.42f, 0.58f, 0.99f);

        private RadialMenuSegmentGraphic CreateRadialGraphic(
            Transform parent,
            string name,
            float innerRadius,
            float outerRadius,
            float centerAngle,
            float spanAngle,
            int subdivisions,
            Color color)
        {
            RectTransform rect = CreateRectObject(name, parent);
            StretchFull(rect);
            RadialMenuSegmentGraphic graphic = rect.gameObject.AddComponent<RadialMenuSegmentGraphic>();
            graphic.Configure(innerRadius, outerRadius, centerAngle, spanAngle, subdivisions, color);
            return graphic;
        }

        private void UpdateInvestigationWheel()
        {
            if (!wheelInteractive || wheelClosing || Mouse.current == null || wheelContainer == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    wheelContainer, Mouse.current.position.ReadValue(), null, out Vector2 pointerPosition)) return;

            int nextIndex = -1;
            if (pointerPosition.magnitude >= wheelCenterDeadZone)
            {
                float pointerAngle = Mathf.Atan2(pointerPosition.y, pointerPosition.x) * Mathf.Rad2Deg;
                float nearestDifference = float.PositiveInfinity;
                for (int i = 0; i < wheelItems.Count; i++)
                {
                    float difference = Mathf.Abs(Mathf.DeltaAngle(pointerAngle, wheelItems[i].Angle));
                    if (difference >= nearestDifference) continue;
                    nearestDifference = difference;
                    nextIndex = i;
                }
            }

            if (nextIndex != selectedWheelIndex) SetWheelSelection(nextIndex);
            UpdateWheelHoverVisuals();
        }

        private void SetWheelSelection(int index)
        {
            selectedWheelIndex = index;
            if (wheelCenterText == null) return;
            wheelCenterText.text = index >= 0 && index < wheelItems.Count
                ? $"{wheelItems[index].Label}\n<size=18>单击进入</size>"
                : "调查菜单\n<size=18>移动鼠标选择</size>";
        }

        private void UpdateWheelHoverVisuals(bool immediate = false)
        {
            float duration = Mathf.Max(0.01f, wheelHoverDuration);
            float blend = immediate ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f / duration);
            for (int i = 0; i < wheelItems.Count; i++)
            {
                WheelItemView item = wheelItems[i];
                bool selected = i == selectedWheelIndex;
                float radians = item.Angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 targetPosition = item.BasePosition + (selected ? direction * wheelHoverOffset : Vector2.zero);
                Vector3 targetScale = Vector3.one * (selected ? wheelHoverScale : 1f);
                Color targetColor = selected ? WheelSelectedColor : WheelNormalColor;

                item.Content.anchoredPosition = Vector2.Lerp(item.Content.anchoredPosition, targetPosition, blend);
                item.Content.localScale = Vector3.Lerp(item.Content.localScale, targetScale, blend);
                item.Segment.color = Color.Lerp(item.Segment.color, targetColor, blend);
                item.Icon.color = Color.Lerp(item.Icon.color, selected ? Color.white : Paper, blend);
                item.Caption.color = Color.Lerp(item.Caption.color,
                    selected ? new Color(0.72f, 0.9f, 1f, 1f) : Paper, blend);
            }
        }

        private void ConfirmWheelSelection()
        {
            if (!wheelInteractive || wheelClosing || selectedWheelIndex < 0 ||
                selectedWheelIndex >= wheelItems.Count) return;
            controller?.SelectInvestigationMenuOption(wheelItems[selectedWheelIndex].Option);
        }

        private void BeginWheelTransition(IEnumerator transition)
        {
            StopWheelTransition();
            wheelTransition = StartCoroutine(transition);
        }

        private void StopWheelTransition()
        {
            if (wheelTransition == null) return;
            StopCoroutine(wheelTransition);
            wheelTransition = null;
        }

        private IEnumerator AnimateWheelOpen()
        {
            wheelClosing = false;
            wheelInteractive = false;
            SetWheelSelection(-1);
            wheelCanvasGroup.alpha = 0f;
            wheelContainer.localScale = Vector3.one * wheelStartScale;
            wheelContainer.localRotation = Quaternion.Euler(0f, 0f, -6f);

            float iconStartRadius = Mathf.Max(30f, wheelInnerRadius * 0.62f);
            for (int i = 0; i < wheelItems.Count; i++)
            {
                WheelItemView item = wheelItems[i];
                float radians = item.Angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                item.Content.anchoredPosition = direction * iconStartRadius;
                item.Content.localScale = Vector3.one * 0.72f;
                item.ContentCanvasGroup.alpha = 0f;
                item.Segment.color = WheelNormalColor;
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, wheelOpenDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutCubic(t);
                float scaleProgress = wheelOpenScaleCurve == null ? eased : wheelOpenScaleCurve.Evaluate(t);
                wheelCanvasGroup.alpha = eased;
                wheelContainer.localScale = Vector3.one * Mathf.LerpUnclamped(wheelStartScale, 1f, scaleProgress);
                wheelContainer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-6f, 0f, eased));

                for (int i = 0; i < wheelItems.Count; i++)
                {
                    WheelItemView item = wheelItems[i];
                    float localDuration = Mathf.Max(0.01f, duration - wheelSegmentStagger * (wheelItems.Count - 1));
                    float localT = Mathf.Clamp01((elapsed - wheelSegmentStagger * i) / localDuration);
                    float itemEase = EaseOutCubic(localT);
                    float radians = item.Angle * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    item.Content.anchoredPosition = Vector2.LerpUnclamped(
                        direction * iconStartRadius, item.BasePosition, itemEase);
                    item.Content.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, itemEase);
                    item.ContentCanvasGroup.alpha = itemEase;
                }

                yield return null;
            }

            wheelCanvasGroup.alpha = 1f;
            wheelContainer.localScale = Vector3.one;
            wheelContainer.localRotation = Quaternion.identity;
            for (int i = 0; i < wheelItems.Count; i++)
            {
                wheelItems[i].Content.anchoredPosition = wheelItems[i].BasePosition;
                wheelItems[i].Content.localScale = Vector3.one;
                wheelItems[i].ContentCanvasGroup.alpha = 1f;
            }

            wheelInteractive = true;
            wheelTransition = null;
            UpdateWheelHoverVisuals(true);
        }

        private IEnumerator AnimateWheelClose(bool returnToDialogue, Action onHidden)
        {
            wheelClosing = true;
            wheelInteractive = false;
            SetWheelSelection(-1);

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, wheelCloseDuration);
            Vector3 initialScale = wheelContainer.localScale;
            Quaternion initialRotation = wheelContainer.localRotation;
            float[] initialAlpha = new float[wheelItems.Count];
            Vector2[] initialPositions = new Vector2[wheelItems.Count];
            for (int i = 0; i < wheelItems.Count; i++)
            {
                initialAlpha[i] = wheelItems[i].ContentCanvasGroup.alpha;
                initialPositions[i] = wheelItems[i].Content.anchoredPosition;
            }

            float iconEndRadius = Mathf.Max(30f, wheelInnerRadius * 0.62f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = wheelCloseCurve == null ? t : wheelCloseCurve.Evaluate(t);
                wheelCanvasGroup.alpha = 1f - eased;
                wheelContainer.localScale = Vector3.LerpUnclamped(
                    initialScale, Vector3.one * wheelCloseScale, eased);
                wheelContainer.localRotation = Quaternion.SlerpUnclamped(
                    initialRotation, Quaternion.Euler(0f, 0f, 5f), eased);

                for (int i = 0; i < wheelItems.Count; i++)
                {
                    WheelItemView item = wheelItems[i];
                    float radians = item.Angle * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    item.Content.anchoredPosition = Vector2.LerpUnclamped(
                        initialPositions[i], direction * iconEndRadius, eased);
                    item.Content.localScale = Vector3.one * Mathf.Lerp(1f, 0.78f, eased);
                    item.ContentCanvasGroup.alpha = Mathf.Lerp(initialAlpha[i], 0f, eased);
                }

                yield return null;
            }

            wheelTransition = null;
            FinishHidingWheel(returnToDialogue, onHidden);
        }

        private void FinishHidingWheel(bool returnToDialogue, Action onHidden)
        {
            wheelClosing = false;
            wheelInteractive = false;
            selectedWheelIndex = -1;
            wheelCanvasGroup.alpha = 0f;
            wheelRoot.SetActive(false);
            if (returnToDialogue)
            {
                persistentHud.SetActive(true);
                dialogueRoot.SetActive(dialogueVisible);
                choiceRoot.SetActive(dialogueVisible && choicesVisible);
            }
            onHidden?.Invoke();
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private void BuildDialogue(Transform parent)
        {
            dialogueRoot = CreatePanel(parent, "Dialogue", Color.clear);
            StretchFull(dialogueRoot.GetComponent<RectTransform>());

            GameObject box = CreatePanel(dialogueRoot.transform, "Dialogue Box", InkBlack);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.025f, 0.02f);
            boxRect.anchorMax = new Vector2(0.97f, 0.02f);
            boxRect.pivot = new Vector2(0.5f, 0f);
            boxRect.sizeDelta = new Vector2(0f, 245f);
            boxRect.anchoredPosition = Vector2.zero;
            Outline outline = box.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.82f, 0.72f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button advanceButton = box.AddComponent<Button>();
            advanceButton.transition = Selectable.Transition.None;
            advanceButton.onClick.AddListener(() => controller?.AdvanceStory());

            GameObject portraitBox = CreatePanel(box.transform, "Portrait", new Color(0.12f, 0.12f, 0.14f, 1f));
            RectTransform portraitRect = portraitBox.GetComponent<RectTransform>();
            SetRect(portraitRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(30f, 0f), new Vector2(215f, 215f), new Vector2(0f, 0.5f));
            portraitImage = portraitBox.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitPlaceholder = CreateText(portraitBox.transform, "Portrait Placeholder", "人物\n图片", 28,
                TextAnchor.MiddleCenter, Paper);
            StretchFull(portraitPlaceholder.rectTransform, 8f);

            GameObject nameBox = CreatePanel(box.transform, "Speaker Name", Paper);
            RectTransform nameRect = nameBox.GetComponent<RectTransform>();
            SetRect(nameRect, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(48f, 12f), new Vector2(260f, 58f), new Vector2(0f, 0f));
            speakerText = CreateText(nameBox.transform, "Name", "人物姓名", 27, TextAnchor.MiddleCenter, Color.black);
            StretchFull(speakerText.rectTransform, 8f);

            dialogueText = CreateText(box.transform, "Dialogue Text", "", 31, TextAnchor.UpperLeft, Paper);
            RectTransform textRect = dialogueText.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(300f, 38f);
            textRect.offsetMax = new Vector2(-48f, -38f);
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogueText.verticalOverflow = VerticalWrapMode.Truncate;

            Text hint = CreateText(box.transform, "Advance Hint", "空格 / 点击继续", 19,
                TextAnchor.LowerRight, new Color(0.72f, 0.72f, 0.72f, 1f));
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.anchoredPosition = new Vector2(-30f, 14f);
            hintRect.sizeDelta = new Vector2(260f, 32f);
            dialogueRoot.SetActive(false);
        }

        private void BuildChoices(Transform parent)
        {
            choiceRoot = CreatePanel(parent, "Dialogue Choices", new Color(0f, 0f, 0f, 0.48f));
            StretchFull(choiceRoot.GetComponent<RectTransform>());
            choiceContainer = CreateRectObject("Choice Container", choiceRoot.transform);
            choiceContainer.anchorMin = choiceContainer.anchorMax = new Vector2(0.5f, 0.58f);
            choiceContainer.pivot = new Vector2(0.5f, 0.5f);
            VerticalLayoutGroup layout = choiceContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            choiceRoot.SetActive(false);
        }

        private void BuildBoard(Transform parent)
        {
            boardRoot = CreatePanel(parent, "Testimony Board", new Color(0.018f, 0.018f, 0.022f, 0.985f));
            StretchFull(boardRoot.GetComponent<RectTransform>());

            Text title = CreateText(boardRoot.transform, "Title", "证言板", 44, TextAnchor.MiddleLeft, Paper);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(42f, -30f), new Vector2(360f, 70f), new Vector2(0f, 1f));

            Button close = CreateButton(boardRoot.transform, "Close", "返回  [Esc]", new Vector2(180f, 58f),
                new Color(0.15f, 0.15f, 0.17f, 1f), Paper, 24);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-35f, -30f), new Vector2(180f, 58f), new Vector2(1f, 1f));
            close.onClick.AddListener(() => controller?.CloseBoard());

            RectTransform graph = CreateRectObject("Graph", boardRoot.transform);
            StretchFull(graph, 80f, 25f, 45f, 90f);
            RectTransform lines = CreateRectObject("Connections", graph);
            StretchFull(lines);
            RectTransform nodes = CreateRectObject("Nodes", graph);
            StretchFull(nodes);

            Vector2 edward = new Vector2(-710f, 95f);
            Vector2 margaret = new Vector2(-400f, 315f);
            Vector2 james = new Vector2(-400f, 55f);
            Vector2 peter = new Vector2(-400f, -205f);
            spouseConnection = CreateLine(lines, "Spouse Line", edward, margaret, Accent);
            partnerConnection = CreateLine(lines, "Partner Line", edward, james, Accent);
            superiorConnection = CreateLine(lines, "Superior Line", edward, peter, Accent);
            spouseLabel = CreateConnectionLabel(nodes, "夫妻", new Vector2(-565f, 230f));
            partnerLabel = CreateConnectionLabel(nodes, "合伙人", new Vector2(-555f, 80f));
            superiorLabel = CreateConnectionLabel(nodes, "上下级", new Vector2(-550f, -95f));

            edwardNode = CreateBoardNode(nodes, "Edward", edward);
            margaretNode = CreateBoardNode(nodes, "Margaret", margaret);
            jamesNode = CreateBoardNode(nodes, "James", james);
            peterNode = CreateBoardNode(nodes, "Peter", peter);

            CreateLine(lines, "Margaret Relation", new Vector2(-275f, 315f), new Vector2(-80f, 315f), Muted);
            CreateLine(lines, "Margaret Action", new Vector2(-275f, 315f), new Vector2(-80f, 70f), Muted);
            CreateLine(lines, "Margaret Motive", new Vector2(-275f, 315f), new Vector2(-80f, -205f), Muted);

            CreateCategoryNode(nodes, "关系", new Vector2(20f, 315f));
            CreateCategoryNode(nodes, "行为", new Vector2(20f, 70f));
            CreateCategoryNode(nodes, "动机", new Vector2(20f, -205f));
            relationshipEvidence = CreateEvidenceCard(nodes, "Relationship Evidence", new Vector2(440f, 315f), 150f);
            actionEvidence = CreateEvidenceCard(nodes, "Action Evidence", new Vector2(440f, 50f), 235f);
            motiveEvidence = CreateEvidenceCard(nodes, "Motive Evidence", new Vector2(440f, -220f), 110f);

            timePeriodEvidence = CreateText(nodes, "Time Period", "", 22, TextAnchor.MiddleLeft,
                new Color(0.75f, 0.82f, 0.9f, 1f));
            SetRect(timePeriodEvidence.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(250f, 10f), new Vector2(700f, 55f), new Vector2(0.5f, 0f));
            boardRoot.SetActive(false);
        }

        private void BuildDialogueLog(Transform parent)
        {
            logRoot = CreatePanel(parent, "Dialogue Log", new Color(0.018f, 0.018f, 0.022f, 0.985f));
            StretchFull(logRoot.GetComponent<RectTransform>());
            Text title = CreateText(logRoot.transform, "Title", "对话日志", 44, TextAnchor.MiddleLeft, Paper);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(42f, -30f), new Vector2(360f, 70f), new Vector2(0f, 1f));
            Button close = CreateButton(logRoot.transform, "Close", "返回  [Esc]", new Vector2(180f, 58f),
                new Color(0.15f, 0.15f, 0.17f, 1f), Paper, 24);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-35f, -30f), new Vector2(180f, 58f), new Vector2(1f, 1f));
            close.onClick.AddListener(() => controller?.CloseDialogueLog());
            logText = CreateText(logRoot.transform, "Entries", "暂无对话记录", 27,
                TextAnchor.UpperLeft, Paper);
            logText.rectTransform.anchorMin = new Vector2(0.08f, 0.08f);
            logText.rectTransform.anchorMax = new Vector2(0.92f, 0.88f);
            logText.rectTransform.offsetMin = Vector2.zero;
            logText.rectTransform.offsetMax = Vector2.zero;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Truncate;
            logRoot.SetActive(false);
        }

        private void BuildToast(Transform parent)
        {
            toastRoot = CreatePanel(parent, "Board Updated Toast", new Color(0.08f, 0.08f, 0.09f, 0.96f));
            RectTransform rect = toastRoot.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-35f, -112f), new Vector2(310f, 64f), new Vector2(1f, 1f));
            toastText = CreateText(toastRoot.transform, "Text", "证言板已更新", 26, TextAnchor.MiddleCenter, Paper);
            StretchFull(toastText.rectTransform, 8f);
            toastRoot.SetActive(false);
        }

        private void RefreshBoard()
        {
            if (knowledge == null || database == null || edwardNode == null) return;
            SetCharacterNode(edwardNode, InvestigationIds.Characters.Edward);
            SetCharacterNode(margaretNode, InvestigationIds.Characters.Margaret);
            SetCharacterNode(jamesNode, InvestigationIds.Characters.James);
            SetCharacterNode(peterNode, InvestigationIds.Characters.Peter);

            SetConnection(spouseConnection, spouseLabel,
                knowledge.IsRelationshipKnown(InvestigationIds.Relationships.EdwardMargaretSpouse));
            SetConnection(partnerConnection, partnerLabel,
                knowledge.IsRelationshipKnown(InvestigationIds.Relationships.EdwardJamesPartner));
            SetConnection(superiorConnection, superiorLabel,
                knowledge.IsRelationshipKnown(InvestigationIds.Relationships.EdwardPeterSuperior));

            relationshipEvidence.text = BuildTestimonyText(TestimonyCategory.Relationship, "关系信息暂未获得");
            actionEvidence.text = BuildTestimonyText(TestimonyCategory.Action, "行为信息暂未获得");
            motiveEvidence.text = "暂未发现";
            bool timeKnown = knowledge.IsTimePeriodUnlocked(InvestigationIds.TimePeriods.Study1900To1915);
            timePeriodEvidence.text = timeKnown ? "已获得回溯时间段：19:00–19:15" : "";
        }

        private void SetCharacterNode(Text node, string characterId)
        {
            bool known = knowledge.IsCharacterKnown(characterId);
            if (known && database.TryGetCharacter(characterId, out CharacterDefinition character))
            {
                node.text = $"{character.DisplayName}\n<size=20>{character.Role}</size>";
                node.transform.parent.GetComponent<Image>().color = new Color(0.93f, 0.91f, 0.84f, 1f);
                node.color = Color.black;
            }
            else
            {
                node.text = "？";
                node.transform.parent.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.09f, 1f);
                node.color = Paper;
            }
        }

        private string BuildTestimonyText(TestimonyCategory category, string emptyText)
        {
            var builder = new StringBuilder();
            IReadOnlyList<TestimonyDefinition> items = database.Testimonies;
            for (int i = 0; i < items.Count; i++)
            {
                TestimonyDefinition item = items[i];
                if (item.CharacterId != InvestigationIds.Characters.Margaret || item.Category != category ||
                    !knowledge.IsTestimonyUnlocked(item.Id)) continue;
                if (builder.Length > 0) builder.Append('\n');
                builder.Append("• ").Append(item.DisplayText);
            }
            return builder.Length == 0 ? emptyText : builder.ToString();
        }

        private static void SetConnection(GameObject line, GameObject label, bool visible)
        {
            line.SetActive(visible);
            label.SetActive(visible);
        }

        private void ClearChoices()
        {
            for (int i = 0; i < choiceObjects.Count; i++)
                if (choiceObjects[i] != null) Destroy(choiceObjects[i]);
            choiceObjects.Clear();
        }

        private Button CreateHudButton(Transform parent, string label)
        {
            Button button = CreateButton(parent, label, label, new Vector2(130f, 64f),
                new Color(0.08f, 0.08f, 0.09f, 0.94f), Paper, 24);
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 130f;
            element.preferredHeight = 64f;
            return button;
        }

        private Text CreateBoardNode(Transform parent, string name, Vector2 position)
        {
            GameObject panel = CreatePanel(parent, name, Paper);
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, new Vector2(250f, 92f), new Vector2(0.5f, 0.5f));
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = Accent;
            outline.effectDistance = new Vector2(2f, -2f);
            Text text = CreateText(panel.transform, "Label", "？", 25, TextAnchor.MiddleCenter, Color.black);
            StretchFull(text.rectTransform, 8f);
            return text;
        }

        private void CreateCategoryNode(Transform parent, string label, Vector2 position)
        {
            GameObject panel = CreatePanel(parent, label, new Color(0.16f, 0.16f, 0.18f, 1f));
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, new Vector2(145f, 70f), new Vector2(0.5f, 0.5f));
            Text text = CreateText(panel.transform, "Label", label, 27, TextAnchor.MiddleCenter, Paper);
            StretchFull(text.rectTransform, 6f);
        }

        private Text CreateEvidenceCard(Transform parent, string name, Vector2 position, float height)
        {
            GameObject panel = CreatePanel(parent, name, new Color(0.08f, 0.08f, 0.095f, 1f));
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, new Vector2(650f, height), new Vector2(0.5f, 0.5f));
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.75f, 0.78f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);
            Text text = CreateText(panel.transform, "Text", "？", 23, TextAnchor.MiddleLeft, Paper);
            StretchFull(text.rectTransform, 18f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private GameObject CreateConnectionLabel(Transform parent, string label, Vector2 position)
        {
            GameObject panel = CreatePanel(parent, label, new Color(0.08f, 0.08f, 0.09f, 1f));
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, new Vector2(100f, 38f), new Vector2(0.5f, 0.5f));
            Text text = CreateText(panel.transform, "Text", label, 20, TextAnchor.MiddleCenter, Paper);
            StretchFull(text.rectTransform, 2f);
            return panel;
        }

        private GameObject CreateLine(Transform parent, string name, Vector2 from, Vector2 to, Color color)
        {
            GameObject line = CreatePanel(parent, name, color);
            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(Vector2.Distance(from, to), 3f);
            Vector2 direction = to - from;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            return line;
        }

        private Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 size,
            Color background,
            Color foreground,
            int fontSize)
        {
            GameObject gameObject = CreatePanel(parent, name, background);
            gameObject.GetComponent<RectTransform>().sizeDelta = size;
            Button button = gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(background.r, background.g, background.b, 0.42f);
            button.colors = colors;
            Text text = CreateText(gameObject.transform, "Text", label, fontSize, TextAnchor.MiddleCenter, foreground);
            StretchFull(text.rectTransform, 8f);
            return button;
        }

        private Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            RectTransform rect = CreateRectObject(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private RectTransform CreateRectObject(string name, Transform parent)
        {
            GameObject value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            return value.GetComponent<RectTransform>();
        }

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRectObject(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > 0.001f;
            return rect.gameObject;
        }

        private static void StretchFull(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void StretchFull(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventSystemObject = new GameObject("EventSystem (Runtime)");
            eventSystemObject.AddComponent<EventSystem>();
            InputSystemUIInputModule module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
