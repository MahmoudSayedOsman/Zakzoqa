namespace Zakzoqa
{
    // The size ladder from Docs/Decisions.md §2. Locked: changing any of these means every sprite
    // and every level gets re-measured, which is the exact rework this project is set up to avoid.
    // ProjectSettingsTests fails if the camera or the art import drift away from these numbers.
    public static class WorldScale
    {
        // 10.8 world units fill the screen height. On a 1080p display that is 100 screen pixels
        // per unit, and art is drawn at twice that so it stays sharp on 1440p and above.
        public const float OrthographicSize = 5.4f;
        public const int PixelsPerUnit = 200;

        // Heights in world units. A sprite's pixel height is height × PixelsPerUnit.
        public const float ZakzoqaHeight = 1.2f;     // 240 px
        public const float PinSoldierHeight = 0.9f;  // 180 px
        public const float BossMinHeight = 3f;       // 600 px
        public const float BossMaxHeight = 5f;       // 1000 px
    }
}
