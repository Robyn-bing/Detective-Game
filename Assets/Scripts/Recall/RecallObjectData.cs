using System;
using DetectiveGame.Investigation;
using UnityEngine;
using UnityEngine.Serialization;

namespace DetectiveGame.Recall
{
    [Serializable]
    public sealed class RecallPeriod
    {
        [SerializeField, Tooltip("Stable ID used by evidence and save data. Do not change it after content ships.")]
        private string periodId;
        [SerializeField] private string label = "Memory";
        [SerializeField, Range(0, 23)] private int startHour;
        [SerializeField, Range(0, 59)] private int startMinute;
        [SerializeField, Range(0, 23)] private int endHour;
        [SerializeField, Range(0, 59)] private int endMinute;
        [SerializeField] private AnimationClip animationClip;
        [FormerlySerializedAs("rewindPresentationSeconds")]
        [SerializeField, Min(0.1f), Tooltip("Shared duration for the camera move and the object's memory-reconstruction transition.")]
        private float synchronizedEntrySeconds = 1.25f;
        [FormerlySerializedAs("rewindProgressCurve")]
        [SerializeField, HideInInspector]
        private AnimationCurve legacyRewindProgressCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 2.2f, 2.2f),
            new Keyframe(0.5f, 0.5f, 0.25f, 0.25f),
            new Keyframe(1f, 1f, 2.2f, 2.2f));
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
        [SerializeField] private bool available = true;
        [SerializeField, Tooltip("Leave empty for an always-available period, or use a stable investigation time-period ID.")]
        private string unlockId;

        public string PeriodId => string.IsNullOrWhiteSpace(periodId)
            ? $"{StartTime}-{EndTime}"
            : periodId;
        public string Label => label;
        public AnimationClip AnimationClip => animationClip;
        public float SynchronizedEntrySeconds => synchronizedEntrySeconds;
        public float PlaybackSpeed => playbackSpeed;
        public bool Available => available && (string.IsNullOrWhiteSpace(unlockId) ||
                                                InvestigationKnowledgeService.IsTimePeriodUnlockedGlobally(unlockId));
        public string UnlockId => unlockId;
        public string StartTime => FormatMinutes(StartTotalMinutes);
        public string EndTime => FormatMinutes(EndTotalMinutes);
        private int StartTotalMinutes => startHour * 60 + startMinute;
        private int EndTotalMinutes => endHour * 60 + endMinute;

        public string GetTime(float normalizedTime)
        {
            int start = StartTotalMinutes;
            int end = EndTotalMinutes;
            if (end < start) end += 24 * 60;
            return FormatMinutes(Mathf.RoundToInt(Mathf.Lerp(start, end, Mathf.Clamp01(normalizedTime))));
        }

        private static string FormatMinutes(int totalMinutes)
        {
            totalMinutes %= 24 * 60;
            if (totalMinutes < 0) totalMinutes += 24 * 60;
            return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
        }
    }

    [CreateAssetMenu(fileName = "RecallObject", menuName = "Detective Game/Recall Object")]
    public sealed class RecallObjectData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Recall Object";
        [SerializeField, TextArea] private string description;
        [SerializeField] private GameObject previewPrefab;

        [Header("Preview Framing")]
        [SerializeField] private Vector3 previewCameraEuler = new Vector3(28f, 0f, 0f);
        [SerializeField, Range(20f, 80f)] private float previewFieldOfView = 42f;
        [SerializeField, Range(1f, 3f)] private float framingPadding = 1.25f;
        [SerializeField, Tooltip("Camera progress during the synchronized entry. X is time and Y is camera progress.")]
        private AnimationCurve cameraEntryProgressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0.05f, 2f)] private float cameraReturnTransitionSeconds = 0.7f;
        [SerializeField] private Color backgroundColor = new Color(0.055f, 0.035f, 0.022f, 1f);

        [Header("Memory Reconstruction Transition")]
        [SerializeField, Range(0f, 0.5f), Tooltip("Normalized entry time when the color-drain phase finishes.")]
        private float colorFadeEnd = 0.2f;
        [SerializeField, Range(0f, 0.5f), Tooltip("Normalized entry time when the current-world object starts dissolving.")]
        private float dissolveOutStart = 0.12f;
        [SerializeField, Range(0.2f, 0.75f), Tooltip("Normalized entry time when the current-world object is fully hidden.")]
        private float dissolveOutEnd = 0.46f;
        [SerializeField, Range(0.25f, 0.85f), Tooltip("Normalized entry time when the recalled object starts appearing at animation time zero.")]
        private float revealStart = 0.54f;
        [SerializeField] private AnimationCurve colorFadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve dissolveOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color dissolveEdgeColor = new Color(0.58f, 0.9f, 1.15f, 1f);
        [SerializeField, Range(0.005f, 0.25f)] private float dissolveEdgeWidth = 0.07f;
        [SerializeField, Range(0.1f, 20f)] private float dissolveNoiseScale = 5f;

        [Header("Authored Time Periods")]
        [SerializeField] private RecallPeriod[] periods = Array.Empty<RecallPeriod>();

        public string DisplayName => displayName;
        public string Description => description;
        public GameObject PreviewPrefab => previewPrefab;
        public Vector3 PreviewCameraEuler => previewCameraEuler;
        public float PreviewFieldOfView => previewFieldOfView;
        public float FramingPadding => framingPadding;
        public AnimationCurve CameraEntryProgressCurve => cameraEntryProgressCurve;
        public float CameraReturnTransitionSeconds => cameraReturnTransitionSeconds;
        public Color BackgroundColor => backgroundColor;
        public float ColorFadeEnd => colorFadeEnd;
        public float DissolveOutStart => dissolveOutStart;
        public float DissolveOutEnd => dissolveOutEnd;
        public float RevealStart => revealStart;
        public AnimationCurve ColorFadeCurve => colorFadeCurve;
        public AnimationCurve DissolveOutCurve => dissolveOutCurve;
        public AnimationCurve RevealCurve => revealCurve;
        public Color DissolveEdgeColor => dissolveEdgeColor;
        public float DissolveEdgeWidth => dissolveEdgeWidth;
        public float DissolveNoiseScale => dissolveNoiseScale;
        public int PeriodCount => periods?.Length ?? 0;
        public RecallPeriod GetPeriod(int index) => periods[index];

        public int AvailablePeriodCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < PeriodCount; i++)
                    if (periods[i] != null && periods[i].Available && periods[i].AnimationClip != null) count++;
                return count;
            }
        }

        public int GetAvailablePeriodIndex(int availableIndex)
        {
            for (int i = 0, found = 0; i < PeriodCount; i++)
            {
                if (periods[i] == null || !periods[i].Available || periods[i].AnimationClip == null) continue;
                if (found++ == availableIndex) return i;
            }
            return -1;
        }

        private void OnValidate()
        {
            colorFadeEnd = Mathf.Clamp(colorFadeEnd, 0.01f, 0.5f);
            dissolveOutStart = Mathf.Clamp(dissolveOutStart, 0f, 0.49f);
            dissolveOutEnd = Mathf.Clamp(dissolveOutEnd, dissolveOutStart + 0.01f, 0.75f);
            revealStart = Mathf.Clamp(revealStart, dissolveOutEnd, 0.9f);
        }
    }
}
