using NUnit.Framework;

namespace Zakzoqa.Tests
{
    // The air is Zakzoqa's life (Decisions §3). Whole units, so "three hits and you pop" is exactly
    // three, in every transformation.
    public class AirTankTests
    {
        [Category("P0")]
        [Test]
        public void ThreeHits_PopAThreeUnitBalloon_AndPoppingIsAnnouncedOnce()
        {
            AirTank air = new AirTank(3);
            int popped = 0;
            air.Emptied += () => popped++;

            air.Leak(1);
            air.Leak(1);
            Assert.IsFalse(air.IsEmpty);
            air.Leak(1);
            Assert.IsTrue(air.IsEmpty);
            air.Leak(1);

            Assert.AreEqual(1, popped, "popping was announced more than once: the level would offer two revives.");
            Assert.AreEqual(0, air.Air);
        }

        [Test]
        public void ABigHit_TakesOnlyWhatIsThere()
        {
            AirTank air = new AirTank(3);
            Assert.AreEqual(3, air.Leak(10));
            Assert.AreEqual(0, air.Air);
        }

        [Category("P0")]
        [Test]
        public void Tape_FillsUpToFull_AndNoFurther()
        {
            AirTank air = new AirTank(3);
            air.Leak(2);

            Assert.AreEqual(1, air.Refill(1));
            Assert.AreEqual(1, air.Refill(5), "tape overfilled the balloon.");
            Assert.AreEqual(0, air.Refill(1), "tape was used up on a full balloon.");
            Assert.AreEqual(3, air.Air);
        }

        // Popped is final for the level; the way back is "carry on from here", not a pickup.
        [Category("P0")]
        [Test]
        public void Tape_DoesNotBringBackAPoppedBalloon_ButCarryingOnDoes()
        {
            AirTank air = new AirTank(3);
            air.Leak(3);

            Assert.AreEqual(0, air.Refill(1));
            Assert.IsTrue(air.IsEmpty);

            air.Restore();
            Assert.AreEqual(3, air.Air);
        }

        // Changing shape is neither a free heal nor a penalty.
        [Category("P0")]
        [Test]
        public void ANewShape_KeepsTheSameShareOfAir()
        {
            AirTank air = new AirTank(4);
            air.Leak(2);                 // half

            air.Resize(6);
            Assert.AreEqual(3, air.Air, "changing shape healed or hurt her.");
            Assert.AreEqual(6, air.Capacity);
        }

        [Category("P0")]
        [Test]
        public void ANewShape_NeverPopsABalloonThatHasAir()
        {
            AirTank air = new AirTank(10);
            air.Leak(9);                 // one unit left of ten

            air.Resize(3);
            Assert.AreEqual(1, air.Air, "a smaller shape rounded her last bit of air to nothing.");
        }

        [Test]
        public void Changes_AreAnnounced()
        {
            AirTank air = new AirTank(3);
            int last = -1;
            air.Changed += a => last = a;

            air.Leak(1);
            Assert.AreEqual(2, last);
            air.Refill(1);
            Assert.AreEqual(3, last);
        }
    }
}
