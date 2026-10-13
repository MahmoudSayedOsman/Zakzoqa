using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Zakzoqa.Tests
{
    // How the balloon moves and gets hurt (Decisions §3). Physics is stepped by hand
    // (Physics2D.Simulate), so every test is exact, repeatable and takes no real time — and none of
    // them depend on the frame rate of the machine running them.
    public class BalloonMovementTests
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

            def = ScriptableObject.CreateInstance<TransformationDef>();   // the balloon's defaults
            made.Add(def);
            z = Spawn(Vector2.zero);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            Physics2D.simulationMode = previousMode;
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

        void Run(float seconds, InputFrame? everyStep = null)
        {
            for (float t = 0; t < seconds; t += Dt)
            {
                if (everyStep.HasValue) z.Feed(everyStep.Value);
                z.Step(Dt);
                Physics2D.Simulate(Dt);
            }
        }

        void Tap(Vector2 at)
        {
            z.Feed(new InputFrame { Press = true, PointerPress = true, PointerWorld = at, Held = true });
            z.Step(Dt);
            Physics2D.Simulate(Dt);
        }

        // =====================================================================================
        // Floating
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void LeftAlone_SheSinksSlowly()
        {
            Run(2f);
            Assert.Less(z.Position.y, -0.1f, "she does not sink: nothing to fight against.");
            Assert.Greater(z.Position.y, -4f, "she falls like a stone, not a balloon.");
        }

        [Category("P0")]
        [Test]
        public void ATapAboveHer_PuffsHerStraightUp()
        {
            Tap(z.Position + Vector2.up * 3f);
            Run(0.5f);

            Assert.Greater(z.Position.y, 0.5f, "a puff did not lift her.");
            Assert.AreEqual(0f, z.Position.x, 0.01f, "a tap straight above her pushed her sideways.");
        }

        [Category("P0")]
        [Test]
        public void ATapToOneSide_LeansThePuffThatWay()
        {
            Tap(z.Position + new Vector2(-3f, 1f));
            Run(0.5f);
            Assert.Less(z.Position.x, -0.2f, "tapping to her left did not send her left.");
            Assert.Greater(z.Position.y, 0.3f, "a sideways puff stopped lifting her.");

            z = Spawn(new Vector2(10f, 0f));
            Tap(z.Position + new Vector2(3f, 1f));
            Run(0.5f);
            Assert.Greater(z.Position.x, 10.2f, "tapping to her right did not send her right.");
        }

        // A tap within her own width counts as "above her", so a small child aiming at her goes up.
        [Test]
        public void ATapOnHerself_IsStraightUp()
        {
            Tap(z.Position + new Vector2(def.diameter * 0.4f, 0f));
            Run(0.3f);
            Assert.AreEqual(0f, z.Position.x, 0.01f);
        }

        [Test]
        public void Keys_LeanAndSteer()
        {
            z.Feed(new InputFrame { Press = true, Horizontal = 1f });
            z.Step(Dt);
            Physics2D.Simulate(Dt);
            Run(0.5f, new InputFrame { Horizontal = 1f });
            Assert.Greater(z.Position.x, 0.3f);
        }

        // A child hammering the screen climbs steadily, not like a rocket.
        [Category("P0")]
        [Test]
        public void HammeringTheScreen_ClimbsNoFasterThanTheCooldownAllows()
        {
            // A tap on every physics step for a second, counting the puffs by their jump in speed. The
            // speed limit is lifted for this test only, or puffs near the limit would not show.
            def.maxSpeed = 1000f;
            int puffs = 0;
            for (float t = 0; t < 1f; t += Dt)
            {
                float before = z.Velocity.y;
                z.Feed(new InputFrame { Press = true, PointerPress = true, PointerWorld = z.Position + Vector2.up * 5f });
                z.Step(Dt);
                if (z.Velocity.y - before > def.puffStrength * 0.5f) puffs++;
                Physics2D.Simulate(Dt);
            }

            int allowed = Mathf.CeilToInt(1f / def.puffCooldown) + 1;
            Assert.LessOrEqual(puffs, allowed, $"{puffs} puffs in a second from hammering; the cooldown allows {allowed}.");
            Assert.GreaterOrEqual(puffs, allowed - 3, "hammering was throttled far more than the cooldown says.");
        }

        [Category("P0")]
        [Test]
        public void NothingPushesHer_PastTheSpeedLimit()
        {
            for (int i = 0; i < 200; i++)
            {
                z.AddVelocity(Vector2.up * 5f);
                z.Step(Dt);
                Assert.LessOrEqual(z.Velocity.magnitude, def.maxSpeed + 0.001f);
                Physics2D.Simulate(Dt);
            }
        }

        // =====================================================================================
        // Pins
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void APin_LetsAirOut_AndPushesHerAway()
        {
            int before = z.Air.Air;
            Assert.IsTrue(z.TakeHit(1, z.Position + Vector2.left));

            Assert.AreEqual(before - 1, z.Air.Air);
            Assert.Greater(z.Velocity.x, 0f, "she was not pushed away from the pin.");
        }

        // Still touching the pin a moment later must not be a second hit.
        [Category("P0")]
        [Test]
        public void AfterAHit_SheIsSafeForAMoment_AndThenNotAnyMore()
        {
            z.TakeHit(1, Vector2.left);
            Assert.IsFalse(z.TakeHit(1, Vector2.left), "two hits in the same instant.");

            Run(def.hitGraceSeconds * 0.5f);
            Assert.IsFalse(z.TakeHit(1, Vector2.left), "the grace ended early.");

            Run(def.hitGraceSeconds * 0.6f);
            Assert.IsTrue(z.TakeHit(1, Vector2.left), "the grace never ended: sitting on a pin would be safe.");
        }

        [Category("P0")]
        [Test]
        public void EnoughHits_PopHer_OnceAndForAll()
        {
            int popped = 0;
            z.Popped += () => popped++;

            for (int i = 0; i < def.airCapacity; i++)
            {
                z.TakeHit(1, Vector2.left);
                Run(def.hitGraceSeconds + Dt);
            }

            Assert.IsTrue(z.IsPopped);
            Assert.AreEqual(1, popped);

            Vector2 at = z.Position;
            Tap(at + Vector2.up);
            Run(0.5f);
            Assert.AreEqual(at, z.Position, "a popped balloon still flew.");
        }

        [Category("P0")]
        [Test]
        public void CarryingOn_GivesFullAirAndAMomentOfSafety()
        {
            for (int i = 0; i < def.airCapacity; i++) { z.TakeHit(1, Vector2.left); Run(def.hitGraceSeconds + Dt); }
            Assert.IsTrue(z.IsPopped);

            z.Revive();

            Assert.IsFalse(z.IsPopped);
            Assert.AreEqual(def.airCapacity, z.Air.Air);
            Assert.IsFalse(z.TakeHit(1, Vector2.left), "she was hit again the instant she came back.");

            float y = z.Position.y;
            Tap(z.Position + Vector2.up * 3f);
            Run(0.3f);
            Assert.Greater(z.Position.y, y, "she came back but cannot fly.");
        }

        // The real thing: a pin in the scene, a trigger, physics.
        [Category("P0")]
        [Test]
        public void TouchingAPinInTheLevel_Hurts()
        {
            GameObject pin = new GameObject("Pin");
            made.Add(pin);
            pin.transform.position = z.Position + Vector2.up * 0.3f;
            CircleCollider2D col = pin.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.2f;
            pin.AddComponent<Pin>();

            int before = z.Air.Air;
            Run(0.1f);
            Assert.AreEqual(before - 1, z.Air.Air, "touching a pin did nothing.");
        }

        [Test]
        public void Tape_PatchesHer_AndIsUsedUp_ButNotWhenSheIsFull()
        {
            GameObject tape = new GameObject("Tape");
            made.Add(tape);
            tape.transform.position = z.Position;
            BoxCollider2D col = tape.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            tape.AddComponent<TapePickup>();

            Run(0.1f);
            Assert.IsTrue(col.enabled, "tape was used up on a full balloon.");

            z.TakeHit(1, Vector2.down);
            Run(0.1f);
            Assert.AreEqual(def.airCapacity, z.Air.Air, "tape did not patch her.");
            Assert.IsFalse(col.enabled, "the same tape could patch her twice.");
        }

        // =====================================================================================
        // Changing shape
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void ChangingShape_KeepsHerShareOfAir_AndTakesTheNewShapesPhysics()
        {
            z.TakeHit(1, Vector2.left);               // 2 of 3

            TransformationDef heavier = ScriptableObject.CreateInstance<TransformationDef>();
            made.Add(heavier);
            heavier.airCapacity = 6;
            heavier.gravityScale = 1f;
            heavier.diameter = 0.9f;

            TransformationDef seen = null;
            z.Transformed += d => seen = d;
            z.Transform(heavier);

            Assert.AreEqual(4, z.Air.Air, "changing shape healed or hurt her.");
            Assert.AreEqual(1f, z.GetComponent<Rigidbody2D>().gravityScale);
            Assert.AreEqual(0.45f, z.GetComponent<CircleCollider2D>().radius, 0.0001f);
            Assert.AreSame(heavier, seen);
        }
    }
}
