using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FantaLandia.Ragdoll
{
    /// <summary>Camera orbitale che segue il manichino in modo morbido.</summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 4.5f;
        public float minDistance = 1.5f;
        public float maxDistance = 15f;
        public float yaw = 150f;
        public float pitch = 15f;
        public float focusHeight = 0.1f;
        public float smoothTime = 0.12f;
        public float orbitSpeed = 90f;

        Vector3 focus, focusVelocity;
        bool initialized;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            initialized = false;
        }

        void LateUpdate()
        {
            if (target == null) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.qKey.isPressed) yaw += orbitSpeed * Time.deltaTime;
                if (kb.eKey.isPressed) yaw -= orbitSpeed * Time.deltaTime;
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    distance = Mathf.Clamp(distance * (scroll > 0f ? 0.9f : 1.1f), minDistance, maxDistance);
                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * 0.2f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.2f, -10f, 80f);
                }
            }
#endif

            Vector3 desired = target.position + Vector3.up * focusHeight;
            if (!initialized)
            {
                focus = desired;
                focusVelocity = Vector3.zero;
                initialized = true;
            }
            focus = Vector3.SmoothDamp(focus, desired, ref focusVelocity, smoothTime);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
