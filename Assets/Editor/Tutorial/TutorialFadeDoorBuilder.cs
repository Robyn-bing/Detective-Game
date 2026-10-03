using System;
using System.Linq;
using DetectiveGame.Dialogue;
using DetectiveGame.Player;
using DetectiveGame.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DetectiveGame.EditorTools
{
    public static class TutorialFadeDoorBuilder
    {
        [MenuItem("Tools/Detective Game/Configure Tutorial Fade and Exit Door")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Tutorial.unity")
                throw new InvalidOperationException("Open Tutorial in Edit Mode.");
            var fade = UnityEngine.Object.FindAnyObjectByType<ScreenFade>();
            if (fade == null)
            {
                var root = new GameObject("ScreenFade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
                Undo.RegisterCreatedObjectUndo(root, "Add shared Screen Fade");
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                root.GetComponent<Canvas>().sortingOrder = 10000;
                root.GetComponent<CanvasGroup>().alpha = 0;
                root.GetComponent<CanvasGroup>().blocksRaycasts = false;
                var image = new GameObject("FadeImage", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)image.transform; rect.SetParent(root.transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                image.GetComponent<Image>().color = Color.black;
                fade = Undo.AddComponent<ScreenFade>(root);
            }
            var doorObject = GameObject.Find("Tutorial_ExitDoor");
            if (doorObject == null) throw new InvalidOperationException("Missing Tutorial_ExitDoor model.");
            var door = doorObject.GetComponent<TutorialExitDoor>();
            if (door == null) door = Undo.AddComponent<TutorialExitDoor>(doorObject);
            var data = new SerializedObject(door);
            data.FindProperty("dialogue").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<DialogueController>();
            data.FindProperty("phoneProgress").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<TutorialPhoneCallController>();
            data.FindProperty("interactionAnchor").objectReferenceValue = GameObject.Find("PB_DoorKnob_Inside").transform;
            data.ApplyModifiedProperties();
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in new[] { "Assets/Scenes/Tutorial.unity", "Assets/Scenes/Test.unity" })
            {
                int index = scenes.FindIndex(s => s.path == path);
                if (index < 0) scenes.Add(new EditorBuildSettingsScene(path, true));
                else scenes[index].enabled = true;
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = doorObject;
        }
    }
}
