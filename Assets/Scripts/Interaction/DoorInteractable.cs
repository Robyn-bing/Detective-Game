using System.Collections;
using UnityEngine;

namespace DetectiveGame.Interaction
{
    [DisallowMultipleComponent]
    public sealed class DoorInteractable : WorldInteractable
    {
        [Header("Door Poses (Local Space)")]
        [SerializeField] private Transform movingPart;
        [SerializeField] private bool startOpen;
        [SerializeField] private Vector3 closedLocalPosition;
        [SerializeField] private Vector3 closedLocalEulerAngles;
        [SerializeField] private Vector3 openLocalPosition;
        [SerializeField] private Vector3 openLocalEulerAngles = new Vector3(0f, 100f, 0f);

        [Header("Door Animation")]
        [SerializeField, Min(0.05f)] private float animationDuration = 0.9f;
        [SerializeField] private AnimationCurve motionCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

        private Coroutine animationRoutine;
        private bool isOpen;
        private bool isAnimating;

        public override string PromptText => isOpen ? "[E] 关门" : "[E] 开门";
        public override bool IsInteractionAvailable => base.IsInteractionAvailable && !isAnimating;
        public bool IsOpen => isOpen;
        public bool IsAnimating => isAnimating;

        private void Awake()
        {
            if (movingPart == null) movingPart = transform;
            isOpen = startOpen;
            ApplyPose(isOpen);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (animationRoutine != null) StopCoroutine(animationRoutine);
            animationRoutine = null;
            isAnimating = false;
        }

        public override void Interact(Transform interactor)
        {
            if (!IsInteractionAvailable) return;
            SetOpen(!isOpen);
        }

        public void SetOpen(bool open, bool instant = false)
        {
            if (movingPart == null) movingPart = transform;
            if (animationRoutine != null) StopCoroutine(animationRoutine);

            isOpen = open;
            if (instant || !isActiveAndEnabled)
            {
                ApplyPose(open);
                animationRoutine = null;
                isAnimating = false;
                return;
            }

            animationRoutine = StartCoroutine(AnimateToPose(open));
        }

        private IEnumerator AnimateToPose(bool open)
        {
            isAnimating = true;
            Vector3 startPosition = movingPart.localPosition;
            Quaternion startRotation = movingPart.localRotation;
            Vector3 targetPosition = open ? openLocalPosition : closedLocalPosition;
            Quaternion targetRotation = Quaternion.Euler(open ? openLocalEulerAngles : closedLocalEulerAngles);
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, animationDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float progress = EvaluateMotion(normalizedTime);
                movingPart.localPosition = Vector3.LerpUnclamped(startPosition, targetPosition, progress);
                movingPart.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, progress);
                yield return null;
            }

            movingPart.SetLocalPositionAndRotation(targetPosition, targetRotation);
            animationRoutine = null;
            isAnimating = false;
        }

        private float EvaluateMotion(float normalizedTime)
        {
            if (motionCurve == null || motionCurve.length < 2)
                return normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            return Mathf.Clamp01(motionCurve.Evaluate(normalizedTime));
        }

        private void ApplyPose(bool open)
        {
            Vector3 position = open ? openLocalPosition : closedLocalPosition;
            Quaternion rotation = Quaternion.Euler(open ? openLocalEulerAngles : closedLocalEulerAngles);
            movingPart.SetLocalPositionAndRotation(position, rotation);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (movingPart == null) movingPart = transform;
            animationDuration = Mathf.Max(0.05f, animationDuration);
        }
    }
}
