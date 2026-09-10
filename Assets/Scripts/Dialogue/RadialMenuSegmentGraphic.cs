using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DetectiveGame.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class RadialMenuSegmentGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        private float innerRadius = 100f;
        private float outerRadius = 300f;
        private float centerAngle;
        private float spanAngle = 60f;
        private int subdivisions = 16;

        public void Configure(
            float newInnerRadius,
            float newOuterRadius,
            float newCenterAngle,
            float newSpanAngle,
            int newSubdivisions,
            Color newColor)
        {
            innerRadius = Mathf.Max(0f, newInnerRadius);
            outerRadius = Mathf.Max(innerRadius + 0.01f, newOuterRadius);
            centerAngle = newCenterAngle;
            spanAngle = Mathf.Clamp(newSpanAngle, 0.1f, 360f);
            subdivisions = Mathf.Clamp(newSubdivisions, 2, 96);
            color = newColor;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            float startAngle = centerAngle - spanAngle * 0.5f;
            int stepCount = spanAngle >= 359.9f ? Mathf.Max(subdivisions, 48) : subdivisions;

            if (innerRadius <= 0.01f)
            {
                AddDisc(vertexHelper, startAngle, stepCount);
                return;
            }

            for (int i = 0; i <= stepCount; i++)
            {
                float angle = (startAngle + spanAngle * i / stepCount) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(vertexHelper, direction * innerRadius);
                AddVertex(vertexHelper, direction * outerRadius);
            }

            for (int i = 0; i < stepCount; i++)
            {
                int first = i * 2;
                vertexHelper.AddTriangle(first, first + 1, first + 3);
                vertexHelper.AddTriangle(first, first + 3, first + 2);
            }
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPoint, eventCamera, out Vector2 localPoint)) return false;

            float radius = localPoint.magnitude;
            if (radius < innerRadius || radius > outerRadius) return false;
            if (spanAngle >= 359.9f) return true;

            float pointAngle = Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(centerAngle, pointAngle)) <= spanAngle * 0.5f;
        }

        private void AddDisc(VertexHelper vertexHelper, float startAngle, int stepCount)
        {
            AddVertex(vertexHelper, Vector2.zero);
            for (int i = 0; i <= stepCount; i++)
            {
                float angle = (startAngle + spanAngle * i / stepCount) * Mathf.Deg2Rad;
                AddVertex(vertexHelper, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius);
            }

            for (int i = 0; i < stepCount; i++)
                vertexHelper.AddTriangle(0, i + 1, i + 2);
        }

        private void AddVertex(VertexHelper vertexHelper, Vector2 position)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertex.uv0 = new Vector2(
                Mathf.InverseLerp(-outerRadius, outerRadius, position.x),
                Mathf.InverseLerp(-outerRadius, outerRadius, position.y));
            vertexHelper.AddVert(vertex);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class RadialMenuPointerHandler : MonoBehaviour, IPointerClickHandler
    {
        private Action clicked;

        public void Configure(Action onClicked) => clicked = onClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) clicked?.Invoke();
        }
    }
}
