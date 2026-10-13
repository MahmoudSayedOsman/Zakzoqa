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
        public void BootScene_IsFirstInTheBuild()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.IsNotEmpty(scenes);
            Assert.AreEqual(ProjectSetup.BootScenePath, scenes[0].path);
        }

        // Scenes are loaded by name (Scenes.cs). A name missing from the build fails at runtime, on a
        // player's phone, so it fails here instead.
        [Test]
        public void EveryNamedScene_IsInTheBuild_WithItsCameraAtTheLockedSize()
        {
            var inBuild = new System.Collections.Generic.HashSet<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) inBuild.Add(s.path);

            foreach (string name in Scenes.All)
            {
                string path = ProjectSetup.ScenePath(name);
                Assert.IsTrue(inBuild.Contains(path), $"{name} is loaded by name but is not in the build.");

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var cams = Object.FindObjectsByType<Camera>();
                Assert.IsNotEmpty(cams, name + " has no camera");
                foreach (var cam in cams)
                {
                    Assert.IsTrue(cam.orthographic, $"{name}/{cam.name} is not orthographic");
                    Assert.AreEqual(WorldScale.OrthographicSize, cam.orthographicSize, 0.0001f, $"{name}/{cam.name}");
                }
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
