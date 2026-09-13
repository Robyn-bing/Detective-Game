using System;
using DetectiveGame.Player;
using StarterAssets;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#pragma warning disable CS0618 // Test.unity still uses CinemachineVirtualCamera; keep its proven setup for parity.

namespace DetectiveGame.EditorTools
{
    /// <summary>
    /// Creates the Clara bedroom tutorial whitebox from the approved 3.6 x 4.2 x 2.8 metre plan.
    /// All visible blockout geometry is editable ProBuilder geometry.
    /// </summary>
    public static class TutorialRoomWhiteboxBuilder
    {
        public const string TutorialScenePath = "Assets/Scenes/Tutorial.unity";
        public const string GeneratedRootName = "ClaraRoom_Whitebox";
        public const string PlayerRigRootName = "Tutorial_PlayerRig";

        private const string TestScenePath = "Assets/Scenes/Test.unity";
        private const string MaterialFolder = "Assets/Tutorial/Materials";

        private static Material wallMaterial;
        private static Material floorMaterial;
        private static Material darkWoodMaterial;
        private static Material mediumWoodMaterial;
        private static Material fabricGreenMaterial;
        private static Material fabricRedMaterial;
        private static Material creamMaterial;
        private static Material blackMaterial;
        private static Material brassMaterial;
        private static Material glassMaterial;
        private static Material paperMaterial;

        [MenuItem("Tools/Detective Game/Build Tutorial Clara Room Whitebox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before rebuilding the tutorial room.");
                return;
            }

            Scene destinationScene = SceneManager.GetActiveScene();
            if (!destinationScene.IsValid() || !destinationScene.isLoaded)
            {
                Debug.LogError("No loaded scene is available for the tutorial whitebox.");
                return;
            }

            EnsureMaterials();
            DeleteGeneratedRoot(destinationScene, GeneratedRootName);
            DeleteGeneratedRoot(destinationScene, PlayerRigRootName);

            GameObject roomRoot = new GameObject(GeneratedRootName);
            SceneManager.MoveGameObjectToScene(roomRoot, destinationScene);
            BuildArchitecture(roomRoot.transform);
            BuildFurniture(roomRoot.transform);
            BuildStoryProps(roomRoot.transform);
            BuildLighting(roomRoot.transform, destinationScene);
            BuildPlayerRig(destinationScene);

            EditorSceneManager.MarkSceneDirty(destinationScene);
            if (!EditorSceneManager.SaveScene(destinationScene, TutorialScenePath))
                throw new InvalidOperationException("Unity could not save the tutorial scene to " + TutorialScenePath);

