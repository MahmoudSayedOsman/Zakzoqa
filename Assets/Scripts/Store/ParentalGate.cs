using System;
using UnityEngine;

namespace Zakzoqa
{
    // The question a grown-up answers before any real-money purchase (Decisions §6). Apple requires
    // one in the Kids category, and Google's Families policy expects one.
    //
    // ENFORCED HERE, NOT BY THE SCREEN. IapManager.Purchase refuses to reach the store unless the
    // gate is open, so a new "buy" button somewhere cannot skip it by forgetting to show the
    // question. The screen only asks; this decides.
    //
    // Open for a short window after a right answer, and closed again by the purchase it allowed:
    // one answer, one purchase.
    public static class ParentalGate
    {
        public const float OpenSeconds = 60f;

        // A multiplication a young child cannot do, and a grown-up does without thinking. The answer
        // is typed as digits, so it is the same question in Arabic and English.
        public readonly struct Question
        {
            public readonly int A, B;
            public Question(int a, int b) { A = a; B = b; }
            public int Answer => A * B;
        }

        // Realtime, so a paused game (timeScale 0) does not hold the gate open. Replaceable by tests.
        public static Func<float> Clock = () => Time.realtimeSinceStartup;

        static Question current;
        static bool asked;
        static float openUntil = float.NegativeInfinity;

        public static bool IsOpen => Clock() < openUntil;

        public static Question Ask()
        {
            System.Random rng = new System.Random(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
            current = new Question(rng.Next(6, 10), rng.Next(6, 10));
            asked = true;
            return current;
        }

        // True opens the gate. A wrong answer closes the question: the next try is a new one, so the
        // answer cannot be found by guessing through 1, 2, 3...
        public static bool TryAnswer(int answer)
        {
            if (!asked) return false;
            asked = false;
            if (answer != current.Answer) return false;
            openUntil = Clock() + OpenSeconds;
            return true;
        }

        // Called by IapManager when it sends a purchase to the store.
        public static void Close()
        {
            openUntil = float.NegativeInfinity;
            asked = false;
        }

        // For tests: the state a fresh launch has.
        public static void ResetForTests()
        {
            Close();
            Clock = () => Time.realtimeSinceStartup;
        }
    }
}
