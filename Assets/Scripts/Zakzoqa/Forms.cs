using System;
using UnityEngine;

namespace Zakzoqa
{
    // HOW ONE TRANSFORMATION PLAYS (Decisions §3). The body, the air and the damage are shared and
    // live in ZakzoqaBody; a form only turns the player's input into forces. Adding the football is
    // writing a FootballForm, not touching the balloon or the body.
    public interface IForm
    {
        FormKind Kind { get; }
        void Enter(ZakzoqaBody body);
        void Exit();
        // Called every physics step, with the input gathered since the last one.
        void Step(InputFrame input, float dt);
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

    // THE SMALL BALLOON: float, puff, climb, dodge (Decisions §3, chapter 1).
    //
    // She sinks slowly on her own (a little gravity, a lot of air resistance). Each tap is a puff
    // upward, leaning towards the side the player tapped, so one finger steers: tap to her left and
    // she rises to the left. Taps closer together than the cooldown count once.
    public sealed class BalloonForm : IForm
    {
        ZakzoqaBody body;
        float sinceLastPuff = float.PositiveInfinity;

        public FormKind Kind => FormKind.Balloon;

        public void Enter(ZakzoqaBody body)
        {
            this.body = body;
            sinceLastPuff = float.PositiveInfinity;
        }

        public void Exit() => body = null;

        public void Step(InputFrame input, float dt)
        {
            TransformationDef def = body.Def;
            sinceLastPuff += dt;

            if (input.Press && sinceLastPuff >= def.puffCooldown)
            {
                sinceLastPuff = 0f;
                body.AddVelocity(PuffDirection(input) * def.puffStrength);
            }

            if (input.Horizontal != 0f)
                body.AddVelocity(new Vector2(input.Horizontal * def.steerAcceleration * dt, 0f));
        }

        // Up, leaning towards where the finger is. Keys lean by the arrow held instead.
        Vector2 PuffDirection(InputFrame input)
        {
            float side = input.Horizontal;
            if (input.PointerPress)
            {
                float dx = input.PointerWorld.x - body.Position.x;
                // A tap within her own width is "straight up".
                float halfWidth = body.Def.diameter * 0.5f;
                side = Mathf.Abs(dx) <= halfWidth ? 0f : Mathf.Sign(dx);
            }
            return new Vector2(side * body.Def.puffLean, 1f).normalized;
        }
    }
}
