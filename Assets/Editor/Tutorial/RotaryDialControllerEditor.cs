using DetectiveGame.Interaction;
using DetectiveGame.Recall;
using UnityEditor;
using UnityEngine;

namespace DetectiveGame.EditorTools
{
    [CustomEditor(typeof(RotaryDialController))]
    public sealed class RotaryDialControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var rotary = (RotaryDialController)target;
            EditorGUILayout.HelpBox("实验模式：按下选孔，到挡杆后松手，回弹完成登记。关闭 Use Experimental Finger Stop 恢复旧模式。Scene 视图青色圈为孔位，黄色为止挡；孔位由 Finger Stop Angle − 共享 Target Angle 反算。", MessageType.Info);
            if (rotary.Profile != null && GUILayout.Button("选中共享 0–9 Angle / Tolerance 配置"))
                Selection.activeObject = rotary.Profile;
            if (Application.isPlaying)
                EditorGUILayout.LabelField("Selected Digit / Current Angle", rotary.SelectedDigit + " / " + rotary.CurrentAngle.ToString("F1"));
        }

        private void OnSceneGUI()
        {
            var rotary = (RotaryDialController)target;
            var settings = rotary.GetComponent<TelephoneDialRecall>();
            if (settings == null || rotary.Wheel == null || rotary.Profile == null) return;
            serializedObject.Update();
            float stop = serializedObject.FindProperty("fingerStopAngle").floatValue;
            float radius = serializedObject.FindProperty("digitHoleRadius").floatValue;
            float hit = serializedObject.FindProperty("digitHoleHitRadius").floatValue;
            var axis = settings.ClockwiseAxis;
            var right = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
            if (right.sqrMagnitude < .5f) right = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
            var up = Vector3.Cross(axis, right).normalized;
            var rest = rotary.Wheel.rotation;
            if (Application.isPlaying) rest *= Quaternion.AngleAxis(-rotary.CurrentAngle, axis);
            float scale = Mathf.Max(Mathf.Abs(rotary.Wheel.lossyScale.x), Mathf.Abs(rotary.Wheel.lossyScale.y));
            var origin = rotary.Wheel.position;
            Handles.color = Color.cyan;
            foreach (var entry in rotary.Profile.Digits)
            {
                float angle = (stop - entry.targetAngle) * Mathf.Deg2Rad;
                var point = origin + rest * (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius * scale;
                Handles.DrawWireDisc(point, rest * axis, hit * scale);
                Handles.Label(point, entry.digit.ToString());
            }
            Handles.color = Color.yellow;
            float stopRadians = stop * Mathf.Deg2Rad;
            var stopPoint = origin + rest * (right * Mathf.Cos(stopRadians) + up * Mathf.Sin(stopRadians)) * radius * scale;
            Handles.DrawLine(origin, stopPoint);
            Handles.Label(stopPoint, "Finger Stop " + stop.ToString("F1") + "°");
        }
    }
}
