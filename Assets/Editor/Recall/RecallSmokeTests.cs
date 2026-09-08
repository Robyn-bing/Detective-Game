using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DetectiveGame.Recall;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.EditorTools
{
    public static class RecallSmokeTests
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

        [MenuItem("Tools/Detective Game/Run Recall Smoke Tests (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || running)
                throw new InvalidOperationException("Enter Play Mode in Test and wait for any current test to finish.");
            RecallSessionController session = UnityEngine.Object.FindAnyObjectByType<RecallSessionController>();
            RecallableObject book = GameObject.Find("Desk_Book")?.GetComponent<RecallableObject>();
            if (session == null || book == null) throw new InvalidOperationException("Recall setup is missing from Test.");
            Latest = new Report();
            running = true;
            session.StartCoroutine(Exercise(session, book));
        }

        private static IEnumerator Exercise(RecallSessionController session, RecallableObject book)
        {
            float originalTimeScale = Time.timeScale;
            var input = GameObject.Find("PlayerArmature").GetComponent<PlayerInput>();
            int originalMask = Camera.main.cullingMask;
            Vector3 originalCameraPosition = Camera.main.transform.position;
            Renderer bookRenderer = book.GetComponentInChildren<Renderer>();
            bool originalBookRendererEnabled = bookRenderer != null && bookRenderer.enabled;
            Renderer[] playerRenderers = input.GetComponentsInChildren<Renderer>(true);
            bool[] originalPlayerRendererStates = new bool[playerRenderers.Length];
            for (int i = 0; i < playerRenderers.Length; i++) originalPlayerRendererStates[i] = playerRenderers[i].enabled;
            bool originalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                var multiData = UnityEngine.Object.Instantiate(book.Data);
                var multiDataObject = new SerializedObject(multiData);
                SerializedProperty multiPeriods = multiDataObject.FindProperty("periods");
                multiPeriods.InsertArrayElementAtIndex(0);
                multiDataObject.ApplyModifiedPropertiesWithoutUndo();
                var multiTargetObject = new GameObject("Recall Multiple Period Test Target");
                var multiTarget = multiTargetObject.AddComponent<RecallableObject>();
                var multiTargetSerialized = new SerializedObject(multiTarget);
                multiTargetSerialized.FindProperty("data").objectReferenceValue = multiData;
                multiTargetSerialized.ApplyModifiedPropertiesWithoutUndo();
                session.BeginRecall(multiTarget);
                Check(session.State == RecallSessionController.SessionState.SelectingPeriod && session.ActivePreview == null,
                    "Multiple periods open the selection screen first");
                session.SelectAvailablePeriod(1);
                Check(session.State == RecallSessionController.SessionState.TransitioningIn && session.ActivePreview != null,
                    "Selecting a period starts a smooth transition into that recall clip");
                session.ExitRecall();
                Check(session.State == RecallSessionController.SessionState.TransitioningOut,
                    "Leaving an active recall starts a return camera transition");
                yield return new WaitForSecondsRealtime(multiData.CameraReturnTransitionSeconds + 0.1f);
                Check(session.State == RecallSessionController.SessionState.Idle,
                    "Return transition completes before exploration resumes");
                UnityEngine.Object.Destroy(multiTargetObject);
                UnityEngine.Object.Destroy(multiData);
                yield return null;

                session.BeginRecall(book);
                Check(session.State == RecallSessionController.SessionState.TransitioningIn,
                    "Single period begins the synchronized camera and rewind entry");
                Check(Vector3.Distance(Camera.main.transform.position, originalCameraPosition) < 0.01f,
                    "Camera transition starts from the current player view without a cut");
                Check(session.NormalizedTime >= 0.999f,
                    "Object starts the synchronized entry at its latest authored state");
                Check(session.ActivePreview != null, "Preview instance is created");
                Check(Time.timeScale == 0f && !input.inputIsActive, "Gameplay pauses and player input is inactive");
                Check(UnityEngine.Object.FindObjectsByType<Camera>().Length == 1, "Recall reuses the one real MainCamera");
                Check(!Camera.main.GetComponent<CinemachineBrain>().enabled, "Cinemachine yields the MainCamera during recall");
                Check((Camera.main.cullingMask & 1) != 0, "Recall camera keeps the scene world visible behind the recalled object");
                Check(bookRenderer == null || !bookRenderer.enabled, "Original object is hidden while its recalled copy is shown in-place");
                bool allPlayerVisualsHidden = true;
                for (int i = 0; i < playerRenderers.Length; i++)
                    if (playerRenderers[i] != null && playerRenderers[i].enabled) allPlayerVisualsHidden = false;
                Check(allPlayerVisualsHidden, "Player renderers are hidden so the character cannot block the recalled object");

                RecallPeriod authoredPeriod = book.Data.GetPeriod(book.Data.GetAvailablePeriodIndex(0));
                AnimationCurve rewindCurve = authoredPeriod.RewindProgressCurve;
                Check(rewindCurve != null && rewindCurve.length >= 2 &&
                      rewindCurve.Evaluate(0f) <= 0.01f && rewindCurve.Evaluate(1f) >= 0.99f,
                    "Inspector-authored rewind curve spans the synchronized entry timeline");

                yield return new WaitForSecondsRealtime(0.25f);
                Check(session.State == RecallSessionController.SessionState.TransitioningIn &&
                      Vector3.Distance(Camera.main.transform.position, originalCameraPosition) > 0.01f,
                    "Camera moves smoothly during the transition instead of cutting");
                Check(session.NormalizedTime < 0.99f && session.NormalizedTime > 0.01f,
                    "Object rewinds while the camera is still moving");

                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, authoredPeriod.SynchronizedEntrySeconds - 0.1f));
                Vector3 framedCameraPosition = (Vector3)typeof(RecallSessionController)
                    .GetField("previewCameraPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(session);
                Check(session.State == RecallSessionController.SessionState.Paused && session.NormalizedTime <= 0.001f,
                    "Camera transition and rewind stop together at the earliest state");
                Check(Vector3.Distance(Camera.main.transform.position, framedCameraPosition) < 0.01f,
                    "Synchronized entry lands on the fixed camera in the same completion frame");
                Check(Mathf.Abs(session.ActivePreview.transform.localPosition.x + 0.58f) < 0.03f,
                    "Earliest authored keyframe is sampled");

                session.SetNormalizedTime(0.5f);
                yield return null;
                Check(session.State == RecallSessionController.SessionState.Paused && Mathf.Abs(session.NormalizedTime - 0.5f) < 0.01f,
                    "Timeline scrubbing samples an exact normalized time");
                Check(session.ActivePreview.transform.localPosition.x > 0.5f, "Middle keyframe moves the book to the right");

                session.TogglePlayback();
                yield return new WaitForSecondsRealtime(0.45f);
                Check(session.State == RecallSessionController.SessionState.Playing && session.NormalizedTime > 0.6f,
                    "Space-equivalent playback advances from the scrubbed position");
                session.TogglePlayback();
                Check(session.State == RecallSessionController.SessionState.Paused, "Playback can pause without changing the sampled state");

                Vector3 fixedCameraPosition = Camera.main.transform.position;
                session.ExitRecall();
                Check(session.State == RecallSessionController.SessionState.TransitioningOut && session.ActivePreview != null,
                    "Exit keeps the recalled scene alive while the camera begins returning");
                yield return new WaitForSecondsRealtime(0.25f);
                Check(session.State == RecallSessionController.SessionState.TransitioningOut &&
                      Vector3.Distance(Camera.main.transform.position, fixedCameraPosition) > 0.01f,
                    "Camera moves smoothly from the fixed recall view toward the player view");
                yield return new WaitForSecondsRealtime(book.Data.CameraReturnTransitionSeconds + 0.1f);
                Check(session.State == RecallSessionController.SessionState.Idle && session.ActivePreview == null,
                    "Return transition destroys the preview and resumes exploration");
                Check(Mathf.Approximately(Time.timeScale, originalTimeScale) && input.inputIsActive,
                    "Exit restores game time and PlayerInput");
                Check(Camera.main.cullingMask == originalMask && Camera.main.GetComponent<CinemachineBrain>().enabled,
                    "Exit restores the gameplay camera");
                Check(bookRenderer == null || bookRenderer.enabled == originalBookRendererEnabled,
                    "Exit restores the original object's renderer");
                bool playerVisualsRestored = true;
                for (int i = 0; i < playerRenderers.Length; i++)
                    if (playerRenderers[i] != null && playerRenderers[i].enabled != originalPlayerRendererStates[i]) playerVisualsRestored = false;
                Check(playerVisualsRestored, "Return transition restores every player renderer to its original state");
                Latest.complete = true;
            }
            finally
            {
                if (session.IsActive)
                {
                    session.enabled = false;
                    session.enabled = true;
                }
                Time.timeScale = originalTimeScale;
                Application.runInBackground = originalRunInBackground;
                running = false;
                Directory.CreateDirectory("Temp/RecallTests");
                File.WriteAllText("Temp/RecallTests/report.json", JsonUtility.ToJson(Latest, true));
                Debug.Log($"Recall smoke tests: {Latest.passed} passed, {Latest.failed} failed. Report: Temp/RecallTests/report.json");
            }
        }

        private static void Check(bool passed, string message)
        {
            Latest.checks.Add((passed ? "PASS: " : "FAIL: ") + message);
            if (passed) Latest.passed++; else Latest.failed++;
        }
    }
}
