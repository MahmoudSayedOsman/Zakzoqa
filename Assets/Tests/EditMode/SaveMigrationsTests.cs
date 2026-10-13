using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Zakzoqa.Tests
{
    // The save's format version (SaveMigrations, Decisions §8). Each test plays a phone whose save was
    // written by an older or newer build, and checks what this one does with it.
    //
    // They run against the editor's real PlayerPrefs, so the progress found at the start is captured
    // and put back at the end — the developer's own playtest save comes out as it went in.
    public class SaveMigrationsTests
    {
        static readonly string T = SaveKeys.Game + "TestMigration_";
        SaveSnapshot backup;
        bool hadVersion;
        int versionBefore;

        [SetUp]
        public void SetUp()
        {
            Prefs.ForgetCache();
            backup = SaveSnapshot.Capture();
            hadVersion = Prefs.HasKey(SaveKeys.SaveVersion);
            versionBefore = Prefs.GetInt(SaveKeys.SaveVersion, 0);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string key in new List<string>(Prefs.Keys.Keys))
                if (key.StartsWith(T, StringComparison.Ordinal)) Prefs.DeleteKey(key);
            backup.Apply();
            if (hadVersion) Prefs.SetInt(SaveKeys.SaveVersion, versionBefore);
            else Prefs.DeleteKey(SaveKeys.SaveVersion);
            Prefs.Save();
        }

        // Raising Current without a step (or the reverse) would leave some phone's save unconverted.
        [Category("P0")]
        [Test]
        public void EveryVersion_HasExactlyOneStepToReachIt()
        {
            Assert.AreEqual(SaveMigrations.Current - 1, SaveMigrations.Steps.Count,
                "Current and the number of steps disagree: add one step per version, at the end.");
            foreach (Action step in SaveMigrations.Steps) Assert.IsNotNull(step);
        }

        [Category("P0")]
        [Test]
        public void AFreshSave_IsMarkedCurrent_AndNothingRuns()
        {
            Prefs.DeleteKey(SaveKeys.SaveVersion);
            bool ran = false;

            SaveMigrations.RunWith(3, new Action[] { () => ran = true, () => ran = true });

            Assert.AreEqual(3, Prefs.GetInt(SaveKeys.SaveVersion));
            Assert.IsFalse(ran, "a fresh install was converted from a format it never had.");
        }

        [Category("P0")]
        [Test]
        public void AnOldSave_RunsEachMissingStep_InOrder_AndOnlyThose()
        {
            Prefs.SetInt(SaveKeys.SaveVersion, 2);
            var order = new List<int>();

            SaveMigrations.RunWith(4, new Action[]
            {
                () => order.Add(1),
                () => order.Add(2),
                () => order.Add(3),
            });

            CollectionAssert.AreEqual(new[] { 2, 3 }, order, "the wrong steps ran, or in the wrong order.");
            Assert.AreEqual(4, Prefs.GetInt(SaveKeys.SaveVersion));
        }

        // What a step is for: an update renames a key, and the player's value comes with it.
        [Test]
        public void AStep_CarriesTheOldValueToItsNewPlace()
        {
            Prefs.SetInt(SaveKeys.SaveVersion, 1);
            Prefs.SetInt(T + "OldName", 42);

            SaveMigrations.RunWith(2, new Action[]
            {
                () =>
                {
                    Prefs.SetInt(T + "NewName", Prefs.GetInt(T + "OldName"));
                    Prefs.DeleteKey(T + "OldName");
                },
            });

            Assert.AreEqual(42, Prefs.GetInt(T + "NewName"), "the player's value was lost in the conversion.");
            Assert.IsFalse(Prefs.HasKey(T + "OldName"));
        }

        // Killed halfway: the version written after each step means the next launch resumes, not repeats.
        [Test]
        public void AStepThatFails_LeavesTheSaveAtTheLastFinishedVersion()
        {
            Prefs.SetInt(SaveKeys.SaveVersion, 1);

            Assert.Throws<InvalidOperationException>(() => SaveMigrations.RunWith(3, new Action[]
            {
                () => { },
                () => throw new InvalidOperationException("the phone died here"),
            }));

            Assert.AreEqual(2, Prefs.GetInt(SaveKeys.SaveVersion), "a finished step would run again next launch.");
        }

        // A downgraded app must not "convert" a format it does not know.
        [Category("P0")]
        [Test]
        public void ASaveFromANewerBuild_IsLeftUntouched()
        {
            Prefs.SetInt(SaveKeys.SaveVersion, 9);
            bool ran = false;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer than this build"));

            SaveMigrations.RunWith(2, new Action[] { () => ran = true });

            Assert.IsFalse(ran);
            Assert.AreEqual(9, Prefs.GetInt(SaveKeys.SaveVersion));
        }

        // The same from the cloud: a save written by a newer build on the player's other phone.
        [Category("P0")]
        [Test]
        public void ACloudSaveFromANewerBuild_IsRefused()
        {
            Prefs.SetInt(SaveKeys.SaveVersion, SaveMigrations.Current + 1);
            string json = SaveSnapshot.Capture().ToJson();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer than this build"));

            Assert.IsNull(SaveSnapshot.FromJson(json), "a save this build cannot read was accepted.");
        }

        [Test]
        public void TheVersion_TravelsWithTheSave()
        {
            Assert.IsTrue(SaveScope.IsProgress(SaveKeys.SaveVersion),
                "the cloud copy would arrive without saying which format it is in.");
        }
    }
}
