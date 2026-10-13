using System;
using UnityEngine;

namespace Zakzoqa
{
    // THE AIR INSIDE ZAKZOQA, WHICH IS HER LIFE (Decisions §3). A pin lets some out, tape puts it
    // back, and empty is popped. Every transformation is a balloon underneath, so every one of them
    // runs on this: the football and the warplane lose air to a pin exactly as the balloon does.
    //
    // Plain C#, no MonoBehaviour: the rules of life and death are tested without a scene.
    // Whole units, not a float: "three hits and you pop" must be exactly three hits.
    public sealed class AirTank
    {
        public int Capacity { get; private set; }
        public int Air { get; private set; }

        public bool IsEmpty => Air <= 0;
        public float Fraction => Capacity > 0 ? (float)Air / Capacity : 0f;

        // Raised after every change, with the new amount. The HUD's air gauge listens.
        public event Action<int> Changed;
        // Raised once, when the last of the air goes out.
        public event Action Emptied;

        public AirTank(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Air = capacity;
        }

        // Returns how much actually went out.
        public int Leak(int amount)
        {
            if (amount <= 0 || IsEmpty) return 0;
            int lost = Math.Min(amount, Air);
            Air -= lost;
            Changed?.Invoke(Air);
            if (Air == 0) Emptied?.Invoke();
            return lost;
        }

        // Returns how much actually went in. A popped balloon is not refilled by tape: popping is
        // final for the level, and coming back is "carry on from here", not a pickup.
        public int Refill(int amount)
        {
            if (amount <= 0 || IsEmpty || Air == Capacity) return 0;
            int added = Math.Min(amount, Capacity - Air);
            Air += added;
            Changed?.Invoke(Air);
            return added;
        }

        // A new capacity keeps the same fraction full, so changing shape is neither a free heal nor
        // a penalty. Never pops a balloon that has air: at least one unit stays.
        public void Resize(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (capacity == Capacity) return;
            int air = IsEmpty ? 0 : Mathf.Clamp(Mathf.RoundToInt(Fraction * capacity), 1, capacity);
            Capacity = capacity;
            Air = air;
            Changed?.Invoke(Air);
        }

        // "Carry on from here": full again. The one way back from empty.
        public void Restore()
        {
            Air = Capacity;
            Changed?.Invoke(Air);
        }
    }
}
