using NUnit.Framework;

namespace Zakzoqa.Tests
{
    // Helium is floating time (Decisions §3), spent by the second and refilled by bubbles.
    public class HeliumTankTests
    {
        [Category("P0")]
        [Test]
        public void Floating_SpendsWhatIsThere_AndNoMore()
        {
            HeliumTank h = new HeliumTank(1.5f);
            Assert.AreEqual(1f, h.Spend(1f), 0.0001f);
            Assert.AreEqual(0.5f, h.Spend(2f), 0.0001f, "it spent helium it did not have.");
            Assert.IsTrue(h.IsEmpty);
            Assert.AreEqual(0f, h.Spend(1f));
        }

        [Test]
        public void ABubble_FillsUpToFull_AndNoFurther()
        {
            HeliumTank h = new HeliumTank(1.5f);
            h.Spend(1f);
            Assert.AreEqual(1f, h.Fill(3f), 0.0001f);
            Assert.AreEqual(0f, h.Fill(1f), "a bubble was used on a full tank.");
        }

        [Test]
        public void ANewShape_KeepsTheSameShare()
        {
            HeliumTank h = new HeliumTank(2f);
            h.Spend(1f);
            h.Resize(4f);
            Assert.AreEqual(2f, h.Seconds, 0.0001f);
        }

        // Unlike air, an empty tank is not the end: the next bubble fills it.
        [Test]
        public void AnEmptyTank_TakesHeliumAgain()
        {
            HeliumTank h = new HeliumTank(1f);
            h.Spend(1f);
            Assert.Greater(h.Fill(0.5f), 0f);
        }
    }
}
