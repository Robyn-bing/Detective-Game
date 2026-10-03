using UnityEngine;
using UnityEngine.EventSystems;
namespace DetectiveGame.Evidence
{
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
