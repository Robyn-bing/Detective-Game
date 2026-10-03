using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DetectiveGame.Dialogue;
using DetectiveGame.Evidence;
using DetectiveGame.Input;
using DetectiveGame.Recall;
using Ink.UnityIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DetectiveGame.EditorTools
{
    public static class CaseNotebookSmokeTests
    {
        [Serializable] public sealed class Report
        {
            public bool complete;
            public List<string> passed = new List<string>();
            public List<string> failed = new List<string>();
        }
        private static bool running;
        private static Report report;
        private static Keyboard keyboard;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);

        [MenuItem("Tools/Detective Game/Run Case Notebook Smoke Tests (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || running || DialogueController.Current == null || !DialogueController.Current.UseCaseNotebook)
                throw new InvalidOperationException("Enter Play Mode in the Test scene first.");
            running = true;
            report = new Report();
            DialogueController.Current.StartCoroutine(Exercise());
        }

        private static void Check(bool condition, string label)
        {
            (condition ? report.passed : report.failed).Add(label);
            File.WriteAllText("Temp/NotebookQA/report.json", JsonUtility.ToJson(report, true));
        }

        private static IEnumerator KeyDown(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
        }
        private static IEnumerator Release()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }
        private static IEnumerator Tap(Key key) { yield return KeyDown(key); yield return Release(); }

        private static IEnumerator Exercise()
        {
            Directory.CreateDirectory("Temp/NotebookQA");
            var controller = DialogueController.Current;
            var ui = controller.GetComponent<DialogueRuntimeUI>();
            var evidence = EvidenceBoardController.Current;
            var evidenceUi = evidence.GetComponent<EvidenceBoardRuntimeUI>();
            bool background = Application.runInBackground;
            float initialTime = Time.timeScale;
            keyboard = InputSystem.AddDevice<Keyboard>();
            Application.runInBackground = true;
            try
            {
                yield return null;
                yield return Tap(Key.Escape);
                Check(!controller.HasOpenModal && GameInputModeController.Current.IsGameplay, "Short Esc during exploration does not open menu");
                yield return Tap(Key.Tab);
                Check(Field<bool>(controller, "boardOpen") && !Field<bool>(controller, "wheelOpen"), "Tab directly opens notebook");
                Check(Field<GameObject>(ui, "notebookNavigation").activeSelf, "Notebook navigation visible");
                ScreenCapture.CaptureScreenshot("Temp/NotebookQA/testimony.png");
                yield return new WaitForSecondsRealtime(.1f);
                Field<Button>(ui, "evidenceTab").onClick.Invoke();
                yield return null;
                Check(evidence.IsOpen && evidenceUi.IsVisible && GameInputModeController.Current.IsOwnedBy(controller), "Mouse tab opens complete evidence page without losing input ownership");
                ScreenCapture.CaptureScreenshot("Temp/NotebookQA/evidence-empty.png");
                yield return new WaitForSecondsRealtime(.1f);
                yield return Tap(Key.Tab);
                Check(!controller.HasOpenModal && !evidence.IsOpen && GameInputModeController.Current.IsGameplay, "Tab closes evidence and restores exploration");
                yield return Tap(Key.Tab);
                Check(evidence.IsOpen, "Notebook remembers last page");
                yield return KeyDown(Key.Escape);
                yield return new WaitForSecondsRealtime(.7f);
                Check(!controller.HasOpenModal, "Holding Esc after closing notebook never opens pause menu");
                yield return Release();
                yield return KeyDown(Key.Escape);
                yield return new WaitForSecondsRealtime(.15f);
                Check(Field<GameObject>(ui, "pauseHoldRoot").activeSelf && Field<RectTransform>(ui, "pauseHoldFill").anchorMax.x > 0f,
                    "Esc hold displays advancing progress");
                ScreenCapture.CaptureScreenshot("Temp/NotebookQA/hold-progress.png");
                yield return Release();
                Check(!Field<bool>(controller, "wheelOpen") && !Field<GameObject>(ui, "pauseHoldRoot").activeSelf, "Early release cancels progress");
                yield return KeyDown(Key.Escape);
                yield return new WaitForSecondsRealtime(.8f);
                Check(Field<bool>(controller, "wheelOpen") && Time.timeScale == 0f && Field<bool>(ui, "wheelInteractive"), "Hold opens animated pause wheel and pauses game time");
                Canvas.ForceUpdateCanvases();
                var radial = Field<GameObject>(ui, "wheelRoot").GetComponentInChildren<RadialMenuSegmentGraphic>();
                var mesh = radial.GetComponent<CanvasRenderer>() != null ? radial.canvasRenderer.GetMesh() : null;
                Check(mesh != null && mesh.vertexCount > 0, "Pause wheel sectors have rendered geometry");
                var wheelClick = Field<GameObject>(ui, "wheelRoot").GetComponent<IPointerClickHandler>();
                typeof(DialogueRuntimeUI).GetMethod("SetWheelSelection", Private).Invoke(ui, new object[] { 0 });
                wheelClick.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                Check(Field<Coroutine>(ui, "wheelClickFeedback") != null, "Pointer click starts menu feedback animation");
                yield return new WaitForSecondsRealtime(.2f);
                yield return Release();
                Check(Field<bool>(controller, "wheelOpen"), "Releasing successful hold leaves menu open");
                ScreenCapture.CaptureScreenshot("Temp/NotebookQA/pause-menu.png");
                yield return new WaitForSecondsRealtime(.1f);
                foreach (var option in new[] { InvestigationMenuOption.Save, InvestigationMenuOption.QuickSave, InvestigationMenuOption.Hint, InvestigationMenuOption.Settings, InvestigationMenuOption.ReturnToTitle })
                {
                    controller.SelectInvestigationMenuOption(option);
                    Check(Field<Text>(ui, "toastText").text.Contains("正在开发中") && Field<bool>(controller, "wheelOpen"), option + " is placeholder only");
                }
                yield return Tap(Key.Tab);
                Check(Field<bool>(controller, "wheelOpen") && !Field<bool>(controller, "boardOpen"), "Tab cannot open notebook over pause menu");
                yield return Tap(Key.Escape);
                yield return new WaitForSecondsRealtime(.3f);
                Check(!controller.HasOpenModal && Time.timeScale == initialTime && GameInputModeController.Current.IsGameplay, "Short Esc animates close and restores time/input");

                var book = GameObject.Find("Desk_Book").GetComponent<RecallableObject>();
                var data = book.Data;
                var definition = book.Evidence;
                EvidenceBoardService.Current.RecordReviewedEvidence(definition, data, 0);
                yield return Tap(Key.Tab);
                Check(evidence.IsOpen && Field<GameObject>(evidenceUi, "previewObject") != null, "Existing evidence preview loads in notebook");
                var slider = Field<Slider>(evidenceUi, "timelineSlider");
                yield return Tap(Key.Space);
                Check(Field<bool>(evidenceUi, "previewPlaying"), "Space plays existing evidence recall preview");
                yield return Tap(Key.Space);
                Check(!Field<bool>(evidenceUi, "previewPlaying"), "Space pauses existing evidence recall preview");
                slider.value = .5f;
                float previewTime = Field<float>(evidenceUi, "previewClipTime");
                var moment = definition.GetMoment(0);
                evidenceUi.CommitChoice(moment.Id, moment.GetChoice(0).Id);
                Check(!string.IsNullOrEmpty(EvidenceBoardService.Current.GetSelectedChoiceId(definition, moment)), "Existing deduction choice commit works");
                Field<Button>(ui, "testimonyTab").onClick.Invoke();
                Field<Button>(ui, "evidenceTab").onClick.Invoke();
                Check(Mathf.Approximately(Field<float>(evidenceUi, "previewClipTime"), previewTime), "Switching pages preserves recall time");
                yield return null;
                ScreenCapture.CaptureScreenshot("Temp/NotebookQA/evidence-populated.png");
                yield return new WaitForSecondsRealtime(.1f);
                yield return Tap(Key.Tab);

                InkFile ink = null;
                foreach (var guid in AssetDatabase.FindAssets("t:InkFile"))
                {
                    var candidate = AssetDatabase.LoadAssetAtPath<InkFile>(AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate != null && candidate.isCompiled && !candidate.name.Contains("Tutorial")) { ink = candidate; break; }
                }
                Check(ink != null, "Dialogue fixture available");
                if (ink != null)
                {
                    controller.BeginDialogue(ink);
                    yield return Tap(Key.Tab);
                    Check(controller.IsDialogueActive && evidence.IsOpen, "Notebook opens from normal dialogue");
                    yield return Tap(Key.Escape);
                    Check(controller.IsDialogueActive && !evidence.IsOpen && GameInputModeController.Current.Mode == GameInputMode.Dialogue, "Esc closes only notebook and returns to dialogue");
                    yield return Tap(Key.Escape);
                }
                report.complete = true;
            }
            finally
            {
                controller.CloseBoard();
                controller.CloseInvestigationWheel();
                Time.timeScale = initialTime;
                Application.runInBackground = background;
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                running = false;
                File.WriteAllText("Temp/NotebookQA/report.json", JsonUtility.ToJson(report, true));
            }
        }
    }
}
