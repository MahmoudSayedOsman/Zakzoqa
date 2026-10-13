using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Zakzoqa.Tests
{
    // Cloud save rests on one promise: every key the game writes is known to Prefs, so a snapshot of
    // the player's progress is complete. A key written behind its back is progress that silently
    // stays on the old phone. These pin that promise, what counts as progress, and how two saves
    // are chosen between.
    //
    // They run against the editor's real PlayerPrefs, so each one backs up the progress it finds and
    // puts it back — the developer's own playtest save must survive the test run.
    public class CloudSaveTests
    {
        static readonly string TestPrefix = SaveKeys.Game + "TestCloud_";
        SaveSnapshot backup;

        [SetUp]
        public void SetUp()
        {
            Prefs.ForgetCache();
            backup = SaveSnapshot.Capture();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string key in Prefs.Keys.Keys.Where(k => k.StartsWith(TestPrefix)).ToList())
                Prefs.DeleteKey(key);
            backup.Apply();
            Prefs.DeleteKey(CloudSaveManager.ResetPendingKey);
        }

        // THE ONE THAT MATTERS MOST. Nothing in the game touches PlayerPrefs except through Prefs:
        // a direct write is a key cloud save never learns exists, and a direct read of progress
        // believes the file instead of SaveGuard's signed copy — the one door a cheat needs.
        [Category("P0")]
        [Test]
        public void NothingTouchesPlayerPrefs_ExceptThroughPrefs()
        {
            string[] roots = { Path.Combine(Application.dataPath, "Scripts"), Path.Combine(Application.dataPath, "Editor") };
            string[] doors = { "Prefs.cs", "SaveGuard.cs" };
            Regex direct = new Regex(@"\bPlayerPrefs\.(SetInt|SetFloat|SetString|DeleteKey|DeleteAll|GetInt|GetFloat|GetString|HasKey|Save)\s*\(");

            var offenders = roots.Where(Directory.Exists)
                .SelectMany(r => Directory.GetFiles(r, "*.cs", SearchOption.AllDirectories))
                .Where(f => !doors.Contains(Path.GetFileName(f)))
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, line, i)))
                .Where(x => !x.line.TrimStart().StartsWith("//") && direct.IsMatch(x.line))
                .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}")
                .ToList();

            Assert.IsEmpty(offenders,
                "these touch PlayerPrefs directly, around cloud save and the anti-cheat guard:\n  "
                + string.Join("\n  ", offenders));
        }

        // A save written into the cloud by hand, or edited on the way, is refused when it comes back.
        [Category("P0")]
        [Test]
        public void ASnapshotEditedAfterSigning_IsRefused()
        {
            Prefs.SetInt(TestPrefix + "Coins", 10);
            SaveSnapshot snap = SaveSnapshot.Capture();
            Assert.IsNotNull(SaveSnapshot.FromJson(snap.ToJson()), "an honest save was refused.");

            snap.entries.First(e => e.k == TestPrefix + "Coins").i = 999999;
            Assert.IsNull(SaveSnapshot.FromJson(snap.ToJson()), "an edited save was accepted.");

            Assert.IsNull(SaveSnapshot.FromJson("{\"version\":1,\"entries\":[]}"), "an unsigned save was accepted.");
        }

        // A float travels as its exact bits, so a best time is the same on both phones.
        [Test]
        public void AFloat_SurvivesTheCloudExactly()
        {
            Prefs.SetFloat(TestPrefix + "Time", 12.345678f);
            SaveSnapshot back = SaveSnapshot.FromJson(SaveSnapshot.Capture().ToJson());
            Assert.IsNotNull(back);

            Prefs.SetFloat(TestPrefix + "Time", 0f);
            back.Apply();
            Assert.AreEqual(12.345678f, Prefs.GetFloat(TestPrefix + "Time"));
        }

        [Test]
        public void Prefs_RemembersEveryKeyWritten_WithItsType_AndForgetsDeletedOnes()
        {
            Prefs.SetInt(TestPrefix + "I", 3);
            Prefs.SetFloat(TestPrefix + "F", 1.5f);
            Prefs.SetString(TestPrefix + "S", "x");

            Prefs.ForgetCache();   // read back from PlayerPrefs, as a fresh launch would
            Assert.AreEqual(Prefs.Kind.Int, Prefs.Keys[TestPrefix + "I"]);
            Assert.AreEqual(Prefs.Kind.Float, Prefs.Keys[TestPrefix + "F"]);
            Assert.AreEqual(Prefs.Kind.String, Prefs.Keys[TestPrefix + "S"]);

            Prefs.DeleteKey(TestPrefix + "I");
            Prefs.ForgetCache();
            Assert.IsFalse(Prefs.Keys.ContainsKey(TestPrefix + "I"), "a deleted key is still in the index.");
        }

        [Category("P0")]
        [Test]
        public void Progress_IsWhatFollowsThePlayer_SettingsAndPurchasesStay()
        {
            Assert.IsTrue(SaveScope.IsProgress(SaveKeys.Game + "LevelStars_1-01"));
            Assert.IsTrue(SaveScope.IsProgress(SaveKeys.Coins));
            Assert.IsTrue(SaveScope.IsProgress(SaveKeys.AchievementPrefix + "first_pop"));

            Assert.IsFalse(SaveScope.IsProgress(SaveKeys.SettingPrefix + "MusicVolume"), "a setting would follow the player to another phone.");
            Assert.IsFalse(SaveScope.IsProgress(SaveKeys.SettingPrefix + "TouchCentres"), "one phone's button layout would land on another's screen.");
            // Any setting, including ones nobody has written yet: the prefix decides, not a list.
            Assert.IsFalse(SaveScope.IsProgress(SaveKeys.SettingPrefix + "SomethingAddedNextYear"),
                "a new setting was carried to the cloud because nobody listed it.");
            Assert.IsFalse(SaveScope.IsProgress(SaveKeys.Guard));
            Assert.IsFalse(SaveScope.IsProgress(SaveKeys.CloudPrefix + "Backup"));
            Assert.IsFalse(SaveScope.IsProgress(SaveKeys.EntitlementPrefix + Entitlements.RemoveAds),
                "a purchase would travel in a save file — a way to get it without paying.");
            Assert.IsFalse(SaveScope.IsProgress(SaveKeys.CheckpointPrefix + "X"));
            Assert.IsFalse(SaveScope.IsProgress(Prefs.IndexKey));
            Assert.IsFalse(SaveScope.IsProgress("UnityGraphicsQuality"), "a key that is not the game's own was treated as progress.");
        }

        [Test]
        public void ASnapshot_SurvivesTheTripThroughJson()
        {
            Prefs.SetInt(TestPrefix + "Stars", 7);
            Prefs.SetString(TestPrefix + "Name", "مرحبا");
            SaveSnapshot back = SaveSnapshot.FromJson(SaveSnapshot.Capture().ToJson());

            Assert.IsNotNull(back);
            Assert.AreEqual(7, back.entries.First(e => e.k == TestPrefix + "Stars").i);
            Assert.AreEqual("مرحبا", back.entries.First(e => e.k == TestPrefix + "Name").s);
        }

        // A save that cannot be read must never be applied: it would wipe the player's progress.
        [Category("P0")]
        [Test]
        public void AnUnreadableSave_IsRejected_NeverApplied()
        {
            Assert.IsNull(SaveSnapshot.FromJson(null));
            Assert.IsNull(SaveSnapshot.FromJson(""));
            Assert.IsNull(SaveSnapshot.FromJson("{not json"));
            Assert.IsNull(SaveSnapshot.FromJson("{\"version\":99,\"entries\":[]}"), "a save from a future version was accepted.");
        }

        // Applying a save replaces the progress and leaves the phone's settings alone.
        [Category("P0")]
        [Test]
        public void ApplyingASave_ReplacesProgress_AndLeavesSettingsAlone()
        {
            Prefs.SetInt(TestPrefix + "Kept", 1);
            SaveSnapshot cloud = SaveSnapshot.Capture();

            Prefs.SetInt(TestPrefix + "OnlyOnThisPhone", 1);
            float volume = PlayerPrefs.GetFloat(SaveKeys.SettingPrefix + "MusicVolume", 0.8f);

            cloud.Apply();

            Assert.AreEqual(1, PlayerPrefs.GetInt(TestPrefix + "Kept", 0));
            Assert.IsFalse(PlayerPrefs.HasKey(TestPrefix + "OnlyOnThisPhone"),
                "progress the chosen save does not have survived — the two saves were mixed.");
            Assert.AreEqual(volume, PlayerPrefs.GetFloat(SaveKeys.SettingPrefix + "MusicVolume", 0.8f), "applying a save changed a setting.");
        }

        static SaveSnapshot Save(int stars, int chapter = 1, int completed = 0, int coins = 0)
        {
            SaveSnapshot s = new SaveSnapshot();
            s.entries.Add(new SaveSnapshot.Entry { k = SaveKeys.TotalStars, t = 0, i = stars });
            s.entries.Add(new SaveSnapshot.Entry { k = SaveKeys.HighestChapter, t = 0, i = chapter });
            s.entries.Add(new SaveSnapshot.Entry { k = SaveKeys.Coins, t = 0, i = coins });
            for (int i = 0; i < completed; i++)
                s.entries.Add(new SaveSnapshot.Entry { k = SaveKeys.LevelCompletedPrefix + "1-" + i, t = 0, i = 1 });
            return s;
        }

        // Further along wins, measured only by things that never go down in play.
        [Category("P0")]
        [Test]
        public void TheSaveFurtherAlong_Wins_AndSpendingCoinsDoesNotMakeASaveLose()
        {
            Assert.Greater(SaveSnapshot.CompareProgress(Save(10), Save(9)), 0);
            Assert.Greater(SaveSnapshot.CompareProgress(Save(10, 2), Save(10, 1)), 0);
            Assert.Greater(SaveSnapshot.CompareProgress(Save(10, 2, 5), Save(10, 2, 4)), 0);
            Assert.AreEqual(0, SaveSnapshot.CompareProgress(Save(10, 2, 5, coins: 0), Save(10, 2, 5, coins: 999)),
                "coins decided between two saves — spending them would lose a player their progress.");
        }

        [Category("P0")]
        [Test]
        public void TheDecision_BetweenThePhoneAndTheCloud()
        {
            Assert.AreEqual(CloudSaveManager.Decision.UploadLocal, CloudSaveManager.Decide(Save(3), null, false),
                "an empty cloud did not receive the phone's save.");
            Assert.AreEqual(CloudSaveManager.Decision.ApplyCloud, CloudSaveManager.Decide(Save(3), Save(9), false),
                "a new phone did not get the progress waiting in the cloud.");
            Assert.AreEqual(CloudSaveManager.Decision.UploadLocal, CloudSaveManager.Decide(Save(9), Save(3), false));
            Assert.AreEqual(CloudSaveManager.Decision.Nothing, CloudSaveManager.Decide(Save(5), Save(5), false));
            Assert.AreEqual(CloudSaveManager.Decision.UploadLocal, CloudSaveManager.Decide(Save(0), Save(9), true),
                "a deliberate reset lost to the cloud — the next launch would undo it.");
        }

        // An edited phone save never reaches the cloud, and a clean cloud save replaces it — even
        // one with less progress, since the edited one's progress is not believed.
        [Category("P0")]
        [Test]
        public void AnUntrustedPhoneSave_IsNeverUploaded_AndTheCloudsReplacesIt()
        {
            Assert.AreEqual(CloudSaveManager.Decision.Nothing, CloudSaveManager.Decide(Save(99), null, false, true),
                "an edited save was uploaded to an empty cloud.");
            Assert.AreEqual(CloudSaveManager.Decision.ApplyCloud, CloudSaveManager.Decide(Save(99), Save(3), false, true),
                "an edited save beat the clean one in the cloud.");
            Assert.AreEqual(CloudSaveManager.Decision.Nothing, CloudSaveManager.Decide(Save(99), null, true, true),
                "a reset flag let an edited save through.");
        }
    }
}
