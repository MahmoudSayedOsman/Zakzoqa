using UnityEngine;

namespace Zakzoqa
{
    // Tape lying in the level: patches the hole, then is gone. Not when she is already full — it
    // stays for later, which teaches that tape is worth saving.
    [RequireComponent(typeof(Collider2D))]
    public class TapePickup : MonoBehaviour
    {
        [Min(1)] public int air = 1;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other) => Take(other);
        void OnTriggerStay2D(Collider2D other) => Take(other);

        void Take(Collider2D other)
        {
            ZakzoqaBody z = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<ZakzoqaBody>() : null;
            if (z == null || z.Patch(air) <= 0) return;
            // Off at once: Destroy waits for the end of the frame, and a second touch before then
            // would patch her twice.
            GetComponent<Collider2D>().enabled = false;
            Destroy(gameObject);
        }
    }
}
