using System;
using UnityEngine;

namespace Zakzoqa
{
    // HOW ONE TRANSFORMATION PLAYS (Decisions §3). The body, the air, the helium, the ground check and
    // being hit are shared and live in ZakzoqaBody; a form turns the player's input into movement.
    // Adding the football is writing a FootballForm, not touching the balloon or the body.
    public interface IForm
    {
        FormKind Kind { get; }
        void Enter(ZakzoqaBody body);
        void Exit();
        // Called every physics step, with the input gathered since the last one.
        void Step(InputFrame input, float dt);
    }

    // Anything the puff can push: enemies (knocked onto their backs), loose boxes, windmills. The
    // puff only reports itself; what it does is the target's business.
    public interface IPuffable
    {
        void Puffed(Vector2 direction, float force);
    }

    // The state machine's other half: which class plays which shape. A shape with a def but no form
    // fails TransformationTests, so a level can never offer a transformation that does nothing.
    public static class Forms
    {
        public static bool IsPlayable(FormKind kind) => kind == FormKind.Balloon;

        public static IForm Create(FormKind kind)
        {
            switch (kind)
            {
                case FormKind.Balloon: return new BalloonForm();
                default: throw new NotSupportedException($"{kind} has no form yet (Decisions §10: it arrives in a later update).");
            }
        }
    }

    // THE SMALL BALLOON: a classic platformer (Decisions §3).
    //
    // Run, and jump: hold for a full jump, tap for a hop. Forgiving the way platformers for every age
    // are: a jump just after leaving a ledge still counts (coyote time), and one pressed just before
    // landing happens on landing (buffer). Holding jump on the way down floats on helium, falling
    // slowly until the helium runs out. The ability is the puff: it pushes whatever is in front of her.
    public sealed class BalloonForm : IForm
    {
        static readonly System.Collections.Generic.List<Collider2D> PuffHits = new System.Collections.Generic.List<Collider2D>();

        ZakzoqaBody body;
        float coyoteLeft, bufferLeft, puffReadyIn;
        bool rising;

        public FormKind Kind => FormKind.Balloon;
        public bool IsFloating { get; private set; }

        public void Enter(ZakzoqaBody body)
        {
            this.body = body;
            coyoteLeft = bufferLeft = puffReadyIn = 0f;
            rising = IsFloating = false;
        }

        public void Exit() => body = null;

        public void Step(InputFrame input, float dt)
        {
            TransformationDef def = body.Def;
            bool grounded = body.IsGrounded;
            Vector2 v = body.Velocity;

            coyoteLeft = grounded ? def.coyoteTime : coyoteLeft - dt;
            bufferLeft = input.JumpPressed ? def.jumpBuffer : bufferLeft - dt;
            puffReadyIn -= dt;

            // Running. Knocked back, the hit carries her for a moment before the player has her again.
            if (!body.IsStunned)
            {
                if (Mathf.Abs(input.Move) > 0.01f) body.Face(input.Move);
                float accel = grounded ? def.groundAcceleration : def.airAcceleration;
                v.x = Mathf.MoveTowards(v.x, input.Move * def.runSpeed, accel * dt);
            }

            // Jumping.
            if (bufferLeft > 0f && coyoteLeft > 0f && !body.IsStunned)
            {
                v.y = def.JumpSpeed(Physics2D.gravity.y);
                bufferLeft = coyoteLeft = 0f;
                rising = true;
            }
            if (rising && (v.y <= 0f || !input.JumpHeld))
            {
                if (v.y > 0f) v.y *= def.jumpCut;
                rising = false;
            }

            // Floating: holding jump while falling, as long as there is helium.
            // The caps are on the speed AFTER this step's gravity, which the physics adds once this
            // returns; capping before it let her fall half again as fast as the asset says.
            float gravityThisStep = Mathf.Abs(Physics2D.gravity.y) * def.gravityScale * dt;

            // A fresh press in the air is a jump waiting to land first, and only becomes a float once
            // the buffer has run out: floating at once slowed her so much that the waiting jump expired
            // before she touched down, and a jump pressed just before landing was lost (tests caught it).
            IsFloating = !grounded && input.JumpHeld && v.y < 0f && bufferLeft <= 0f && !body.Helium.IsEmpty;
            if (IsFloating)
            {
                v.y = Mathf.Max(v.y, -def.floatFallSpeed + gravityThisStep);
                body.Helium.Spend(dt);
            }

            v.y = Mathf.Max(v.y, -def.maxFallSpeed + gravityThisStep);
            body.SetVelocity(v);

            // The puff.
            if (input.AbilityPressed && puffReadyIn <= 0f)
            {
                puffReadyIn = def.puffCooldown;
                Puff(def);
            }
        }

        void Puff(TransformationDef def)
        {
            Vector2 dir = new Vector2(body.Facing, 0f);
            Vector2 centre = body.Position + dir * (def.puffReach * 0.5f);
            Vector2 size = new Vector2(def.puffReach, def.diameter);
            Physics2D.OverlapBox(centre, size, 0f, ContactFilter2D.noFilter, PuffHits);
            for (int i = 0; i < PuffHits.Count; i++)
            {
                if (PuffHits[i].attachedRigidbody == body.Rigidbody) continue;
                foreach (IPuffable p in PuffHits[i].GetComponentsInParent<IPuffable>())
                    p.Puffed(dir, def.puffForce);
            }
            body.RaisePuffed(dir);
        }
    }
}
