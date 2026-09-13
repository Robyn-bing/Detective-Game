using System;
using System.Collections.Generic;
using UnityEngine;

namespace DetectiveGame.Recall
{
    /// <summary>
    /// Temporarily replaces a visual hierarchy's materials with the recall dissolve shader.
    /// Source materials, property blocks, and renderer enabled states are restored verbatim.
    /// </summary>
    internal sealed class RecallDissolveVisual : IDisposable
    {
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static readonly int DesaturationId = Shader.PropertyToID("_Desaturation");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly RendererState[] rendererStates;
        private readonly List<Material> runtimeMaterials = new List<Material>();
        private readonly Color edgeColor;
        private readonly float edgeWidth;
        private readonly float noiseScale;
        private bool disposed;

        public float DissolveAmount { get; private set; }
        public float Desaturation { get; private set; }

        private sealed class RendererState
        {
            public Renderer Renderer;
            public bool Enabled;
            public Material[] SourceMaterials;
            public Material[] TransitionMaterials;
            public MaterialPropertyBlock SourceProperties;
            public MaterialPropertyBlock TransitionProperties;
        }

        public RecallDissolveVisual(
            GameObject root,
            Shader dissolveShader,
            Color newEdgeColor,
            float newEdgeWidth,
            float newNoiseScale)
        {
            edgeColor = newEdgeColor;
            edgeWidth = Mathf.Max(0.001f, newEdgeWidth);
            noiseScale = Mathf.Max(0.01f, newNoiseScale);

            if (root == null)
            {
                rendererStates = Array.Empty<RendererState>();
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            var states = new List<RendererState>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.gameObject.name == "Recall Outline (Runtime)") continue;

                var sourceProperties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(sourceProperties);
                var transitionProperties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(transitionProperties);
                Material[] sources = renderer.sharedMaterials;
                var transitions = new Material[sources.Length];
                for (int materialIndex = 0; materialIndex < sources.Length; materialIndex++)
                {
                    Material source = sources[materialIndex];
                    if (source == null || dissolveShader == null)
                    {
                        transitions[materialIndex] = source;
                        continue;
                    }

                    Material transition = CreateTransitionMaterial(source, dissolveShader);
                    transitions[materialIndex] = transition;
                    runtimeMaterials.Add(transition);
                }

                states.Add(new RendererState
                {
                    Renderer = renderer,
                    Enabled = renderer.enabled,
                    SourceMaterials = sources,
                    TransitionMaterials = transitions,
                    SourceProperties = sourceProperties,
                    TransitionProperties = transitionProperties
                });
            }
            rendererStates = states.ToArray();
        }

        public void BeginTransition(bool visible, float dissolveAmount, float desaturation)
        {
            if (disposed) return;
            for (int i = 0; i < rendererStates.Length; i++)
            {
                RendererState state = rendererStates[i];
                if (state.Renderer == null) continue;
                state.Renderer.sharedMaterials = state.TransitionMaterials;
                state.Renderer.enabled = visible && state.Enabled;
            }
            SetEffect(dissolveAmount, desaturation);
        }

        public void SetVisible(bool visible)
        {
            if (disposed) return;
            for (int i = 0; i < rendererStates.Length; i++)
            {
                RendererState state = rendererStates[i];
                if (state.Renderer != null) state.Renderer.enabled = visible && state.Enabled;
            }
        }

        public void SetEffect(float dissolveAmount, float desaturation)
        {
            if (disposed) return;
            dissolveAmount = Mathf.Clamp01(dissolveAmount);
            desaturation = Mathf.Clamp01(desaturation);
            DissolveAmount = dissolveAmount;
            Desaturation = desaturation;
            for (int i = 0; i < rendererStates.Length; i++)
            {
                RendererState state = rendererStates[i];
                if (state.Renderer == null) continue;
                MaterialPropertyBlock properties = state.TransitionProperties;
                properties.SetFloat(DissolveAmountId, dissolveAmount);
                properties.SetFloat(DesaturationId, desaturation);
                properties.SetColor(EdgeColorId, edgeColor);
                properties.SetFloat(EdgeWidthId, edgeWidth);
                properties.SetFloat(NoiseScaleId, noiseScale);
                state.Renderer.SetPropertyBlock(properties);
            }
        }

        public void RestoreAppearance(bool visible)
        {
            if (disposed) return;
            for (int i = 0; i < rendererStates.Length; i++)
            {
                RendererState state = rendererStates[i];
                if (state.Renderer == null) continue;
                state.Renderer.sharedMaterials = state.SourceMaterials;
                state.Renderer.SetPropertyBlock(state.SourceProperties);
                state.Renderer.enabled = visible && state.Enabled;
            }
        }

        public void Dispose() => Dispose(true);

        public void Dispose(bool restoreVisible)
        {
            if (disposed) return;
            RestoreAppearance(restoreVisible);
            disposed = true;
            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                Material material = runtimeMaterials[i];
                if (material == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(material);
                else UnityEngine.Object.DestroyImmediate(material);
            }
            runtimeMaterials.Clear();
        }

        private static Material CreateTransitionMaterial(Material source, Shader dissolveShader)
        {
            var material = new Material(dissolveShader)
            {
                name = $"{source.name} (Recall Dissolve Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };

            Texture baseTexture = null;
            Vector2 textureScale = Vector2.one;
            Vector2 textureOffset = Vector2.zero;
            if (source.HasProperty(BaseMapId))
            {
                baseTexture = source.GetTexture(BaseMapId);
                textureScale = source.GetTextureScale(BaseMapId);
                textureOffset = source.GetTextureOffset(BaseMapId);
            }
            else if (source.mainTexture != null)
            {
                baseTexture = source.mainTexture;
                textureScale = source.mainTextureScale;
                textureOffset = source.mainTextureOffset;
            }

            Color baseColor = Color.white;
            if (source.HasProperty(BaseColorId)) baseColor = source.GetColor(BaseColorId);
            else if (source.HasProperty("_Color")) baseColor = source.GetColor("_Color");

            material.SetTexture(BaseMapId, baseTexture);
            material.SetTextureScale(BaseMapId, textureScale);
            material.SetTextureOffset(BaseMapId, textureOffset);
            material.SetColor(BaseColorId, baseColor);
            material.enableInstancing = source.enableInstancing;
            return material;
        }
    }
}
