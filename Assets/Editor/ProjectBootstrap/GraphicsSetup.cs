using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectBootstrap
{
    // Configures the render pipeline and the office post-processing stack.
    // Kept in source so the look is reproducible instead of hand-tweaked in inspectors.
    public static class GraphicsSetup
    {
        public const string ProfilePath = "Assets/Settings/OfficePostProcessing.asset";
        const string PipelinePath = "Assets/Settings/PC_RPAsset.asset";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";

        [MenuItem("Office Imposter/Apply Graphics Settings")]
        public static void Apply()
        {
            ConfigurePipeline();
            ConfigureAmbientOcclusion();
            BuildPostProcessingProfile();
            AssetDatabase.SaveAssets();
            Debug.Log("[GraphicsSetup] Applied");
        }

        public static void ApplyFromCommandLine()
        {
            try
            {
                Apply();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GraphicsSetup] Failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        static void ConfigurePipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                Debug.LogWarning($"[GraphicsSetup] No pipeline asset at {PipelinePath}");
                return;
            }

            var so = new SerializedObject(asset);
            Set(so, "m_MSAA", 4);                                  // smooth edges on all the box geometry
            Set(so, "m_ShadowDistance", 38f);                      // tighter cascades = sharper indoor shadows
            Set(so, "m_AdditionalLightsPerObjectLimit", 8);        // ceiling fixtures overlap a lot
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_MainLightShadowmapResolution", 4096);
            Set(so, "m_AdditionalLightsShadowmapResolution", 2048);
            Set(so, "m_Cascade4Split", new Vector3(0.06f, 0.16f, 0.38f));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void ConfigureAmbientOcclusion()
        {
            foreach (Object entry in AssetDatabase.LoadAllAssetsAtPath(RendererPath))
            {
                if (entry == null || entry.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;

                var so = new SerializedObject(entry);
                Set(so, "m_Settings.Intensity", 1.1f);
                Set(so, "m_Settings.Radius", 0.28f);
                Set(so, "m_Settings.Falloff", 80f);
                Set(so, "m_Settings.Downsample", false);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(entry);
            }
        }

        static void Set(SerializedObject so, string path, object value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property == null) return;

            switch (value)
            {
                case int i: property.intValue = i; break;
                case float f: property.floatValue = f; break;
                case bool b: property.boolValue = b; break;
                case Vector3 v: property.vector3Value = v; break;
            }
        }

        static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.TryGet(out T component) ? component : profile.Add<T>(true);
        }

        static void BuildPostProcessingProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var tonemapping = Override<Tonemapping>(profile);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            var bloom = Override<Bloom>(profile);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.15f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.38f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.68f;
            bloom.tint.overrideState = true;
            bloom.tint.value = new Color(1f, 0.96f, 0.88f);

            var color = Override<ColorAdjustments>(profile);
            color.postExposure.overrideState = true;
            color.postExposure.value = -0.35f;
            color.contrast.overrideState = true;
            color.contrast.value = 14f;
            color.saturation.overrideState = true;
            color.saturation.value = 10f;

            var whiteBalance = Override<WhiteBalance>(profile);
            whiteBalance.temperature.overrideState = true;
            whiteBalance.temperature.value = 9f;   // fluorescent office warmth

            var vignette = Override<Vignette>(profile);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.31f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.4f;

            var grain = Override<FilmGrain>(profile);
            grain.type.overrideState = true;
            grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.overrideState = true;
            grain.intensity.value = 0.18f;

            var aberration = Override<ChromaticAberration>(profile);
            aberration.intensity.overrideState = true;
            aberration.intensity.value = 0.09f;

            EditorUtility.SetDirty(profile);
        }
    }
}
