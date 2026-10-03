using UnityEngine;
using UnityEngine.UI;
namespace DetectiveGame.Evidence
{
    public sealed class EvidenceMomentView : MonoBehaviour
    {
        public Text fact;
        public Text selection;
        public EvidenceChoiceDropZone dropZone;
        public RectTransform options;
        public Color selectedColor = new Color(.12f, .23f, .32f, 1f);
    }
}
