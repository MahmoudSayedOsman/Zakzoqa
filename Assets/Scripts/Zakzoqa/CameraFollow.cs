using UnityEngine;

namespace Zakzoqa
{
    // Keeps Zakzoqa in view, side-on (Decisions §3: a platformer). Looks ahead in the direction she
    // faces, so the player sees what is coming rather than what is behind; and sits her a little below
    // the middle, so the platforms above are on screen. Smoothed, so a jump does not jerk the screen.
    public class CameraFollow : MonoBehaviour
    {
        public ZakzoqaBody target;
        // How far ahead of her the camera looks, in world units.
        public float lookAhead = 2f;
        // How far above her the camera centres.
        public float raise = 1.2f;
        public float smoothTime = 0.2f;
        // Levels set these to their bounds; the camera never shows past them.
        public Vector2 min = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        public Vector2 max = new Vector2(float.PositiveInfinity, float.PositiveInfinity);

        Vector3 velocity;

        void Awake()
        {
            Camera cam = GetComponent<Camera>();
            if (cam != null) cam.orthographicSize = WorldScale.OrthographicSize;
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 goal = (Vector3)target.Position + new Vector3(target.Facing * lookAhead, raise, 0f);
            goal.x = Mathf.Clamp(goal.x, min.x, max.x);
            goal.y = Mathf.Clamp(goal.y, min.y, max.y);
            goal.z = transform.position.z;
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }
    }
}
