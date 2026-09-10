using System;
using System.Collections.Generic;
using System.Text;
using DetectiveGame.Recall;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DetectiveGame.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceBoardRuntimeUI : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.018f, 0.02f, 0.026f, 0.985f);
        private static readonly Color Panel = new Color(0.055f, 0.06f, 0.075f, 0.98f);
        private static readonly Color PanelLight = new Color(0.105f, 0.115f, 0.14f, 1f);
        private static readonly Color Paper = new Color(0.92f, 0.9f, 0.82f, 1f);
        private static readonly Color Accent = new Color(0.34f, 0.62f, 0.88f, 1f);

        [Header("Preview")]
        [SerializeField, Range(256, 1536)] private int previewTextureWidth = 1024;
        [SerializeField, Range(256, 1024)] private int previewTextureHeight = 576;
        [SerializeField] private int previewLayer = 2;

        private readonly List<GameObject> evidenceCards = new List<GameObject>();
        private readonly List<GameObject> deductionRows = new List<GameObject>();
        private readonly List<GameObject> timelineMarkers = new List<GameObject>();

        private EvidenceBoardController controller;
        private EvidenceBoardService service;
        private EvidenceDefinition selectedEvidence;
        private RecallObjectData selectedRecallData;
        private RecallPeriod previewPeriod;
        private Font font;
        private bool ownsFont;
        private GameObject canvasObject;
        private RectTransform canvasRect;
        private GameObject boardRoot;
        private GameObject deductionPanelRoot;
        private GameObject previewPanelRoot;
        private GameObject emptyRightPanel;
        private RectTransform evidenceContent;
        private RectTransform deductionContent;
        private Text descriptionText;
        private Text selectedTitleText;
        private Text emptyText;
        private RawImage previewImage;
        private Slider timelineSlider;
        private Text previewTimeText;
        private Text playButtonText;
        private GameObject toastRoot;
        private Text toastText;
        private float toastRemaining;

        private GameObject previewStage;
        private GameObject previewObject;
        private Camera previewCamera;
        private RenderTexture previewTexture;
        private float previewClipTime;
        private bool previewPlaying;

        private void Awake()
        {
            EnsureEventSystem();
            CreatePreviewInfrastructure();
            BuildUI();
        }

        private void Update()
        {
            if (toastRemaining > 0f)
            {
                toastRemaining -= Time.unscaledDeltaTime;
                if (toastRemaining <= 0f && toastRoot != null) toastRoot.SetActive(false);
            }

            if (!IsVisible || !previewPlaying || previewPeriod?.AnimationClip == null) return;
            previewClipTime = Mathf.Min(
                previewPeriod.AnimationClip.length,
                previewClipTime + Time.unscaledDeltaTime * previewPeriod.PlaybackSpeed);
            SamplePreview();
            if (previewClipTime >= previewPeriod.AnimationClip.length) previewPlaying = false;
            UpdatePreviewControls();
        }

        private void OnDestroy()
        {
            if (service != null)
            {
                service.EvidenceDiscovered -= OnEvidenceDiscovered;
                service.EvidenceUpdated -= OnEvidenceUpdated;
            }
            if (canvasObject != null) Destroy(canvasObject);
            if (previewStage != null) Destroy(previewStage);
            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
            }
            if (ownsFont && font != null) Destroy(font);
        }

        public bool IsVisible => boardRoot != null && boardRoot.activeSelf;

        public void Initialize(EvidenceBoardController owner, EvidenceBoardService evidenceService)
        {
            controller = owner;
            if (service != null)
            {
                service.EvidenceDiscovered -= OnEvidenceDiscovered;
                service.EvidenceUpdated -= OnEvidenceUpdated;
            }
            service = evidenceService;
            if (service != null)
            {
                service.EvidenceDiscovered += OnEvidenceDiscovered;
                service.EvidenceUpdated += OnEvidenceUpdated;
            }
        }

        public void Show()
        {
            boardRoot.SetActive(true);
            if (previewCamera != null) previewCamera.enabled = true;
            RefreshEvidenceList();
            if (selectedEvidence != null && service != null && service.Contains(selectedEvidence.EvidenceId))
                SelectEvidence(selectedEvidence);
            else if (service != null && service.DiscoveredEvidence.Count > 0)
                SelectEvidence(service.DiscoveredEvidence[0]);
            else ShowEmptyState();
        }

        public void Hide()
        {
            boardRoot.SetActive(false);
            previewPlaying = false;
            if (previewCamera != null) previewCamera.enabled = false;
        }

        public void TogglePreviewPlayback()
        {
            if (!IsVisible || previewPeriod?.AnimationClip == null) return;
            if (previewClipTime >= previewPeriod.AnimationClip.length) previewClipTime = 0f;
            previewPlaying = !previewPlaying;
            SamplePreview();
            UpdatePreviewControls();
        }

        public void CommitChoice(string momentId, string choiceId)
        {
            if (selectedEvidence == null || service == null) return;
            EvidenceMomentDefinition moment = FindMoment(selectedEvidence, momentId);
            if (moment == null) return;
            service.SetSelectedChoice(selectedEvidence, moment, choiceId);
        }

        private void OnEvidenceDiscovered(EvidenceDefinition definition)
        {
            ShowToast($"{definition.DisplayName}已加入证据板");
            if (!IsVisible) return;
            RefreshEvidenceList();
            SelectEvidence(definition);
        }

        private void OnEvidenceUpdated(EvidenceDefinition definition)
        {
            if (!IsVisible || selectedEvidence != definition) return;
            RefreshDescription();
            RefreshDeductionRows();
        }

        private void ShowToast(string message)
        {
            toastText.text = message;
            toastRemaining = 3.5f;
            toastRoot.SetActive(true);
        }

        private void RefreshEvidenceList()
        {
            ClearObjects(evidenceCards);
            if (service == null) return;
            for (int i = 0; i < service.DiscoveredEvidence.Count; i++)
            {
                EvidenceDefinition evidence = service.DiscoveredEvidence[i];
                if (evidence == null) continue;
                EvidenceDefinition captured = evidence;
                Button card = CreateButton(evidenceContent, evidence.DisplayName, evidence.DisplayName,
                    Vector2.zero, evidence == selectedEvidence ? Accent : PanelLight, Paper, 23);
                card.onClick.AddListener(() => SelectEvidence(captured));
                evidenceCards.Add(card.gameObject);

                if (evidence.Icon != null)
                {
                    Image icon = CreateRectObject("Icon", card.transform).gameObject.AddComponent<Image>();
                    icon.sprite = evidence.Icon;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    RectTransform iconRect = icon.rectTransform;
                    iconRect.anchorMin = new Vector2(0.1f, 0.3f);
                    iconRect.anchorMax = new Vector2(0.9f, 0.92f);
                    iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                }
            }
        }

        private void SelectEvidence(EvidenceDefinition evidence)
        {
            selectedEvidence = evidence;
            selectedRecallData = service?.GetRecallData(evidence);
            emptyRightPanel.SetActive(false);
            deductionPanelRoot.SetActive(true);
            previewPanelRoot.SetActive(true);
            selectedTitleText.text = evidence.DisplayName;
            RefreshEvidenceList();
            RefreshDescription();
            RefreshDeductionRows();
            LoadPreview();
        }

        private void ShowEmptyState()
        {
            selectedEvidence = null;
            selectedRecallData = null;
            selectedTitleText.text = "证据板";
            descriptionText.text = "证据会记录在这里。";
            deductionPanelRoot.SetActive(false);
            previewPanelRoot.SetActive(false);
            emptyRightPanel.SetActive(true);
            ClearObjects(deductionRows);
            ClearPreview();
        }

        private void RefreshDescription()
        {
            if (selectedEvidence == null) return;
            var text = new StringBuilder();
            text.Append("<b>证据描述</b>\n").Append(selectedEvidence.Description)
                .Append("\n\n<b>当前推断</b>");
            bool hasInference = false;
            for (int i = 0; i < selectedEvidence.MomentCount; i++)
            {
                EvidenceMomentDefinition moment = selectedEvidence.GetMoment(i);
                EvidenceChoiceDefinition choice = service.GetSelectedChoice(selectedEvidence, moment);
                if (choice == null) continue;
                hasInference = true;
                text.Append("\n<color=#75B8F4>[").Append(moment.TimeLabel).Append("] ")
                    .Append(choice.Text).Append("</color>");
            }
            if (!hasInference) text.Append("\n<color=#999999>尚未作出推断</color>");
            descriptionText.text = text.ToString();
        }

        private void RefreshDeductionRows()
        {
            ClearObjects(deductionRows);
            if (selectedEvidence == null) return;

            for (int i = 0; i < selectedEvidence.MomentCount; i++)
            {
                EvidenceMomentDefinition moment = selectedEvidence.GetMoment(i);
                if (moment == null) continue;
                GameObject row = CreatePanel(deductionContent, $"Moment {moment.Id}", Panel);
                LayoutElement rowLayout = row.AddComponent<LayoutElement>();
                rowLayout.preferredHeight = 215f;
                deductionRows.Add(row);

                Text fact = CreateText(row.transform, "Observed Fact",
                    $"<b>[{moment.TimeLabel}]</b>  {moment.ObservedFact}", 25, TextAnchor.MiddleLeft, Paper);
                SetRect(fact.rectTransform, new Vector2(0f, 1f), new Vector2(0.58f, 1f),
                    new Vector2(18f, -16f), new Vector2(-26f, 64f), new Vector2(0f, 1f));

                EvidenceChoiceDefinition selected = service.GetSelectedChoice(selectedEvidence, moment);
                GameObject drop = CreatePanel(row.transform, "Inference Drop Zone",
                    selected == null ? new Color(0.08f, 0.085f, 0.1f, 1f) : new Color(0.12f, 0.23f, 0.32f, 1f));
                SetRect(drop.GetComponent<RectTransform>(), new Vector2(0.6f, 1f), new Vector2(1f, 1f),
                    new Vector2(-18f, -14f), new Vector2(-12f, 68f), new Vector2(1f, 1f));
                Outline outline = drop.AddComponent<Outline>();
                outline.effectColor = Accent;
                outline.effectDistance = new Vector2(2f, -2f);
                Text dropText = CreateText(drop.transform, "Selection",
                    selected == null ? "拖拽选项到这里" : selected.Text, 22, TextAnchor.MiddleCenter, Paper);
                StretchFull(dropText.rectTransform, 8f);
                drop.AddComponent<EvidenceChoiceDropZone>().Configure(this, moment.Id);

                for (int choiceIndex = 0; choiceIndex < moment.ChoiceCount; choiceIndex++)
                {
                    EvidenceChoiceDefinition choice = moment.GetChoice(choiceIndex);
                    if (choice == null) continue;
                    string prefix = ((char)('A' + choiceIndex)).ToString();
                    Button option = CreateButton(row.transform, $"Choice {prefix}", $"{prefix}. {choice.Text}",
                        new Vector2(0f, 66f), PanelLight, Paper, 21);
                    float width = 1f / Mathf.Max(1, moment.ChoiceCount);
                    RectTransform optionRect = option.GetComponent<RectTransform>();
                    optionRect.anchorMin = new Vector2(choiceIndex * width, 0f);
                    optionRect.anchorMax = new Vector2((choiceIndex + 1) * width, 0f);
                    optionRect.pivot = new Vector2(0.5f, 0f);
                    optionRect.offsetMin = new Vector2(10f, 18f);
                    optionRect.offsetMax = new Vector2(-10f, 84f);
                    EvidenceChoiceDragHandler drag = option.gameObject.AddComponent<EvidenceChoiceDragHandler>();
                    drag.Configure(this, canvasRect, moment.Id, choice.Id);
                }
            }
        }

        private void CreatePreviewInfrastructure()
        {
            previewStage = new GameObject("Evidence Preview Stage (Runtime)");
            previewStage.transform.position = new Vector3(0f, -1000f, 0f);
            SetLayerRecursively(previewStage, previewLayer);

            GameObject cameraObject = new GameObject("Evidence Preview Camera");
            cameraObject.transform.SetParent(previewStage.transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.018f, 0.02f, 0.026f, 1f);
            previewCamera.cullingMask = 1 << previewLayer;
            previewCamera.nearClipPlane = 0.02f;
            previewCamera.fieldOfView = 42f;
            previewTexture = new RenderTexture(previewTextureWidth, previewTextureHeight, 24,
                RenderTextureFormat.ARGB32) { name = "Evidence Preview (Runtime)" };
            previewTexture.Create();
            previewCamera.targetTexture = previewTexture;
            previewCamera.enabled = false;

            GameObject lightObject = new GameObject("Evidence Preview Light");
            lightObject.transform.SetParent(previewStage.transform, false);
            lightObject.transform.localPosition = new Vector3(-1.6f, 2.5f, -2.2f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.intensity = 4.5f;
            light.color = new Color(0.84f, 0.9f, 1f);
            light.cullingMask = 1 << previewLayer;
            SetLayerRecursively(lightObject, previewLayer);
        }

        private void LoadPreview()
        {
            ClearPreview();
            if (selectedRecallData == null || selectedRecallData.PreviewPrefab == null ||
                selectedRecallData.PeriodCount == 0) return;
            int periodIndex = Mathf.Clamp(selectedEvidence.PreviewPeriodIndex, 0, selectedRecallData.PeriodCount - 1);
            previewPeriod = selectedRecallData.GetPeriod(periodIndex);
            if (previewPeriod?.AnimationClip == null) return;

            previewObject = Instantiate(selectedRecallData.PreviewPrefab, previewStage.transform);
            previewObject.name = $"{selectedEvidence.DisplayName} Preview";
            previewObject.transform.localPosition = Vector3.zero;
            previewObject.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(previewObject, previewLayer);
            foreach (Collider collider in previewObject.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in previewObject.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (Animator animator in previewObject.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Animation animation in previewObject.GetComponentsInChildren<Animation>(true)) animation.enabled = false;

            previewClipTime = 0f;
            previewPlaying = false;
            SamplePreview();
            FramePreviewCamera();
            RefreshTimelineMarkers();
            UpdatePreviewControls();
        }

        private void ClearPreview()
        {
            if (previewObject != null) Destroy(previewObject);
            previewObject = null;
            previewPeriod = null;
            previewPlaying = false;
            previewClipTime = 0f;
            ClearObjects(timelineMarkers);
            UpdatePreviewControls();
        }

        private void SamplePreview()
        {
            if (previewObject != null && previewPeriod?.AnimationClip != null)
                previewPeriod.AnimationClip.SampleAnimation(previewObject,
                    Mathf.Clamp(previewClipTime, 0f, previewPeriod.AnimationClip.length));
        }

        private void FramePreviewCamera()
        {
            float savedTime = previewClipTime;
            Bounds bounds = default;
            const int sampleCount = 20;
            for (int i = 0; i <= sampleCount; i++)
            {
                previewClipTime = previewPeriod.AnimationClip.length * i / sampleCount;
                SamplePreview();
                Bounds sample = GetPreviewBounds();
                if (i == 0) bounds = sample;
                else bounds.Encapsulate(sample);
            }
            previewClipTime = savedTime;
            SamplePreview();

            Quaternion rotation = Quaternion.Euler(selectedRecallData.PreviewCameraEuler);
            float aspect = (float)previewTextureWidth / previewTextureHeight;
            float extent = Mathf.Max(bounds.extents.y, bounds.extents.x / Mathf.Max(1f, aspect));
            extent = Mathf.Max(extent, bounds.extents.z * 0.55f);
            float distance = extent / Mathf.Tan(selectedRecallData.PreviewFieldOfView * 0.5f * Mathf.Deg2Rad) *
                             selectedRecallData.FramingPadding;
            distance = Mathf.Max(distance, bounds.extents.magnitude * 1.05f, 0.45f);
            previewCamera.transform.SetPositionAndRotation(
                bounds.center - rotation * Vector3.forward * distance, rotation);
            previewCamera.fieldOfView = selectedRecallData.PreviewFieldOfView;
        }

        private Bounds GetPreviewBounds()
        {
            Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(previewObject.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private void OnTimelineChanged(float normalizedTime)
        {
            if (previewPeriod?.AnimationClip == null) return;
            previewPlaying = false;
            previewClipTime = Mathf.Clamp01(normalizedTime) * previewPeriod.AnimationClip.length;
            SamplePreview();
            UpdatePreviewControls();
        }

        private void UpdatePreviewControls()
        {
            float normalized = previewPeriod?.AnimationClip == null || previewPeriod.AnimationClip.length <= 0f
                ? 0f : Mathf.Clamp01(previewClipTime / previewPeriod.AnimationClip.length);
            if (timelineSlider != null) timelineSlider.SetValueWithoutNotify(normalized);
            if (playButtonText != null) playButtonText.text = previewPlaying ? "暂停 [Space]" : "播放 [Space]";
            if (previewTimeText != null)
                previewTimeText.text = previewPeriod == null ? "--:--" : previewPeriod.GetTime(normalized);
        }

        private void RefreshTimelineMarkers()
        {
            ClearObjects(timelineMarkers);
            if (selectedEvidence == null || timelineSlider == null) return;
            for (int i = 0; i < selectedEvidence.MomentCount; i++)
            {
                EvidenceMomentDefinition moment = selectedEvidence.GetMoment(i);
                if (moment == null) continue;
                GameObject marker = CreatePanel(timelineSlider.transform, $"Marker {moment.TimeLabel}", Accent);
                RectTransform rect = marker.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(moment.NormalizedTime, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(8f, 30f);
                rect.anchoredPosition = Vector2.zero;
                marker.GetComponent<Image>().raycastTarget = false;
                timelineMarkers.Add(marker);
            }
        }

        private void BuildUI()
        {
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 28);
            ownsFont = font != null;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("Evidence Board UI (Runtime)", typeof(RectTransform));
            canvasRect = canvasObject.GetComponent<RectTransform>();
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5100;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            BuildBoard(canvasObject.transform);
            BuildToast(canvasObject.transform);
        }

        private void BuildBoard(Transform parent)
        {
            boardRoot = CreatePanel(parent, "Evidence Board", Background);
            StretchFull(boardRoot.GetComponent<RectTransform>(), 24f);

            Button close = CreateButton(boardRoot.transform, "Close", "×", new Vector2(58f, 58f),
                new Color(0.22f, 0.07f, 0.07f, 1f), Paper, 38);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -18f), new Vector2(58f, 58f), new Vector2(0f, 1f));
            close.onClick.AddListener(() => controller?.Close());

            selectedTitleText = CreateText(boardRoot.transform, "Evidence Title", "证据板", 38,
                TextAnchor.MiddleLeft, Paper);
            SetRect(selectedTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(0.38f, 1f),
                new Vector2(92f, -16f), new Vector2(-20f, 62f), new Vector2(0f, 1f));

            GameObject listPanel = CreatePanel(boardRoot.transform, "Evidence List Panel", Panel);
            SetAnchors(listPanel.GetComponent<RectTransform>(), new Vector2(0.015f, 0.33f), new Vector2(0.37f, 0.92f), 0f);
            evidenceContent = CreateGridScrollView(listPanel.transform, "Evidence List");

            GameObject descriptionPanel = CreatePanel(boardRoot.transform, "Evidence Description Panel", Panel);
            SetAnchors(descriptionPanel.GetComponent<RectTransform>(), new Vector2(0.015f, 0.025f), new Vector2(0.37f, 0.31f), 0f);
            descriptionText = CreateText(descriptionPanel.transform, "Description",
                "完成物体回溯后，证据会记录在这里。", 23, TextAnchor.UpperLeft, Paper);
            descriptionText.supportRichText = true;
            descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
            StretchFull(descriptionText.rectTransform, 22f);

            deductionPanelRoot = CreatePanel(boardRoot.transform, "Deduction Panel", Panel);
            SetAnchors(deductionPanelRoot.GetComponent<RectTransform>(), new Vector2(0.39f, 0.55f), new Vector2(0.985f, 0.92f), 0f);
            Text deductionTitle = CreateText(deductionPanelRoot.transform, "Title", "回溯事实与玩家推断", 28,
                TextAnchor.MiddleLeft, Paper);
            SetRect(deductionTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -8f), new Vector2(-36f, 52f), new Vector2(0f, 1f));
            deductionContent = CreateVerticalScrollView(deductionPanelRoot.transform, "Deduction List", 62f);

            previewPanelRoot = CreatePanel(boardRoot.transform, "Recall Preview Panel", Panel);
            SetAnchors(previewPanelRoot.GetComponent<RectTransform>(), new Vector2(0.39f, 0.025f), new Vector2(0.985f, 0.525f), 0f);
            BuildPreview(previewPanelRoot.transform);

            emptyRightPanel = CreatePanel(boardRoot.transform, "Empty Evidence Panel", Panel);
            SetAnchors(emptyRightPanel.GetComponent<RectTransform>(), new Vector2(0.39f, 0.025f), new Vector2(0.985f, 0.92f), 0f);
            emptyText = CreateText(emptyRightPanel.transform, "Empty Evidence", "尚未获得任何证据", 30,
                TextAnchor.MiddleCenter, new Color(Paper.r, Paper.g, Paper.b, 0.62f));
            StretchFull(emptyText.rectTransform, 20f);
            emptyRightPanel.SetActive(false);
            boardRoot.SetActive(false);
        }

        private void BuildPreview(Transform parent)
        {
            previewImage = CreateRectObject("Recall Preview", parent).gameObject.AddComponent<RawImage>();
            previewImage.texture = previewTexture;
            previewImage.color = Color.white;
            RectTransform imageRect = previewImage.rectTransform;
            imageRect.anchorMin = new Vector2(0.04f, 0.18f);
            imageRect.anchorMax = new Vector2(0.96f, 0.94f);
            imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;

            Button play = CreateButton(parent, "Play Pause", "播放 [Space]", new Vector2(190f, 54f),
                PanelLight, Paper, 22);
            SetRect(play.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 18f), new Vector2(190f, 54f), new Vector2(0f, 0f));
            play.onClick.AddListener(TogglePreviewPlayback);
            playButtonText = play.GetComponentInChildren<Text>();

            timelineSlider = CreateSlider(parent, "Timeline");
            RectTransform sliderRect = timelineSlider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(1f, 0f);
            sliderRect.pivot = new Vector2(0.5f, 0f);
            sliderRect.offsetMin = new Vector2(235f, 26f);
            sliderRect.offsetMax = new Vector2(-130f, 64f);
            timelineSlider.onValueChanged.AddListener(OnTimelineChanged);

            previewTimeText = CreateText(parent, "Time", "--:--", 22, TextAnchor.MiddleCenter, Paper);
            SetRect(previewTimeText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-18f, 20f), new Vector2(100f, 52f), new Vector2(1f, 0f));
        }

        private void BuildToast(Transform parent)
        {
            toastRoot = CreatePanel(parent, "Evidence Added Toast", new Color(0.04f, 0.055f, 0.07f, 0.97f));
            SetRect(toastRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -55f), new Vector2(620f, 72f), new Vector2(0.5f, 1f));
            Outline outline = toastRoot.AddComponent<Outline>();
            outline.effectColor = Accent;
            outline.effectDistance = new Vector2(2f, -2f);
            toastText = CreateText(toastRoot.transform, "Text", "证据已加入证据板", 27,
                TextAnchor.MiddleCenter, Paper);
            StretchFull(toastText.rectTransform, 12f);
            toastRoot.SetActive(false);
        }

        private RectTransform CreateGridScrollView(Transform parent, string name)
        {
            ScrollRect scroll = CreateScrollRoot(parent, name, 12f, out RectTransform viewport);
            RectTransform content = CreateRectObject("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(175f, 132f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        private RectTransform CreateVerticalScrollView(Transform parent, string name, float topInset)
        {
            ScrollRect scroll = CreateScrollRoot(parent, name, 12f, out RectTransform viewport);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.offsetMax = new Vector2(-12f, -topInset);
            viewport.offsetMax = new Vector2(-30f, 0f);
            RectTransform content = CreateRectObject("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.verticalScrollbar = CreateVerticalScrollbar(scroll.transform);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.verticalScrollbarSpacing = 6f;
            return content;
        }

        private Scrollbar CreateVerticalScrollbar(Transform parent)
        {
            GameObject root = CreatePanel(parent, "Vertical Scrollbar", new Color(0.12f, 0.13f, 0.16f, 1f));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 0.5f);
            rootRect.anchoredPosition = new Vector2(-2f, 0f);
            rootRect.sizeDelta = new Vector2(18f, -4f);

            Scrollbar scrollbar = root.AddComponent<Scrollbar>();
            RectTransform slidingArea = CreateRectObject("Sliding Area", root.transform);
            StretchFull(slidingArea, 2f);
            Image handle = CreatePanel(slidingArea, "Handle", Accent).GetComponent<Image>();
            StretchFull(handle.rectTransform);
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private ScrollRect CreateScrollRoot(Transform parent, string name, float inset, out RectTransform viewport)
        {
            GameObject root = CreatePanel(parent, name, new Color(0f, 0f, 0f, 0.12f));
            StretchFull(root.GetComponent<RectTransform>(), inset);
            ScrollRect scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            viewport = CreateRectObject("Viewport", root.transform);
            StretchFull(viewport);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;
            return scroll;
        }

        private Slider CreateSlider(Transform parent, string name)
        {
            RectTransform root = CreateRectObject(name, parent);
            Slider slider = root.gameObject.AddComponent<Slider>();
            GameObject background = CreatePanel(root, "Background", new Color(0.24f, 0.25f, 0.28f, 1f));
            StretchFull(background.GetComponent<RectTransform>(), 0f);
            GameObject fillArea = CreateRectObject("Fill Area", root).gameObject;
            StretchFull(fillArea.GetComponent<RectTransform>(), 5f);
            Image fill = CreatePanel(fillArea.transform, "Fill", Accent).GetComponent<Image>();
            StretchFull(fill.rectTransform);
            GameObject handleArea = CreateRectObject("Handle Slide Area", root).gameObject;
            StretchFull(handleArea.GetComponent<RectTransform>(), 7f);
            Image handle = CreatePanel(handleArea.transform, "Handle", Paper).GetComponent<Image>();
            handle.rectTransform.sizeDelta = new Vector2(24f, 34f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 size,
            Color background, Color foreground, int fontSize)
        {
            GameObject gameObject = CreatePanel(parent, name, background);
            gameObject.GetComponent<RectTransform>().sizeDelta = size;
            Button button = gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            Text text = CreateText(gameObject.transform, "Text", label, fontSize, TextAnchor.MiddleCenter, foreground);
            StretchFull(text.rectTransform, 8f);
            return button;
        }

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRectObject(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > 0.001f;
            return rect.gameObject;
        }

        private Text CreateText(Transform parent, string name, string value, int fontSize,
            TextAnchor alignment, Color color)
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

        private static RectTransform CreateRectObject(string name, Transform parent)
        {
            GameObject value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            return value.GetComponent<RectTransform>();
        }

        private static void StretchFull(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, float inset)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void ClearObjects(List<GameObject> objects)
        {
            for (int i = 0; i < objects.Count; i++)
                if (objects[i] != null) Destroy(objects[i]);
            objects.Clear();
        }

        private static EvidenceMomentDefinition FindMoment(EvidenceDefinition definition, string momentId)
        {
            for (int i = 0; i < definition.MomentCount; i++)
            {
                EvidenceMomentDefinition moment = definition.GetMoment(i);
                if (moment != null && moment.Id == momentId) return moment;
            }
            return null;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
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