            Selection.activeGameObject = roomRoot;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0f, 1.1f, 0f), Quaternion.Euler(28f, 138f, 0f), 6.2f);

            Debug.Log("Tutorial Clara room whitebox built and saved: " + TutorialScenePath);
        }

        [MenuItem("Tools/Detective Game/Validate Tutorial Clara Room Whitebox")]
        public static void ValidateActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject room = FindRoot(scene, GeneratedRootName);
            GameObject rig = FindRoot(scene, PlayerRigRootName);
            int proBuilderCount = room != null ? room.GetComponentsInChildren<ProBuilderMesh>(true).Length : 0;
            int colliderCount = room != null ? room.GetComponentsInChildren<Collider>(true).Length : 0;
            FullBodyViewController fullBody = rig != null ? rig.GetComponentInChildren<FullBodyViewController>(true) : null;
            Camera mainCamera = rig != null ? rig.GetComponentInChildren<Camera>(true) : null;

            bool storyPropsPresent = FindChild(room, "Tutorial_Note") != null &&
                                     FindChild(room, "Tutorial_Telephone") != null &&
                                     FindChild(room, "Tutorial_AlarmClock") != null &&
                                     FindChild(room, "Tutorial_ExitDoor") != null;
            bool valid = room != null && rig != null && proBuilderCount >= 35 && colliderCount >= 20 &&
                         storyPropsPresent && fullBody != null && fullBody.UseFirstPerson && mainCamera != null;

            string report = string.Format(
                "Tutorial validation: valid={0}, ProBuilder meshes={1}, colliders={2}, story props={3}, full-body first person={4}, main camera={5}",
                valid, proBuilderCount, colliderCount, storyPropsPresent, fullBody != null && fullBody.UseFirstPerson, mainCamera != null);
            if (valid) Debug.Log(report);
            else Debug.LogError(report);
        }

        private static void BuildArchitecture(Transform root)
        {
            Transform architecture = CreateGroup("01_Architecture", root);

            CreateCube("PB_Floor_3.6x4.2", architecture, new Vector3(0f, -0.06f, 0f), new Vector3(3.6f, 0.12f, 4.2f), floorMaterial);
            CreateCube("PB_Ceiling", architecture, new Vector3(0f, 2.86f, 0f), new Vector3(3.84f, 0.12f, 4.44f), wallMaterial);
            CreateCube("PB_WestWall", architecture, new Vector3(-1.86f, 1.4f, 0f), new Vector3(0.12f, 2.8f, 4.32f), wallMaterial);
            CreateCube("PB_SouthWall", architecture, new Vector3(0f, 1.4f, -2.16f), new Vector3(3.84f, 2.8f, 0.12f), wallMaterial);

            // North wall is split around the 1.5 m wide window shown in the layout.
            CreateCube("PB_NorthWall_Left", architecture, new Vector3(-1f, 1.4f, 2.16f), new Vector3(1.6f, 2.8f, 0.12f), wallMaterial);
            CreateCube("PB_NorthWall_Right", architecture, new Vector3(1.55f, 1.4f, 2.16f), new Vector3(0.5f, 2.8f, 0.12f), wallMaterial);
            CreateCube("PB_NorthWall_WindowSill", architecture, new Vector3(0.55f, 0.475f, 2.16f), new Vector3(1.5f, 0.95f, 0.12f), wallMaterial);
            CreateCube("PB_NorthWall_WindowHeader", architecture, new Vector3(0.55f, 2.575f, 2.16f), new Vector3(1.5f, 0.45f, 0.12f), wallMaterial);

            // East wall is split around a 0.9 x 2.1 m doorway near the south-east corner.
            CreateCube("PB_EastWall_North", architecture, new Vector3(1.86f, 1.4f, 0.55f), new Vector3(0.12f, 2.8f, 3.1f), wallMaterial);
            CreateCube("PB_EastWall_South", architecture, new Vector3(1.86f, 1.4f, -2.0f), new Vector3(0.12f, 2.8f, 0.2f), wallMaterial);
            CreateCube("PB_EastWall_DoorHeader", architecture, new Vector3(1.86f, 2.45f, -1.45f), new Vector3(0.12f, 0.7f, 0.9f), wallMaterial);

            Transform window = CreateGroup("Window_North", architecture);
            CreateCube("PB_WindowGlass", window, new Vector3(0.55f, 1.65f, 2.175f), new Vector3(1.42f, 1.32f, 0.035f), glassMaterial, false);
            CreateCube("PB_WindowFrame_Left", window, new Vector3(-0.18f, 1.65f, 2.12f), new Vector3(0.07f, 1.45f, 0.1f), creamMaterial, false);
            CreateCube("PB_WindowFrame_Right", window, new Vector3(1.28f, 1.65f, 2.12f), new Vector3(0.07f, 1.45f, 0.1f), creamMaterial, false);
            CreateCube("PB_WindowFrame_Top", window, new Vector3(0.55f, 2.35f, 2.12f), new Vector3(1.53f, 0.07f, 0.1f), creamMaterial, false);
            CreateCube("PB_WindowFrame_Bottom", window, new Vector3(0.55f, 0.95f, 2.12f), new Vector3(1.53f, 0.07f, 0.1f), creamMaterial, false);
            CreateCube("PB_WindowFrame_MiddleV", window, new Vector3(0.55f, 1.65f, 2.10f), new Vector3(0.05f, 1.36f, 0.08f), creamMaterial, false);
            CreateCube("PB_WindowFrame_MiddleH", window, new Vector3(0.55f, 1.65f, 2.10f), new Vector3(1.42f, 0.05f, 0.08f), creamMaterial, false);
            CreateCube("PB_Curtain_Left", window, new Vector3(-0.29f, 1.63f, 2.02f), new Vector3(0.20f, 1.62f, 0.08f), fabricGreenMaterial, false);
            CreateCube("PB_Curtain_Right", window, new Vector3(1.39f, 1.63f, 2.02f), new Vector3(0.20f, 1.62f, 0.08f), fabricGreenMaterial, false);

            Transform door = CreateGroup("Tutorial_ExitDoor", architecture);
            door.localPosition = new Vector3(1.79f, 0f, -1.90f);
            CreateCube("PB_DoorLeaf", door, new Vector3(-0.015f, 1.05f, 0.43f), new Vector3(0.08f, 2.05f, 0.86f), mediumWoodMaterial);
            CreateCube("PB_DoorKnob_Inside", door, new Vector3(-0.08f, 1.02f, 0.73f), new Vector3(0.08f, 0.09f, 0.09f), brassMaterial, false);
            CreateCube("PB_DoorFrame_Hinge", architecture, new Vector3(1.77f, 1.08f, -1.92f), new Vector3(0.16f, 2.16f, 0.10f), darkWoodMaterial);
            CreateCube("PB_DoorFrame_Latch", architecture, new Vector3(1.77f, 1.08f, -0.98f), new Vector3(0.16f, 2.16f, 0.10f), darkWoodMaterial);
            CreateCube("PB_DoorFrame_Top", architecture, new Vector3(1.77f, 2.12f, -1.45f), new Vector3(0.16f, 0.10f, 1.04f), darkWoodMaterial);
        }

        private static void BuildFurniture(Transform root)
        {
            Transform furniture = CreateGroup("02_Furniture", root);

            Transform bed = CreateGroup("Bed_1.05x2.05", furniture);
            CreateCube("PB_BedFrame", bed, new Vector3(-1.18f, 0.23f, -0.62f), new Vector3(1.05f, 0.28f, 2.05f), darkWoodMaterial);
            CreateCube("PB_Mattress", bed, new Vector3(-1.18f, 0.46f, -0.62f), new Vector3(0.95f, 0.22f, 1.92f), creamMaterial);
            CreateCube("PB_Blanket", bed, new Vector3(-1.18f, 0.59f, -0.88f), new Vector3(0.97f, 0.06f, 1.25f), fabricGreenMaterial, false);
            CreateCube("PB_BedThrow", bed, new Vector3(-1.18f, 0.64f, -1.41f), new Vector3(0.99f, 0.07f, 0.30f), fabricRedMaterial, false);
            CreateCube("PB_Pillow", bed, new Vector3(-1.18f, 0.63f, 0.13f), new Vector3(0.72f, 0.16f, 0.38f), creamMaterial, false);
            CreateCube("PB_Headboard", bed, new Vector3(-1.18f, 0.83f, 0.43f), new Vector3(1.05f, 0.72f, 0.08f), darkWoodMaterial);

            Transform wardrobe = CreateGroup("Wardrobe_0.95x0.60x2.20", furniture);
            CreateCube("PB_WardrobeBody", wardrobe, new Vector3(-1.25f, 1.10f, 1.72f), new Vector3(0.95f, 2.20f, 0.60f), darkWoodMaterial);
            CreateCube("PB_WardrobeDoor", wardrobe, new Vector3(-1.25f, 1.12f, 1.405f), new Vector3(0.78f, 1.86f, 0.04f), mediumWoodMaterial, false);
            CreateCube("PB_WardrobeTop", wardrobe, new Vector3(-1.25f, 2.23f, 1.72f), new Vector3(1.05f, 0.10f, 0.68f), mediumWoodMaterial);
            CreateCube("PB_WardrobeKnob", wardrobe, new Vector3(-0.96f, 1.12f, 1.36f), new Vector3(0.07f, 0.07f, 0.07f), brassMaterial, false);

            Transform desk = CreateGroup("Desk_1.35x0.62x0.76", furniture);
            CreateCube("PB_DeskTop", desk, new Vector3(0.55f, 0.76f, 1.70f), new Vector3(1.35f, 0.10f, 0.62f), mediumWoodMaterial);
            CreateCube("PB_DeskApron", desk, new Vector3(0.55f, 0.65f, 1.88f), new Vector3(1.18f, 0.16f, 0.16f), darkWoodMaterial);
            CreateCube("PB_DeskLeg_FL", desk, new Vector3(-0.02f, 0.36f, 1.48f), new Vector3(0.09f, 0.72f, 0.09f), darkWoodMaterial);
            CreateCube("PB_DeskLeg_FR", desk, new Vector3(1.12f, 0.36f, 1.48f), new Vector3(0.09f, 0.72f, 0.09f), darkWoodMaterial);
            CreateCube("PB_DeskLeg_BL", desk, new Vector3(-0.02f, 0.36f, 1.92f), new Vector3(0.09f, 0.72f, 0.09f), darkWoodMaterial);
            CreateCube("PB_DeskLeg_BR", desk, new Vector3(1.12f, 0.36f, 1.92f), new Vector3(0.09f, 0.72f, 0.09f), darkWoodMaterial);

            Transform chair = CreateGroup("Chair_FacingDesk", furniture);
            CreateCube("PB_ChairSeat", chair, new Vector3(0.55f, 0.45f, 0.94f), new Vector3(0.48f, 0.09f, 0.46f), darkWoodMaterial);
            CreateCube("PB_ChairBack", chair, new Vector3(0.55f, 0.85f, 0.70f), new Vector3(0.48f, 0.72f, 0.08f), mediumWoodMaterial);
            CreateCube("PB_ChairLeg_FL", chair, new Vector3(0.37f, 0.22f, 1.10f), new Vector3(0.07f, 0.44f, 0.07f), darkWoodMaterial);
            CreateCube("PB_ChairLeg_FR", chair, new Vector3(0.73f, 0.22f, 1.10f), new Vector3(0.07f, 0.44f, 0.07f), darkWoodMaterial);
            CreateCube("PB_ChairLeg_BL", chair, new Vector3(0.37f, 0.22f, 0.76f), new Vector3(0.07f, 0.44f, 0.07f), darkWoodMaterial);
            CreateCube("PB_ChairLeg_BR", chair, new Vector3(0.73f, 0.22f, 0.76f), new Vector3(0.07f, 0.44f, 0.07f), darkWoodMaterial);

            CreateCube("PB_CentralRug", furniture, new Vector3(0.48f, 0.018f, -0.43f), new Vector3(1.55f, 0.036f, 2.05f), fabricRedMaterial, false);
        }

        private static void BuildStoryProps(Transform root)
        {
            Transform props = CreateGroup("03_TutorialStoryProps", root);

            Transform note = CreateGroup("Tutorial_Note", props);
            note.localPosition = new Vector3(0.60f, 0.825f, 1.54f);
            note.localEulerAngles = new Vector3(0f, -8f, 0f);
            CreateCube("PB_NotePaper", note, Vector3.zero, new Vector3(0.28f, 0.012f, 0.20f), paperMaterial);

            Transform telephone = CreateGroup("Tutorial_Telephone", props);
            telephone.localPosition = new Vector3(0.22f, 0.84f, 1.68f);
            CreateCube("PB_TelephoneBase", telephone, Vector3.zero, new Vector3(0.28f, 0.09f, 0.22f), blackMaterial);
            CreateCube("PB_TelephoneHandset", telephone, new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.07f, 0.08f), blackMaterial);

            Transform clock = CreateGroup("Tutorial_AlarmClock", props);
            clock.localPosition = new Vector3(0.99f, 0.90f, 1.71f);
            CreateCube("PB_AlarmClockBody", clock, Vector3.zero, new Vector3(0.18f, 0.20f, 0.09f), brassMaterial);
            CreateCube("PB_AlarmClockFace", clock, new Vector3(0f, 0f, -0.052f), new Vector3(0.14f, 0.14f, 0.015f), creamMaterial, false);

            Transform lamp = CreateGroup("Desk_Lamp", props);
            lamp.localPosition = new Vector3(-0.02f, 0.84f, 1.74f);
            CreateCube("PB_LampBase", lamp, Vector3.zero, new Vector3(0.16f, 0.04f, 0.16f), brassMaterial);
            CreateCube("PB_LampStem", lamp, new Vector3(0f, 0.18f, 0f), new Vector3(0.035f, 0.34f, 0.035f), brassMaterial, false);
            CreateCube("PB_LampShade", lamp, new Vector3(0f, 0.38f, 0f), new Vector3(0.26f, 0.22f, 0.26f), creamMaterial, false);

            Transform book = CreateGroup("Desk_Book", props);
            book.localPosition = new Vector3(0.76f, 0.84f, 1.79f);
            book.localEulerAngles = new Vector3(0f, 12f, 0f);
            CreateCube("PB_DeskBook", book, Vector3.zero, new Vector3(0.22f, 0.045f, 0.16f), fabricRedMaterial);
        }

        private static void BuildLighting(Transform roomRoot, Scene destinationScene)
        {
            // Remove only the untouched default directional light in the new scene.
            GameObject defaultLight = FindRoot(destinationScene, "Directional Light");
            if (defaultLight != null && defaultLight.GetComponents<Component>().Length <= 2)
                Object.DestroyImmediate(defaultLight);

            Transform lighting = CreateGroup("04_Lighting", roomRoot);
            GameObject ceilingLight = new GameObject("Room_WarmFillLight");
            ceilingLight.transform.SetParent(lighting, false);
            ceilingLight.transform.localPosition = new Vector3(0.25f, 2.46f, -0.1f);
            Light light = ceilingLight.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.80f, 0.60f);
            light.intensity = 4.2f;
            light.range = 7f;
            light.shadows = LightShadows.Soft;

            GameObject windowFill = new GameObject("Window_CoolFillLight");
            windowFill.transform.SetParent(lighting, false);
            windowFill.transform.localPosition = new Vector3(0.55f, 1.8f, 1.85f);
            windowFill.transform.localRotation = Quaternion.Euler(45f, 180f, 0f);
            Light fill = windowFill.AddComponent<Light>();
            fill.type = LightType.Spot;
            fill.color = new Color(0.72f, 0.86f, 1f);
            fill.intensity = 2.2f;
            fill.range = 5f;
            fill.spotAngle = 105f;
            fill.shadows = LightShadows.None;
        }

        private static void BuildPlayerRig(Scene destinationScene)
        {
            GameObject oldDefaultCamera = FindRoot(destinationScene, "Main Camera");
            if (oldDefaultCamera != null)
                Object.DestroyImmediate(oldDefaultCamera);

            Scene testScene = SceneManager.GetSceneByPath(TestScenePath);
            bool openedTestScene = !testScene.IsValid() || !testScene.isLoaded;
            if (openedTestScene)
                testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive);

            try
            {
                GameObject sourcePlayer = FindRoot(testScene, "PlayerArmature");
                GameObject sourceMainCamera = FindRoot(testScene, "MainCamera");
                GameObject sourceThirdCamera = FindRoot(testScene, "PlayerFollowCamera");
                GameObject sourceFirstCamera = FindRoot(testScene, "PlayerFirstPersonCamera");
                if (sourcePlayer == null || sourceMainCamera == null || sourceThirdCamera == null || sourceFirstCamera == null)
                    throw new InvalidOperationException("The tested player camera rig could not be found in " + TestScenePath);

                GameObject rigRoot = new GameObject(PlayerRigRootName);
                SceneManager.MoveGameObjectToScene(rigRoot, destinationScene);

                GameObject player = Object.Instantiate(sourcePlayer);
                GameObject mainCamera = Object.Instantiate(sourceMainCamera);
                GameObject thirdCameraObject = Object.Instantiate(sourceThirdCamera);
                GameObject firstCameraObject = Object.Instantiate(sourceFirstCamera);
                player.name = "PlayerArmature";
                mainCamera.name = "MainCamera";
                thirdCameraObject.name = "PlayerFollowCamera";
                firstCameraObject.name = "PlayerFirstPersonCamera";

                MoveAndParent(player, rigRoot.transform, destinationScene);
                MoveAndParent(mainCamera, rigRoot.transform, destinationScene);
                MoveAndParent(thirdCameraObject, rigRoot.transform, destinationScene);
                MoveAndParent(firstCameraObject, rigRoot.transform, destinationScene);

                RemoveComponentByName(player, "DetectiveGame.Recall.RecallInteractor");
                RemoveComponentByName(player, "DetectiveGame.Interaction.PlayerInteractor");
                RemoveComponentByName(player, "DetectiveGame.Player.PlayerDoorInteractionController");

                player.transform.SetPositionAndRotation(new Vector3(0.55f, 0f, -1.15f), Quaternion.identity);
                Transform cameraTarget = FindChild(player, "PlayerCameraRoot");
                ThirdPersonController movement = player.GetComponent<ThirdPersonController>();
                if (movement != null) movement.CinemachineCameraTarget = cameraTarget != null ? cameraTarget.gameObject : null;

                CinemachineVirtualCamera thirdCamera = thirdCameraObject.GetComponent<CinemachineVirtualCamera>();
                CinemachineCamera firstCamera = firstCameraObject.GetComponent<CinemachineCamera>();
                Camera unityCamera = mainCamera.GetComponent<Camera>();
                FullBodyViewController fullBody = player.GetComponent<FullBodyViewController>();
                if (thirdCamera == null || firstCamera == null || unityCamera == null || fullBody == null || cameraTarget == null)
                    throw new InvalidOperationException("The copied player rig is missing required camera or full-body components.");

                thirdCamera.Follow = cameraTarget;
                thirdCamera.LookAt = cameraTarget;
                ConfigureFullBodyController(fullBody, unityCamera, thirdCamera, firstCamera);
                mainCamera.tag = "MainCamera";
                mainCamera.transform.SetPositionAndRotation(new Vector3(0.55f, 1.55f, -1.1f), Quaternion.identity);
                firstCameraObject.transform.SetPositionAndRotation(new Vector3(0.55f, 1.50f, -0.93f), Quaternion.identity);
            }
            finally
            {
                if (openedTestScene && testScene.IsValid() && testScene.isLoaded)
                    EditorSceneManager.CloseScene(testScene, true);
                SceneManager.SetActiveScene(destinationScene);
            }
        }

        private static void ConfigureFullBodyController(FullBodyViewController controller, Camera mainCamera,
            CinemachineVirtualCameraBase thirdCamera, CinemachineCamera firstCamera)
        {
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("useFirstPerson").boolValue = true;
            serialized.FindProperty("mainCamera").objectReferenceValue = mainCamera;
            serialized.FindProperty("thirdPersonCamera").objectReferenceValue = thirdCamera;
            serialized.FindProperty("firstPersonCamera").objectReferenceValue = firstCamera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            controller.UseFirstPerson = true;
        }

        private static void MoveAndParent(GameObject gameObject, Transform parent, Scene destinationScene)
        {
            gameObject.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(gameObject, destinationScene);
            gameObject.transform.SetParent(parent, true);
        }

        private static void RemoveComponentByName(GameObject target, string fullTypeName)
        {
            Component[] components = target.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && component.GetType().FullName == fullTypeName)
                    Object.DestroyImmediate(component);
            }
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 size,
            Material material, bool addCollider = true)
        {
            ProBuilderMesh mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            GameObject gameObject = mesh.gameObject;
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            mesh.ToMesh();
            mesh.Refresh();

            if (addCollider && gameObject.GetComponent<Collider>() == null)
                gameObject.AddComponent<BoxCollider>();
            GameObjectUtility.SetStaticEditorFlags(gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
            return gameObject;
        }

        private static void DeleteGeneratedRoot(Scene scene, string rootName)
        {
            GameObject root = FindRoot(scene, rootName);
            if (root != null) Object.DestroyImmediate(root);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == name) return roots[i];
            return null;
        }

        private static Transform FindChild(GameObject root, string name)
        {
            if (root == null) return null;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
                if (children[i].name == name) return children[i];
            return null;
        }

        private static void EnsureMaterials()
        {
            EnsureFolder("Assets/Tutorial");
            EnsureFolder(MaterialFolder);
            wallMaterial = GetOrCreateMaterial("M_Tutorial_Wall", new Color(0.76f, 0.70f, 0.58f));
            floorMaterial = GetOrCreateMaterial("M_Tutorial_Floor", new Color(0.22f, 0.10f, 0.055f));
            darkWoodMaterial = GetOrCreateMaterial("M_Tutorial_DarkWood", new Color(0.18f, 0.07f, 0.035f));
            mediumWoodMaterial = GetOrCreateMaterial("M_Tutorial_MediumWood", new Color(0.36f, 0.14f, 0.07f));
            fabricGreenMaterial = GetOrCreateMaterial("M_Tutorial_GreenFabric", new Color(0.08f, 0.19f, 0.14f));
            fabricRedMaterial = GetOrCreateMaterial("M_Tutorial_RedFabric", new Color(0.38f, 0.06f, 0.055f));
            creamMaterial = GetOrCreateMaterial("M_Tutorial_Cream", new Color(0.87f, 0.82f, 0.70f));
            blackMaterial = GetOrCreateMaterial("M_Tutorial_Black", new Color(0.025f, 0.025f, 0.025f));
            brassMaterial = GetOrCreateMaterial("M_Tutorial_Brass", new Color(0.58f, 0.35f, 0.08f), 0.65f, 0.40f);
            glassMaterial = GetOrCreateMaterial("M_Tutorial_WindowBlue", new Color(0.26f, 0.55f, 0.75f), 0.15f, 0.10f);
            paperMaterial = GetOrCreateMaterial("M_Tutorial_NotePaper", new Color(0.94f, 0.84f, 0.48f));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static Material GetOrCreateMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.25f)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
#pragma warning restore CS0618
