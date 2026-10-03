using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DetectiveGame.EditorTools
{
    /// <summary>Explicit, repeatable scene authoring; never runs on import or in a player.</summary>
    public static class TutorialWindowLightingSetup
    {
        private const string Folder = "Assets/Tutorial/Lighting";
        private const string RendererPath = Folder + "/Tutorial_WindowRenderer.asset";
        private const string ProfilePath = Folder + "/Tutorial_WindowAtmosphere.asset";

        [MenuItem("Tools/Detective Game/Configure Tutorial Window Lighting")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Tutorial.unity")
                throw new InvalidOperationException("Open Tutorial in Edit Mode before configuring window lighting.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Tutorial", "Lighting");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Configure Tutorial Window Lighting");

            var glassObject = Required("PB_WindowGlass");
            var glass = glassObject.GetComponent<MeshRenderer>();
            var glassMaterial = MaterialAsset("M_Tutorial_WindowGlass", "DetectiveGame/Tutorial/Window Glass");
            glassMaterial.SetFloat("_Surface", 1);
            glassMaterial.SetFloat("_Blend", 0);
            glassMaterial.SetFloat("_BlendModePreserveSpecular", 1);
            glassMaterial.SetFloat("_AlphaClip", 0);
            glassMaterial.SetFloat("_Cull", (float)CullMode.Back);
            glassMaterial.SetFloat("_Metallic", 0);
            glassMaterial.SetFloat("_Smoothness", 0.92f);
            glassMaterial.SetColor("_BaseColor", new Color(0.86f, 0.91f, 0.93f, 0.12f));
            glassMaterial.SetColor("_EmissionColor", Color.black);
            glassMaterial.SetFloat("_EnvironmentReflections", 1);
            glassMaterial.SetFloat("_SpecularHighlights", 1);
            if (glassMaterial.shader.name == "Universal Render Pipeline/Lit")
                UnityEditor.BaseShaderGUI.SetMaterialKeywords(glassMaterial);
            else
                glassMaterial.renderQueue = (int)RenderQueue.Transparent;
            Undo.RecordObject(glass, "Window glass material");
            glass.sharedMaterial = glassMaterial;
            glass.shadowCastingMode = ShadowCastingMode.Off;
            glass.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            // A transparent window must not become a baked occluder either.
            var flags = GameObjectUtility.GetStaticEditorFlags(glassObject);
            GameObjectUtility.SetStaticEditorFlags(glassObject, flags & ~StaticEditorFlags.OccluderStatic & ~StaticEditorFlags.ContributeGI);
            EditorUtility.SetDirty(glassMaterial);

            var windowLight = Required("Window_CoolFillLight").GetComponent<Light>();
            Undo.RecordObjects(new UnityEngine.Object[] { windowLight, windowLight.transform }, "Window sunlight");
            windowLight.type = LightType.Spot;
            windowLight.transform.SetPositionAndRotation(new Vector3(0.51f, 2.491f, 3.862f), Quaternion.Euler(30.9f, 180, 0));
            windowLight.color = new Color(1, 0.91f, 0.74f);
            windowLight.intensity = 1.5f;
            windowLight.range = 9f;
            windowLight.spotAngle = 65f;
            windowLight.innerSpotAngle = 48f;
            windowLight.shadows = LightShadows.Soft;
            windowLight.shadowStrength = 1;
            windowLight.shadowBias = 0.02f;
            windowLight.shadowNormalBias = 0.025f;
            windowLight.shadowNearPlane = 0.1f;
            var additional = GetOrAdd<VolumetricAdditionalLight>(windowLight.gameObject);
            Undo.RecordObject(additional, "Window scattering");
            additional.Anisotropy = 0.25f;
            additional.Scattering = 0f;
            additional.Radius = 0.15f;
            var lightData = windowLight.GetUniversalAdditionalLightData();
            Undo.RecordObject(lightData, "Window shadow resolution");
            lightData.usePipelineSettings = false;
            var lightSettings = new SerializedObject(lightData);
            lightSettings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh;
            lightSettings.ApplyModifiedProperties();

            var sun = Required("Directional Light").GetComponent<Light>();
            Undo.RecordObjects(new UnityEngine.Object[] { sun, sun.transform }, "Align exterior ambient sun");
            sun.transform.rotation = Quaternion.Euler(25, 195, 0);
            sun.intensity = 1.8f;
            sun.color = new Color(1, 0.94f, 0.85f);
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.01f;
            sun.shadowNormalBias = 0.015f;
            var sunData = sun.GetUniversalAdditionalLightData();
            Undo.RecordObject(sunData, "Sun shadow bias");
            sunData.usePipelineSettings = false;
            foreach (var r in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true)))
                if (r.name.StartsWith("PB_WindowFrame") || r.name.StartsWith("PB_NorthWall"))
                {
                    Undo.RecordObject(r, "Window shadow casters");
                    r.shadowCastingMode = ShadowCastingMode.On;
                }

            var rig = GameObject.Find("Tutorial_WindowLighting") ?? NewObject("Tutorial_WindowLighting");
            var volume = GetOrAdd<Volume>(rig);
            Undo.RecordObject(volume, "Window atmosphere");
            volume.isGlobal = true;
            volume.priority = 20;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            volume.sharedProfile = profile;
            var fog = VolumeComponent<VolumetricFogVolumeComponent>(profile);
            fog.enabled.Override(true);
            fog.enableMainLightContribution.Override(true);
            fog.enableAdditionalLightsContribution.Override(false);
            fog.scattering.Override(1f);
            fog.anisotropy.Override(0.45f);
            fog.enableAPVContribution.Override(false);
            fog.distance.Override(12);
            fog.baseHeight.Override(0);
            fog.maximumHeight.Override(4);
            fog.enableGround.Override(true);
            fog.groundHeight.Override(0.02f);
            fog.density.Override(0.25f);
            fog.attenuationDistance.Override(90);
            fog.maxSteps.Override(96);
            fog.blurIterations.Override(1);
            fog.renderPassEvent.Override(VolumetricFogRenderPassEvent.BeforeRenderingTransparents);
            var bloom = VolumeComponent<Bloom>(profile);
            bloom.intensity.Override(0.12f);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(0.55f);
            EditorUtility.SetDirty(fog);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(profile);

            int rendererIndex = ConfigureRenderer();
            foreach (var camera in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)))
            {
                var data = camera.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Tutorial window renderer");
                data.SetRenderer(rendererIndex);
                data.renderPostProcessing = true;
                data.requiresDepthTexture = true;
                data.requiresColorTexture = true;
            }
            ConfigureProbe(rig.transform);
            ConfigureDust(rig.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = rig;
            Debug.Log("Tutorial window lighting configured; glass, shadows, atmosphere, reflection probe and dust saved.");
        }

        private static int ConfigureRenderer()
        {
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath) == null)
                AssetDatabase.CopyAsset("Assets/Settings/PC_Renderer.asset", RendererPath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (!renderer.rendererFeatures.OfType<VolumetricFogRendererFeature>().Any())
            {
                var feature = ScriptableObject.CreateInstance<VolumetricFogRendererFeature>();
                feature.name = "Tutorial Window Volumetric Fog";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                feature.Create();
                EditorUtility.SetDirty(feature);
            }
            var rendererSettings = new SerializedObject(renderer);
            var featureMap = rendererSettings.FindProperty("m_RendererFeatureMap");
            featureMap.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out long localId);
                featureMap.GetArrayElementAtIndex(i).longValue = localId;
            }
            rendererSettings.ApplyModifiedProperties();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            // Same index in each configured quality asset; default renderer is untouched.
            int index = -1;
            foreach (string path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                var serialized = new SerializedObject(pipeline);
                var list = serialized.FindProperty("m_RendererDataList");
                int found = -1;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) found = i;
                if (found < 0)
                {
                    found = list.arraySize;
                    list.InsertArrayElementAtIndex(found);
                    list.GetArrayElementAtIndex(found).objectReferenceValue = renderer;
                    serialized.ApplyModifiedProperties();
                }
                if (index >= 0 && index != found) throw new InvalidOperationException("Renderer indices differ between quality assets.");
                index = found;
            }
            return index;
        }

        private static void ConfigureProbe(Transform parent)
        {
            var go = GameObject.Find("Tutorial_WindowReflection") ?? NewObject("Tutorial_WindowReflection", parent);
            go.transform.position = new Vector3(0, 1.4f, 0);
            var probe = GetOrAdd<ReflectionProbe>(go);
            Undo.RecordObject(probe, "Window reflection probe");
            probe.mode = ReflectionProbeMode.Custom;
            probe.size = new Vector3(4.5f, 3.2f, 5.5f);
            probe.center = Vector3.zero;
            probe.boxProjection = true;
            probe.resolution = 128;
            probe.hdr = true;
            probe.nearClipPlane = 0.1f;
            probe.farClipPlane = 30;
            probe.blendDistance = 0.3f;
            var baked = AssetDatabase.LoadAssetAtPath<Cubemap>(Folder + "/Tutorial_WindowReflection.exr");
            if (baked != null) probe.customBakedTexture = baked;
            EditorUtility.SetDirty(probe);
            // Bake separately via BakeProbe after scene lighting has settled.
        }

        [MenuItem("Tools/Detective Game/Bake Tutorial Window Reflection")]
        public static void BakeProbe()
        {
            var probe = Required("Tutorial_WindowReflection").GetComponent<ReflectionProbe>();
            if (!Lightmapping.BakeReflectionProbe(probe, Folder + "/Tutorial_WindowReflection.exr"))
                throw new InvalidOperationException("Reflection probe bake failed.");
            AssetDatabase.Refresh();
            probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(Folder + "/Tutorial_WindowReflection.exr");
            EditorUtility.SetDirty(probe);
            EditorSceneManager.MarkSceneDirty(probe.gameObject.scene);
            EditorSceneManager.SaveScene(probe.gameObject.scene);
        }

        private static void ConfigureDust(Transform parent)
        {
            var go = GameObject.Find("Tutorial_WindowDust") ?? NewObject("Tutorial_WindowDust", parent);
            go.transform.position = new Vector3(0.3f, 1.15f, 1.1f);
            go.transform.rotation = Quaternion.Euler(25, 195, 0);
            var particles = GetOrAdd<ParticleSystem>(go);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false;
            particles.randomSeed = 19580909;
            var main = particles.main;
            main.duration = 10;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6, 10);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.02f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.009f);
            main.startColor = new Color(1, 0.94f, 0.8f, 0.45f);
            main.gravityModifier = 0;
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = 7;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.2f, 0.55f, 1.6f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.015f;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.03f;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialAsset("M_Tutorial_WindowDust", "DetectiveGame/Tutorial/Window Dust");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            EditorUtility.SetDirty(particles);
        }

        private static T VolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component)) return component;
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static Material MaterialAsset(string name, string shaderName)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader " + shaderName);
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Required(string name) => GameObject.Find(name) ?? throw new InvalidOperationException("Missing " + name);
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }
        private static GameObject NewObject(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create window lighting");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }
    }
}
