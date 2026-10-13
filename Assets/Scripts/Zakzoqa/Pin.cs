using UnityEngine;

namespace Zakzoqa
{
    // A PIN (Decisions §9: the thumbtack soldiers and their kind). Touching it lets air out of
    // Zakzoqa and pushes her away. The body decides whether it hurts (grace after a hit, already
    // popped); a pin only reports the touch, so every kind of pin behaves the same way at the edge.
    [RequireComponent(typeof(Collider2D))]
    public class Pin : MonoBehaviour
    {
        [Min(1)] public int airDamage = 1;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other) => Touch(other);
        // Still touching when the grace runs out is another hit: sitting on a pin is not safe.
        void OnTriggerStay2D(Collider2D other) => Touch(other);

        void Touch(Collider2D other)
        {
            ZakzoqaBody z = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<ZakzoqaBody>() : null;
            if (z != null) z.TakeHit(airDamage, transform.position);
        }
    }
}
