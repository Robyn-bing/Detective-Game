using System.Collections.Generic;
using DetectiveGame.Recall;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private RecallSessionController recallSession;

        [Header("Focus")]
        [SerializeField, Range(0.03f, 0.35f)] private float viewportFocusRadius = 0.16f;
        [SerializeField] private LayerMask visibilityLayers = Physics.DefaultRaycastLayers;

        private InputAction interactAction;
        private InteractionPromptUI promptUI;
        private WorldInteractable focusedTarget;

        public WorldInteractable FocusedTarget => focusedTarget;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (recallSession == null) recallSession = FindAnyObjectByType<RecallSessionController>();

            promptUI = GetComponent<InteractionPromptUI>();
            if (promptUI == null) promptUI = gameObject.AddComponent<InteractionPromptUI>();
            promptUI.SetCamera(viewCamera);

            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonNorth");
        }

        private void OnEnable()
        {
            interactAction.performed += OnInteract;
            interactAction.Enable();
        }

        private void OnDisable()
        {
            interactAction.performed -= OnInteract;
            interactAction.Disable();
            SetFocus(null);
        }

        private void OnDestroy() => interactAction.Dispose();

        private void Update()
        {
            if (viewCamera == null || (recallSession != null && recallSession.IsActive))
            {
                SetFocus(null);
                return;
            }

            SetFocus(FindBestTarget());
        }

        private WorldInteractable FindBestTarget()
        {
            WorldInteractable best = null;
            float bestScore = float.MaxValue;
            IReadOnlyList<WorldInteractable> objects = WorldInteractable.Active;

            for (int i = 0; i < objects.Count; i++)
            {
                WorldInteractable candidate = objects[i];
                if (candidate == null || !candidate.IsInteractionAvailable) continue;

                Vector3 targetPoint = candidate.InteractionAnchor.position;
                float distance = Vector3.Distance(transform.position, targetPoint);
                if (distance > candidate.InteractionDistance) continue;

                Vector3 viewport = viewCamera.WorldToViewportPoint(targetPoint);
                if (viewport.z <= 0f) continue;
                float centerDistance = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f).magnitude;
                if (centerDistance > viewportFocusRadius) continue;
                if (!HasLineOfSight(candidate, targetPoint)) continue;

                float score = centerDistance * 4f + distance / candidate.InteractionDistance;
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }

            return best;
        }

        private bool HasLineOfSight(WorldInteractable candidate, Vector3 targetPoint)
        {
            Vector3 origin = viewCamera.transform.position;
            Vector3 delta = targetPoint - origin;
            if (delta.sqrMagnitude <= Mathf.Epsilon) return true;

            int mask = visibilityLayers.value & ~(1 << gameObject.layer);
            if (!Physics.Raycast(origin, delta.normalized, out RaycastHit hit,
                    delta.magnitude + 0.1f, mask, QueryTriggerInteraction.Ignore))
                return true;

            return hit.transform == candidate.transform || hit.transform.IsChildOf(candidate.transform);
        }

        private void SetFocus(WorldInteractable target)
        {
            if (focusedTarget == target) return;
            focusedTarget = target;
            if (promptUI != null) promptUI.Show(focusedTarget);
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            if (focusedTarget == null || !focusedTarget.IsInteractionAvailable) return;
            if (recallSession != null && recallSession.IsActive) return;
            focusedTarget.Interact(transform);
            SetFocus(null);
        }
    }
}
