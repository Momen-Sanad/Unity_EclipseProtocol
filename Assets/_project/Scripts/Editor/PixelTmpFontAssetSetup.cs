using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace EclipseProtocol.Editor
{
    public static class PixelTmpFontAssetSetup
    {
        private const string SourceFontPath = "Assets/Minecraftia-Regular.ttf";
        private const string OutputFolderPath = "Assets/_project/Fonts";
        private const string OutputFontAssetPath = OutputFolderPath + "/Minecraftia Pixel TMP.asset";
        private const int SamplingPointSize = 16;
        private const int AtlasPadding = 0;
        private const int AtlasSize = 512;
        private const string HudCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789:/+-. ";

        [InitializeOnLoadMethod]
        private static void EnsurePixelFontAssetOnLoad()
        {
            EditorApplication.delayCall += EnsurePixelFontAsset;
        }

        [MenuItem("Tools/Eclipse Protocol/Rebuild Pixel TMP Font")]
        public static void RebuildPixelFontAsset()
        {
            EnsurePixelFontAsset(true);
        }

        private static void EnsurePixelFontAsset()
        {
            EnsurePixelFontAsset(false);
        }

        private static void EnsurePixelFontAsset(bool forceRebuild)
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[PixelTmpFontAssetSetup] Missing source font at {SourceFontPath}.");
                return;
            }

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath);
            if (fontAsset == null)
            {
                Directory.CreateDirectory(OutputFolderPath);
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    SamplingPointSize,
                    AtlasPadding,
                    GlyphRenderMode.RASTER,
                    AtlasSize,
                    AtlasSize,
                    AtlasPopulationMode.Dynamic,
                    true);

                if (fontAsset == null)
                {
                    Debug.LogError("[PixelTmpFontAssetSetup] Failed to create the Minecraftia TMP font asset.");
                    return;
                }

                fontAsset.name = "Minecraftia Pixel TMP";
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = "Minecraftia Pixel TMP Material";
                }

                if (fontAsset.atlasTexture != null)
                {
                    fontAsset.atlasTexture.name = "Minecraftia Pixel TMP Atlas";
                }

                AssetDatabase.CreateAsset(fontAsset, OutputFontAssetPath);
                if (fontAsset.material != null)
                {
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }

                if (fontAsset.atlasTexture != null)
                {
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                }
            }
            else if (forceRebuild)
            {
                Debug.Log("[PixelTmpFontAssetSetup] Existing TMP font asset kept to preserve scene references. Delete it manually, then run this menu item again to rebuild from scratch.");
            }

            ConfigurePixelFontAsset(fontAsset);
            if (!fontAsset.TryAddCharacters(HudCharacters, out string missingCharacters) && !string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogWarning($"[PixelTmpFontAssetSetup] Missing pixel HUD characters: {missingCharacters}");
            }

            EditorUtility.SetDirty(fontAsset);
            if (fontAsset.material != null)
            {
                EditorUtility.SetDirty(fontAsset.material);
            }

            if (fontAsset.atlasTexture != null)
            {
                EditorUtility.SetDirty(fontAsset.atlasTexture);
            }

            AssetDatabase.SaveAssets();
        }

        private static void ConfigurePixelFontAsset(TMP_FontAsset fontAsset)
        {
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
            SerializedProperty clearDynamicDataOnBuild = serializedFontAsset.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearDynamicDataOnBuild != null)
            {
                clearDynamicDataOnBuild.boolValue = false;
                serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
            }

            Texture2D atlasTexture = fontAsset.atlasTexture;
            if (atlasTexture != null)
            {
                atlasTexture.filterMode = FilterMode.Point;
                atlasTexture.wrapMode = TextureWrapMode.Clamp;
                atlasTexture.anisoLevel = 0;
            }

            if (fontAsset.material == null)
            {
                return;
            }

            fontAsset.material.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            fontAsset.material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            fontAsset.material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
            fontAsset.material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            fontAsset.material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        }
    }
}
