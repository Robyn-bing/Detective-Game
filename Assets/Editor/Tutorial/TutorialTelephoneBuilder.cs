using System;
using DetectiveGame.Interaction;
using DetectiveGame.Recall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DetectiveGame.EditorTools
{
    public static class TutorialTelephoneBuilder
    {
        public const string ProfilePath = "Assets/Tutorial/Telephone/TutorialTelephoneDialProfile.asset";
        public const string SoundPath = "Assets/Tutorial/Telephone/TelephoneConnectedPrototype.wav";

        [MenuItem("Tools/Detective Game/Configure Tutorial Telephone Dialing")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Configure in Edit Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Tutorial.unity") throw new InvalidOperationException("Open Tutorial first.");
            GameObject telephone = GameObject.Find("telephone");
            GameObject player = GameObject.Find("PlayerArmature");
            if (telephone == null || player == null) throw new InvalidOperationException("Missing existing telephone / PlayerArmature.");
            TelephoneDialRecall recall = telephone.GetComponent<TelephoneDialRecall>();
            if (recall == null || recall.DialWheel == null) throw new InvalidOperationException("Missing existing recall dial authoring.");
            if (!AssetDatabase.IsValidFolder("Assets/Tutorial/Telephone")) AssetDatabase.CreateFolder("Assets/Tutorial", "Telephone");
            TelephoneDialProfile profile = AssetDatabase.LoadAssetAtPath<TelephoneDialProfile>(ProfilePath);
            if (profile == null)
            {
                float[] angles = recall.DigitAngles;
                profile = ScriptableObject.CreateInstance<TelephoneDialProfile>();
                for (int i = 0; i < 10; i++) profile.Digits[i].targetAngle = angles[i];
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            SetReference(recall, "angleProfile", profile);

            Transform anchor = telephone.transform.Find("DialViewAnchor");
            if (anchor == null)
            {
                var anchorObject = new GameObject("DialViewAnchor");
                Undo.RegisterCreatedObjectUndo(anchorObject, "Add Telephone DialViewAnchor");
                anchor = anchorObject.transform;
                anchor.SetParent(telephone.transform, false);
                anchor.SetPositionAndRotation(recall.DialWheel.position + recall.DialWheel.forward * 0.6f,
                    Quaternion.LookRotation(-recall.DialWheel.forward, recall.DialWheel.up));
            }
            RotaryDialController rotary = telephone.GetComponent<RotaryDialController>();
            if (rotary == null) rotary = Undo.AddComponent<RotaryDialController>(telephone);
            SetReference(rotary, "dialSettings", recall);
            TelephoneDialController controller = telephone.GetComponent<TelephoneDialController>();
            if (controller == null) controller = Undo.AddComponent<TelephoneDialController>(telephone);
            SetReference(controller, "dialSettings", recall);
            SetReference(controller, "rotaryDial", rotary);
            SetReference(controller, "dialViewAnchor", anchor);
            SetReference(controller, "interactionAnchor", recall.DialWheel);

            TelephoneDialUI ui = UnityEngine.Object.FindAnyObjectByType<TelephoneDialUI>(FindObjectsInactive.Include);
            if (ui == null) ui = CreateUI();
            SetReference(controller, "dialUI", ui);
            AudioSource audio = telephone.GetComponent<AudioSource>();
            if (audio == null)
            {
                audio = Undo.AddComponent<AudioSource>(telephone);
                audio.playOnAwake = false; audio.spatialBlend = 0.3f; audio.volume = 0.25f;
            }
            SetReference(controller, "telephoneAudio", audio);
            SetReference(controller, "connectedSound", AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath));

            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
            if (interactor == null) interactor = Undo.AddComponent<PlayerInteractor>(player);
            SetReference(interactor, "viewCamera", Camera.main);
            SetReference(interactor, "recallSession", UnityEngine.Object.FindAnyObjectByType<RecallSessionController>());
            if (player.GetComponent<InteractionPromptUI>() == null)
                Undo.AddComponent<InteractionPromptUI>(player);
            // Existing recall preview / player hierarchy and camera priorities are unchanged.
            TelephoneDialRecallEditor.Bake(recall);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = telephone;
        }

        private static TelephoneDialUI CreateUI()
        {
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }, 32);
            var canvasObject = new GameObject("TelephoneDialUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TelephoneDialUI));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Telephone Dial UI");
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4800;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            RectTransform panel = Rect("DialPanel", canvasObject.transform, new Vector2(670, 150), new Vector2(0, 65));
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f); panel.pivot = new Vector2(0.5f, 0f);
            Image background = panel.gameObject.AddComponent<Image>(); background.color = new Color(0.025f, 0.025f, 0.025f, 0.78f); background.raycastTarget = false;
            TelephoneDialUI ui = canvasObject.GetComponent<TelephoneDialUI>();
            var data = new SerializedObject(ui);
            data.FindProperty("panel").objectReferenceValue = panel.gameObject;
            SerializedProperty slots = data.FindProperty("slots"); slots.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                RectTransform root = Rect("DialDigitSlot_" + i, panel, new Vector2(80, 85), new Vector2((i - 2.5f) * 90f, 24f));
                Text number = Label("Digit", root, font, 48, new Vector2(80, 68), new Vector2(0, 5), "");
                RectTransform underline = Rect("Underline", root, new Vector2(58, 3), new Vector2(0, -34));
                Image line = underline.gameObject.AddComponent<Image>(); line.color = new Color(0.92f, 0.88f, 0.78f); line.raycastTarget = false;
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("root").objectReferenceValue = root;
                slot.FindPropertyRelative("digit").objectReferenceValue = number;
                slot.FindPropertyRelative("underline").objectReferenceValue = line;
            }
            Text tip = Label("Instructions", panel, font, 18, new Vector2(650, 36), new Vector2(0, -50), "按住转盘顺时针拖动，松开拨号   ·   Backspace 删除   ·   Esc 返回");
            data.FindProperty("instruction").objectReferenceValue = tip;
            data.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }

        private static Text Label(string name, Transform parent, Font font, int fontSize, Vector2 size, Vector2 position, string value)
        {
            Text text = Rect(name, parent, size, position).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
            text.text = value; text.color = new Color(0.92f, 0.88f, 0.78f); text.raycastTarget = false;
            return text;
        }

        private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException(target.name + " has no " + propertyName);
            property.objectReferenceValue = value; data.ApplyModifiedProperties(); EditorUtility.SetDirty(target);
        }
    }
}
