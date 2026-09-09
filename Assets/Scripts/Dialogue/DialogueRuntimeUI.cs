using System;
using System.Collections.Generic;
using System.Text;
using DetectiveGame.Investigation;
using UnityEngine;
using UnityEngine.EventSystems;
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

        private static readonly Color InkBlack = new Color(0.025f, 0.025f, 0.03f, 0.94f);
        private static readonly Color Paper = new Color(0.93f, 0.91f, 0.84f, 1f);
        private static readonly Color Accent = new Color(0.66f, 0.12f, 0.1f, 1f);
        private static readonly Color Muted = new Color(0.42f, 0.42f, 0.44f, 1f);

        private readonly List<GameObject> choiceObjects = new List<GameObject>();
        private readonly List<GameObject> ownedObjects = new List<GameObject>();

        private DialogueController controller;
        private InvestigationKnowledgeService knowledge;
        private InvestigationDatabase database;
        private Font font;
        private bool ownsFont;
        private GameObject canvasObject;
        private GameObject persistentHud;
        private GameObject dialogueLogButtonObject;
        private GameObject wheelRoot;
        private GameObject firstWheelButtonObject;
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

        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();
        }

        private void Update()
        {
            if (toastRemaining <= 0f) return;
            toastRemaining -= Time.unscaledDeltaTime;
            if (toastRemaining <= 0f && toastRoot != null) toastRoot.SetActive(false);
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
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstWheelButtonObject);
        }

        public void HideInvestigationWheel()
        {
            if (wheelRoot != null) wheelRoot.SetActive(false);
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

            GameObject hintPanel = CreatePanel(hudRect, "Investigation Menu Hint",
                new Color(0.035f, 0.035f, 0.045f, 0.82f));
            hintPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 64f);
            hintPanel.GetComponent<Image>().raycastTarget = false;
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

            Shader blurShader = Shader.Find("DetectiveGame/UI/InvestigationBackgroundBlur");
            if (blurShader != null)
            {
                blurMaterial = new Material(blurShader) { name = "Investigation Background Blur (Runtime)" };
                blurMaterial.SetFloat("_BlurRadius", backgroundBlurRadius);
                blurMaterial.SetColor("_Tint", backgroundBlurTint);
                wheelRoot.GetComponent<Image>().material = blurMaterial;
            }

            GameObject center = CreatePanel(wheelRoot.transform, "Wheel Center",
                new Color(0.025f, 0.03f, 0.04f, 0.94f));
            SetRect(center.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(300f, 150f), new Vector2(0.5f, 0.5f));
            Outline centerOutline = center.AddComponent<Outline>();
            centerOutline.effectColor = new Color(0.72f, 0.68f, 0.58f, 0.65f);
            centerOutline.effectDistance = new Vector2(2f, -2f);
            Text centerText = CreateText(center.transform, "Title", "调查菜单\n<size=20>Tab / Esc 返回</size>", 34,
                TextAnchor.MiddleCenter, Paper);
            StretchFull(centerText.rectTransform, 12f);

            InvestigationMenuOption[] options =
            {
                InvestigationMenuOption.TestimonyBoard,
                InvestigationMenuOption.Clues,
                InvestigationMenuOption.Save,
                InvestigationMenuOption.Hint,
                InvestigationMenuOption.Settings
            };
            string[] labels = { "证言板", "线索", "存档", "提示", "设置" };
            float[] angles = { 90f, 18f, -54f, -126f, 162f };
            for (int i = 0; i < options.Length; i++)
            {
                InvestigationMenuOption captured = options[i];
                float radians = angles[i] * Mathf.Deg2Rad;
                Vector2 position = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * wheelRadius;
                Button button = CreateButton(wheelRoot.transform, $"Wheel {labels[i]}", labels[i],
                    new Vector2(230f, 88f),
                    i == 0 ? new Color(0.72f, 0.68f, 0.58f, 0.98f) : new Color(0.09f, 0.105f, 0.13f, 0.97f),
                    i == 0 ? Color.black : Paper, 29);
                SetRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    position, new Vector2(230f, 88f), new Vector2(0.5f, 0.5f));
                Outline outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.82f, 0.8f, 0.72f, 0.65f);
                outline.effectDistance = new Vector2(2f, -2f);
                button.onClick.AddListener(() => controller?.SelectInvestigationMenuOption(captured));
                if (i == 0) firstWheelButtonObject = button.gameObject;
            }

            wheelRoot.SetActive(false);
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
