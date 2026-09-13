using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DetectiveGame.Interaction;
using DetectiveGame.Player;
using StarterAssets;
using UnityEditor;
using UnityEngine;

namespace DetectiveGame.EditorTools
{
    public static class DoorInteractionSmokeTests
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

        [MenuItem("Tools/Detective Game/Run Door Interaction Smoke Tests (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || running)
                throw new InvalidOperationException("Enter Play Mode in Test before running this test.");

            GameObject player = GameObject.Find("PlayerArmature");
            DoorInteractable door = GameObject.Find("Door_Hinge_Open100deg")?.GetComponent<DoorInteractable>();
            PlayerDoorInteractionController interaction =
                player != null ? player.GetComponent<PlayerDoorInteractionController>() : null;
            if (player == null || door == null || interaction == null)
                throw new InvalidOperationException("The Test scene door interaction setup is incomplete.");

            Latest = new Report();
            running = true;
            interaction.StartCoroutine(Exercise(player, door, interaction));
        }

        private static IEnumerator Exercise(
            GameObject player,
            DoorInteractable door,
            PlayerDoorInteractionController interaction)
        {
            Vector3 originalPosition = player.transform.position;
            Quaternion originalRotation = player.transform.rotation;
            bool originalDoorOpen = door.IsOpen;
            float originalDoorDuration = door.AnimationDuration;
            bool originalUsePlayerOpenAnimation = door.UsePlayerOpenAnimation;
            bool originalRunInBackground = Application.runInBackground;
            ThirdPersonController movement = player.GetComponent<ThirdPersonController>();
            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
            Application.runInBackground = true;

            try
            {
                var doorSerialized = new SerializedObject(door);
                doorSerialized.FindProperty("animationDuration").floatValue = 300f;
                doorSerialized.FindProperty("usePlayerOpenAnimation").boolValue = false;
                doorSerialized.ApplyModifiedPropertiesWithoutUndo();
                door.SetOpen(false, true);
                door.Interact(player.transform);
                Check(door.IsOpen && !interaction.IsInteracting && !door.CharacterInteractionActive,
                    "Disabling Use Player Open Animation bypasses alignment and hand IK");

                door.SetOpen(false, true);
                doorSerialized.Update();
                doorSerialized.FindProperty("usePlayerOpenAnimation").boolValue = true;
                doorSerialized.ApplyModifiedPropertiesWithoutUndo();
                door.Interact(player.transform);
                Check(interaction.IsInteracting && door.CharacterInteractionActive,
                    "A closed door reserves and starts the player open-door interaction");
                Check(!movement.enabled && !interactor.enabled,
                    "Player movement and additional interactions are locked during the action");

                yield return new WaitForSecondsRealtime(0.32f);
                Check(Vector3.Distance(player.transform.position, new Vector3(-2.17f, 0f, 9.45f)) < 0.04f,
                    "The player aligns smoothly to the outside stand point");

                yield return new WaitForSecondsRealtime(0.52f);
                Check(door.IsOpen && door.IsAnimating,
                    "The door begins its adjustable motion after the hand reaches the handle");
                // End the deliberately slowed mechanical motion so the action can finish promptly.
                door.SetOpen(true, true);

                float timeout = Time.realtimeSinceStartup + 3f;
                while (interaction.IsInteracting && Time.realtimeSinceStartup < timeout) yield return null;
                Check(!interaction.IsInteracting && !door.CharacterInteractionActive && !door.IsAnimating,
                    "The action finishes after the door motion completes");
                Check(interaction.IkAppliedFrames > 0 && interaction.MinimumHandTargetDistance < 0.03f,
                    $"Humanoid IK keeps the right hand on the moving handle " +
                    $"({interaction.IkAppliedFrames} frames, closest distance " +
                    $"{interaction.MinimumHandTargetDistance:F3} m)");
                Check(movement.enabled && interactor.enabled,
                    "Player movement and interaction controls are restored at the end");
                Latest.complete = true;
            }
            finally
            {
                door.SetOpen(originalDoorOpen, true);
                var doorSerialized = new SerializedObject(door);
                doorSerialized.FindProperty("animationDuration").floatValue = originalDoorDuration;
                doorSerialized.FindProperty("usePlayerOpenAnimation").boolValue = originalUsePlayerOpenAnimation;
                doorSerialized.ApplyModifiedPropertiesWithoutUndo();
                player.transform.SetPositionAndRotation(originalPosition, originalRotation);
                FullBodyViewController view = player.GetComponent<FullBodyViewController>();
                if (view != null)
                {
                    view.SetInteractionLock(true);
                    view.SetInteractionLock(false);
                }
                Application.runInBackground = originalRunInBackground;
                running = false;
                Directory.CreateDirectory("Temp/DoorInteractionTests");
                File.WriteAllText(
                    "Temp/DoorInteractionTests/report.json",
                    JsonUtility.ToJson(Latest, true));
                Debug.Log($"Door interaction smoke tests: {Latest.passed} passed, {Latest.failed} failed. " +
                          "Report: Temp/DoorInteractionTests/report.json");
            }
        }

        private static void Check(bool passed, string message)
        {
            Latest.checks.Add((passed ? "PASS: " : "FAIL: ") + message);
            if (passed) Latest.passed++; else Latest.failed++;
        }
    }
}
