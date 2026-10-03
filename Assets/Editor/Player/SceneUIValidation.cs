using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DetectiveGame.EditorTools
{
    public static class SceneUIValidation
    {
        [MenuItem("Tools/Detective Game/UI/Validate Authored Scenes")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Exit Play Mode and save scene edits first.");
            string original = SceneManager.GetActiveScene().path;
            var results = new List<string>();
            try
            {
                foreach (string name in new[] { "Test", "Tutorial" })
                {
                    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                    if (GameObject.Find("UI") == null) throw new Exception(name + ": missing UI root");
                    int count = 0;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                            throw new Exception(name + ": missing script on " + child.name);
                        foreach (MonoBehaviour view in child.GetComponents<MonoBehaviour>())
                        {
                            var so = new SerializedObject(view);
                            var authored = so.FindProperty("useSceneUI");
                            if (authored == null) continue;
                            if (!authored.boolValue) throw new Exception(name + ": runtime layout on " + view);
                            foreach (string field in new[] { "canvasObject", "font", "choiceTemplate", "cardTemplate", "momentTemplate", "optionTemplate", "markerTemplate", "periodTemplate", "defaultTemplate", "recallTemplate", "interactionTemplate", "closeButton", "playButton" })
                            {
                                var p = so.FindProperty(field);
                                if (p != null && p.objectReferenceValue == null)
                                    throw new Exception(name + ": missing " + field + " on " + view);
                            }
                            count++;
                        }
                    }
                    results.Add(name + ": " + count + " authored UI controllers; references valid; no missing scripts.");
                }
            }
            finally { EditorSceneManager.OpenScene(original); }
            Directory.CreateDirectory("Temp/SceneUIQA");
            File.WriteAllLines("Temp/SceneUIQA/validation.txt", results);
            Debug.Log(string.Join("\n", results));
        }
    }
}
