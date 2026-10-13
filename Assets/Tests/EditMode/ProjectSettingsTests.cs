using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zakzoqa.EditorTools;

namespace Zakzoqa.Tests
{
    // Guards the locked decisions in Docs/Decisions.md §1–2. If one of these fails, either the
    // change was a mistake or the decision changed — and then Decisions.md changes first.
    [Category("P0")]
    public class ProjectSettingsTests
    {
        [Test]
        public void Identity_MatchesDecisions()
        {
            Assert.AreEqual(ProjectSetup.CompanyName, PlayerSettings.companyName);
            Assert.AreEqual(ProjectSetup.ProductName, PlayerSettings.productName);
            Assert.AreEqual(ProjectSetup.AndroidId, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
        }

        [Test]
        public void Orientation_IsLandscapeOnly()
        {
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortrait, "portrait allowed");
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortraitUpsideDown, "upside-down portrait allowed");
            Assert.IsTrue(PlayerSettings.allowedAutorotateToLandscapeLeft || PlayerSettings.allowedAutorotateToLandscapeRight);
        }

        [Test]
        public void BootScene_IsFirstAndCameraIsAtTheLockedSize()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.IsNotEmpty(scenes);
            Assert.AreEqual(ProjectSetup.BootScenePath, scenes[0].path);

            EditorSceneManager.OpenScene(ProjectSetup.BootScenePath, OpenSceneMode.Single);
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Assert.IsNotEmpty(cams, "Boot has no camera");
            foreach (var cam in cams)
            {
                Assert.IsTrue(cam.orthographic, cam.name + " is not orthographic");
                Assert.AreEqual(WorldScale.OrthographicSize, cam.orthographicSize, 0.0001f, cam.name);
            }
        }

        [Test]
        public void ArtImports_AtTheLockedPixelsPerUnit()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(WorldScale.PixelsPerUnit, importer.spritePixelsPerUnit, 0.0001f, path);
            }
        }
    }
}
