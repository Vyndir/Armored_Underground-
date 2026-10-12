using UnityEditor;

namespace AU.EditorTools
{
    /// <summary>Import settings for baked mech textures (colour space, normal maps, size).</summary>
    class MechTexturePostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Art/Mech/v3/textures/")) return;
            var imp = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal.png");
            bool linear = assetPath.EndsWith("_mask.png") || assetPath.EndsWith("_mra.png");
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !(normal || linear);
            imp.mipmapEnabled = true;
            imp.anisoLevel = 8;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
