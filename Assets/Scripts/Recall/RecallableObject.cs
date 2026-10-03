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

        private DetectiveGame.Interaction.InteractionHighlight highlight;

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



        public void SetInteractionHighlight(bool value, Material outlineMaterial)
        {
            if (highlight == null) highlight = GetComponent<DetectiveGame.Interaction.InteractionHighlight>();
            if (highlight == null && value) highlight = gameObject.AddComponent<DetectiveGame.Interaction.InteractionHighlight>();
            if (highlight != null) highlight.SetHighlighted(value, outlineMaterial, outlinePulseSpeed);
        }

        private void OnDisable()
        {
            ActiveObjects.Remove(this);
            SetInteractionHighlight(false, null);
        }

        private void OnValidate()
        {
            if (interactionAnchor == null) interactionAnchor = transform;
        }
    }
}

