using System;
using System.Linq;
using DetectiveGame.Player;
using DetectiveGame.Tutorial;
using StarterAssets;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace DetectiveGame.EditorTools
{
    public static class TutorialWakeUpBuilder
    {
        public const string FbxPath = "Assets/Tutorial/Animations/WakeUp.fbx";
        public const string TimelinePath = "Assets/Tutorial/Timeline/TutorialWakeUp.playable";
        private const string OriginalModel = "Assets/StarterAssets/ThirdPersonController/Character/Models/Armature.fbx";
        private const string IdlePath = "Assets/StarterAssets/ThirdPersonController/Character/Animations/Stand--Idle.anim.fbx";
        private const double Duration = 5.6;

        [MenuItem("Tools/Detective Game/Tutorial WakeUp/Import Animation")]
        public static void ImportAnimation()
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport);
            var source = (ModelImporter)AssetImporter.GetAtPath(OriginalModel);
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            importer.animationType = ModelImporterAnimationType.Human;
            // This is an animation authored on the original skeleton, not a new character Avatar.
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(OriginalModel).OfType<Avatar>().First(a => a.isValid);
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0) clips = importer.clipAnimations;
            if (clips.Length == 0) throw new InvalidOperationException("WakeUp FBX contains no animation take.");
            var clip = clips[0];
            clip.name = "WakeUp";
            clip.firstFrame = 0;
            clip.lastFrame = 168;
            clip.loopTime = false;
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.heightFromFeet = false;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
            var avatar = importer.sourceAvatar;
            var animation = LoadClip(FbxPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman || !animation.isHumanMotion)
                throw new InvalidOperationException("WakeUp Avatar/clip did not import as valid Humanoid.");
            Debug.Log("WakeUp imported as Humanoid: " + animation.length + "s, Avatar valid=" + avatar.isValid);
        }

        [MenuItem("Tools/Detective Game/Tutorial WakeUp/Build Opening")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Tutorial.unity") throw new InvalidOperationException("Open Tutorial first.");
            if (GameObject.Find("TutorialWakeUp") != null || AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
                throw new InvalidOperationException("Opening already exists. Edit the existing assets; builder never replaces them.");
            var rig = GameObject.Find("Tutorial_PlayerRig");
            var player = rig.GetComponentInChildren<ThirdPersonController>(true).gameObject;
            var animator = player.GetComponent<Animator>();
            var body = player.GetComponent<FullBodyViewController>();
            if (!animator.isHuman || !animator.avatar.isValid) throw new InvalidOperationException("Player must remain valid Humanoid.");
            var mattress = GameObject.Find("PB_Mattress").GetComponent<Renderer>().bounds;
            var pillow = GameObject.Find("PB_Pillow").GetComponent<Renderer>().bounds;
            var floor = GameObject.Find("PB_Floor_3.6x4.2").GetComponent<Renderer>().bounds.max.y;

            // Container is organizational, not a motion root. Preserve the existing child hierarchy.
            Undo.RecordObject(rig.transform, "Restore upright tutorial rig container");
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rig.transform.localScale = Vector3.one;
            var host = new GameObject("TutorialWakeUp");
            Undo.RegisterCreatedObjectUndo(host, "Create tutorial opening");
            var anchors = Child(host.transform, "Anchors");
            float z = pillow.center.z + 0.93f;
            var lie = Anchor(anchors, "LieAnchor", new Vector3(mattress.center.x, floor, z), 0f);
            var sit = Anchor(anchors, "SitAnchor", new Vector3(mattress.max.x - 0.02f, floor, z), 0f);
            var stand = Anchor(anchors, "StandAnchor", new Vector3(mattress.max.x + 0.60f, floor, z), 90f);
            Undo.RecordObject(player.transform, "Set tutorial gameplay spawn");
            player.transform.SetPositionAndRotation(stand.position, stand.rotation);
            var opening = Child(host.transform, "WakeUpOpeningCamera").gameObject.AddComponent<CinemachineCamera>();
            opening.Priority = 100;
            opening.Lens = body.FirstPersonCamera.Lens;
            opening.enabled = false;
            var director = host.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.extrapolationMode = DirectorWrapMode.Hold;
            var controller = host.AddComponent<TutorialWakeUpController>();

            var canvasObject = new GameObject("WakeUpEyelids", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasObject.transform.SetParent(host.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 1f; group.blocksRaycasts = false; group.interactable = false;
            var imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = imageObject.GetComponent<Image>(); image.color = Color.black; image.raycastTarget = false;
            var main = rig.GetComponentInChildren<Camera>(true);
            var so = new SerializedObject(controller);
            Set(so, "playerRoot", player.transform); Set(so, "animator", animator);
            Set(so, "movement", player.GetComponent<ThirdPersonController>());
            Set(so, "capsule", player.GetComponent<CharacterController>());
            Set(so, "inputs", player.GetComponent<StarterAssetsInputs>());
            Set(so, "playerInput", player.GetComponent<PlayerInput>()); Set(so, "bodyView", body);
            Set(so, "director", director); Set(so, "openingCamera", opening); Set(so, "outputCamera", main);
            Set(so, "eyelids", group); Set(so, "lieAnchor", lie); Set(so, "sitAnchor", sit); Set(so, "standAnchor", stand);
            var behaviours = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(b => b != null && (b.GetType().Name == "RecallInteractor" || b.GetType().Name == "PlayerInteractor" ||
                    b.GetType().Name == "PlayerDoorInteractionController" || b.GetType().Name == "BasicRigidBodyPush")).ToArray();
            var suspend = so.FindProperty("suspendDuringOpening"); suspend.arraySize = behaviours.Length;
            for (int i = 0; i < behaviours.Length; i++) suspend.GetArrayElementAtIndex(i).objectReferenceValue = behaviours[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            ConfigureIntroductionUI(host, controller);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);
            PopulateTimeline(timeline, director, animator, body, controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = host;
        }

        [MenuItem("Tools/Detective Game/Tutorial WakeUp/Finish Pending Timeline")]
        public static void FinishPendingTimeline()
        {
            var host = GameObject.Find("TutorialWakeUp");
            var director = host.GetComponent<PlayableDirector>();
            var player = GameObject.Find("PlayerArmature");
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (director.playableAsset != null || timeline.GetOutputTracks().Any(t => t.GetClips().Any())) throw new InvalidOperationException("Timeline already populated.");
            PopulateTimeline(timeline, director, player.GetComponent<Animator>(), player.GetComponent<FullBodyViewController>(), host.GetComponent<TutorialWakeUpController>());
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void PopulateTimeline(TimelineAsset timeline, PlayableDirector director, Animator animator, FullBodyViewController body, TutorialWakeUpController controller)
        {
            timeline.editorSettings.frameRate = 30;
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = Duration;
            var animationTrack = timeline.GetOutputTracks().OfType<AnimationTrack>().FirstOrDefault() ?? timeline.CreateTrack<AnimationTrack>(null, "WakeUp — Humanoid / Idle handoff");
            animationTrack.trackOffset = TrackOffset.ApplySceneOffsets;
            var wake = animationTrack.CreateClip(LoadClip(FbxPath));
            wake.displayName = "WakeUp"; wake.start = 0; wake.duration = Duration;
            var wakeAsset = (AnimationPlayableAsset)wake.asset;
            wakeAsset.removeStartOffset = false;
            wakeAsset.applyFootIK = false;
            var idle = animationTrack.CreateClip(LoadClip(IdlePath));
            idle.displayName = "Existing Idle — settle"; idle.start = 5.25; idle.duration = 0.35;
            ((AnimationPlayableAsset)idle.asset).applyFootIK = false;
            wake.blendOutDuration = idle.blendInDuration = 0.35;
            var motionTrack = timeline.CreateTrack<TutorialWakeUpMotionTrack>(null, "Anchors + independent first-person camera");
            var motionClip = motionTrack.CreateDefaultClip(); motionClip.start = 0; motionClip.duration = Duration;
            motionClip.displayName = "WakeUp motion / eye trajectory";
            var motion = (TutorialWakeUpMotionAsset)motionClip.asset;
            motion.cameraKeys = new[] {
                Key(0f,0,new Vector3(0,.84f,-.78f),new Vector3(-88,0,0)),
                Key(.125f,0,new Vector3(0,.85f,-.76f),new Vector3(-83,2,0)),
                Key(1.8f / 5.6f,0,new Vector3(0,1.42f,.22f),new Vector3(-4,0,0)),
                Key(.50f,1,new Vector3(.28f,1.40f,0),new Vector3(4,90,0)),
                Key(.625f,1,new Vector3(.32f,1.40f,0),new Vector3(18,90,0)),
                Key(.714f,1,new Vector3(.44f,1.40f,0),new Vector3(25,90,0)),
                Key(.866f,2,new Vector3(.20f,body.EyeHeight-.06f,0),new Vector3(5,90,0)),
                Key(.938f,2,new Vector3(body.ForwardOffset,body.EyeHeight,0),new Vector3(0,90,0)),
                Key(1f,2,new Vector3(body.ForwardOffset,body.EyeHeight,0),new Vector3(0,90,0))
            };
            var audio = timeline.CreateTrack<AudioTrack>(null, "Future: breathing / bed / footsteps (empty)");
            director.playableAsset = timeline;
            director.SetGenericBinding(animationTrack, animator);
            director.SetGenericBinding(motionTrack, controller);
            EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(controller); EditorUtility.SetDirty(director);
            AssetDatabase.SaveAssets();
            Debug.Log("Tutorial WakeUp opening built. Original FBX, Prefab and Avatar untouched; hierarchy preserved.");
        }

        [MenuItem("Tools/Detective Game/Tutorial WakeUp/Configure Introduction UI")]
        public static void ConfigureIntroductionUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/Tutorial.unity")
                throw new InvalidOperationException("Open Tutorial in Edit Mode first.");
            var host = GameObject.Find("TutorialWakeUp");
            if (host == null) throw new InvalidOperationException("Build the WakeUp opening first.");
            ConfigureIntroductionUI(host, host.GetComponent<TutorialWakeUpController>());
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = host;
        }

        private static void ConfigureIntroductionUI(GameObject host, TutorialWakeUpController controller)
        {
            var canvas = host.transform.Find("WakeUpEyelids").GetComponent<Canvas>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }
            var panel = canvas.transform.Find("IntroTextPanel");
            if (panel == null)
            {
                panel = new GameObject("IntroTextPanel", typeof(RectTransform), typeof(Image)).transform;
                panel.SetParent(canvas.transform, false);
                var rect = (RectTransform)panel;
                rect.anchorMin = new Vector2(0.08f, 0f);
                rect.anchorMax = new Vector2(0.92f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 50f);
                rect.sizeDelta = new Vector2(0f, 220f);
                var background = panel.GetComponent<Image>();
                background.color = new Color(0.08f, 0.08f, 0.08f, 1f);
                background.raycastTarget = false;
                var label = new GameObject("IntroText", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(panel, false);
                var textRect = label.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(32f, 24f);
                textRect.offsetMax = new Vector2(-32f, -24f);
                var text = label.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 32;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.supportRichText = true;
                text.raycastTarget = false;
            }
            // Black and subtitles need independent alpha; never fade their shared Canvas parent.
            canvas.GetComponent<CanvasGroup>().alpha = 1f;
            var black = canvas.transform.Find("Black").gameObject;
            var blackGroup = black.GetComponent<CanvasGroup>();
            if (blackGroup == null) blackGroup = black.AddComponent<CanvasGroup>();
            blackGroup.alpha = 1f;
            blackGroup.blocksRaycasts = false;
            blackGroup.interactable = false;
            var subtitleGroup = panel.GetComponent<CanvasGroup>();
            if (subtitleGroup == null) subtitleGroup = panel.gameObject.AddComponent<CanvasGroup>();
            subtitleGroup.alpha = 1f;
            subtitleGroup.blocksRaycasts = false;
            subtitleGroup.interactable = false;
            var date = panel.Find("DateText") ?? panel.Find("IntroText");
            date.name = "DateText";
            var story = panel.Find("StoryText");
            if (story == null)
            {
                story = UnityEngine.Object.Instantiate(date.gameObject, panel, false).transform;
                story.name = "StoryText";
            }
            date.gameObject.SetActive(true);
            story.gameObject.SetActive(false);
            var so = new SerializedObject(controller);
            Set(so, "eyelids", blackGroup);
            Set(so, "introPanel", panel.gameObject);
            Set(so, "subtitleGroup", subtitleGroup);
            Set(so, "dateText", date.GetComponent<Text>());
            Set(so, "storyText", story.GetComponent<Text>());
            date.GetComponent<Text>().text = so.FindProperty("dateSubtitle").stringValue;
            story.GetComponent<Text>().text = so.FindProperty("storySubtitle").stringValue;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static WakeUpCameraKey Key(float time, int anchor, Vector3 offset, Vector3 rotation)
            => new WakeUpCameraKey { time = time, anchor = anchor, offset = offset, rotation = rotation };
        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }
        private static Transform Anchor(Transform parent, string name, Vector3 point, float yaw)
        {
            var t = Child(parent, name); t.SetPositionAndRotation(point, Quaternion.Euler(0,yaw,0)); return t;
        }
        private static void Set(SerializedObject so, string name, UnityEngine.Object value)
            => so.FindProperty(name).objectReferenceValue = value;
        private static AnimationClip LoadClip(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    }
}

