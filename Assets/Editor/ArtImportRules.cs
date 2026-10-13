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
            // One file, one sprite, the size of its canvas. Unity 6 imports new sprites as Multiple
            // and slices them to their visible edges, which made a 240-pixel balloon 238 — the size
            // drift Shadow Escape's art suffered, caught by TransformationTests. Characters drawn in
            // parts for bones come in as PSB through the PSD importer, not through here.
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = WorldScale.PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
        }
    }
}
