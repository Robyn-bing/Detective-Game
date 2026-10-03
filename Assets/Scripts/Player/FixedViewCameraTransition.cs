using UnityEngine;

namespace DetectiveGame.Player
{
    /// <summary>The same fixed-view interpolation used by recall and telephone interactions.</summary>
    public static class FixedViewCameraTransition
    {
        public static void Apply(Camera camera, Vector3 fromPosition, Quaternion fromRotation,
            float fromFov, Vector3 toPosition, Quaternion toRotation, float toFov, float progress)
        {
            progress = Mathf.Clamp01(progress);
            camera.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, progress),
                Quaternion.Slerp(fromRotation, toRotation, progress));
            camera.fieldOfView = Mathf.Lerp(fromFov, toFov, progress);
        }

        public static float Progress(AnimationCurve curve, float time)
        {
            time = Mathf.Clamp01(time);
            if (curve == null || curve.length < 2) return time * time * (3f - 2f * time);
            Keyframe first = curve[0], last = curve[curve.length - 1];
            float range = last.value - first.value;
            if (Mathf.Abs(range) < 0.00001f) return time * time * (3f - 2f * time);
            return Mathf.Clamp01((curve.Evaluate(Mathf.Lerp(first.time, last.time, time)) - first.value) / range);
        }
    }
}
