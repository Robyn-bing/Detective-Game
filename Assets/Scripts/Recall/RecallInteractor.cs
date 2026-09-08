using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Recall
{
    [DisallowMultipleComponent]
    public sealed class RecallInteractor : MonoBehaviour
    {
        [SerializeField] private RecallSessionController recallSession;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Material outlineMaterial;
        [SerializeField, Range(0.03f, 0.3f)] private float viewportFocusRadius = 0.12f;
        [SerializeField] private LayerMask visibilityLayers = Physics.DefaultRaycastLayers;

        private InputAction recallAction;
        private RecallableObject focusedTarget;

        public RecallableObject FocusedTarget => focusedTarget;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            recallAction = new InputAction("Recall", InputActionType.Button, "<Keyboard>/r");
            recallAction.AddBinding("<Gamepad>/buttonWest");
        }

        private void OnEnable()
        {
            recallAction.performed += OnRecall;
            recallAction.Enable();
        }

        private void OnDisable()
        {
            recallAction.performed -= OnRecall;
            recallAction.Disable();
            SetFocus(null);
        }

        private void OnDestroy() => recallAction.Dispose();

        private void Update()
        {
            if (recallSession == null || recallSession.IsActive || viewCamera == null)
            {
                SetFocus(null);
                return;
            }
            SetFocus(FindBestTarget());
        }

        private RecallableObject FindBestTarget()
        {
            RecallableObject best = null;
            float bestScore = float.MaxValue;
            var objects = RecallableObject.Active;
            for (int i = 0; i < objects.Count; i++)
            {
                RecallableObject candidate = objects[i];
                if (candidate == null || candidate.Data == null || candidate.Data.AvailablePeriodCount == 0) continue;
                Vector3 point = candidate.InteractionAnchor.position;
                float distance = Vector3.Distance(transform.position, point);
                if (distance > candidate.InteractionDistance) continue;

                Vector3 viewport = viewCamera.WorldToViewportPoint(point);
                if (viewport.z <= 0f) continue;
                float centerDistance = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f).magnitude;
                if (centerDistance > viewportFocusRadius) continue;
                if (!HasLineOfSight(candidate, point)) continue;

                float score = centerDistance * 4f + distance / candidate.InteractionDistance;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        private bool HasLineOfSight(RecallableObject candidate, Vector3 targetPoint)
        {
            Vector3 origin = viewCamera.transform.position;
            Vector3 delta = targetPoint - origin;
            int mask = visibilityLayers.value & ~(1 << gameObject.layer);
            if (!Physics.Raycast(origin, delta.normalized, out RaycastHit hit, delta.magnitude + 0.1f,
                    mask, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform == candidate.transform || hit.transform.IsChildOf(candidate.transform);
        }

        private void SetFocus(RecallableObject target)
        {
            if (focusedTarget == target) return;
            if (focusedTarget != null) focusedTarget.SetInteractionHighlight(false, outlineMaterial);
            focusedTarget = target;
            if (focusedTarget != null) focusedTarget.SetInteractionHighlight(true, outlineMaterial);
            if (recallSession != null) recallSession.SetWorldPrompt(focusedTarget);
        }

        private void OnRecall(InputAction.CallbackContext context)
        {
            if (focusedTarget != null && recallSession != null && !recallSession.IsActive)
                recallSession.BeginRecall(focusedTarget);
        }
    }
}
