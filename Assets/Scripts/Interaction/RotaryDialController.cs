using System;
using DetectiveGame.Recall;
using UnityEngine;

namespace DetectiveGame.Interaction
{
    [DisallowMultipleComponent]
    public sealed class RotaryDialController : MonoBehaviour
    {
        [SerializeField] private TelephoneDialRecall dialSettings;
        [Header("Drag")]
        [SerializeField, Min(0.01f)] private float dragSensitivity = 1f;
        [SerializeField, Range(1f, 360f), Tooltip("Clockwise mechanical stop measured from the captured rest pose. Keep above every profile target angle.")]
        private float maximumAngle = 350f;
        [SerializeField, Min(0.001f)] private float pointerRadius = 0.061f;
        [SerializeField, Min(0f)] private float pointerInnerRadius = 0.012f;
        [Header("Experimental Finger Stop (off = original angle matching)")]
        [SerializeField] private bool useExperimentalFingerStop = true;
        [SerializeField, Tooltip("Rest-plane polar angle of the fixed stop. Hole centres are Stop Angle minus the shared digit target.")]
        private float fingerStopAngle = 220f;
        [SerializeField, Min(0.001f)] private float digitHoleRadius = 0.045f;
        [SerializeField, Min(0.001f)] private float digitHoleHitRadius = 0.010f;
        [SerializeField] private bool fingerStopEnabled = true;
        [SerializeField, Range(0f, 5f)] private float stopSnapStrength = 1.5f;
        [SerializeField, Range(0f, 3f)] private float stopBounceAmount = 0.65f;
        [SerializeField, Min(0.01f)] private float stopBounceDuration = 0.12f;
        [Header("Mechanical Return")]
        [SerializeField, Min(1f)] private float returnSpeed = 270f;
        [SerializeField] private AnimationCurve returnCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField, Min(0f)] private float afterDigitPause = 0.12f;

        private enum Phase { Resting, Dragging, Returning, Settling }
        private Phase phase;
        private Quaternion restRotation;
        private float previousPolarAngle, returnStartAngle, elapsed;
        private int pendingDigit = -1;
        private bool experimentalDrag, reachedStop;
        private float selectedLimit, pointerTravel, pointerRadiusAtHit, bounceElapsed = float.PositiveInfinity;
        public int SelectedDigit { get; private set; } = -1;
        public bool ReachedStop => reachedStop;
        public bool UseExperimentalFingerStop => useExperimentalFingerStop;
        public event Action<int> ReturnCompleted;
        public float CurrentAngle { get; private set; }
        public Transform Wheel => dialSettings != null ? dialSettings.DialWheel : null;
        public TelephoneDialProfile Profile => dialSettings != null ? dialSettings.AngleProfile : null;

        private void Awake() { if (Wheel != null) restRotation = Wheel.localRotation; }

        public bool TryBeginDrag(Camera camera, Vector2 screenPoint)
        {
            if (phase != Phase.Resting || Wheel == null || Profile == null) return false;
            if (!GetPointerAngle(camera, screenPoint, true, out previousPolarAngle)) return false;
            experimentalDrag = useExperimentalFingerStop;
            SelectedDigit = -1;
            reachedStop = false;
            pointerTravel = 0f;
            bounceElapsed = float.PositiveInfinity;
            if (experimentalDrag)
            {
                float closest = digitHoleHitRadius;
                foreach (var entry in Profile.Digits)
                {
                    float separation = Mathf.DeltaAngle(previousPolarAngle, fingerStopAngle - entry.targetAngle) * Mathf.Deg2Rad;
                    float distance = Mathf.Sqrt(Mathf.Max(0f, pointerRadiusAtHit * pointerRadiusAtHit + digitHoleRadius * digitHoleRadius -
                        2f * pointerRadiusAtHit * digitHoleRadius * Mathf.Cos(separation)));
                    if (distance > closest) continue;
                    closest = distance;
                    SelectedDigit = entry.digit;
                    selectedLimit = entry.targetAngle;
                }
                if (SelectedDigit < 0) return false;
            }
            phase = Phase.Dragging;
            return true;
        }

