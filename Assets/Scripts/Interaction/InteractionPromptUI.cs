using UnityEngine;
using UnityEngine.UI;

namespace DetectiveGame.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        private Font font;
        private bool ownsFont;
        private GameObject canvasObject;
        private GameObject promptPanel;
        private RectTransform promptRect;
        private Text promptText;
        private Camera viewCamera;
        private WorldInteractable target;

        private void Awake()
        {
            viewCamera = Camera.main;
            BuildUI();
            Show(null);
        }

        private void LateUpdate()
        {
            if (target == null || viewCamera == null || !promptPanel.activeSelf) return;

            promptText.text = target.PromptText;
            Vector3 screenPosition = viewCamera.WorldToScreenPoint(target.InteractionAnchor.position);
            if (screenPosition.z <= 0f)
            {
                promptPanel.SetActive(false);
                return;
            }

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPosition, null, out Vector2 localPosition))
                promptRect.localPosition = localPosition + target.PromptScreenOffset;
        }

        private void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
            if (ownsFont && font != null) Destroy(font);
        }

        public void SetCamera(Camera camera)
        {
            viewCamera = camera != null ? camera : Camera.main;
        }

        public void Show(WorldInteractable interactable)
        {
            target = interactable;
            if (promptPanel == null) return;
            bool visible = target != null;
            promptPanel.SetActive(visible);
            if (visible) promptText.text = target.PromptText;
        }

        private void BuildUI()
        {
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 32);
            ownsFont = font != null;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("Interaction UI (Runtime)", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4900;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            promptPanel = new GameObject("World Interaction Prompt", typeof(RectTransform));
            promptPanel.transform.SetParent(canvasObject.transform, false);
            promptRect = promptPanel.GetComponent<RectTransform>();
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0f, 0.5f);
            promptRect.sizeDelta = new Vector2(175f, 48f);

            Image background = promptPanel.AddComponent<Image>();
            background.color = new Color(0.025f, 0.025f, 0.025f, 0.88f);
            background.raycastTarget = false;

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(promptPanel.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            promptText = textObject.AddComponent<Text>();
            promptText.font = font;
            promptText.fontSize = 26;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color = Color.white;
            promptText.raycastTarget = false;
            promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
            promptText.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
