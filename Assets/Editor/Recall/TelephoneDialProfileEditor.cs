using System;
using DetectiveGame.Interaction;
using DetectiveGame.Recall;
using UnityEditor;
using UnityEngine;

namespace DetectiveGame.EditorTools
{
    [CustomEditor(typeof(TelephoneDialProfile))]
    public sealed class TelephoneDialProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUILayout.HelpBox("Target Angle 是从 TEL_DialWheel 静止姿态起算的顺时针角度，Angle Tolerance 是该数字的识别容差。玩家拨号和回溯共用此资源。非播放模式修改后，会自动重烘焙已加载电话的回溯动画。", MessageType.Info);
            bool rebuild = GUILayout.Button("更新关联的回溯拨号动画");
            if ((changed || rebuild) && !EditorApplication.isPlayingOrWillChangePlaymode)
                Rebuild((TelephoneDialProfile)target);
        }

        public static int Rebuild(TelephoneDialProfile profile)
        {
            string error;
            if (!profile.IsValid(out error)) { Debug.LogWarning(error, profile); return 0; }
            int count = 0;
            var clips = new System.Collections.Generic.HashSet<AnimationClip>();
            foreach (TelephoneDialRecall settings in Resources.FindObjectsOfTypeAll<TelephoneDialRecall>())
            {
                if (settings.AngleProfile != profile || settings.DialClip == null || !clips.Add(settings.DialClip)) continue;
                try { TelephoneDialRecallEditor.Bake(settings); count++; }
                catch (InvalidOperationException exception) { Debug.LogWarning(exception.Message, settings); }
            }
            return count;
        }
    }
}

