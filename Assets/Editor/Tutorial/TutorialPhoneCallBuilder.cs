using System;
using DetectiveGame.Dialogue;
using DetectiveGame.Interaction;
using DetectiveGame.Investigation;
using DetectiveGame.Tutorial;
using Ink.UnityIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DetectiveGame.EditorTools
{
    public static class TutorialPhoneCallBuilder
    {
        public const string InkPath = "Assets/Dialogue/TutorialTelephoneCall.ink";
        [MenuItem("Tools/Detective Game/Configure Tutorial Phone Story")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/Scenes/Tutorial.unity")
                throw new InvalidOperationException("Open Tutorial in Edit Mode first.");
            var phone = GameObject.Find("telephone");
            if (phone == null || phone.GetComponent<TelephoneDialController>() == null)
                throw new InvalidOperationException("Configure the existing telephone first.");
            var ink = AssetDatabase.LoadAssetAtPath<InkFile>(InkPath);
            if (ink == null || !ink.isCompiled) throw new InvalidOperationException("Compile the phone Ink first.");
            var dialogue = UnityEngine.Object.FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
            if (dialogue == null)
            {
                var root = new GameObject("TutorialDialogue");
                Undo.RegisterCreatedObjectUndo(root, "Add shared Tutorial Dialogue");
                dialogue = Undo.AddComponent<DialogueController>(root);
                Set(dialogue, "investigationDatabase", AssetDatabase.LoadAssetAtPath<InvestigationDatabase>(
                    "Assets/GameData/Investigation/PrototypeInvestigationDatabase.asset"));
            }
            var visual = UnityEngine.Object.FindAnyObjectByType<PhoneCallVisual>(FindObjectsInactive.Include);
            if (visual == null)
            {
                var root = new GameObject("PhoneCallVisual", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(CanvasScaler));
                Undo.RegisterCreatedObjectUndo(root, "Add replaceable Phone Call Visual");
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                root.GetComponent<Canvas>().sortingOrder = 4600;
                root.GetComponent<CanvasGroup>().alpha = 0;
                root.GetComponent<CanvasGroup>().blocksRaycasts = false;
                var overlay = new GameObject("BlackOverlay_ReplaceWithSilhouette", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)overlay.transform;
                rect.SetParent(root.transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                overlay.GetComponent<Image>().color = Color.black;
                overlay.GetComponent<Image>().raycastTarget = false;
                visual = Undo.AddComponent<PhoneCallVisual>(root);
            }
            var story = phone.GetComponent<TutorialPhoneCallController>();
            if (story == null) story = Undo.AddComponent<TutorialPhoneCallController>(phone);
            Set(story, "telephone", phone.GetComponent<TelephoneDialController>());
            Set(story, "dialogue", dialogue);
            Set(story, "phoneDialogue", ink);
            Set(story, "phoneCallVisual", visual);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = phone;
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
