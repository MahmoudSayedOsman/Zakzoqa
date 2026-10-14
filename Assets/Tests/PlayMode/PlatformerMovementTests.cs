using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Zakzoqa.Tests
{
    // How Zakzoqa moves, floats, puffs and gets hurt (Decisions §3: a classic platformer). Physics is
    // stepped by hand (Physics2D.Simulate), so every test is exact, repeatable and frame-rate free.
    //
    // The forgiving parts (coyote time, jump buffer, the cut on a released jump) are tested as hard as
    // the rest: they are what makes a platformer feel fair to a child, and they are the first thing
    // a "small tidy-up" breaks without anyone noticing.
    public class PlatformerMovementTests
    {
        const float Dt = 1f / 50f;
        const float FloorEnd = 50f;

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
            Box("Floor", new Vector2(0f, -0.5f), new Vector2(FloorEnd * 2f, 1f));   // top at y = 0
            z = Spawn(new Vector2(0f, def.diameter * 0.5f + 0.01f));
            Run(0.2f);                                                    // settle onto the floor
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            Physics2D.simulationMode = previousMode;
        }

        // --- fixture -----------------------------------------------------------------------------

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

        // Presses jump and holds it for `hold` seconds; returns the highest point reached.
        float Jump(float hold, float watch = 1.5f)
        {
            float startY = z.Position.y, top = startY;
            Step(new InputFrame { JumpPressed = true, JumpHeld = true });
            for (float t = Dt; t < watch; t += Dt)
            {
                Step(new InputFrame { JumpHeld = t < hold });
                top = Mathf.Max(top, z.Position.y);
            }
            return top - startY;
        }

        static InputFrame Moving(float dir) => new InputFrame { Move = dir };

        // =====================================================================================
        // Running
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void SheStandsOnTheFloor()
        {
            Assert.IsTrue(z.IsGrounded);
            float y = z.Position.y;
            Run(1f);
            Assert.AreEqual(y, z.Position.y, 0.02f, "she sinks into the floor or drifts off it.");
        }

        [Category("P0")]
        [Test]
        public void SheRunsBothWays_AtHerRunSpeed_AndStopsWhenLetGo()
        {
            Run(0.5f, Moving(1f));
            Assert.AreEqual(def.runSpeed, z.Velocity.x, 0.05f, "she does not reach her run speed.");
            Assert.AreEqual(1f, z.Facing);

            Run(0.3f);
            Assert.AreEqual(0f, z.Velocity.x, 0.05f, "she slides on after the stick is let go.");

            Run(0.5f, Moving(-1f));
            Assert.AreEqual(-def.runSpeed, z.Velocity.x, 0.05f);
            Assert.AreEqual(-1f, z.Facing, "she runs left facing right: the puff would go the wrong way.");
        }

        // =====================================================================================
        // Jumping
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void AFullJump_ReachesTheJumpHeight()
        {
            float height = Jump(hold: 1f);
            Assert.AreEqual(def.jumpHeight, height, 0.2f, "the jump height in the asset is not the jump height in the game.");
        }

        // A tap is a hop, a hold is a jump: the control that makes small gaps and high ledges both fair.
        [Category("P0")]
        [Test]
        public void ATap_IsAHop()
        {
            float hop = Jump(hold: Dt);
            Assert.Greater(hop, 0.2f, "a tap did not jump at all.");
            Assert.Less(hop, def.jumpHeight * 0.6f, "letting go early still gave a full jump.");
        }

        [Category("P0")]
        [Test]
        public void ThereIsNoJumpInTheAir()
        {
            Step(new InputFrame { JumpPressed = true, JumpHeld = true });
            Run(0.3f, new InputFrame { JumpHeld = true });
            Assert.IsFalse(z.IsGrounded, "precondition: she should be in the air.");

            float vy = z.Velocity.y;
            Step(new InputFrame { JumpPressed = true, JumpHeld = true });
            Assert.LessOrEqual(z.Velocity.y, vy + 0.01f, "a second jump in mid-air.");
        }

        // Running off a ledge and pressing jump a moment late still jumps. Without it, players swear
        // they pressed in time — and they did.
        [Category("P0")]
        [Test]
        public void AJumpJustAfterTheLedge_StillCounts()
        {
            Assert.IsTrue(RunOffTheLedgeThenJump(afterLeaving: def.coyoteTime * 0.4f), "a jump a moment after the ledge was ignored.");
        }

        [Test]
        public void AJumpLongAfterTheLedge_DoesNot()
        {
            Assert.IsFalse(RunOffTheLedgeThenJump(afterLeaving: def.coyoteTime * 3f), "she jumped off thin air.");
        }

        bool RunOffTheLedgeThenJump(float afterLeaving)
        {
            z = Spawn(new Vector2(FloorEnd - 1.5f, def.diameter * 0.5f + 0.01f));
            Run(0.2f);
            while (z.IsGrounded) Step(Moving(1f));
            Run(afterLeaving, Moving(1f));
            Step(new InputFrame { Move = 1f, JumpPressed = true, JumpHeld = true });
            return z.Velocity.y > 1f;
        }

        // Jump pressed just before landing happens on landing.
        [Category("P0")]
        [Test]
        public void AJumpPressedJustBeforeLanding_HappensOnLanding()
        {
            z = Spawn(new Vector2(5f, 1.5f));
            while (z.Position.y > def.diameter * 0.5f + 0.25f) Step(default);   // falling, just above the floor

            Step(new InputFrame { JumpPressed = true, JumpHeld = true });
            float top = z.Position.y;
            for (int i = 0; i < 30; i++)
            {
                Step(new InputFrame { JumpHeld = true });
                top = Mathf.Max(top, z.Position.y);
            }
            Assert.Greater(top, 1.5f, "a jump pressed a moment before landing was lost.");
        }

        // A wall is not a floor: touching one in mid-air must not allow a jump.
        [Category("P0")]
        [Test]
        public void AWall_IsNotGround()
        {
            Box("Wall", new Vector2(3f, 5f), new Vector2(1f, 10f));
            Step(new InputFrame { JumpPressed = true, JumpHeld = true });
            for (int i = 0; i < 15; i++) Step(new InputFrame { Move = 1f, JumpHeld = true });
            Assert.IsFalse(z.IsGrounded, "pressing against a wall counted as standing on the ground.");
        }

        // =====================================================================================
        // Floating on helium
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void HoldingJumpOnTheWayDown_FloatsAndSpendsHelium()
        {
            z = Spawn(new Vector2(5f, 8f));
            Run(0.3f);                                                    // falling fast now
            float before = z.Helium.Seconds;

            Run(0.5f, new InputFrame { JumpHeld = true });

            Assert.GreaterOrEqual(z.Velocity.y, -def.floatFallSpeed - 0.01f, "holding jump did not slow her fall.");
            Assert.AreEqual(before - 0.5f, z.Helium.Seconds, 0.03f, "floating did not spend helium by the second.");
        }

        [Category("P0")]
        [Test]
        public void WithoutHelium_SheFalls()
        {
            z = Spawn(new Vector2(5f, 10f));
            z.Helium.Spend(100f);
            Run(0.3f);
            Run(0.3f, new InputFrame { JumpHeld = true });
            Assert.Less(z.Velocity.y, -def.floatFallSpeed * 2f, "she floated on an empty tank.");
        }

        // Floating lets her cross a gap a jump cannot: the reason helium is worth finding.
        [Test]
        public void Floating_CarriesHerFurtherThanAJump()
        {
            float jumpOnly = DistanceOfJumpFromHeight(floatAtTop: false);
            float floated = DistanceOfJumpFromHeight(floatAtTop: true);
            Assert.Greater(floated, jumpOnly * 1.5f, "floating barely helps: nobody will bother with helium.");
        }

        float DistanceOfJumpFromHeight(bool floatAtTop)
        {
            z = Spawn(new Vector2(-40f, def.diameter * 0.5f + 0.01f));
            Run(0.3f, Moving(1f));
            float startX = z.Position.x;
            Step(new InputFrame { Move = 1f, JumpPressed = true, JumpHeld = true });
            do Step(new InputFrame { Move = 1f, JumpHeld = floatAtTop || z.Velocity.y > 0f });
            while (!z.IsGrounded);
            return z.Position.x - startX;
        }

        [Test]
        public void HeliumInTheLevel_RefillsTheTank_ButWaitsWhenItIsFull()
        {
            GameObject bubble = Box("Helium", z.Position, Vector2.one, trigger: true);
            bubble.AddComponent<HeliumPickup>();
            Collider2D col = bubble.GetComponent<Collider2D>();

            Run(0.1f);
            Assert.IsTrue(col.enabled, "helium was used up on a full tank.");

            z.Helium.Spend(0.5f);
            Run(0.1f);
            Assert.AreEqual(def.heliumCapacity, z.Helium.Seconds, 0.001f);
            Assert.IsFalse(col.enabled, "the same bubble could be taken twice.");
        }

        // =====================================================================================
        // The puff
        // =====================================================================================

        class PuffTarget : MonoBehaviour, IPuffable
        {
            public int Times;
            public Vector2 Direction;
            public void Puffed(Vector2 direction, float force) { Times++; Direction = direction; }
        }

        PuffTarget TargetAt(Vector2 at)
        {
            GameObject go = Box("Target", at, new Vector2(0.6f, 0.6f), trigger: true);
            return go.AddComponent<PuffTarget>();
        }

        [Category("P0")]
        [Test]
        public void ThePuff_PushesWhatIsInFront_NotBehind()
        {
            PuffTarget front = TargetAt(z.Position + Vector2.right * 1.2f);
            PuffTarget behind = TargetAt(z.Position + Vector2.left * 1.2f);

            Step(new InputFrame { AbilityPressed = true });

            Assert.AreEqual(1, front.Times, "the puff missed what was right in front of her.");
            Assert.AreEqual(Vector2.right, front.Direction);
            Assert.AreEqual(0, behind.Times, "the puff hit something behind her.");
        }

        [Test]
        public void ThePuff_GoesTheWaySheFaces()
        {
            PuffTarget left = TargetAt(z.Position + Vector2.left * 1.2f);
            Step(Moving(-1f));
            Step(new InputFrame { AbilityPressed = true });
            Assert.AreEqual(1, left.Times);
            Assert.AreEqual(Vector2.left, left.Direction);
        }

        [Category("P0")]
        [Test]
        public void ThePuff_CannotBeSpammed()
        {
            PuffTarget front = TargetAt(z.Position + Vector2.right * 1.2f);
            for (int i = 0; i < 10; i++) Step(new InputFrame { AbilityPressed = true });
            Assert.AreEqual(1, front.Times, "ten puffs in a fifth of a second.");

            Run(def.puffCooldown);
            Step(new InputFrame { AbilityPressed = true });
            Assert.AreEqual(2, front.Times, "the puff never came back.");
        }

        // =====================================================================================
        // Being hurt
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void AHit_LetsAirOut_AndKnocksHerAwayAndUp()
        {
            int before = z.Air.Air;
            Assert.IsTrue(z.TakeHit(1, z.Position + Vector2.right));

            Assert.AreEqual(before - 1, z.Air.Air);
            Assert.Less(z.Velocity.x, 0f, "she was not pushed away from the pin.");
            Assert.Greater(z.Velocity.y, 0f, "she was not lifted off the pin.");
        }

        // For a moment after a hit the knockback carries her, whatever the player presses.
        [Category("P0")]
        [Test]
        public void AfterAHit_TheKnockbackWins_ForAMoment()
        {
            z.TakeHit(1, z.Position + Vector2.left);
            Step(Moving(-1f));
            Assert.Greater(z.Velocity.x, 0f, "the player cancelled the knockback at once: pins would not push.");

            Run(def.hitStunSeconds + Dt, Moving(-1f));
            Run(0.3f, Moving(-1f));
            Assert.Less(z.Velocity.x, 0f, "the player never got control back.");
        }

        [Category("P0")]
        [Test]
        public void AfterAHit_SheIsSafeForAMoment_AndThenNotAnyMore()
        {
            z.TakeHit(1, Vector2.left);
            Assert.IsFalse(z.TakeHit(1, Vector2.left), "two hits in the same instant.");

            Run(def.hitGraceSeconds * 0.5f);
            Assert.IsFalse(z.TakeHit(1, Vector2.left), "the grace ended early.");

            Run(def.hitGraceSeconds * 0.6f);
            Assert.IsTrue(z.TakeHit(1, Vector2.left), "the grace never ended: standing on a pin would be safe.");
        }

        [Category("P0")]
        [Test]
        public void EnoughHits_PopHer_OnceAndForAll()
        {
            int popped = 0;
            z.Popped += () => popped++;
            for (int i = 0; i < def.airCapacity; i++) { z.TakeHit(1, Vector2.left); Run(def.hitGraceSeconds + Dt); }

            Assert.IsTrue(z.IsPopped);
            Assert.AreEqual(1, popped);

            Vector2 at = z.Position;
            Step(new InputFrame { JumpPressed = true, JumpHeld = true, Move = 1f });
            Run(0.5f, Moving(1f));
            Assert.AreEqual(at, z.Position, "a popped balloon still moved.");
        }

        [Category("P0")]
        [Test]
        public void CarryingOn_GivesFullAirAndHelium_AndAMomentOfSafety()
        {
            z.Helium.Spend(1f);
            for (int i = 0; i < def.airCapacity; i++) { z.TakeHit(1, Vector2.left); Run(def.hitGraceSeconds + Dt); }

            z.Revive();

            Assert.IsFalse(z.IsPopped);
            Assert.AreEqual(def.airCapacity, z.Air.Air);
            Assert.AreEqual(def.heliumCapacity, z.Helium.Seconds, 0.001f);
            Assert.IsFalse(z.TakeHit(1, Vector2.left), "she was hit again the instant she came back.");
            float x = z.Position.x;
            Run(0.3f, Moving(1f));
            Assert.Greater(z.Position.x, x, "she came back but cannot move.");
        }

        [Category("P0")]
        [Test]
        public void TouchingAPinInTheLevel_Hurts()
        {
            GameObject pin = Box("Pin", z.Position + Vector2.right * 0.5f, new Vector2(0.3f, 0.6f), trigger: true);
            pin.AddComponent<Pin>();

            int before = z.Air.Air;
            Run(0.1f);
            Assert.AreEqual(before - 1, z.Air.Air, "touching a pin did nothing.");
        }

        [Test]
        public void Tape_PatchesHer_AndIsUsedUp_ButNotWhenSheIsFull()
        {
            GameObject tape = Box("Tape", z.Position, Vector2.one, trigger: true);
            tape.AddComponent<TapePickup>();
            Collider2D col = tape.GetComponent<Collider2D>();

            Run(0.1f);
            Assert.IsTrue(col.enabled, "tape was used up on a full balloon.");

            z.TakeHit(1, z.Position + Vector2.up);
            Run(0.1f);
            Assert.AreEqual(def.airCapacity, z.Air.Air, "tape did not patch her.");
            Assert.IsFalse(col.enabled, "the same tape could patch her twice.");
        }

        // =====================================================================================
        // Changing shape
        // =====================================================================================

        [Category("P0")]
        [Test]
        public void ChangingShape_KeepsHerShareOfAirAndHelium_AndTakesTheNewShapesBody()
        {
            z.TakeHit(1, Vector2.left);               // 2 of 3 air
            z.Helium.Spend(def.heliumCapacity / 2f);  // half the helium

            TransformationDef other = ScriptableObject.CreateInstance<TransformationDef>();
            made.Add(other);
            other.airCapacity = 6;
            other.heliumCapacity = 4f;
            other.gravityScale = 5f;
            other.diameter = 0.9f;

            TransformationDef seen = null;
            z.Transformed += d => seen = d;
            z.Transform(other);

            Assert.AreEqual(4, z.Air.Air, "changing shape healed or hurt her.");
            Assert.AreEqual(2f, z.Helium.Seconds, 0.001f, "changing shape filled or emptied her helium.");
            Assert.AreEqual(5f, z.GetComponent<Rigidbody2D>().gravityScale);
            Assert.AreEqual(0.45f, z.GetComponent<CircleCollider2D>().radius, 0.0001f);
            Assert.AreSame(other, seen);
        }
    }
}
