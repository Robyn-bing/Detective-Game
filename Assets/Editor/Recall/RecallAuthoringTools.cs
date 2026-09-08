using System.IO;
using UnityEditor;
using UnityEngine;

namespace DetectiveGame.EditorTools
{
    public static class RecallAuthoringTools
    {
        [MenuItem("Tools/Detective Game/Recall/Create Preview Prefab From Selected")]
        private static void CreatePreviewFromSelected()
        {
            GameObject source = Selection.activeGameObject;
            if (source == null)
            {
                EditorUtility.DisplayDialog("Recall Preview", "Select a scene object or prefab first.", "OK");
                return;
            }

            EnsureFolder("Assets/Recall");
            EnsureFolder("Assets/Recall/Generated");
            string safeName = MakeSafeName(source.name);
            string objectFolder = AssetDatabase.GenerateUniqueAssetPath($"Assets/Recall/Generated/{safeName}");
            AssetDatabase.CreateFolder("Assets/Recall/Generated", Path.GetFileName(objectFolder));
            string meshFolder = objectFolder + "/Meshes";
            AssetDatabase.CreateFolder(objectFolder, "Meshes");

            GameObject clone = Object.Instantiate(source);
            clone.name = safeName + "_RecallPreview";
            clone.transform.SetParent(null);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;

            foreach (MeshFilter filter in clone.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Mesh copy = Object.Instantiate(filter.sharedMesh);
                copy.name = MakeSafeName(filter.gameObject.name) + "_PreviewMesh";
                string path = AssetDatabase.GenerateUniqueAssetPath(meshFolder + "/" + copy.name + ".asset");
                AssetDatabase.CreateAsset(copy, path);
                filter.sharedMesh = copy;
            }
            foreach (SkinnedMeshRenderer renderer in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null) continue;
                Mesh copy = Object.Instantiate(renderer.sharedMesh);
                copy.name = MakeSafeName(renderer.gameObject.name) + "_PreviewMesh";
                string path = AssetDatabase.GenerateUniqueAssetPath(meshFolder + "/" + copy.name + ".asset");
                AssetDatabase.CreateAsset(copy, path);
                renderer.sharedMesh = copy;
            }

            // A recall preview is authored visual data, not a second gameplay object.
            foreach (Component component in clone.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || component is MeshFilter ||
                    component is Renderer || component is Animator) continue;
                Object.DestroyImmediate(component);
            }

            string prefabPath = objectFolder + "/" + clone.name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
            Object.DestroyImmediate(clone);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Created recall preview prefab: {prefabPath}", prefab);
        }

        [MenuItem("Tools/Detective Game/Recall/Create Preview Prefab From Selected", true)]
        private static bool ValidateCreatePreviewFromSelected() => Selection.activeGameObject != null && !EditorApplication.isPlaying;

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string MakeSafeName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "RecallObject" : value.Replace(' ', '_');
        }
    }
}
