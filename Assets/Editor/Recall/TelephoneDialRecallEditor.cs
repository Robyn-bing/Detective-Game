using System;
using System.Collections.Generic;
using DetectiveGame.Recall;
using UnityEditor;
using UnityEngine;

namespace DetectiveGame.EditorTools
{
    [CustomEditor(typeof(TelephoneDialRecall))]
    public sealed class TelephoneDialRecallEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (((TelephoneDialRecall)target).AngleProfile == null)
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("digitAngles"), true);
                serializedObject.ApplyModifiedProperties();
            }
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUILayout.HelpBox("参数修改后自动更新拨号 Animation Clip。Angle Profile 是与玩家共用的角度资源。请在非播放模式调整。", MessageType.Info);
            bool rebuild = GUILayout.Button("Rebuild Dial Animation / 更新拨号动画");
            if ((changed || rebuild) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                try { Bake((TelephoneDialRecall)target); }
                catch (InvalidOperationException exception) { Debug.LogWarning(exception.Message, target); }
            }
        }

        public static void Bake(TelephoneDialRecall settings)
        {
            if (settings.DialWheel == null || settings.DialClip == null || settings.PreviewPrefab == null)
                throw new InvalidOperationException("Assign Dial Wheel, Preview Prefab and Dial Clip before baking.");
            if (!settings.DialWheel.IsChildOf(settings.transform) || settings.DialWheel == settings.transform)
                throw new InvalidOperationException("Dial Wheel must be a child of the telephone.");
            if (settings.DigitAngles == null || settings.DigitAngles.Length != 10 ||
                string.IsNullOrEmpty(settings.PhoneNumber) || settings.ClockwiseAxis.sqrMagnitude < 0.5f)
                throw new InvalidOperationException("Provide ten digit angles, a phone number and a nonzero rotation axis.");
            foreach (char digit in settings.PhoneNumber)
                if (digit < '0' || digit > '9') throw new InvalidOperationException("Phone Number accepts only digits 0–9.");
            foreach (float angle in settings.DigitAngles)
                if (float.IsNaN(angle) || float.IsInfinity(angle) || angle < 0f || angle > 360f)
                    throw new InvalidOperationException("Digit angles must be between 0 and 360 degrees.");

            string path = AnimationUtility.CalculateTransformPath(settings.DialWheel, settings.transform);
            Transform previewWheel = settings.PreviewPrefab.transform.Find(path);
            if (previewWheel == null) throw new InvalidOperationException("Preview prefab must preserve the telephone hierarchy.");
            Quaternion rest = previewWheel.localRotation;
            var keys = new List<Keyframe>[4];
            for (int i = 0; i < keys.Length; i++) keys[i] = new List<Keyframe>();
            float time = 0f;
            AddRotation(keys, time, rest);
            AddSweep(keys, ref time, 0f, 0f, settings.InitialPauseSeconds, rest, settings.ClockwiseAxis);
            foreach (char digit in settings.PhoneNumber)
            {
                float angle = settings.DigitAngles[digit - '0'];
                AddSweep(keys, ref time, 0f, angle, angle / settings.ClockwiseSpeed, rest, settings.ClockwiseAxis);
                AddSweep(keys, ref time, angle, angle, settings.DialHoldSeconds, rest, settings.ClockwiseAxis);
                AddSweep(keys, ref time, angle, 0f, angle / settings.ReturnSpeed, rest, settings.ClockwiseAxis);
                AddSweep(keys, ref time, 0f, 0f, settings.BetweenDigitsSeconds, rest, settings.ClockwiseAxis);
            }
            if (time <= 0f) AddRotation(keys, 1f / 30f, rest);

            AnimationClip clip = settings.DialClip;
            Undo.RecordObject(clip, "Rebuild telephone dial animation");
            clip.ClearCurves();
            clip.frameRate = 30f;
            clip.wrapMode = WrapMode.ClampForever;
            string[] properties = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            for (int i = 0; i < 4; i++)
            {
                var curve = new AnimationCurve(keys[i].ToArray());
                for (int k = 0; k < curve.length; k++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), properties[i]), curve);
            }
            clip.EnsureQuaternionContinuity();
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }

        private static void AddSweep(List<Keyframe>[] keys, ref float time, float from, float to,
            float duration, Quaternion rest, Vector3 axis)
        {
            if (duration <= 0f) return;
            // Intermediate keys prevent a 300-degree clockwise dial becoming a 60-degree reverse turn.
            int steps = Mathf.Max(1, Mathf.CeilToInt(duration * 30f), Mathf.CeilToInt(Mathf.Abs(to - from) / 5f));
            float start = time;
            for (int i = 1; i <= steps; i++)
            {
                float fraction = (float)i / steps;
                AddRotation(keys, start + duration * fraction,
                    rest * Quaternion.AngleAxis(Mathf.Lerp(from, to, fraction), axis));
            }
            time = start + duration;
        }

        private static void AddRotation(List<Keyframe>[] keys, float time, Quaternion rotation)
        {
            float[] components = { rotation.x, rotation.y, rotation.z, rotation.w };
            for (int i = 0; i < 4; i++)
            {
                int last = keys[i].Count - 1;
                var key = new Keyframe(time, components[i]);
                if (last >= 0 && Mathf.Abs(keys[i][last].time - time) < 0.00001f) keys[i][last] = key;
                else keys[i].Add(key);
            }
        }
    }
}

