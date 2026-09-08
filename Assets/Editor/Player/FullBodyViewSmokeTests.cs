using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DetectiveGame.Player;
using StarterAssets;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace DetectiveGame.EditorTools
{
    /// <summary>Run in the Test scene during Play Mode. Drives the same input values consumed by Starter Assets.</summary>
    public static class FullBodyViewSmokeTests
    {
        [Serializable]
        public sealed class Report
        {
            public bool complete;
            public int passed;
            public int failed;
            public List<string> checks = new List<string>();
        }

        public static Report Latest { get; private set; }
        private static bool running;

        [MenuItem("Tools/Detective Game/Run Full Body View Smoke Tests (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || running)
                throw new InvalidOperationException("Enter Play Mode in Test and wait for any current test to finish.");
            var player = GameObject.Find("PlayerArmature");
            if (player == null || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Test")
                throw new InvalidOperationException("The Test scene with PlayerArmature must be active.");
            Latest = new Report();
            running = true;
            player.GetComponent<ThirdPersonController>().StartCoroutine(Exercise(player));
        }

        private static IEnumerator Exercise(GameObject player)
        {
            var view = player.GetComponent<FullBodyViewController>();
            var move = player.GetComponent<ThirdPersonController>();
            var input = player.GetComponent<StarterAssetsInputs>();
            var device = player.GetComponent<PlayerInput>();
            var controller = player.GetComponent<CharacterController>();
            var camera = Camera.main;
            var brain = camera.GetComponent<CinemachineBrain>();
            var renderer = player.transform.Find("Geometry/Armature_Mesh").GetComponent<SkinnedMeshRenderer>();
            var startPosition = player.transform.position;
            var startRotation = player.transform.rotation;
            bool originalMode = view.UseFirstPerson;
            bool originalJump = move.CanJump;
            bool originalInput = device.inputIsActive;
            var originalCursor = Cursor.lockState;
            bool originalRunInBackground = Application.runInBackground;
            device.DeactivateInput();
            input.MoveInput(Vector2.zero);
            input.LookInput(Vector2.zero);
            input.SprintInput(false);
            input.JumpInput(false);
            Application.runInBackground = true;

            try
            {
                view.UseFirstPerson = true;
                yield return new WaitForSeconds(0.5f);
                Check(view.IsFirstPerson && brain.ActiveVirtualCamera.Name == "PlayerFirstPersonCamera", "First-person camera is active");
                Check(Mathf.Abs(camera.fieldOfView - 75f) < 0.1f && camera.nearClipPlane < 0.05f, "First-person lens is applied");
                Check(UnityEngine.Object.FindObjectsByType<Camera>().Length == 1, "Only one real rendering camera");
                var body = renderer.transform.GetChild(0).GetComponent<SkinnedMeshRenderer>();
                Check(body != null && body.enabled && renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly,
                    "Headless body is visible; complete original still casts shadows");
                Check(body != null && body.bones[0] == renderer.bones[0] && body.sharedMesh != renderer.sharedMesh,
                    "Both meshes share the same animated skeleton without changing the original mesh");

                Vector3 before = player.transform.position;
                float yaw = player.transform.eulerAngles.y;
                input.MoveInput(Vector2.right);
                yield return new WaitForSeconds(0.6f);
                input.MoveInput(Vector2.zero);
                Check(Vector3.Dot(player.transform.position - before, startRotation * Vector3.right) > 0.5f &&
                    Mathf.Abs(Mathf.DeltaAngle(yaw, player.transform.eulerAngles.y)) < 0.1f, "A/D strafe without turning the body");
                before = player.transform.position;
                input.MoveInput(Vector2.down);
                yield return new WaitForSeconds(0.6f);
                input.MoveInput(Vector2.zero);
                Check(Vector3.Dot(player.transform.position - before, startRotation * Vector3.forward) < -0.5f &&
                    Mathf.Abs(Mathf.DeltaAngle(yaw, player.transform.eulerAngles.y)) < 0.1f, "S walks backwards without turning the body");

                Teleport(controller, startPosition, startRotation);
                input.MoveInput(Vector2.up);
                yield return new WaitForSeconds(3.0f);
                input.MoveInput(Vector2.zero);
                yield return new WaitForSeconds(0.4f);
                Check(player.transform.position.z > 12f && move.Grounded, "Walk through the open study door onto the study floor");
                Capture(camera, "first_person_study.png");

                before = player.transform.position;
                input.LookInput(new Vector2(0f, 1000f));
                yield return null;
                input.LookInput(Vector2.zero);
                yield return null;
                yield return new WaitForEndOfFrame();
                Check(Mathf.Abs(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x) - 85f) < 0.5f &&
                    Mathf.Abs(Mathf.DeltaAngle(0f, player.transform.eulerAngles.x)) < 0.1f, "Look-down clamp affects camera only, not body pitch");
                Check(Vector3.Distance(before, player.transform.position) < 0.05f, "Looking does not displace the player");
                Capture(camera, "first_person_body.png");

                move.CanJump = false;
                float floorHeight = player.transform.position.y;
                input.JumpInput(true);
                yield return new WaitForSeconds(0.4f);
                Check(Mathf.Abs(player.transform.position.y - floorHeight) < 0.05f, "CanJump=false prevents jumping in first person");
                input.JumpInput(false);

                before = player.transform.position;
                view.UseFirstPerson = false;
                yield return null;
                yield return new WaitForEndOfFrame();
                Check(!view.IsFirstPerson && brain.ActiveVirtualCamera.Name == "PlayerFollowCamera" && !brain.IsBlending,
                    "Unchecking Use First Person cuts back to the original third-person camera");
                Check(renderer.enabled && renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !body.enabled,
                    "Third person restores the complete original character");
                Check(Vector3.Distance(before, player.transform.position) < 0.05f && Mathf.Abs(camera.fieldOfView - 30f) < 0.1f,
                    "Switching preserves position and restores the original 30-degree lens");
                input.LookInput(new Vector2(0f, -60f));
                yield return null;
                // Third-person look runs in LateUpdate, after the coroutine resumes.
                yield return null;
                input.LookInput(Vector2.zero);
                yield return new WaitForSeconds(0.3f);
                Capture(camera, "third_person_restored.png");
                yaw = player.transform.eulerAngles.y;
                input.MoveInput(Vector2.right);
                yield return new WaitForSeconds(0.5f);
                input.MoveInput(Vector2.zero);
                Check(Mathf.Abs(Mathf.DeltaAngle(yaw, player.transform.eulerAngles.y)) > 45f, "Third person still turns to face movement");

                view.UseFirstPerson = true;
                yield return null;
                input.LookInput(new Vector2(10000f, -10000f));
                yield return null;
                input.LookInput(Vector2.zero);
                yield return new WaitForEndOfFrame();
                Check(Mathf.Abs(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x) + 80f) < 0.5f &&
                    Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.y, player.transform.eulerAngles.y)) < 0.1f,
                    "Look-up clamp and mouse yaw/body alignment");

                // Re-enter from a known third-person orientation to face the study's right wall.
                view.UseFirstPerson = false;
                yield return null;
                Teleport(controller, new Vector3(-2.405f, 0.13f, 12.5f), Quaternion.Euler(0f, 90f, 0f));
                move.SetThirdPersonLook(90f, 0f);
                yield return new WaitForSeconds(0.3f);
                view.UseFirstPerson = true;
                yield return null;
                input.MoveInput(Vector2.up);
                yield return new WaitForSeconds(3.3f);
                input.MoveInput(Vector2.zero);
                yield return new WaitForSeconds(0.2f);
                Check(player.transform.position.x < 2.4f && player.transform.position.x > 1.8f, "Study wall blocks forward movement");
                Check(!Physics.CheckSphere(camera.transform.position, 0.09f, 1 << 0, QueryTriggerInteraction.Ignore),
                    "Camera near-wall clearance remains outside world colliders");
                Check(Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit wallHit, 0.6f, 1 << 0) &&
                    wallHit.distance > camera.nearClipPlane, "Facing a close wall keeps its surface in front of the near plane");

                view.enabled = false;
                yield return null;
                yield return new WaitForEndOfFrame();
                Check(renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !body.enabled &&
                    brain.ActiveVirtualCamera.Name == "PlayerFollowCamera", "Disabling the component restores third person and the original body");
                view.enabled = true;
                yield return null;
                yield return new WaitForEndOfFrame();
                Check(view.IsFirstPerson && body.enabled, "Re-enabling the component safely restores first person");
                for (int i = 0; i < 6; i++)
                {
                    view.UseFirstPerson = !view.UseFirstPerson;
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }
                Check(renderer.transform.childCount == 1, "Repeated switching does not create duplicate body renderers");
                Latest.complete = true;
            }
            finally
            {
                input.MoveInput(Vector2.zero);
                input.LookInput(Vector2.zero);
                input.JumpInput(false);
                input.SprintInput(false);
                move.CanJump = originalJump;
                view.UseFirstPerson = originalMode;
                Teleport(controller, startPosition, startRotation);
                if (originalInput) device.ActivateInput();
                Application.runInBackground = originalRunInBackground;
                Cursor.lockState = originalCursor;
                running = false;
                Directory.CreateDirectory("Temp/FullBodyViewTests");
                File.WriteAllText("Temp/FullBodyViewTests/report.json", JsonUtility.ToJson(Latest, true));
                Debug.Log($"Full body view smoke tests: {Latest.passed} passed, {Latest.failed} failed. Report: Temp/FullBodyViewTests/report.json");
            }
        }

        private static void Teleport(CharacterController controller, Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            controller.transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static void Check(bool passed, string message)
        {
            Latest.checks.Add((passed ? "PASS: " : "FAIL: ") + message);
            if (passed) Latest.passed++;
            else Latest.failed++;
        }

        private static void Capture(Camera camera, string name)
        {
            Directory.CreateDirectory("Temp/FullBodyViewTests");
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = RenderTexture.GetTemporary(1440, 900, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes("Temp/FullBodyViewTests/" + name, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(texture);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
