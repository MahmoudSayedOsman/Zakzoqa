using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zakzoqa
{
    // Where a save lives off the phone. The game never talks to Firebase directly — only through this,
    // so the provider can be swapped (or be the offline one) without anything else knowing.
    public interface ICloudSaveProvider
    {
        bool IsSignedIn { get; }
        void SignIn(Action<bool> done);
        // ok = the cloud answered; json = the save it holds, or null if it holds none yet.
        void Load(Action<bool, string> done);
        void Save(string json, Action<bool> done);
    }

    // No cloud: the editor, the tests, and a phone build without Firebase. Never signs in, so the game
    // plays exactly as it did before cloud save existed.
    public class OfflineCloudSaveProvider : ICloudSaveProvider
    {
        public bool IsSignedIn => false;
        public void SignIn(Action<bool> done) => done?.Invoke(false);
        public void Load(Action<bool, string> done) => done?.Invoke(false, null);
        public void Save(string json, Action<bool> done) => done?.Invoke(false);
    }

    // Keeps the player's progress on this phone and in the cloud the same.
    //
    // ON START: sign in, read the cloud's save, and keep whichever is further along
    // (SaveSnapshot.CompareProgress). The one that loses is kept on the phone as a backup, never thrown
    // away. Further along, not newer: a phone left in a drawer for a month must not overwrite a month of
    // play just because its clock says it saved last.
    //
    // WHILE PLAYING: any change to progress marks the save dirty, and it goes up a short while later —
    // or at once when the game is sent to the background, which on a phone is often the last thing that
    // happens before it is killed.
    //
    // A DELIBERATE RESET WINS. After "reset progress" the phone's empty save must replace the cloud's,
    // or the next launch would find the cloud further along and undo the reset. SaveManager marks it.
    //
    // Nothing here ever blocks play: offline, signed out or failing, the game carries on with the save
    // on the phone, and tries again next time.
    public class CloudSaveManager : MonoBehaviour
    {
        public static CloudSaveManager Instance { get; private set; }

        // Seconds between a change and its upload, so a burst of changes is one write.
        public const float UploadDelay = 30f;
        public const string ResetPendingKey = SaveKeys.CloudPrefix + "ResetPending";
        public const string BackupKey = SaveKeys.CloudPrefix + "Backup";
        const string MenuScene = Scenes.MainMenu;

        public enum Decision { Nothing, UploadLocal, ApplyCloud }

        ICloudSaveProvider provider = new OfflineCloudSaveProvider();
        bool dirty;
        float dirtySince;
        bool uploading;
        bool applying;
        SaveSnapshot waitingForMenu;

        public bool IsSignedIn => provider != null && provider.IsSignedIn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("CloudSaveManager");
            Instance = go.AddComponent<CloudSaveManager>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Prefs.Changed += OnPrefsChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SetProvider(CloudSavePlatform.CreateProvider());
        }

        void OnDestroy()
        {
            Prefs.Changed -= OnPrefsChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // A new provider starts a fresh sync with it.
        public void SetProvider(ICloudSaveProvider newProvider)
        {
            if (newProvider == null) return;
            provider = newProvider;
            Sync();
        }

        // Sign in if needed, then reconcile with what the cloud holds.
        public void Sync()
        {
            provider.SignIn(signedIn =>
            {
                if (!signedIn) return;
                provider.Load((ok, json) =>
                {
                    if (ok) Reconcile(json);
                });
            });
        }

        // What to do with a phone save and a cloud save. Pure, so it can be tested without either.
        //
        // An UNTRUSTED phone save (SaveGuard found it edited) never goes up: it would carry the edits
        // into the cloud and, from there, to every phone the player owns. If the cloud holds a clean
        // save, that replaces it, whatever either one's progress.
        public static Decision Decide(SaveSnapshot local, SaveSnapshot cloud, bool resetPending, bool localUntrusted = false)
        {
            if (localUntrusted) return cloud != null ? Decision.ApplyCloud : Decision.Nothing;
            if (resetPending || cloud == null) return Decision.UploadLocal;
            int c = SaveSnapshot.CompareProgress(cloud, local);
            if (c > 0) return Decision.ApplyCloud;
            if (c < 0) return Decision.UploadLocal;
            return Decision.Nothing;
        }

        void Reconcile(string cloudJson)
        {
            SaveSnapshot local = SaveSnapshot.Capture();
            SaveSnapshot cloud = SaveSnapshot.FromJson(cloudJson);
            bool resetPending = Prefs.GetInt(ResetPendingKey, 0) == 1;

            switch (Decide(local, cloud, resetPending, SaveGuard.Untrusted))
            {
                case Decision.UploadLocal:
                    Upload(local, ok => { if (ok && resetPending) Prefs.DeleteKey(ResetPendingKey); });
                    break;

                case Decision.ApplyCloud:
                    // The phone's save is kept, not lost, in case the choice was the wrong one.
                    Prefs.SetString(BackupKey, local.ToJson());
                    if (SceneManager.GetActiveScene().name == MenuScene) ApplyNow(cloud);
                    else waitingForMenu = cloud;
                    break;
            }
        }

        // Only in the menu: swapping a save under a level being played would pull the floor out from
        // under it. A save found mid-level waits for the next trip to the menu.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (waitingForMenu == null || scene.name != MenuScene) return;
            SaveSnapshot snap = waitingForMenu;
            waitingForMenu = null;
            ApplyNow(snap);
        }

        void ApplyNow(SaveSnapshot snap)
        {
            applying = true;
            try { snap.Apply(); }
            finally { applying = false; }
            // A signed save from the cloud is a clean one: whatever was wrong with this phone's is gone.
            SaveGuard.MarkTrusted();
            dirty = false;
            Debug.Log($"[CloudSave] the cloud's save was further along and is now this phone's ({snap.entries.Count} entries).");
            SaveManager.ReloadAfterSaveChanged();
        }

        void OnPrefsChanged(string key)
        {
            if (applying) return;
            // null is DeleteAll: everything changed.
            if (key != null && !SaveScope.IsProgress(key)) return;
            if (!dirty) dirtySince = Time.unscaledTime;
            dirty = true;
        }

        void Update()
        {
            if (dirty && !uploading && IsSignedIn && Time.unscaledTime - dirtySince >= UploadDelay)
                Upload(SaveSnapshot.Capture(), null);
        }

        // Backgrounded is often the last moment a phone game gets: upload now, not in thirty seconds.
        void OnApplicationPause(bool paused)
        {
            if (paused && dirty && IsSignedIn) Upload(SaveSnapshot.Capture(), null);
        }

        void OnApplicationQuit()
        {
            if (dirty && IsSignedIn) Upload(SaveSnapshot.Capture(), null);
        }

        void Upload(SaveSnapshot snap, Action<bool> done)
        {
            // Never an edited save: see Decide.
            if (SaveGuard.Untrusted) { dirty = false; done?.Invoke(false); return; }
            uploading = true;
            dirty = false;
            provider.Save(snap.ToJson(), ok =>
            {
                uploading = false;
                // Failed: stays dirty and is tried again on the next change, pause or launch.
                if (!ok) { dirty = true; dirtySince = Time.unscaledTime; }
                done?.Invoke(ok);
            });
        }

        // Called by SaveManager after a reset: the empty save must win over the cloud's.
        public static void NoteDeliberateReset()
        {
            Prefs.SetInt(ResetPendingKey, 1);
            Prefs.Save();
            if (Instance != null && Instance.IsSignedIn) Instance.Sync();
        }
    }

    // Which provider this build uses. Offline until the Firebase SDK is installed, which is its own
    // step: it needs Zakzoqa's Firebase project and google-services.json. Shadow Escape's
    // FirebaseCloudSaveProvider moves over then, behind FIREBASE_SDK, and is returned from here on a
    // phone build; the editor and the tests always stay offline.
    public static class CloudSavePlatform
    {
        public static ICloudSaveProvider CreateProvider() => new OfflineCloudSaveProvider();
    }
}
