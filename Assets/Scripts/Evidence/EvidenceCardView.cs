using UnityEngine;
using UnityEngine.UI;
namespace DetectiveGame.Evidence
{
    public sealed class EvidenceCardView : MonoBehaviour
    {
        public Button button;
        public Text label;
        public Image icon;
        public Color selectedColor = new Color(.34f, .62f, .88f, 1f);
    }
}
