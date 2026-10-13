using UnityEngine;

namespace Zakzoqa
{
    // Keeps Zakzoqa in view. Her climb is the point of the balloon levels, so the camera leads upward:
    // she sits a little below the middle, with more of what is coming above her than behind her.
    // Smoothed, so a puff does not jerk the whole screen.
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        // Where she sits on screen, as a fraction of the half-height below the centre.
        [Range(0f, 0.8f)] public float leadUp = 0.25f;
        public float smoothTime = 0.25f;
        // Levels set these to their bounds; the camera never shows past them.
        public float minY = float.NegativeInfinity;
        public float minX = float.NegativeInfinity, maxX = float.PositiveInfinity;

        Vector3 velocity;
        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null) cam.orthographicSize = WorldScale.OrthographicSize;
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 goal = target.position + Vector3.up * (WorldScale.OrthographicSize * leadUp);
            goal.x = Mathf.Clamp(goal.x, minX, maxX);
            goal.y = Mathf.Max(goal.y, minY);
            goal.z = transform.position.z;
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }
    }
}
