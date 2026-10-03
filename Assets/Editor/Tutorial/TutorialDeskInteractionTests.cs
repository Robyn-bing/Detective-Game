using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DetectiveGame.Interaction;
using DetectiveGame.Input;
using DetectiveGame.Recall;
using DetectiveGame.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.EditorTools
{
    public static class TutorialDeskInteractionTests
    {
        [Serializable] public class Report { public bool complete; public List<string> passed = new List<string>(); public List<string> failed = new List<string>(); }
        static Report report;
        static void Check(bool ok, string text)
        {
            (ok ? report.passed : report.failed).Add(text);
            File.WriteAllText(".tmp-desk-interactions/tests.json", JsonUtility.ToJson(report, true));
        }
        [MenuItem("Tools/Detective Game/Test Tutorial Desk Interactions (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            report = new Report();
            UnityEngine.Object.FindAnyObjectByType<TelephoneDialController>().StartCoroutine(Exercise());
        }
        static IEnumerator Exercise()
        {
            var lamp = UnityEngine.Object.FindAnyObjectByType<LampSwitchInteractable>();
            var paper = UnityEngine.Object.FindAnyObjectByType<PaperInspectInteractable>();
            var phone = UnityEngine.Object.FindAnyObjectByType<TelephoneDialController>();
            var recall = UnityEngine.Object.FindAnyObjectByType<RecallSessionController>();
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerInput>();
            var wake = UnityEngine.Object.FindAnyObjectByType<TutorialWakeUpController>();
            if (wake != null) wake.CompleteOpening();
            yield return new WaitForSecondsRealtime(.3f);
            var camera = Camera.main;
            bool lampWasOn = lamp.IsOn;
            float intensity = lamp.PointLight.intensity;
            Vector3 pos = camera.transform.position; Quaternion rot = camera.transform.rotation; float fov = camera.fieldOfView;
            lamp.Interact(player.transform);
            Check(lamp.IsOn != lampWasOn, "Lamp toggles authored point light");
            Check(camera.transform.position == pos && camera.transform.rotation == rot && camera.fieldOfView == fov, "Lamp interaction leaves camera unchanged");
            Check(GameInputModeController.Current.IsGameplay, "Lamp keeps gameplay controls active");
            lamp.Interact(player.transform);
            Check(lamp.IsOn == lampWasOn && lamp.PointLight.intensity == intensity, "Second toggle restores light without changing brightness");
            float near = camera.nearClipPlane;
            paper.Interact(player.transform);
            Check(paper.IsInspecting && GameInputModeController.Current.Mode == GameInputMode.InspectObject, "Paper acquires exclusive input ownership");
            lamp.Interact(player.transform);
            Check(lamp.IsOn == lampWasOn, "Lamp ignores interaction during paper inspection");
            yield return new WaitForSecondsRealtime(1.2f);
            Check(paper.IsViewing && Vector3.Distance(camera.transform.position, paper.ViewAnchor.position) < .001f && Quaternion.Angle(camera.transform.rotation, paper.ViewAnchor.rotation) < .1f, "Paper reaches authored close-up pose");
            Check(Vector3.Dot(camera.transform.up, -paper.transform.forward) > .99f, "Paper view uses upright reading direction");
            bool framed = true;
            foreach (var r in paper.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                for (int x=-1;x<=1;x+=2) for (int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
                { var v=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))); framed &= v.z>camera.nearClipPlane && v.x>.01f && v.x<.99f && v.y>.01f && v.y<.99f; }
            }
            Check(framed, "Both paper meshes fit the camera without near clipping");
            ScreenCapture.CaptureScreenshot(".tmp-desk-interactions/paper-view.png");
            paper.ExitInspect(); yield return new WaitForSecondsRealtime(.9f);
            Check(!paper.IsInspecting && GameInputModeController.Current.IsGameplay && Mathf.Approximately(camera.nearClipPlane,near), "Paper exit restores input and camera clipping");
            paper.Interact(player.transform); yield return new WaitForSecondsRealtime(.1f); paper.enabled=false;
            Check(!paper.IsInspecting && GameInputModeController.Current.IsGameplay, "Disabling paper mid-transition restores gameplay");
            paper.enabled=true;
            phone.Interact(player.transform); yield return new WaitForSecondsRealtime(1.2f);
            Check(phone.State==TelephoneDialController.DialState.Ready, "Telephone E dialing still enters successfully");
            ScreenCapture.CaptureScreenshot(".tmp-desk-interactions/telephone-E.png");
            yield return null;
            Vector3 dialPos=camera.transform.position;Quaternion dialRot=camera.transform.rotation;float dialFov=camera.fieldOfView;
            phone.ExitDialMode();yield return new WaitForSecondsRealtime(.9f);
            recall.BeginRecall(phone.GetComponent<RecallableObject>());
            if(recall.State==RecallSessionController.SessionState.SelectingPeriod)recall.SelectAvailablePeriod(0);
            float deadline=Time.realtimeSinceStartup+12;
            while(recall.State==RecallSessionController.SessionState.TransitioningIn && Time.realtimeSinceStartup<deadline)yield return null;
            Check(recall.IsActive && Vector3.Distance(camera.transform.position,dialPos)<.001f && Quaternion.Angle(camera.transform.rotation,dialRot)<.1f && Mathf.Abs(camera.fieldOfView-dialFov)<.01f,"Telephone R and E share exact position, rotation and FOV");
            var stage=GameObject.Find("Recall Preview Stage (Runtime)");
            var previewBase=System.Array.Find(stage.GetComponentsInChildren<MeshFilter>(), m=>m.name=="TEL_Base_Static");
            var originalBase=System.Array.Find(phone.GetComponentsInChildren<MeshFilter>(), m=>m.name=="TEL_Base_Static");
            bool geometryMatches=true;
            foreach(var corner in new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.forward})
                geometryMatches &= Vector3.Distance(previewBase.transform.TransformPoint(corner), originalBase.transform.TransformPoint(corner))<.001f;
            Check(geometryMatches,"Recall telephone mesh transform matches E model, including scale");
            ScreenCapture.CaptureScreenshot(".tmp-desk-interactions/telephone-R.png");
            yield return null;
            recall.ExitRecall();yield return new WaitForSecondsRealtime(1.5f);
            Check(!recall.IsActive && GameInputModeController.Current.IsGameplay,"Recall exit still restores gameplay");
            report.complete=true;File.WriteAllText(".tmp-desk-interactions/tests.json",JsonUtility.ToJson(report,true));
        }
    }
}
