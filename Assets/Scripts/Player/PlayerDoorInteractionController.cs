using System.Collections;
using DetectiveGame.Interaction;
using StarterAssets;
using UnityEngine;

namespace DetectiveGame.Player
{
    /// <summary>
    /// Coordinates player alignment, humanoid hand IK and a door's own motion.
    /// The mechanical door remains controlled by DoorInteractable so its angle and speed stay editable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator), typeof(ThirdPersonController), typeof(CharacterController))]
    public sealed class PlayerDoorInteractionController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Animator animator;
        [SerializeField] private ThirdPersonController movement;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private StarterAssetsInputs inputs;
        [SerializeField] private FullBodyViewController viewController;
        [SerializeField] private PlayerInteractor playerInteractor;

        [Header("Open Door Timing")]
        [SerializeField, Min(0.05f)] private float alignDuration = 0.28f;
        [SerializeField, Min(0.05f)] private float reachDuration = 0.42f;
        [SerializeField, Min(0f)] private float gripPause = 0.08f;
        [SerializeField, Min(0.05f)] private float handleFollowDuration = 0.22f;
        [SerializeField, Min(0.05f)] private float releaseDuration = 0.24f;

        [Header("Motion Curves")]
        [SerializeField] private AnimationCurve alignCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));
        [SerializeField] private AnimationCurve reachCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));
        [SerializeField] private AnimationCurve releaseCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Header("Right Hand IK")]
        [SerializeField] private Vector3 handPositionOffset;
        [SerializeField] private Vector3 handRotationOffset;
        [SerializeField, Range(0f, 1f)] private float handRotationWeight = 0.75f;
        [SerializeField] private Vector3 elbowHintLocalPosition = new Vector3(0.52f, 1.12f, 0.2f);
        [SerializeField, Range(0f, 1f)] private float elbowHintWeight = 0.65f;

        private Coroutine interactionRoutine;
        private DoorInteractable activeDoor;
        private Transform activeHandTarget;
        private float handIkWeight;
        private bool movementWasEnabled;
        private bool characterControllerWasEnabled;
        private bool interactorWasEnabled;
        private float minimumHandTargetDistance = float.PositiveInfinity;
        private int ikAppliedFrames;

        public bool IsInteracting => interactionRoutine != null || activeDoor != null;
        public float MinimumHandTargetDistance => minimumHandTargetDistance;
        public int IkAppliedFrames => ikAppliedFrames;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (movement == null) movement = GetComponent<ThirdPersonController>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (inputs == null) inputs = GetComponent<StarterAssetsInputs>();
            if (viewController == null) viewController = GetComponent<FullBodyViewController>();
            if (playerInteractor == null) playerInteractor = GetComponent<PlayerInteractor>();
        }

        public bool TryOpenDoor(DoorInteractable door)
        {
            if (!isActiveAndEnabled || door == null || IsInteracting || door.IsOpen) return false;

            door.GetOpenInteractionTargets(transform.position, out Transform standPoint, out Transform handTarget);
            if (standPoint == null || handTarget == null)
            {
                Debug.LogWarning("Door interaction needs a stand point and matching hand target.", door);
                return false;
            }

            activeDoor = door;
            activeHandTarget = handTarget;
            minimumHandTargetDistance = float.PositiveInfinity;
            ikAppliedFrames = 0;
            interactionRoutine = StartCoroutine(OpenDoorRoutine(standPoint));
            return true;
        }

        private IEnumerator OpenDoorRoutine(Transform standPoint)
        {
            FreezeGameplayControl();
            yield return AlignWithStandPoint(standPoint);

            if (activeDoor == null || activeHandTarget == null)
            {
                CompleteInteraction();
                yield break;
            }

            yield return AnimateIkWeight(0f, 1f, reachDuration, reachCurve);
            if (gripPause > 0f) yield return WaitUnscaled(gripPause);

            if (activeDoor == null || activeHandTarget == null)
            {
                CompleteInteraction();
                yield break;
            }

            activeDoor.BeginOpenMotionFromCharacter();
            yield return WaitUnscaled(handleFollowDuration);
            yield return AnimateIkWeight(1f, 0f, releaseDuration, releaseCurve);

            while (activeDoor != null && activeDoor.IsAnimating) yield return null;
            CompleteInteraction();
        }

        private IEnumerator AlignWithStandPoint(Transform standPoint)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            Vector3 targetPosition = standPoint.position;
            Quaternion targetRotation = Quaternion.Euler(0f, standPoint.eulerAngles.y, 0f);
            float duration = Mathf.Max(0.05f, alignDuration);
            float elapsed = 0f;

            if (characterControllerWasEnabled) characterController.enabled = false;
            while (elapsed < duration && standPoint != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float progress = EvaluateCurve(alignCurve, normalizedTime);
                transform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(startPosition, targetPosition, progress),
                    Quaternion.SlerpUnclamped(startRotation, targetRotation, progress));
                yield return null;
            }

            if (standPoint != null) transform.SetPositionAndRotation(targetPosition, targetRotation);
            if (characterControllerWasEnabled) characterController.enabled = true;
        }

        private IEnumerator AnimateIkWeight(float from, float to, float duration, AnimationCurve curve)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);
            while (elapsed < duration && activeDoor != null && activeHandTarget != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                handIkWeight = Mathf.LerpUnclamped(from, to, EvaluateCurve(curve, normalizedTime));
                yield return null;
            }
            handIkWeight = to;
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;

            float weight = activeHandTarget != null ? handIkWeight : 0f;
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight * handRotationWeight);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, weight * elbowHintWeight);
            if (weight <= 0f || activeHandTarget == null) return;

            animator.SetIKPosition(
                AvatarIKGoal.RightHand,
                activeHandTarget.TransformPoint(handPositionOffset));
            animator.SetIKRotation(
                AvatarIKGoal.RightHand,
                activeHandTarget.rotation * Quaternion.Euler(handRotationOffset));
            animator.SetIKHintPosition(
                AvatarIKHint.RightElbow,
                transform.TransformPoint(elbowHintLocalPosition));
            ikAppliedFrames++;
        }

        private void LateUpdate()
        {
            if (activeHandTarget == null || handIkWeight < 0.95f || animator == null) return;
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
                minimumHandTargetDistance = Mathf.Min(
                    minimumHandTargetDistance,
                    Vector3.Distance(rightHand.position, activeHandTarget.TransformPoint(handPositionOffset)));
        }

        private void FreezeGameplayControl()
        {
            movementWasEnabled = movement != null && movement.enabled;
            characterControllerWasEnabled = characterController != null && characterController.enabled;
            interactorWasEnabled = playerInteractor != null && playerInteractor.enabled;

            ResetInputValues();
            if (animator != null)
            {
                animator.SetFloat(Animator.StringToHash("Speed"), 0f);
                animator.SetFloat(Animator.StringToHash("MotionSpeed"), 0f);
            }
            if (movement != null) movement.enabled = false;
            if (playerInteractor != null) playerInteractor.enabled = false;
            if (viewController != null) viewController.SetInteractionLock(true);
        }

        private void CompleteInteraction()
        {
            DoorInteractable completedDoor = activeDoor;
            activeDoor = null;
            activeHandTarget = null;
            handIkWeight = 0f;
            interactionRoutine = null;

            if (viewController != null) viewController.SetInteractionLock(false);
            ResetInputValues();
            if (movement != null) movement.enabled = movementWasEnabled;
            if (characterController != null) characterController.enabled = characterControllerWasEnabled;
            if (playerInteractor != null) playerInteractor.enabled = interactorWasEnabled;
            if (completedDoor != null) completedDoor.CompleteCharacterInteraction();
        }

        private void ResetInputValues()
        {
            if (inputs == null) return;
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.JumpInput(false);
            inputs.SprintInput(false);
        }

        private static float EvaluateCurve(AnimationCurve curve, float normalizedTime)
        {
            if (curve == null || curve.length < 2)
                return normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            return Mathf.Clamp01(curve.Evaluate(normalizedTime));
        }

        private void OnDisable()
        {
            if (interactionRoutine != null) StopCoroutine(interactionRoutine);
            if (IsInteracting) CompleteInteraction();
        }

        private void OnValidate()
        {
            alignDuration = Mathf.Max(0.05f, alignDuration);
            reachDuration = Mathf.Max(0.05f, reachDuration);
            gripPause = Mathf.Max(0f, gripPause);
            handleFollowDuration = Mathf.Max(0.05f, handleFollowDuration);
            releaseDuration = Mathf.Max(0.05f, releaseDuration);
        }
    }
}
