using System.Collections.Generic;
using DetectiveGame.Evidence;
using UnityEngine;

namespace DetectiveGame.Recall
{
    [DisallowMultipleComponent]
    public sealed class RecallableObject : MonoBehaviour
    {
        private static readonly List<RecallableObject> ActiveObjects = new List<RecallableObject>();

        [SerializeField] private RecallObjectData data;
        [SerializeField] private EvidenceDefinition evidence;
        [SerializeField] private Transform interactionAnchor;
        [SerializeField, Min(0.25f)] private float interactionDistance = 1.75f;
        [SerializeField] private Vector2 promptScreenOffset = new Vector2(52f, 38f);
        [SerializeField, Range(0.0005f, 0.03f)] private float outlineWidth = 0.006f;
        [SerializeField, Min(0.1f)] private float outlinePulseSpeed = 2.2f;

        private readonly List<Renderer> outlineRenderers = new List<Renderer>();
        private MaterialPropertyBlock outlineProperties;
        private bool highlighted;

        public RecallObjectData Data => data;
        public EvidenceDefinition Evidence => evidence;
        public Transform InteractionAnchor => interactionAnchor != null ? interactionAnchor : transform;
        public float InteractionDistance => interactionDistance;
        public Vector2 PromptScreenOffset => promptScreenOffset;
        public static IReadOnlyList<RecallableObject> Active => ActiveObjects;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveObjects.Clear();

        private void OnEnable()
        {
            ActiveObjects.Remove(this);
            ActiveObjects.Add(this);
        }

        private void OnDisable() => ActiveObjects.Remove(this);

        private void Update()
        {
            if (!highlighted || outlineRenderers.Count == 0) return;
            float alpha = Mathf.Lerp(0.25f, 0.9f, (Mathf.Sin(Time.unscaledTime * outlinePulseSpeed) + 1f) * 0.5f);
            outlineProperties.SetColor("_OutlineColor", new Color(1f, 1f, 1f, alpha));
            outlineProperties.SetFloat("_OutlineWidth", outlineWidth);
            for (int i = 0; i < outlineRenderers.Count; i++)
                if (outlineRenderers[i] != null) outlineRenderers[i].SetPropertyBlock(outlineProperties);
        }

        public void SetInteractionHighlight(bool value, Material outlineMaterial)
        {
            if (value && outlineRenderers.Count == 0 && outlineMaterial != null) CreateOutline(outlineMaterial);
            highlighted = value && outlineRenderers.Count > 0;
            for (int i = 0; i < outlineRenderers.Count; i++)
                if (outlineRenderers[i] != null) outlineRenderers[i].enabled = highlighted;
        }

        private void CreateOutline(Material outlineMaterial)
        {
            outlineProperties = new MaterialPropertyBlock();
            Renderer[] sources = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                Renderer source = sources[i];
                if (source is MeshRenderer meshRenderer)
                {
                    MeshFilter sourceFilter = meshRenderer.GetComponent<MeshFilter>();
                    if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;
                    GameObject outline = CreateOutlineObject(source.transform, outlineMaterial);
                    outline.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                    outlineRenderers.Add(outline.AddComponent<MeshRenderer>());
                }
                else if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    GameObject outline = CreateOutlineObject(source.transform, outlineMaterial);
                    var copy = outline.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = skinned.sharedMesh;
                    copy.bones = skinned.bones;
                    copy.rootBone = skinned.rootBone;
                    copy.localBounds = skinned.localBounds;
                    copy.updateWhenOffscreen = skinned.updateWhenOffscreen;
                    outlineRenderers.Add(copy);
                }
            }
            for (int i = 0; i < outlineRenderers.Count; i++)
            {
                outlineRenderers[i].sharedMaterial = outlineMaterial;
                outlineRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outlineRenderers[i].receiveShadows = false;
            }
        }

        private static GameObject CreateOutlineObject(Transform source, Material outlineMaterial)
        {
            var outline = new GameObject("Recall Outline (Runtime)");
            outline.layer = source.gameObject.layer;
            outline.transform.SetParent(source, false);
            outline.transform.localPosition = Vector3.zero;
            outline.transform.localRotation = Quaternion.identity;
            outline.transform.localScale = Vector3.one;
            return outline;
        }

        private void OnValidate()
        {
            if (interactionAnchor == null) interactionAnchor = transform;
        }
    }
}
