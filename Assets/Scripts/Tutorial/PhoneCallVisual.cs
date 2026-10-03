using System.Collections;
using UnityEngine;

namespace DetectiveGame.Tutorial
{
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
    public sealed class PhoneCallVisual : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.6f;
        private CanvasGroup group;
        private void Awake() { group = GetComponent<CanvasGroup>(); HideImmediate(); }
        public void Show() { group.alpha = 1f; group.blocksRaycasts = false; }
        public void HideImmediate() { if (group == null) group = GetComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false; }
        public IEnumerator FadeOut()
        {
            float elapsed = 0f;
            while (elapsed < fadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, fadeOutSeconds)));
                yield return null;
            }
            HideImmediate();
        }
    }
}
