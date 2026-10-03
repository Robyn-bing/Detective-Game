using System;
using System.IO;
using System.Linq;
using DetectiveGame.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DetectiveGame.EditorTools
{
    public static class TutorialDeskInteractionSetup
    {
        [MenuItem("Tools/Detective Game/Configure Tutorial Desk Interactions")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Tutorial.unity")
                throw new InvalidOperationException("Open Tutorial in Edit Mode first.");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var lamp = all.Single(t => t.name == "TableLamp").gameObject;
            var papers = all.Single(t => t.name == "Papers_foaie1_foaie2").gameObject;
            var phone = all.Select(t => t.GetComponent<TelephoneDialController>()).First(t => t != null);
            var bounds = BoundsOf(lamp);
            var lights = all.Select(t => t.GetComponent<Light>()).Where(l => l != null && l.type == LightType.Point && l.name != "BulbLight_Off")
                .Where(l => l.transform.IsChildOf(lamp.transform) || Vector3.Distance(l.transform.position, bounds.center) < bounds.extents.magnitude + 0.12f)
                .OrderBy(l => Vector3.Distance(l.transform.position, bounds.center)).ToArray();
            if (lights.Length == 0) throw new InvalidOperationException("Cannot find the user's point light at TableLamp.");
            var point = lights[0];
            if (point.intensity <= 0) throw new InvalidOperationException("The authored point light has zero intensity; set its intended on brightness first.");
            Directory.CreateDirectory(".tmp-desk-interactions");
            const string backup = ".tmp-desk-interactions/Tutorial.before-desk-interactions.unity";
            if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true))
                throw new IOException("Could not back up the current Tutorial scene.");
            var lampSwitch = lamp.GetComponent<LampSwitchInteractable>() ?? Undo.AddComponent<LampSwitchInteractable>(lamp);
            Set(lampSwitch, "pointLight", point);
            Set(lampSwitch, "interactionAnchor", Anchor(lamp.transform, "LampInteractionAnchor", bounds.center));
            SetFloat(lampSwitch, "interactionDistance", phone.InteractionDistance);
            var inspect = papers.GetComponent<PaperInspectInteractable>() ?? Undo.AddComponent<PaperInspectInteractable>(papers);
            bounds = BoundsOf(papers);
            Set(inspect, "interactionAnchor", Anchor(papers.transform, "PaperInteractionAnchor", bounds.center + papers.transform.up * .015f));
            SetFloat(inspect, "interactionDistance", phone.InteractionDistance);
            var view = papers.transform.Find("PaperViewAnchor");
            if (view == null)
            {
                view = Anchor(papers.transform, "PaperViewAnchor", bounds.center);
                var rotation = Quaternion.LookRotation(-papers.transform.up, -papers.transform.forward);
                float extent = 0;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 p = Quaternion.Inverse(rotation) * Vector3.Scale(bounds.extents, new Vector3(x,y,z));
                    extent = Mathf.Max(extent, Mathf.Abs(p.y), Mathf.Abs(p.x));
                }
                float distance = Mathf.Max(.3f, extent / Mathf.Tan(phone.DialFieldOfView * .5f * Mathf.Deg2Rad) * 1.18f);
                view.SetPositionAndRotation(bounds.center + papers.transform.up * distance, rotation);
            }
            Set(inspect, "viewAnchor", view); SetFloat(inspect, "fieldOfView", phone.DialFieldOfView);
            var ps = new SerializedObject(phone);
            SetFloat(inspect, "entrySeconds", ps.FindProperty("cameraEntrySeconds").floatValue);
            SetFloat(inspect, "exitSeconds", ps.FindProperty("cameraExitSeconds").floatValue);
            var ins = new SerializedObject(inspect); ins.FindProperty("cameraProgress").animationCurveValue = ps.FindProperty("cameraProgress").animationCurveValue; ins.ApplyModifiedProperties();
            var hint = all.FirstOrDefault(t => t.name == "Paper Inspection Hint");
            if (hint == null)
            {
                var host = new GameObject("Paper Inspection Hint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                Undo.RegisterCreatedObjectUndo(host, "Paper inspection hint");
                var ui = all.FirstOrDefault(t => t.name == "UI"); if (ui != null) host.transform.SetParent(ui, false);
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
                var label = new GameObject("Exit Instruction", typeof(RectTransform), typeof(Text)); label.transform.SetParent(host.transform, false);
                var rect = label.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f,0); rect.anchoredPosition = new Vector2(0,65); rect.sizeDelta = new Vector2(500,50);
                var text = label.GetComponent<Text>(); text.text = "Esc / B  返回"; text.fontSize = 26; text.color = new Color(.92f,.88f,.78f); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
                text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NotoSansCJKsc-Regular.otf");
                hint = host.transform; host.SetActive(false);
            }
            Set(inspect, "exitHint", hint.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Tutorial scene.");
            File.WriteAllText(".tmp-desk-interactions/configured.txt", "PASS\nLight=" + point.name + " intensity=" + point.intensity + " enabled=" + point.enabled + "\nPaper view=" + view.position + "\nPhone shared view=" + phone.DialViewAnchor.position);
        }
        static Bounds BoundsOf(GameObject o)
        {
            var rs = o.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) throw new Exception("Missing renderers: " + o.name);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }
        static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var t = parent.Find(name); if (t != null) return t;
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Desk interaction anchor"); go.transform.SetParent(parent, false); go.transform.position = position; return go.transform;
        }
        static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        { var s = new SerializedObject(target); s.FindProperty(property).objectReferenceValue = value; s.ApplyModifiedProperties(); }
        static void SetFloat(UnityEngine.Object target, string property, float value)
        { var s = new SerializedObject(target); s.FindProperty(property).floatValue = value; s.ApplyModifiedProperties(); }
    }
}
