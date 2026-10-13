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

    // ONE TRANSFORMATION, AS DATA. Tuning how Zakzoqa floats, falls or bounces is an asset edit, never
    // a code change, and a new transformation is a new asset plus its IForm.
    //
    // Sizes are in world units and checked against WorldScale (TransformationTests), so a shape can
    // never quietly drift from the art drawn for it — Shadow Escape's characters did.
    [CreateAssetMenu(fileName = "Transformation", menuName = "Zakzoqa/Transformation")]
    public class TransformationDef : ScriptableObject
    {
        public FormKind kind;
        // A localisation key.
        public string displayNameKey = "";

        [Header("Life")]
        // Whole hits. A pin's damage is in the same units.
        public int airCapacity = 3;
        // After a hit, how long nothing can hurt her again: a pin she is still touching must not
        // empty her in one frame.
        public float hitGraceSeconds = 1.2f;

        [Header("Body")]
        // The collider's diameter in world units. The balloon's is WorldScale.ZakzoqaHeight.
        public float diameter = WorldScale.ZakzoqaHeight;
        public float mass = 1f;
        // 1 is a stone. A balloon only just sinks.
        public float gravityScale = 0.35f;
        // Air resistance: how quickly a movement dies out on its own.
        public float linearDamping = 1.5f;
        [Range(0f, 1f)] public float bounciness = 0.3f;
        public float maxSpeed = 6f;

        [Header("Control")]
        // The upward push of one puff, in units per second added to her speed.
        public float puffStrength = 4.2f;
        // How far a puff leans towards the side tapped: 0 straight up, 1 as much sideways as up.
        [Range(0f, 1f)] public float puffLean = 0.6f;
        // Taps closer together than this are one puff: a child hammering the screen gets a steady
        // climb, not a rocket.
        public float puffCooldown = 0.12f;
        // Sideways acceleration from the keys (editor), units per second squared.
        public float steerAcceleration = 10f;

        [Header("Knocked back")]
        // How hard a pin pushes her away, units per second.
        public float knockback = 4f;
    }
}
