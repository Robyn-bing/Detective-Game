using UnityEngine;
using DetectiveGame.Interaction;

namespace DetectiveGame.Recall
{
    // Authoring settings only. Playback belongs to the existing RecallSessionController.
    // No Update, Animator or coroutine writes to the world telephone's transform.
    [DisallowMultipleComponent]
    public sealed class TelephoneDialRecall : MonoBehaviour
    {
        [Header("Animation Assets")]
        [SerializeField] private Transform dialWheel;
        [SerializeField] private GameObject previewPrefab;
        [SerializeField] private AnimationClip dialClip;

        [Header("Dial Sequence")]
        [SerializeField] private string phoneNumber = "501107";
        [SerializeField] private TelephoneDialProfile angleProfile;
        [SerializeField, HideInInspector]
        private float[] digitAngles = { 340f, 70f, 100f, 130f, 160f, 190f, 220f, 250f, 280f, 310f };
        [SerializeField, Tooltip("Axis in the wheel's own local space. +Z is clockwise when viewing this model from its visible front. Angles remain positive.")]
        private Vector3 clockwiseAxis = Vector3.forward;
        [SerializeField, Min(1f), Tooltip("Degrees per second.")] private float clockwiseSpeed = 180f;
        [SerializeField, Min(1f), Tooltip("Degrees per second.")] private float returnSpeed = 270f;
        [SerializeField, Min(0f)] private float dialHoldSeconds = 0.2f;
        [SerializeField, Min(0f)] private float betweenDigitsSeconds = 0.45f;
        [SerializeField, Min(0f)] private float initialPauseSeconds = 0.4f;

        public Transform DialWheel => dialWheel;
        public GameObject PreviewPrefab => previewPrefab;
        public AnimationClip DialClip => dialClip;
        public string PhoneNumber => phoneNumber;
        public TelephoneDialProfile AngleProfile => angleProfile;
        public float[] DigitAngles => angleProfile != null ? angleProfile.GetAngles() : digitAngles;
        public Vector3 ClockwiseAxis => clockwiseAxis.normalized;
        public float ClockwiseSpeed => Mathf.Max(1f, clockwiseSpeed);
        public float ReturnSpeed => Mathf.Max(1f, returnSpeed);
        public float DialHoldSeconds => Mathf.Max(0f, dialHoldSeconds);
        public float BetweenDigitsSeconds => Mathf.Max(0f, betweenDigitsSeconds);
        public float InitialPauseSeconds => Mathf.Max(0f, initialPauseSeconds);
    }
}

