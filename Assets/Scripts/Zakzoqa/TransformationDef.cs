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

        // The launch speed for a full jump of jumpHeight under this shape's gravity.
        public float JumpSpeed(float worldGravity) =>
            Mathf.Sqrt(2f * Mathf.Abs(worldGravity) * gravityScale * jumpHeight);
    }
}
