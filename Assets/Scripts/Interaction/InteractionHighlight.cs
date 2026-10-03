using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DetectiveGame.Interaction
{
    /// <summary>Screen-pixel silhouette shared by E interactions and R recall.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionHighlight : MonoBehaviour
    {
        private readonly List<Renderer> copies = new List<Renderer>();
        private readonly List<Renderer> sources = new List<Renderer>();
        private MaterialPropertyBlock properties;
        private Material mask;
        private bool highlighted;
        private float pulse = 2.2f;
        public bool IsHighlighted => highlighted;
        private void Awake() => properties = new MaterialPropertyBlock();

        public void SetHighlighted(bool value, Material outline, float pulseSpeed = 2.2f)
        {
            if (value && copies.Count == 0 && outline != null) Build(outline);
            highlighted = value;
            pulse = pulseSpeed;
            UpdateCopies();
        }

        private void LateUpdate() => UpdateCopies();

        private void UpdateCopies()
        {
            float alpha = Mathf.Lerp(.35f, .9f, (Mathf.Sin(Time.unscaledTime * pulse) + 1) * .5f);
            for (int i = 0; i < copies.Count; i++)
            {
                var copy = copies[i];
                if (copy == null) continue;
                copy.enabled = highlighted && sources[i] != null && sources[i].enabled;
                copy.GetPropertyBlock(properties);
                properties.SetColor("_OutlineColor", new Color(1, 1, 1, alpha));
                copy.SetPropertyBlock(properties);
            }
        }

        private void Build(Material outline)
        {
            mask = new Material(Resources.Load<Shader>("InteractionOutlineMask"));
            foreach (var source in GetComponentsInChildren<Renderer>(true))
            {
                if (source.name == "Recall Outline (Runtime)" || source.GetComponentInParent<InteractionHighlight>() != this) continue;
                Mesh mesh = source is SkinnedMeshRenderer skin ? skin.sharedMesh : source.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                // One unexpanded stencil mask, followed by eight two-pixel offsets.
                // This also outlines flat paper and avoids normal seams on the telephone.
                for (int direction = -1; direction < 8; direction++)
                {
                    var go = new GameObject("Recall Outline (Runtime)");
                    go.layer = source.gameObject.layer;
                    go.transform.SetParent(source.transform, false);
                    Renderer copy;
                    if (source is SkinnedMeshRenderer skinned)
                    {
                        var dst = go.AddComponent<SkinnedMeshRenderer>();
                        dst.sharedMesh = mesh; dst.bones = skinned.bones; dst.rootBone = skinned.rootBone;
                        dst.localBounds = skinned.localBounds; copy = dst;
                    }
                    else { go.AddComponent<MeshFilter>().sharedMesh = mesh; copy = go.AddComponent<MeshRenderer>(); }
                    var materials = new Material[mesh.subMeshCount];
                    for (int m = 0; m < materials.Length; m++) materials[m] = direction < 0 ? mask : outline;
                    copy.sharedMaterials = materials;
                    copy.shadowCastingMode = ShadowCastingMode.Off; copy.receiveShadows = false;
                    properties.Clear();
                    float angle = direction * Mathf.PI / 4;
                    properties.SetVector("_OutlineOffset", new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), 0, 0));
                    copy.SetPropertyBlock(properties);
                    copies.Add(copy); sources.Add(source);
                }
            }
        }
        private void OnDisable() { highlighted = false; UpdateCopies(); }
        private void OnDestroy()
        {
            foreach (var copy in copies) if (copy != null) Destroy(copy.gameObject);
            if (mask != null) Destroy(mask);
        }
    }
}
