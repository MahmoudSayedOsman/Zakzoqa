using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Zakzoqa.EditorTools;

namespace Zakzoqa.Tests
{
    // The transformations as data (Decisions §3) and their sizes (§2). Shadow Escape's characters
    // drifted from the sizes their art was drawn for; here the body, the def and the art are checked
    // against the one size ladder in WorldScale.
    public class TransformationTests
    {
        static TransformationDef[] AllDefs() =>
            AssetDatabase.FindAssets("t:TransformationDef")
                .Select(g => AssetDatabase.LoadAssetAtPath<TransformationDef>(AssetDatabase.GUIDToAssetPath(g)))
                .ToArray();

        // A level must never offer a shape that does nothing.
        [Category("P0")]
        [Test]
        public void EveryTransformationAsset_HasAFormThatPlaysIt()
        {
            TransformationDef[] defs = AllDefs();
            Assert.IsNotEmpty(defs, "no transformation assets: run Zakzoqa > Apply Project Settings.");
            foreach (TransformationDef def in defs)
            {
                Assert.IsTrue(Forms.IsPlayable(def.kind), $"{def.name} is a {def.kind}, which has no form yet.");
                Assert.AreEqual(def.kind, Forms.Create(def.kind).Kind);
            }
        }

        [Test]
        public void AShapeWithoutAForm_SaysSo_InsteadOfDoingNothing()
        {
            foreach (FormKind kind in Enum.GetValues(typeof(FormKind)))
                if (!Forms.IsPlayable(kind))
                    Assert.Throws<NotSupportedException>(() => Forms.Create(kind));
        }

        [Category("P0")]
        [Test]
        public void EveryTransformation_HasSaneNumbers()
        {
            foreach (TransformationDef def in AllDefs())
            {
                Assert.Greater(def.airCapacity, 0, def.name);
                Assert.Greater(def.hitGraceSeconds, 0f, $"{def.name}: with no grace, a pin she touches empties her in one go.");
                Assert.Greater(def.maxSpeed, 0f, def.name);
                Assert.Greater(def.diameter, 0f, def.name);
            }
        }

        // The balloon is the size every other size is measured from.
        [Category("P0")]
        [Test]
        public void TheBalloon_IsZakzoqasLockedSize()
        {
            TransformationDef balloon = AssetDatabase.LoadAssetAtPath<TransformationDef>(PlaygroundSetup.BalloonDefPath);
            Assert.IsNotNull(balloon);
            Assert.AreEqual(WorldScale.ZakzoqaHeight, balloon.diameter, 0.0001f);
        }

        // The art and the body agree: what you see is what the pin touches.
        [Category("P0")]
        [Test]
        public void TheArt_IsDrawnAtTheLockedSizes()
        {
            AssertHeight("zakzoqa_balloon", WorldScale.ZakzoqaHeight);
            AssertHeight("zakzoqa_face", WorldScale.ZakzoqaHeight);
            AssertHeight("pin", WorldScale.PinSoldierHeight);
        }

        static void AssertHeight(string sprite, float worldHeight)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>($"{PlaygroundSetup.PlaceholderDir}/{sprite}.png");
            Assert.IsNotNull(s, sprite);
            // The canvas it was drawn on, not its visible edges: the art is placed by its canvas.
            Assert.AreEqual(worldHeight, s.rect.height / s.pixelsPerUnit, 0.001f, $"{sprite} is drawn at the wrong size.");
        }

        // The face is its own layer, drawn on top, so every shape shares one face (Decisions §2).
        [Test]
        public void ThePrefab_HasTheFaceAsItsOwnLayer_OnTop()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaygroundSetup.PrefabPath);
            Assert.IsNotNull(prefab);
            SpriteRenderer body = prefab.transform.Find("Body").GetComponent<SpriteRenderer>();
            SpriteRenderer face = prefab.transform.Find("Face").GetComponent<SpriteRenderer>();
            Assert.Greater(face.sortingOrder, body.sortingOrder, "the face is drawn under the body.");
        }
    }
}
