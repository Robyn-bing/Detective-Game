using System;
using UnityEngine;
using UnityEngine.UI;

namespace DetectiveGame.Interaction
{
    [DisallowMultipleComponent]
    public sealed class TelephoneDialUI : MonoBehaviour
    {
        [Serializable]
        public sealed class DialDigitSlot
        {
            public RectTransform root;
            public Text digit;
            public Image underline;
        }

        [SerializeField] private GameObject panel;
        [SerializeField] private DialDigitSlot[] slots = new DialDigitSlot[6];
        [SerializeField] private Text instruction;
        [Header("Feedback")]
        [SerializeField] private Color normalColor = new Color(0.92f, 0.88f, 0.78f);
        [SerializeField] private Color errorColor = new Color(0.48f, 0.07f, 0.09f);
        [SerializeField] private Color focusColor = new Color(1f, 0.92f, 0.6f);
        [SerializeField, Min(0f)] private float errorShakeStrength = 5f;
        [SerializeField, Min(0.01f)] private float errorShakeTime = 0.35f;
        [SerializeField, Min(0f)] private float focusPulseSpeed = 3f;
        [SerializeField, Range(0f, 1f)] private float focusPulseStrength = 0.35f;

        private readonly bool[] errors = new bool[6];
        private readonly bool[] shaking = new bool[6];
        private Vector2[] restPositions;
        private float shakeElapsed = float.PositiveInfinity;
        private int focus = -1;
        public bool ErrorFeedbackFinished => shakeElapsed >= errorShakeTime;
        public DialDigitSlot[] Slots => slots;

        private void Awake()
        {
            restPositions = new Vector2[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                if (slots[i]?.root != null) restPositions[i] = slots[i].root.anchoredPosition;
            Hide();
        }

        public void Show() { if (panel != null) panel.SetActive(true); }
        public void Hide()
        {
            shakeElapsed = float.PositiveInfinity;
            if (restPositions != null)
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i]?.root != null) slots[i].root.anchoredPosition = restPositions[i];
            if (panel != null) panel.SetActive(false);
        }

        public void Present(int[] digits, bool[] incorrect, int focusIndex)
        {
            focus = focusIndex;
            for (int i = 0; i < 6; i++)
            {
                errors[i] = incorrect != null && incorrect[i];
                if (slots[i]?.digit != null) slots[i].digit.text = digits[i] < 0 ? "" : digits[i].ToString();
            }
            UpdateColors();
        }

        public void SetInstruction(string message) { if (instruction != null) instruction.text = message; }

        public void PlayErrorFeedback(bool[] incorrect)
        {
            for (int i = 0; i < 6; i++) shaking[i] = incorrect[i];
            shakeElapsed = 0;
        }

        private void Update()
        {
            if (panel == null || !panel.activeSelf) return;
            if (shakeElapsed < errorShakeTime)
            {
                shakeElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(shakeElapsed / Mathf.Max(0.01f, errorShakeTime));
                float offset = Mathf.Sin(t * Mathf.PI * 10f) * (1f - t) * errorShakeStrength;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i]?.root != null)
                        slots[i].root.anchoredPosition = restPositions[i] + (shaking[i] ? new Vector2(offset, 0) : Vector2.zero);
            }
            UpdateColors();
        }

        private void UpdateColors()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Color color = errors[i] ? errorColor : normalColor;
                if (slots[i]?.digit != null) slots[i].digit.color = color;
                if (slots[i]?.underline == null) continue;
                float pulse = (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * focusPulseSpeed)) * focusPulseStrength;
                slots[i].underline.color = i == focus ? Color.Lerp(color, focusColor, 0.5f + pulse) : color;
                slots[i].underline.rectTransform.localScale = i == focus ? new Vector3(1f, 1.4f, 1f) : Vector3.one;
            }
        }
    }
}
