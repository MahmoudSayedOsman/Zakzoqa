using System;
using UnityEngine;

namespace Zakzoqa
{
    // ZAKZOQA HERSELF, whatever shape she is in (Decisions §3). Owns what every transformation shares:
    // the physics body, the air (her life), being hit, and changing shape. The shape-specific part —
    // what a tap does — is the current IForm.
    //
    // A state machine with one state per transformation: Transform(def) leaves the old form, sets the
    // body up from the new def, and enters the new form. The air carries over (AirTank.Resize keeps
    // the same fraction), so changing shape is never a free heal.
    //
    // Physics runs in Step(dt), called from FixedUpdate. Tests call it directly with
    // Physics2D.Simulate, so a test of "three taps climb this high" is exact and takes no real time.
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class ZakzoqaBody : MonoBehaviour
    {
        [SerializeField] TransformationDef startingForm;

        Rigidbody2D rb;
        CircleCollider2D circle;
        PhysicsMaterial2D material;
        IForm form;
        IZakzoqaInput input;

        // Input arrives per rendered frame and physics runs per fixed step. A tap is latched until the
        // next step uses it, so it is never missed (a frame with no step) or counted twice (two steps
        // in one frame).
        InputFrame pending;
        bool pendingPress, pendingPointerPress;

        float graceLeft;

        public TransformationDef Def { get; private set; }
        public FormKind Kind => form != null ? form.Kind : Def != null ? Def.kind : FormKind.Balloon;
        public AirTank Air { get; private set; }
        public Vector2 Position => rb != null ? rb.position : (Vector2)transform.position;
        public Vector2 Velocity => rb.linearVelocity;
        public bool IsPopped => Air != null && Air.IsEmpty;
        public bool IsInGrace => graceLeft > 0f;

        // Raised once when the air runs out. The level shows "carry on from here?" from this.
        public event Action Popped;
        // Raised after every hit that let air out, with the amount.
        public event Action<int> Hit;
        // Raised after a change of shape, with the new one.
        public event Action<TransformationDef> Transformed;

        void Awake()
        {
            EnsureComponents();
            if (startingForm != null && Def == null) Transform(startingForm);
        }

        void EnsureComponents()
        {
            if (rb != null) return;
            rb = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.freezeRotation = true;
            // Asleep, a body stops reporting that it is still on a pin.
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            material = new PhysicsMaterial2D("Zakzoqa") { friction = 0.1f };
            circle.sharedMaterial = material;
        }

        void Start()
        {
            if (input == null && Camera.main != null) input = new DeviceInput(Camera.main);
        }

        // For tests and for a level that drives her (a cutscene). A null input means no player control.
        public void SetInput(IZakzoqaInput source) => input = source;

        void Update()
        {
            if (input == null || IsPopped) return;
            InputFrame frame = input.Read();
            pending = frame;
            pendingPress |= frame.Press;
            pendingPointerPress |= frame.PointerPress;
        }

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void Step(float dt)
        {
            if (Def == null || IsPopped) return;
            if (graceLeft > 0f) graceLeft -= dt;

            InputFrame frame = pending;
            frame.Press = pendingPress;
            frame.PointerPress = pendingPointerPress;
            pendingPress = pendingPointerPress = false;

            form?.Step(frame, dt);

            Vector2 v = rb.linearVelocity;
            if (v.sqrMagnitude > Def.maxSpeed * Def.maxSpeed) rb.linearVelocity = v.normalized * Def.maxSpeed;
        }

        // For tests: what Update would have latched from one frame of input.
        public void Feed(InputFrame frame)
        {
            pending = frame;
            pendingPress |= frame.Press;
            pendingPointerPress |= frame.PointerPress;
        }

        // --- the state machine -----------------------------------------------------------------

        public void Transform(TransformationDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            EnsureComponents();

            form?.Exit();

            Def = def;
            if (Air == null)
            {
                Air = new AirTank(def.airCapacity);
                Air.Emptied += OnEmptied;
            }
            else
            {
                Air.Resize(def.airCapacity);
            }

            rb.mass = def.mass;
            rb.gravityScale = def.gravityScale;
            rb.linearDamping = def.linearDamping;
            circle.radius = def.diameter * 0.5f;
            material.bounciness = def.bounciness;
            // A material's values are read when it is assigned.
            circle.sharedMaterial = null;
            circle.sharedMaterial = material;

            form = Forms.Create(def.kind);
            form.Enter(this);
            Transformed?.Invoke(def);
        }

        // --- what forms may do -------------------------------------------------------------------

        public void AddVelocity(Vector2 delta) => rb.linearVelocity += delta;

        // --- being hit ---------------------------------------------------------------------------

        // A pin, a dart, a stapler. Returns whether it hurt: not during the grace after the last hit,
        // and not once popped. `from` is where the hit came from, for the knockback.
        public bool TakeHit(int air, Vector2 from)
        {
            if (Def == null || IsPopped || IsInGrace || air <= 0) return false;

            int lost = Air.Leak(air);
            graceLeft = Def.hitGraceSeconds;

            Vector2 away = Position - from;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
            rb.linearVelocity = away.normalized * Def.knockback;

            Hit?.Invoke(lost);
            return true;
        }

        // Tape. Returns how much went in.
        public int Patch(int air) => Air != null ? Air.Refill(air) : 0;

        // "Carry on from here" (Decisions §5): full air, a moment of grace, and control back.
        public void Revive()
        {
            if (Air == null) return;
            Air.Restore();
            graceLeft = Def.hitGraceSeconds;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = true;
        }

        void OnEmptied()
        {
            // Stops where she popped; the pop itself is the art's job.
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
            Popped?.Invoke();
        }
    }
}
