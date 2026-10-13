using UnityEditor;
using UnityEngine;

namespace Zakzoqa.EditorTools
{
    // Every texture under Assets/Art imports as a sprite at the locked pixels-per-unit
    // (Decisions §2). Shadow Escape set these by hand per sheet and ended up with one sheet at
    // half resolution; here nobody gets to choose.
    public class ArtImportRules : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = WorldScale.PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
        }
    }
}
