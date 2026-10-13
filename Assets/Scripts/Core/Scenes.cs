namespace Zakzoqa
{
    // Every scene loaded by name. ProjectSettingsTests fails if one of these is missing from the
    // build, so a scene renamed in the editor breaks a test instead of a player's game.
    public static class Scenes
    {
        public const string Boot = "Boot";
        // Where the game goes when the save changes underneath it: every screen there reads the save fresh.
        public const string MainMenu = "MainMenu";

        public static readonly string[] All = { Boot, MainMenu };
    }
}
