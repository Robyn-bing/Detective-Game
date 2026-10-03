using System;
using System.Collections.Generic;
using DetectiveGame.Dialogue;
using DetectiveGame.Input;
using DetectiveGame.Recall;
using UnityEngine;
using UnityEngine.UI;

namespace DetectiveGame.Interaction
{
    /// <summary>
    /// Collects the actions available on one focused world object and presents every action as
    /// an independent prompt card. Highlighting is owned by the focused object, not by its cards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class InteractionPromptUI : MonoBehaviour
    {
        public enum PromptCardKind { Default, Recall, Interaction }

        [Serializable]
        private sealed class PromptCardStyle
        {
            public Color background = new Color(.025f, .025f, .025f, .88f);
            public Color textColor = Color.white;
            public Vector2 size = new Vector2(175f, 48f);
            [Min(8)] public int fontSize = 26;
            [Min(0f)] public float horizontalPadding = 12f;
            [Min(0f)] public float verticalPadding = 4f;
            public Font fontOverride;
        }

        private sealed class PromptOption
        {
            public UnityEngine.Object source;
            public Transform owner;
            public Transform anchor;
            public RecallableObject highlightOwner;
            public Vector2 offset;
            public string text;
            public string itemName;
            public int order;
            public PromptCardKind kind;
            public bool current;
            public bool builtIn;
        }

        private sealed class PromptCard
        {
            public GameObject root;
            public RectTransform rect;
            public CanvasGroup canvasGroup;
            public Image background;
            public LayoutElement layout;
            public Text text;
        }

        [Header("Prompt Card Layout")]
        [SerializeField, Min(0f)] private float itemSpacing = 10f;
        [SerializeField, Min(0f)] private float screenMargin = 18f;
        [SerializeField] private PromptCardStyle recallCardStyle = new PromptCardStyle();
        [SerializeField] private PromptCardStyle interactionCardStyle = new PromptCardStyle();
        [SerializeField] private PromptCardStyle defaultCardStyle = new PromptCardStyle();

        [SerializeField] private Font runtimeFont;
        [SerializeField] private GameObject canvasObject, promptContainerObject;
        [SerializeField] private RectTransform promptContainer, canvasRect;
        [SerializeField] private VerticalLayoutGroup verticalLayout;
        private readonly List<PromptCard> cards = new List<PromptCard>();
        private readonly List<PromptOption> options = new List<PromptOption>();
        private readonly List<PromptOption> visibleOptions = new List<PromptOption>();
        private Camera viewCamera;
        private WorldInteractable interactTarget, presentedInteract;
        private RecallableObject recallTarget, presentedRecall;
        private InteractionHighlight highlightedOwner;
        private Material outlineMaterial;

        public bool IsInteractPresented(WorldInteractable target) => target != null && presentedInteract == target;
        public bool IsRecallPresented(RecallableObject target) => target != null && presentedRecall == target;
        public bool IsOptionPresented(UnityEngine.Object source)
        {
            if (source == null) return false;
            for (int i = 0; i < visibleOptions.Count; i++)
                if (visibleOptions[i].source == source) return true;
            return false;
        }

        public int PresentedOptionCount { get; private set; }
        public string[] PresentedTexts
        {
            get
            {
                var result = new string[PresentedOptionCount];
                for (int i = 0; i < result.Length; i++) result[i] = cards[i].text.text;
                return result;
            }
        }

        private void Awake()
        {
            viewCamera = Camera.main;
            if (useSceneUI) BindSceneUI();
            else BuildUI();
            promptContainerObject.SetActive(false);
        }

        public void SetCamera(Camera camera) => viewCamera = camera != null ? camera : Camera.main;
        public void SetOutlineMaterial(Material material) { if (material != null) outlineMaterial = material; }
        public void Show(WorldInteractable target) => interactTarget = target;
        public void ShowRecall(RecallableObject target) => recallTarget = target;

        /// <summary>
        /// Registers an additional action card. This lets another interaction provider add a third
        /// or later action without creating another prompt canvas or another highlight controller.
        /// Call ClearOption when the provider loses focus.
        /// </summary>
        public void SetOption(UnityEngine.Object source, Transform owner, Transform anchor,
            RecallableObject highlightOwner, string itemName, string text, Vector2 screenOffset,
            int order = 200, PromptCardKind kind = PromptCardKind.Default)
        {
            if (source == null) return;
            PromptOption option = FindOption(source);
            if (option == null)
            {
                option = new PromptOption { source = source };
                options.Add(option);
            }
            option.owner = owner != null ? owner : anchor;
            option.anchor = anchor != null ? anchor : option.owner;
            option.highlightOwner = highlightOwner;
            option.itemName = string.IsNullOrWhiteSpace(itemName) ? "InteractionPrompt" : itemName;
            option.text = text ?? string.Empty;
            option.offset = screenOffset;
            option.order = order;
            option.kind = kind;
            option.current = true;
        }

        public void ClearOption(UnityEngine.Object source)
        {
            if (source == null) return;
            for (int i = options.Count - 1; i >= 0; i--)
                if (options[i].source == source) options.RemoveAt(i);
        }

        private void LateUpdate()
        {
            presentedInteract = null;
            presentedRecall = null;
            PresentedOptionCount = 0;
            visibleOptions.Clear();

            if (viewCamera == null || DialogueController.AnyModalOpen ||
                (GameInputModeController.Current != null && !GameInputModeController.Current.IsGameplay))
            { HidePresentation(); return; }

            RefreshBuiltInOptions();
            RemoveDeadOptions();
            if (options.Count == 0) { HidePresentation(); return; }

            Transform selectedOwner = FindClosestOwner();
            if (selectedOwner == null) { HidePresentation(); return; }
            for (int i = 0; i < options.Count; i++)
                if (options[i].current && options[i].owner == selectedOwner) visibleOptions.Add(options[i]);
            if (visibleOptions.Count == 0) { HidePresentation(); return; }

            visibleOptions.Sort((a, b) =>
            {
                int order = a.order.CompareTo(b.order);
                return order != 0 ? order : string.CompareOrdinal(a.itemName, b.itemName);
            });

            PromptOption placement = visibleOptions[0];
            Vector3 screen = viewCamera.WorldToScreenPoint(placement.anchor.position);
            if (screen.z <= 0f) { visibleOptions.Clear(); HidePresentation(); return; }

            float maxWidth = 0f, totalHeight = 0f;
            RecallableObject ownerHighlight = null;
            for (int i = 0; i < visibleOptions.Count; i++)
            {
                PromptOption option = visibleOptions[i];
                PromptCard card = useSceneUI ? GetSceneCard(i, option.kind) : GetCard(i);
                PromptCardStyle style = GetStyle(option.kind);
                ApplyCard(card, option, style);
                maxWidth = Mathf.Max(maxWidth, useSceneUI ? card.layout.preferredWidth : style.size.x);
                totalHeight += useSceneUI ? card.layout.preferredHeight : style.size.y;
                if (i > 0) totalHeight += useSceneUI ? verticalLayout.spacing : itemSpacing;
                if (ownerHighlight == null && option.highlightOwner != null) ownerHighlight = option.highlightOwner;
                if (option.source == interactTarget) presentedInteract = interactTarget;
                if (option.source == recallTarget) presentedRecall = recallTarget;
            }
            for (int i = visibleOptions.Count; i < cards.Count; i++) cards[i].root.SetActive(false);

            PresentedOptionCount = visibleOptions.Count;
            if (!useSceneUI) verticalLayout.spacing = itemSpacing;
            promptContainer.sizeDelta = new Vector2(maxWidth, totalHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(promptContainer);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
            {
                Vector2 position = local + placement.offset;
                position.x = Mathf.Clamp(position.x, canvasRect.rect.xMin + screenMargin,
                    canvasRect.rect.xMax - maxWidth - screenMargin);
                position.y = Mathf.Clamp(position.y, canvasRect.rect.yMin + totalHeight * .5f + screenMargin,
                    canvasRect.rect.yMax - totalHeight * .5f - screenMargin);
                promptContainer.localPosition = position;
            }
            promptContainerObject.SetActive(true);
            SetHighlightOwner(ownerHighlight != null ? ownerHighlight.transform : selectedOwner);
        }

        private void RefreshBuiltInOptions()
        {
            for (int i = 0; i < options.Count; i++)
                if (options[i].builtIn) options[i].current = false;

            bool hasInteract = interactTarget != null && interactTarget.IsInteractionAvailable;
            if (hasInteract)
            {
                RecallableObject associated = interactTarget.AssociatedRecall;
                Transform owner = associated != null ? associated.transform : interactTarget.transform;
                string name = interactTarget is TelephoneDialController ? "DialPrompt" : "InteractPrompt";
                SetOption(interactTarget, owner, interactTarget.InteractionAnchor, associated, name,
                    interactTarget.PromptText, interactTarget.PromptScreenOffset, 100, PromptCardKind.Interaction);
                FindOption(interactTarget).builtIn = true;
            }

            bool hasRecall = recallTarget != null && recallTarget.isActiveAndEnabled && recallTarget.Data != null &&
                recallTarget.Data.AvailablePeriodCount > 0;
            if (hasRecall)
                SetOption(recallTarget, recallTarget.transform, recallTarget.InteractionAnchor, recallTarget,
                    "RecallPrompt", "【R 回溯】", recallTarget.PromptScreenOffset, 0, PromptCardKind.Recall);
            if (hasRecall) FindOption(recallTarget).builtIn = true;
        }

        private void RemoveDeadOptions()
        {
            for (int i = options.Count - 1; i >= 0; i--)
            {
                PromptOption option = options[i];
                if (option.source == null || option.owner == null || option.anchor == null)
                    options.RemoveAt(i);
                else if (!(option.source == interactTarget || option.source == recallTarget) &&
                    option.source is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                    option.current = false;
            }
        }

        private Transform FindClosestOwner()
        {
            Transform best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                PromptOption option = options[i];
                if (!option.current || option.owner == null) continue;
                float distance = (transform.position - option.anchor.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = option.owner;
            }
            return best;
        }

        private PromptOption FindOption(UnityEngine.Object source)
        {
            if (source == null) return null;
            for (int i = 0; i < options.Count; i++)
                if (options[i].source == source) return options[i];
            return null;
        }

        private PromptCard GetCard(int index)
        {
            while (cards.Count <= index) cards.Add(CreateCard(cards.Count));
            PromptCard card = cards[index];
            card.root.SetActive(true);
            card.rect.SetSiblingIndex(index);
            return card;
        }

        private void ApplyCard(PromptCard card, PromptOption option, PromptCardStyle style)
        {
            card.root.name = option.itemName;
            card.canvasGroup.alpha = 1f;
            card.canvasGroup.interactable = false;
            card.canvasGroup.blocksRaycasts = false;
            if (useSceneUI) { card.text.text = option.text; return; }
            card.background.color = style.background;
            card.rect.sizeDelta = style.size;
            card.layout.preferredWidth = style.size.x;
            card.layout.preferredHeight = style.size.y;
            card.text.font = style.fontOverride != null ? style.fontOverride : runtimeFont;
            card.text.fontSize = style.fontSize;
            card.text.color = style.textColor;
            card.text.text = option.text;
            RectTransform textRect = (RectTransform)card.text.transform;
            textRect.offsetMin = new Vector2(style.horizontalPadding, style.verticalPadding);
            textRect.offsetMax = new Vector2(-style.horizontalPadding, -style.verticalPadding);
        }

        private PromptCardStyle GetStyle(PromptCardKind kind)
        {
            if (kind == PromptCardKind.Recall) return recallCardStyle;
            if (kind == PromptCardKind.Interaction) return interactionCardStyle;
            return defaultCardStyle;
        }

        private void HidePresentation()
        {
            if (promptContainerObject != null) promptContainerObject.SetActive(false);
            PresentedOptionCount = 0;
            SetHighlightOwner(null);
        }

        private void SetHighlightOwner(Transform owner)
        {
            var next = owner != null ? owner.GetComponent<InteractionHighlight>() ?? owner.gameObject.AddComponent<InteractionHighlight>() : null;
            if (highlightedOwner == next) return;
            if (highlightedOwner != null) highlightedOwner.SetHighlighted(false, outlineMaterial);
            highlightedOwner = next;
            if (highlightedOwner != null) highlightedOwner.SetHighlighted(true, outlineMaterial);
        }

        private void OnDisable()
        {
            presentedInteract = null;
            presentedRecall = null;
            visibleOptions.Clear();
            HidePresentation();
        }

        private void OnDestroy()
        {
            SetHighlightOwner(null);
            if (!useSceneUI && canvasObject != null) Destroy(canvasObject);
            if (!useSceneUI && runtimeFont != null) Destroy(runtimeFont);
        }

        private void OnValidate()
        {
            itemSpacing = Mathf.Max(0f, itemSpacing);
            screenMargin = Mathf.Max(0f, screenMargin);
            ValidateStyle(recallCardStyle);
            ValidateStyle(interactionCardStyle);
            ValidateStyle(defaultCardStyle);
        }

        private static void ValidateStyle(PromptCardStyle style)
        {
            if (style == null) return;
            style.size.x = Mathf.Max(40f, style.size.x);
            style.size.y = Mathf.Max(20f, style.size.y);
            style.fontSize = Mathf.Max(8, style.fontSize);
            style.horizontalPadding = Mathf.Max(0f, style.horizontalPadding);
            style.verticalPadding = Mathf.Max(0f, style.verticalPadding);
        }

        private void BuildUI()
        {
            runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 32);
            canvasObject = new GameObject("Interaction UI (Runtime)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4900;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            canvasRect = canvasObject.GetComponent<RectTransform>();

            promptContainerObject = new GameObject("PromptContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            promptContainerObject.transform.SetParent(canvasObject.transform, false);
            promptContainer = promptContainerObject.GetComponent<RectTransform>();
            promptContainer.anchorMin = promptContainer.anchorMax = new Vector2(.5f, .5f);
            promptContainer.pivot = new Vector2(0f, .5f);
            verticalLayout = promptContainerObject.GetComponent<VerticalLayoutGroup>();
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlWidth = false;
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandWidth = false;
            verticalLayout.childForceExpandHeight = false;
            if (!useSceneUI) verticalLayout.spacing = itemSpacing;
        }

        private PromptCard CreateCard(int index)
        {
            var root = new GameObject("Prompt_" + index, typeof(RectTransform), typeof(CanvasGroup),
                typeof(Image), typeof(LayoutElement));
            root.transform.SetParent(promptContainer, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0f, 1f);
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            Image image = root.GetComponent<Image>();
            image.raycastTarget = false;
            LayoutElement layout = root.GetComponent<LayoutElement>();

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            Text label = textObject.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return new PromptCard
            {
                root = root,
                rect = rect,
                canvasGroup = group,
                background = image,
                layout = layout,
                text = label
            };
        }
    }
}
