using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Zakzoqa.Tests
{
    // The anti-cheat guard over the save (SaveGuard, Docs/Anti-Cheat.md). Each test plays the cheat
    // — editing the file, adding a key, breaking or removing the signature — and checks what the
    // game does about it at the next launch, which Prefs.ForgetCache stands in for.
    //
    // They run against the editor's real PlayerPrefs. The guard found at the start is put back at the
    // end, raw, and reloaded, so the developer's own playtest save comes out exactly as it went in.
    public class SaveGuardTests
    {
        static readonly string T = SaveKeys.Game + "TestGuard_";
        static readonly string RemoveAdsKey = SaveKeys.EntitlementPrefix + Entitlements.RemoveAds;

        string guardBefore;
        bool hadRemoveAds;
        int removeAdsBefore;

        [SetUp]
        public void SetUp()
        {
            Prefs.ForgetCache();
            SaveGuard.EnsureLoaded();
            guardBefore = PlayerPrefs.GetString(SaveGuard.GuardKey, "");
            hadRemoveAds = PlayerPrefs.HasKey(RemoveAdsKey);
            removeAdsBefore = PlayerPrefs.GetInt(RemoveAdsKey, 0);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string key in new[] { T + "Coins", T + "Injected", T + "F", T + "S" })
            {
                PlayerPrefs.DeleteKey(key);
            }
            if (hadRemoveAds) PlayerPrefs.SetInt(RemoveAdsKey, removeAdsBefore);
            else PlayerPrefs.DeleteKey(RemoveAdsKey);

            PlayerPrefs.SetString(SaveGuard.GuardKey, guardBefore);
            Prefs.ForgetCache();
            SaveGuard.EnsureLoaded();
            // The test keys stay in the index; remove them the ordinary way.
            foreach (string key in new[] { T + "Coins", T + "Injected", T + "F", T + "S" })
                if (Prefs.Keys.ContainsKey(key)) Prefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        [Category("P0")]
        [Test]
        public void AValueEditedInTheSaveFile_IsPutBack()
        {
            Prefs.SetInt(T + "Coins", 10);
            PlayerPrefs.SetInt(T + "Coins", 99999);     // the cheat, straight into the file

            Prefs.ForgetCache();                        // the next launch
            Assert.AreEqual(10, Prefs.GetInt(T + "Coins"), "the edited value was believed.");
            Assert.AreEqual(10, PlayerPrefs.GetInt(T + "Coins"), "the edited value was left in the file.");
            Assert.IsTrue(SaveGuard.TamperFoundThisLaunch);
            Assert.IsFalse(SaveGuard.Untrusted, "an edit the guard could undo exactly should not mark the save untrusted.");
        }

        [Category("P0")]
        [Test]
        public void AKeyAddedFromOutside_DoesNotExist()
        {
            PlayerPrefs.SetInt(T + "Injected", 500);

            Assert.IsFalse(Prefs.HasKey(T + "Injected"), "a progress key the game never wrote was believed.");
            Assert.AreEqual(0, Prefs.GetInt(T + "Injected", 0));
        }

        [Category("P0")]
        [Test]
        public void ABrokenSignature_KeepsTheProgress_ButUntrusted_AndWithoutPurchases()
        {
            Prefs.SetInt(T + "Coins", 10);
            Entitlements.Grant(Entitlements.RemoveAds);
            PlayerPrefs.SetString(SaveGuard.GuardKey, PlayerPrefs.GetString(SaveGuard.GuardKey) + "x");

            Prefs.ForgetCache();
            Assert.AreEqual(10, Prefs.GetInt(T + "Coins"), "a false alarm would have wiped the player's progress.");
            Assert.IsTrue(SaveGuard.Untrusted, "a broken signature was trusted.");
            Assert.IsFalse(Entitlements.Has(Entitlements.RemoveAds), "Remove Ads survived in a save the guard cannot vouch for.");

            // And the mark is sealed in: it survives the next launch too.
            Prefs.ForgetCache();
            Assert.IsTrue(SaveGuard.Untrusted, "removing the guard's complaint took one relaunch.");
        }

        [Category("P0")]
        [Test]
        public void ARemovedSignature_OnASaveWithProgress_IsUntrusted()
        {
            Prefs.SetInt(T + "Coins", 10);
            PlayerPrefs.DeleteKey(SaveGuard.GuardKey);

            Prefs.ForgetCache();
            Assert.IsTrue(SaveGuard.Untrusted, "deleting the guard laundered an edited save.");
        }

        [Test]
        public void ACleanSave_ClearsTheMark()
        {
            Prefs.SetInt(T + "Coins", 10);
            PlayerPrefs.SetString(SaveGuard.GuardKey, "broken");
            Prefs.ForgetCache();
            Assert.IsTrue(SaveGuard.Untrusted);

            SaveGuard.MarkTrusted();
            Prefs.ForgetCache();
            Assert.IsFalse(SaveGuard.Untrusted, "a clean save from the cloud did not clear the mark.");
        }

        [Test]
        public void FloatsAndStrings_ComeBackExactly()
        {
            Prefs.SetFloat(T + "F", 0.1f);
            Prefs.SetString(T + "S", "مرحبا\tx\ny#");

            Prefs.ForgetCache();
            Assert.AreEqual(0.1f, Prefs.GetFloat(T + "F"));
            Assert.AreEqual("مرحبا\tx\ny#", Prefs.GetString(T + "S"));
            Assert.IsFalse(SaveGuard.TamperFoundThisLaunch, "an honest save was taken for an edited one.");
        }

        // A memory editor searches for the number on screen. The guard's copy never holds it.
        [Category("P0")]
        [Test]
        public void TheCopyInMemory_NeverHoldsThePlainNumber()
        {
            Prefs.SetInt(T + "Coins", 12345);

            IDictionary mirror = (IDictionary)typeof(SaveGuard).GetField("mirror", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            object cell = mirror[T + "Coins"];
            int masked = (int)cell.GetType().GetField("Masked").GetValue(cell);

            Assert.AreNotEqual(12345, masked, "the coin balance sits in memory as itself.");
            Assert.AreEqual(12345, Prefs.GetInt(T + "Coins"));
        }

        [Category("P0")]
        [Test]
        public void TheGuard_CoversProgressAndPurchases_NotSettings()
        {
            Assert.IsTrue(SaveGuard.Covers(SaveKeys.Coins));
            Assert.IsTrue(SaveGuard.Covers(SaveKeys.TotalStars));
            Assert.IsTrue(SaveGuard.Covers(RemoveAdsKey));
            Assert.IsFalse(SaveGuard.Covers(SaveKeys.SettingPrefix + "MusicVolume"));
            Assert.IsFalse(SaveGuard.Covers(SaveGuard.GuardKey));
            Assert.IsFalse(SaveGuard.Covers(Prefs.IndexKey));
        }
    }
}
