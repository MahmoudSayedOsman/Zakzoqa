using UnityEngine;

namespace Zakzoqa
{
    // PROTOTYPE (Design.md §14): a floor that holds a full balloon and gives way under a small, heavy
    // one landing hard. What is underneath is a secret.
    [RequireComponent(typeof(Collider2D))]
    public class WeakFloor : MonoBehaviour
    {
        public bool IsBroken { get; private set; }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsBroken) return;
            ZakzoqaBody z = collision.rigidbody != null ? collision.rigidbody.GetComponent<ZakzoqaBody>() : null;
            if (z == null || z.Def == null || !z.IsSmall) return;

            bool fromAbove = z.Position.y > transform.position.y;
            if (fromAbove && Mathf.Abs(collision.relativeVelocity.y) >= z.Def.breakSpeed) Break();
        }

        public void Break()
        {
            IsBroken = true;
            foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
            foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>()) r.enabled = false;
        }
    }
}
