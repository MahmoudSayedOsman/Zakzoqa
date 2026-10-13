namespace Zakzoqa
{
    // Every saved key's name, or the prefix it is built from. One place, so a key cannot be spelled
    // two ways by two owners, and so SaveScope and CompareProgress read the same names the game writes.
    //
    // NAMING RULE. Everything starts with Game. Then:
    //   Setting_  this phone's settings: never leaves the phone, never guarded.
    //   Ent_      purchases: the store's word, guarded, never copied to another phone.
    //   anything else is PROGRESS: guarded, and carried to the cloud. Progress is the default on
    //   purpose — a new key someone forgets to classify follows the player rather than being lost.
    public static class SaveKeys
    {
        public const string Game = "Zakzoqa_";

        public const string SettingPrefix = Game + "Setting_";
        public const string EntitlementPrefix = Game + "Ent_";

        // The save's own machinery.
        public const string PrefsIndex = Game + "PrefsIndex";
        public const string Guard = Game + "Guard";
        public const string CloudPrefix = Game + "Cloud_";
        public const string RemotePrefix = Game + "Remote_";
        public const string CheckpointPrefix = Game + "Checkpoint_";

        // Progress. SaveVersion is progress so it travels with the save it describes.
        public const string SaveVersion = Game + "SaveVersion";
        public const string TotalStars = Game + "TotalStars";
        public const string HighestChapter = Game + "HighestChapter";
        public const string LevelCompletedPrefix = Game + "LevelCompleted_";
        public const string AchievementPrefix = Game + "Achievement_";
        public const string Coins = Game + "Wallet_Coins";
    }
}
