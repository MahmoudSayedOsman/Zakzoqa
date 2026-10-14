using UnityEngine;

namespace Zakzoqa
{
    // PROTOTYPE (Design.md §14): a column of wind. It lifts Zakzoqa by how much the wind takes her —
    // all the way when she is full of air, not at all when she has let it out — which makes "big or
    // small?" a question the level asks.
    [RequireComponent(typeof(Collider2D))]
    public class WindColumn : MonoBehaviour
    {
        // Along the zone's up, units per second squared. More than her gravity, or it only slows a fall.
        public float strength = 45f;
        public float maxSpeed = 6f;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerStay2D(Collider2D other)
        {
            ZakzoqaBody z = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<ZakzoqaBody>() : null;
            if (z != null) z.Wind(transform.up * strength, maxSpeed, Time.fixedDeltaTime);
        }
    }
}
