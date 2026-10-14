using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Zakzoqa.Tests
{
    // PROTOTYPE (Design.md §14): "Zakzoqa controls the air in her body". These pin what the prototype
    // promises, so playing it tests the idea and not a bug. If the idea is not kept, this file goes
    // with the rest of the prototype.
    [Category("Prototype")]
    public class AirControlPrototypeTests
    {
        const float Dt = 1f / 50f;

        SimulationMode2D previousMode;
        readonly List<Object> made = new List<Object>();
        TransformationDef def;
        ZakzoqaBody z;

        [SetUp]
        public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;

            def = ScriptableObject.CreateInstance<TransformationDef>();
            def.airControl = true;
            made.Add(def);
            Box("Floor", new Vector2(0f, -0.5f), new Vector2(100f, 1f));
            z = Spawn(new Vector2(0f, def.diameter * 0.5f + 0.01f));
            Run(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            Physics2D.simulationMode = previousMode;
        }

        GameObject Box(string name, Vector2 centre, Vector2 size, bool trigger = false)
        {
            GameObject go = new GameObject(name);
            made.Add(go);
            go.transform.position = centre;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            col.isTrigger = trigger;
            return go;
        }

        ZakzoqaBody Spawn(Vector2 at)
        {
            GameObject go = new GameObject("Zakzoqa");
            made.Add(go);
            go.transform.position = at;
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<CircleCollider2D>();
            ZakzoqaBody body = go.AddComponent<ZakzoqaBody>();
            body.SetInput(null);
            body.Transform(def);
            return body;
        }

        void Step(InputFrame input)
        {
            z.Feed(input);
            z.Step(Dt);
            Physics2D.Simulate(Dt);
        }

        void Run(float seconds, InputFrame input = default)
        {
            for (float t = 0; t < seconds - 0.0001f; t += Dt) Step(input);
        }

        float Radius => z.GetComponent<CircleCollider2D>().radius;

        // --- size ----------------------------------------------------------------------------------

        [Test]
        public void TheSizeButton_LetsAirOut_AndTakesItBackIn()
        {
            Step(new InputFrame { SizePressed = true });
            Assert.IsTrue(z.IsSmall);
            Assert.AreEqual(def.smallDiameter * 0.5f, Radius, 0.001f);
            Assert.AreEqual(def.smallGravityScale, z.GravityScale);
            Assert.IsFalse(z.CanFloat, "she floats with no air in her.");

            Run(0.3f);
            Step(new InputFrame { SizePressed = true });
            Assert.IsFalse(z.IsSmall);
            Assert.AreEqual(def.diameter * 0.5f, Radius, 0.001f);
        }

        [Test]
        public void UnderALowCeiling_SheCannotGrowBack()
        {
            Step(new InputFrame { SizePressed = true });
            Run(0.3f);
            Box("Ceiling", new Vector2(0f, 0.85f + 0.5f), new Vector2(4f, 1f));   // 0.85 of room above the floor

            bool blocked = false;
            z.GrowBlocked += () => blocked = true;
            Step(new InputFrame { SizePressed = true });

            Assert.IsTrue(z.IsSmall, "she grew into the ceiling.");
            Assert.IsTrue(blocked, "nothing says why the button did nothing.");
        }

        [Test]
        public void Small_SheFitsUnderAWallThatStopsHerBig()
        {
            Box("Low wall", new Vector2(4f, 0.85f + 3f), new Vector2(1f, 6f));     // gap of 0.85 at the bottom

            Run(1.5f, new InputFrame { Move = 1f });
            Assert.Less(z.Position.x, 4f, "big, she went under a gap half her size.");

            Step(new InputFrame { SizePressed = true, Move = 1f });
            Run(1.5f, new InputFrame { Move = 1f });
            Assert.Greater(z.Position.x, 5f, "small, she still could not get under.");
        }

        [Test]
        public void HerSize_FollowsHerAir()
        {
            float full = Radius;
            z.TakeHit(1, Vector2.left);
            Assert.Less(Radius, full, "a hit did not show on her.");

            Run(def.hitGraceSeconds + Dt);
            z.TakeHit(1, Vector2.left);
            Assert.AreEqual(def.diameter * 0.5f * Mathf.Lerp(def.emptiestScale, 1f, 1f / 3f), Radius, 0.001f);

            z.Patch(2);
            Assert.AreEqual(full, Radius, 0.001f, "tape did not bring her size back.");
        }

        [Test]
        public void TheArt_IsScaledToHerSize()
        {
            GameObject art = new GameObject("Body");
            art.transform.SetParent(z.transform, false);
            Step(new InputFrame { SizePressed = true });
            Assert.AreEqual(def.smallDiameter / WorldScale.ZakzoqaHeight, art.transform.localScale.x, 0.001f);
        }

        // Without air control the agreed balloon is untouched.
        [Test]
        public void WithoutAirControl_TheSizeButtonDoesNothing()
        {
            def.airControl = false;
            z.Transform(def);
            Step(new InputFrame { SizePressed = true });
            Assert.IsFalse(z.IsSmall);
            Assert.AreEqual(def.diameter * 0.5f, Radius, 0.001f);
        }

        // --- the jet -------------------------------------------------------------------------------

        [Test]
        public void InTheAir_TheAbilityJets_AndSpendsHelium()
        {
            Step(new InputFrame { JumpPressed = true, JumpHeld = true });
            Run(0.2f, new InputFrame { JumpHeld = true });
            float helium = z.Helium.Seconds;
            float x = z.Position.x;

            Step(new InputFrame { AbilityPressed = true, Move = 1f });
            Run(def.jetSeconds);

            Assert.Greater(z.Position.x - x, def.jetSpeed * def.jetSeconds * 0.8f, "the jet did not carry her.");
            Assert.AreEqual(helium - def.jetHeliumCost, z.Helium.Seconds, 0.01f);
            Assert.AreEqual(def.airCapacity, z.Air.Air, "the jet cost her life, which the prototype decided against.");
        }

        [Test]
        public void JumpThenJetUp_ClearsAStepNoJumpCan()
        {
            float top = 0f;
            Step(new InputFrame { JumpPressed = true, JumpHeld = true });
            Run(0.3f, new InputFrame { JumpHeld = true });
            Step(new InputFrame { AbilityPressed = true, JumpHeld = true });
            for (int i = 0; i < 60; i++) { Step(default); top = Mathf.Max(top, z.Position.y); }

            Assert.Greater(top - def.diameter * 0.5f, def.jumpHeight + 1.5f);
        }

        [Test]
        public void OnTheGround_TheAbilityIsStillThePuff()
        {
            float helium = z.Helium.Seconds;
            Step(new InputFrame { AbilityPressed = true, Move = 1f });
            Assert.AreEqual(helium, z.Helium.Seconds, 0.001f, "she jetted from the ground.");
        }

        [Test]
        public void WithNoHelium_ThereIsNoJet()
        {
            z.Helium.Spend(100f);
            Step(new InputFrame { JumpPressed = true, JumpHeld = true });
            Run(0.2f, new InputFrame { JumpHeld = true });
            float vx = z.Velocity.x;
            Step(new InputFrame { AbilityPressed = true, Move = 1f });
            Assert.Less(z.Velocity.x, def.jetSpeed * 0.5f);
        }

        // --- wind and weak floors -----------------------------------------------------------------

        void WindAroundHer() =>
            Box("Wind", new Vector2(0f, 3f), new Vector2(3f, 6f), trigger: true).AddComponent<WindColumn>();

        [Test]
        public void TheWind_LiftsHerBig()
        {
            WindAroundHer();
            Run(1f);
            Assert.Greater(z.Position.y, 2f, "full of air, the wind did not lift her.");
        }

        [Test]
        public void TheWind_LeavesHerSmall()
        {
            Step(new InputFrame { SizePressed = true });
            Run(0.3f);
            float y = z.Position.y;
            WindAroundHer();
            Run(1f);
            Assert.AreEqual(y, z.Position.y, 0.1f, "small and heavy, the wind still lifted her.");
        }

        [Test]
        public void AWeakFloor_BreaksUnderHerSmall_LandingHard_ButHoldsHerBig()
        {
            GameObject weak = Box("Weak", new Vector2(30f, 2.75f), new Vector2(3f, 0.5f));
            WeakFloor floor = weak.AddComponent<WeakFloor>();

            z = Spawn(new Vector2(30f, 6f));
            Run(1f);
            Assert.IsFalse(floor.IsBroken, "a full balloon broke the floor.");

            Step(new InputFrame { SizePressed = true });
            Step(new InputFrame { JumpPressed = true, JumpHeld = true });
            Run(1f, new InputFrame { JumpHeld = true });
            Assert.IsTrue(floor.IsBroken, "small and heavy, landing hard did not break it.");
        }
    }
}
