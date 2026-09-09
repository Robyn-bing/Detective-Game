using System.Collections.Generic;
using UnityEngine;

namespace DetectiveGame.Interaction
{
    /// <summary>Base contract for objects the player can operate with the Interact action.</summary>
    public abstract class WorldInteractable : MonoBehaviour
    {
        private static readonly List<WorldInteractable> ActiveObjects = new List<WorldInteractable>();

        [Header("Interaction")]
        [SerializeField] private Transform interactionAnchor;
        [SerializeField, Min(0.25f)] private float interactionDistance = 1.8f;
        [SerializeField] private Vector2 promptScreenOffset = new Vector2(52f, 38f);

        public Transform InteractionAnchor => interactionAnchor != null ? interactionAnchor : transform;
        public float InteractionDistance => interactionDistance;
        public Vector2 PromptScreenOffset => promptScreenOffset;
        public abstract string PromptText { get; }
        public virtual bool IsInteractionAvailable => isActiveAndEnabled;
        public static IReadOnlyList<WorldInteractable> Active => ActiveObjects;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveObjects.Clear();

        protected virtual void OnEnable()
        {
            ActiveObjects.Remove(this);
            ActiveObjects.Add(this);
        }

        protected virtual void OnDisable() => ActiveObjects.Remove(this);

        public abstract void Interact(Transform interactor);

        protected virtual void OnValidate()
        {
            interactionDistance = Mathf.Max(0.25f, interactionDistance);
        }

        private void OnDrawGizmosSelected()
        {
            Transform anchor = InteractionAnchor;
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.7f);
            Gizmos.DrawWireSphere(anchor.position, 0.08f);
            Gizmos.DrawWireSphere(anchor.position, interactionDistance);
        }
    }
}
