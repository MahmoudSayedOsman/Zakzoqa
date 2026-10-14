using UnityEngine;

namespace Zakzoqa
{
    // The five shapes (Decisions §3). An id in code, so a save and a level can name one; everything
    // about how it plays is data in its TransformationDef.
    public enum FormKind
    {
        Balloon = 0,
        Football = 1,
        Basketball = 2,
        Carrier = 3,
        Warplane = 4,
    }

    // ONE TRANSFORMATION, AS DATA. Tuning how far Zakzoqa jumps, how long she floats or how hard her
    // puff pushes is an asset edit, never a code change.
    //
    // Distances are in world units and checked against WorldScale (TransformationTests), so a shape
    // can never quietly drift from the art drawn for it — Shadow Escape's characters did.
    [CreateAssetMenu(fileName = "Transformation", menuName = "Zakzoqa/Transformation")]
    public class TransformationDef : ScriptableObject
    {
        public FormKind kind;
        // A localisation key.
        public string displayNameKey = "";

        [Header("Life")]
        // Whole hits. A pin's damage is in the same units.
        public int airCapacity = 3;
        // After a hit, how long nothing can hurt her again: a pin she is still touching must not empty
        // her in one go.
        public float hitGraceSeconds = 1.2f;
        // After a hit, how long the knockback carries her before the player has control again.
        public float hitStunSeconds = 0.25f;
        // How hard a hit pushes her away, units per second; she is also lifted a little.
        public float knockback = 6f;

        [Header("Body")]
        // The collider's diameter in world units. The balloon's is WorldScale.ZakzoqaHeight.
        public float diameter = WorldScale.ZakzoqaHeight;
        public float mass = 1f;
        public float gravityScale = 3f;
        [Range(0f, 1f)] public float bounciness = 0f;
        // Falling never gets faster than this, so she stays readable on screen.
        public float maxFallSpeed = 14f;

        [Header("Running")]
        public float runSpeed = 6f;
        // How quickly she reaches full speed and stops, units per second squared. High on the ground
        // (responsive), lower in the air (a jump commits).
        public float groundAcceleration = 60f;
        public float airAcceleration = 35f;

        [Header("Jumping")]
        // How high a full jump lifts her, in world units. The launch speed is worked out from this and
        // the gravity, so changing gravity never changes how high she jumps.
        public float jumpHeight = 2.6f;
        // Letting go of jump while rising cuts the rise to this share: a tap is a hop, a hold is a jump.
        [Range(0.1f, 1f)] public float jumpCut = 0.45f;
        // A jump pressed just after running off a ledge still counts.
        public float coyoteTime = 0.1f;
        // A jump pressed just before landing still counts, on landing.
        public float jumpBuffer = 0.12f;

        [Header("Floating (helium)")]
        // Seconds of floating a full tank holds.
        public float heliumCapacity = 1.5f;
        // While floating, she falls no faster than this.
        public float floatFallSpeed = 1.2f;

        [Header("Ability: the puff")]
        // How far in front of her the puff reaches, from her centre.
        public float puffReach = 1.6f;
        // How hard it pushes what it hits, units per second.
        public float puffForce = 9f;
        public float puffCooldown = 0.45f;

        // ------------------------------------------------------------------------------------------
        // PROTOTYPE: AIR CONTROL (Design.md §14). Off unless airControl is set, so the agreed balloon
        // plays exactly as before. Kept or removed as a whole once the owner has played it.
        // ------------------------------------------------------------------------------------------
        [Header("PROTOTYPE: air control (Design.md §14)")]
        public bool airControl = false;

        // Let out: small, heavy, fits through gaps, breaks weak floors, ignores the wind, no floating.
        public float smallDiameter = 0.6f;
        public float smallGravityScale = 5f;
        public float smallJumpHeight = 1.6f;
        public float smallRunSpeed = 7f;

        // Her size follows her air: full is full size, one unit left is this share of it. A hit is
        // seen, not read off a gauge.
        [Range(0.3f, 1f)] public float emptiestScale = 0.7f;

        // How much the wind lifts her, big and small.
        public float windLiftBig = 1f;
        public float windLiftSmall = 0f;

        // Landing small at this speed or faster breaks a weak floor.
        public float breakSpeed = 7f;

        // The jet: in the air, the ability lets air out in one burst and she shoots that way.
        public float jetSpeed = 11f;
        public float jetSeconds = 0.18f;
        public float jetHeliumCost = 0.5f;
        // 0 for the prototype: paying for a jet with her life was judged too harsh for children.
        public int jetAirCost = 0;

        // The launch speed for a full jump of `height` under `scale` gravity.
        public static float JumpSpeedFor(float worldGravity, float scale, float height) =>
            Mathf.Sqrt(2f * Mathf.Abs(worldGravity) * scale * height);

        // The launch speed for a full jump of jumpHeight under this shape's gravity.
        public float JumpSpeed(float worldGravity) => JumpSpeedFor(worldGravity, gravityScale, jumpHeight);
    }
}