        public void Drag(Camera camera, Vector2 screenPoint)
        {
            if (phase != Phase.Dragging || !GetPointerAngle(camera, screenPoint, false, out float polar)) return;
            // Work in the wheel's REST basis; a rotating basis would count its motion twice.
            // The polar basis is built from ClockwiseAxis in the captured REST pose. On this phone
            // positive pointer delta and positive AngleAxis both mean visible clockwise rotation.
            // No Euler wrapping and no sign inversion are involved.
            float change = Mathf.DeltaAngle(previousPolarAngle, polar);
            previousPolarAngle = polar;
            if (experimentalDrag)
            {
                // Track net pointer travel, not a sum of positive deltas: wiggling backwards
                // must not ratchet the wheel all the way to the stop.
                pointerTravel += change * dragSensitivity;
                if (!reachedStop)
                {
                    CurrentAngle = Mathf.Clamp(Mathf.Max(CurrentAngle, pointerTravel), 0f, selectedLimit);
                    float snap = fingerStopEnabled ? stopSnapStrength : 0f;
                    if (CurrentAngle >= selectedLimit - snap)
                    {
                        CurrentAngle = selectedLimit;
                        reachedStop = true;
                        bounceElapsed = 0f;
                    }
                }
                ApplyRotation();
                return;
            }
            float limit = Profile != null ? Mathf.Max(maximumAngle, Profile.MaximumTargetAngle) : maximumAngle;
            CurrentAngle = Mathf.Clamp(CurrentAngle + change * dragSensitivity, 0f, Mathf.Min(360f, limit));
            ApplyRotation();
        }

        public void ReleaseAndReturn()
        {
            if (phase != Phase.Dragging) return;
            pendingDigit = experimentalDrag ? (reachedStop ? SelectedDigit : -1) : Profile.MatchDigit(CurrentAngle);
            returnStartAngle = CurrentAngle;
            elapsed = 0;
            phase = Phase.Returning;
            bounceElapsed = float.PositiveInfinity;
        }

        public void Cancel()
        {
            phase = Phase.Resting;
            pendingDigit = -1;
            SelectedDigit = -1;
            reachedStop = false;
            bounceElapsed = float.PositiveInfinity;
            CurrentAngle = 0;
            ApplyRotation();
        }

        private void Update()
        {
            if (phase == Phase.Dragging && reachedStop && bounceElapsed < stopBounceDuration)
            {
                bounceElapsed += Time.unscaledDeltaTime;
                ApplyRotation();
            }
            if (phase == Phase.Returning)
            {
                elapsed += Time.unscaledDeltaTime;
                float duration = Mathf.Max(0.04f, returnStartAngle / Mathf.Max(1f, returnSpeed));
                float t = Mathf.Clamp01(elapsed / duration);
                float progress = DetectiveGame.Player.FixedViewCameraTransition.Progress(returnCurve, t);
                CurrentAngle = returnStartAngle * (1f - progress);
                ApplyRotation();
                if (t >= 1f) { CurrentAngle = 0; ApplyRotation(); elapsed = 0; phase = Phase.Settling; }
            }
            else if (phase == Phase.Settling)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed < afterDigitPause) return;
                int digit = pendingDigit;
                pendingDigit = -1;
                SelectedDigit = -1;
                phase = Phase.Resting;
                // This is the ONLY place a released digit is delivered to the number controller.
                ReturnCompleted?.Invoke(digit);
            }
        }

        private bool GetPointerAngle(Camera camera, Vector2 screenPoint, bool checkRadius, out float angle)
        {
            angle = 0;
            if (camera == null || Wheel == null) return false;
            Vector3 axis = dialSettings.ClockwiseAxis;
            Vector3 localRight = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
            if (localRight.sqrMagnitude < 0.5f) localRight = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
            Vector3 localUp = Vector3.Cross(axis, localRight).normalized;
            Quaternion worldRest = Wheel.parent != null ? Wheel.parent.rotation * restRotation : restRotation;
            Vector3 normal = worldRest * axis;
            var plane = new Plane(normal, Wheel.position);
            Ray ray = camera.ScreenPointToRay(screenPoint);
            if (!plane.Raycast(ray, out float distance) || distance < 0) return false;
            Vector3 offset = ray.GetPoint(distance) - Wheel.position;
            Vector3 local = Quaternion.Inverse(worldRest) * offset;
            float scale = Mathf.Max(Mathf.Abs(Wheel.lossyScale.x), Mathf.Abs(Wheel.lossyScale.y));
            float radius = Vector3.ProjectOnPlane(local, axis).magnitude / Mathf.Max(0.0001f, scale);
            pointerRadiusAtHit = radius;
            if (radius < pointerInnerRadius || (checkRadius && radius > pointerRadius)) return false;
            angle = Mathf.Atan2(Vector3.Dot(local, localUp), Vector3.Dot(local, localRight)) * Mathf.Rad2Deg;
            return true;
        }

        private void ApplyRotation()
        {
            float visualAngle = CurrentAngle;
            if (fingerStopEnabled && phase == Phase.Dragging && reachedStop && bounceElapsed < stopBounceDuration)
                visualAngle -= Mathf.Min(stopBounceAmount, CurrentAngle) * Mathf.Sin(Mathf.Clamp01(bounceElapsed / stopBounceDuration) * Mathf.PI);
            if (Wheel != null) Wheel.localRotation = restRotation * Quaternion.AngleAxis(visualAngle, dialSettings.ClockwiseAxis);
        }

        private void OnDisable() => Cancel();
    }
}
