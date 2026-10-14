using System;
using UnityEngine;

namespace Zakzoqa
{
    // ZAKZOQA HERSELF, whatever shape she is in (Decisions §3). Owns what every transformation shares:
    // the physics body, standing on the ground, the air (her life), the helium (her floating), being
    // hit, and changing shape. What the buttons do is the current IForm.
    //
    // A state machine with one state per transformation: Transform(def) leaves the old form, sets the
    // body up from the new def, and enters the new form. Air and helium carry over at the same share,
    // so changing shape is never a free heal.
    //
    // Physics runs in Step(dt), called from FixedUpdate. Tests call it directly with
    // Physics2D.Simulate, so "a full jump reaches this high" is exact and takes no real time.
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class ZakzoqaBody : MonoBehaviour
    {
        [SerializeField] TransformationDef startingForm;

        Rigidbody2D rb;
        CircleCollider2D circle;
        PhysicsMaterial2D material;
        IForm form;
        IZakzoqaInput input;
        ContactFilter2D groundFilter;

        // Input arrives per rendered frame and physics runs per fixed step. Presses are latched until
        // the next step uses them, so a press is never missed (a frame with no step) or counted twice
        // (two steps in one frame). Held state and the stick are simply the latest.
        InputFrame pending;
        bool pendingJump, pendingAbility, pendingInteract;

        float graceLeft, stunLeft;

        public TransformationDef Def { get; private set; }
        public IForm Form => form;
        public FormKind Kind => form != null ? form.Kind : Def != null ? Def.kind : FormKind.Balloon;
        public AirTank Air { get; private set; }
        public HeliumTank Helium { get; private set; }
        public Rigidbody2D Rigidbody => rb;
        public Vector2 Position => rb != null ? rb.position : (Vector2)transform.position;
        public Vector2 Velocity => rb.linearVelocity;
        // 1 facing right, -1 facing left. The puff goes this way; the art flips by it.
        public float Facing { get; private set; } = 1f;
        public bool IsGrounded { get; private set; }
        public bool IsPopped => Air != null && Air.IsEmpty;
        public bool IsInGrace => graceLeft > 0f;
        public bool IsStunned => stunLeft > 0f;

        // Raised once when the air runs out. The level offers "carry on from here?" from this.
        public event Action Popped;
        // Raised after every hit that let air out, with the amount.
        public event Action<int> Hit;
        // Raised after a change of shape, with the new one.
        public event Action<TransformationDef> Transformed;
        // Raised when she puffs, with the direction. The art and the sound listen.
        public event Action<Vector2> Puffed;

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
            // No friction: the form sets her speed itself, and friction would stick her to walls.
            material = new PhysicsMaterial2D("Zakzoqa") { friction = 0f };
            circle.sharedMaterial = material;

            // The ground is whatever she touches from above: a surface whose normal points up within
            // 50 degrees, so slopes count and walls do not. Triggers (pins, pickups) never do.
            groundFilter = new ContactFilter2D { useTriggers = false };
            groundFilter.SetNormalAngle(40f, 140f);
        }

        void Start()
        {
            if (input == null) input = new DeviceInput();
        }

        // For tests and for a level that drives her (a cutscene). Null means no player control.
        public void SetInput(IZakzoqaInput source) => input = source;

        void Update()
        {
            if (input != null && !IsPopped) Feed(input.Read());
        }

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        // What Update latches from one frame of input. Public for tests, which feed frames directly.
        public void Feed(InputFrame frame)
        {
            pending = frame;
            pendingJump |= frame.JumpPressed;
            pendingAbility |= frame.AbilityPressed;
            pendingInteract |= frame.InteractPressed;
        }

        public void Step(float dt)
        {
            if (Def == null || IsPopped) return;
            if (graceLeft > 0f) graceLeft -= dt;
            if (stunLeft > 0f) stunLeft -= dt;

            // Rising is never standing: the physics reports the floor for one step after a jump has
            // left it, which counted as ground and broke the float and the landing (tests caught it).
            IsGrounded = rb.linearVelocity.y <= 0.01f && rb.IsTouching(groundFilter);

            InputFrame frame = pending;
            frame.JumpPressed = pendingJump;
            frame.AbilityPressed = pendingAbility;
            frame.InteractPressed = pendingInteract;
            pendingJump = pendingAbility = pendingInteract = false;

            form?.Step(frame, dt);
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
                Helium = new HeliumTank(def.heliumCapacity);
            }
            else
            {
                Air.Resize(def.airCapacity);
                Helium.Resize(def.heliumCapacity);
            }

            rb.mass = def.mass;
            rb.gravityScale = def.gravityScale;
            rb.linearDamping = 0f;
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

        public void SetVelocity(Vector2 v) => rb.linearVelocity = v;
        public void AddVelocity(Vector2 delta) => rb.linearVelocity += delta;
        public void Face(float direction) { if (direction != 0f) Facing = Mathf.Sign(direction); }
        public void RaisePuffed(Vector2 direction) => Puffed?.Invoke(direction);

        // --- being hit ---------------------------------------------------------------------------

        // A pin, a needle, a stapler. Returns whether it hurt: not during the grace after the last
        // hit, and not once popped. `from` is where the hit came from: she is pushed away from it and
        // lifted a little, and the player gets her back after a moment.
        public bool TakeHit(int air, Vector2 from)
        {
            if (Def == null || IsPopped || IsInGrace || air <= 0) return false;

            int lost = Air.Leak(air);
            graceLeft = Def.hitGraceSeconds;
            stunLeft = Def.hitStunSeconds;

            float side = Position.x >= from.x ? 1f : -1f;
            if (!IsPopped) rb.linearVelocity = new Vector2(side, 0.6f).normalized * Def.knockback;

            Hit?.Invoke(lost);
            return true;
        }

        // Tape. Returns how much went in.
        public int Patch(int air) => Air != null ? Air.Refill(air) : 0;

        // Helium found in the level. Returns how much went in.
        public float AddHelium(float seconds) => Helium != null ? Helium.Fill(seconds) : 0f;

        // "Carry on from here" (Decisions §5): full air and helium, a moment of grace, control back.
        public void Revive()
        {
            if (Air == null) return;
            Air.Restore();
            Helium.Refill();
            graceLeft = Def.hitGraceSeconds;
            stunLeft = 0f;
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
