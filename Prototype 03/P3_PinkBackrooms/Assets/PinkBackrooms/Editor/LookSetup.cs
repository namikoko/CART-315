using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PinkBackrooms.EditorTools
{
    /// The "Never Made It Home" look: low render scale with point upscaling (pixels),
    /// chromatic aberration, grain, vignette, a slightly bent lens, and crushed, faded colour.
    public static class LookSetup
    {
        public const string Root = "Assets/PinkBackrooms";
        public const string ProfilePath = Root + "/Settings/PinkBackroomsLook.asset";

        [MenuItem("My Ana/1. Apply Look Settings", priority = 1)]
        public static void ApplyAll()
        {
            PlayerSettings.productName = "My Ana";
            ImportTextures();
            ConfigurePipelineAssets();
            CreateOrUpdateProfile();
            AssetDatabase.SaveAssets();
            Debug.Log("[Pink Backrooms] Look settings applied.");
        }

        /// Point filtering, no mipmaps, no compression: keeps every texel crisp and chunky.
        public static void ImportTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                bool decal = Path.GetFileName(path).StartsWith("stain");
                bool changed = ti.filterMode != FilterMode.Point || ti.mipmapEnabled ||
                               ti.textureCompression != TextureImporterCompression.Uncompressed ||
                               ti.npotScale != TextureImporterNPOTScale.None ||
                               ti.wrapMode != (decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat);
                if (!changed) continue;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.alphaIsTransparency = decal;
                ti.wrapMode = decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                ti.SaveAndReimport();
            }
        }

        public static void ConfigurePipelineAssets()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!asset) continue;
                asset.renderScale = 0.42f;                           // fewer, bigger pixels
                asset.upscalingFilter = UpscalingFilterSelection.Point; // hard pixel edges, no smoothing
                asset.msaaSampleCount = 1;
                var so = new SerializedObject(asset);
                SetBool(so, "m_AdditionalLightShadowsSupported", true);
                SetBool(so, "m_SoftShadowsSupported", false);   // hard shadows suit the low-res look
                SetBool(so, "m_SupportsHDR", true);
                SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
                SetInt(so, "m_AdditionalLightsShadowmapResolution", 4096); // room for the shadowed point lights
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }

        private static void SetBool(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name); if (p != null) p.boolValue = v;
        }

        private static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name); if (p != null) p.intValue = v;
        }

        public static VolumeProfile CreateOrUpdateProfile()
        {
            Directory.CreateDirectory(Root + "/Settings");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var ca = Get<ChromaticAberration>(profile);
            ca.intensity.Override(0.45f);

            var grain = Get<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.5f);
            grain.response.Override(0.75f);

            var vig = Get<Vignette>(profile);
            vig.color.Override(Color.black);
            vig.intensity.Override(0.45f);
            vig.smoothness.Override(0.45f);

            var col = Get<ColorAdjustments>(profile);
            col.postExposure.Override(0f);
            col.contrast.Override(18f);
            col.saturation.Override(-18f);
            col.colorFilter.Override(new Color(1f, 0.9f, 0.95f));

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.65f);

            var lens = Get<LensDistortion>(profile);
            lens.intensity.Override(-0.15f);

            var tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.None);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            var c = profile.Add<T>();
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }
    }
}
