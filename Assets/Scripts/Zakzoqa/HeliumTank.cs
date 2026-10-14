using System;
using UnityEngine;

namespace Zakzoqa
{
    // HELIUM: what lets Zakzoqa float, for a while (Decisions §3). Holding jump on the way down spends
    // it; helium found in the level refills it. Measured in seconds of floating.
    //
    // Not a currency: never bought, never saved, full again at the start of every level. Choosing
    // when to spend it is the play — float across this gap now, or keep some for the next.
    public sealed class HeliumTank
    {
        public float Capacity { get; private set; }
        public float Seconds { get; private set; }

        public bool IsEmpty => Seconds <= 0f;
        public float Fraction => Capacity > 0f ? Seconds / Capacity : 0f;

        // The HUD's helium gauge listens.
        public event Action<float> Changed;

        public HeliumTank(float capacity)
        {
            if (capacity < 0f) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Seconds = capacity;
        }

        // Returns how much was actually spent: less than asked when it runs out mid-float.
        public float Spend(float seconds)
        {
            if (seconds <= 0f || IsEmpty) return 0f;
            float spent = Mathf.Min(seconds, Seconds);
            Seconds -= spent;
            Changed?.Invoke(Seconds);
            return spent;
        }

        public float Fill(float seconds)
        {
            if (seconds <= 0f || Seconds >= Capacity) return 0f;
            float added = Mathf.Min(seconds, Capacity - Seconds);
            Seconds += added;
            Changed?.Invoke(Seconds);
            return added;
        }

        // A shape with a different tank keeps the same share, like the air.
        public void Resize(float capacity)
        {
            if (capacity < 0f) throw new ArgumentOutOfRangeException(nameof(capacity));
            Seconds = Fraction * capacity;
            Capacity = capacity;
            Changed?.Invoke(Seconds);
        }

        public void Refill()
        {
            Seconds = Capacity;
            Changed?.Invoke(Seconds);
        }
    }
}
