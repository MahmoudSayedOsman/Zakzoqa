using UnityEngine;

namespace Zakzoqa
{
    // A helium bubble in the level (Decisions §3): more floating. Left where it is when her tank is
    // full, so it is still there when she comes back for it.
    [RequireComponent(typeof(Collider2D))]
    public class HeliumPickup : MonoBehaviour
    {
        [Min(0.1f)] public float seconds = 0.75f;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other) => Take(other);
        void OnTriggerStay2D(Collider2D other) => Take(other);

        void Take(Collider2D other)
        {
            ZakzoqaBody z = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<ZakzoqaBody>() : null;
            if (z == null || z.AddHelium(seconds) <= 0f) return;
            // Off at once: Destroy waits for the end of the frame.
            GetComponent<Collider2D>().enabled = false;
            Destroy(gameObject);
        }
    }
}
