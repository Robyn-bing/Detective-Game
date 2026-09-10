using UnityEngine;
using UnityEngine.EventSystems;

namespace DetectiveGame.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceChoiceDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private EvidenceBoardRuntimeUI owner;
        private RectTransform dragRoot;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Transform originalParent;
        private int originalSiblingIndex;
        private Vector2 originalAnchorMin;
        private Vector2 originalAnchorMax;
        private Vector2 originalPivot;
        private Vector2 originalSizeDelta;
        private Vector3 originalAnchoredPosition;
        private Vector3 originalLocalScale;
        private Quaternion originalLocalRotation;

        public string MomentId { get; private set; }
        public string ChoiceId { get; private set; }

        public void Configure(
            EvidenceBoardRuntimeUI board,
            RectTransform root,
            string momentId,
            string choiceId)
        {
            owner = board;
            dragRoot = root;
            MomentId = momentId;
            ChoiceId = choiceId;
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            originalParent = rectTransform.parent;
            originalSiblingIndex = rectTransform.GetSiblingIndex();
            originalAnchorMin = rectTransform.anchorMin;
            originalAnchorMax = rectTransform.anchorMax;
            originalPivot = rectTransform.pivot;
            originalSizeDelta = rectTransform.sizeDelta;
            originalAnchoredPosition = rectTransform.anchoredPosition3D;
            originalLocalScale = rectTransform.localScale;
            originalLocalRotation = rectTransform.localRotation;
            Vector2 dragSize = rectTransform.rect.size;
            rectTransform.SetParent(dragRoot, true);
            rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = dragSize;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.9f;
        }

        public void OnDrag(PointerEventData eventData) => rectTransform.position = eventData.position;

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            RestoreOriginalLayout();
        }

        private void RestoreOriginalLayout()
        {
            if (originalParent == null) return;
            rectTransform.SetParent(originalParent, false);
            rectTransform.SetSiblingIndex(originalSiblingIndex);
            rectTransform.anchorMin = originalAnchorMin;
            rectTransform.anchorMax = originalAnchorMax;
            rectTransform.pivot = originalPivot;
            rectTransform.sizeDelta = originalSizeDelta;
            rectTransform.anchoredPosition3D = originalAnchoredPosition;
            rectTransform.localScale = originalLocalScale;
            rectTransform.localRotation = originalLocalRotation;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging) owner?.CommitChoice(MomentId, ChoiceId);
        }

    }

    [DisallowMultipleComponent]
    public sealed class EvidenceChoiceDropZone : MonoBehaviour, IDropHandler
    {
        private EvidenceBoardRuntimeUI owner;
        private string momentId;

        public void Configure(EvidenceBoardRuntimeUI board, string targetMomentId)
        {
            owner = board;
            momentId = targetMomentId;
        }

        public void OnDrop(PointerEventData eventData)
        {
            EvidenceChoiceDragHandler choice = eventData.pointerDrag == null
                ? null
                : eventData.pointerDrag.GetComponent<EvidenceChoiceDragHandler>();
            if (choice == null || choice.MomentId != momentId) return;
            owner?.CommitChoice(momentId, choice.ChoiceId);
        }
    }
}
