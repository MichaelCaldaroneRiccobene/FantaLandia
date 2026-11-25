using FantaLandia.Core;
using FantaLandia.Player;
using UnityEngine;

namespace FantaLandia.CameraSystem
{
    /// <summary>
    /// Camera dall'alto: il rig è un perno a terra, la camera è figlia a distanza/inclinazione variabili.
    /// WASD sposta, Q/E e tasto centrale ruotano, rotellina zooma, F segue il personaggio, Spazio centra.
    /// L'altezza del perno segue sempre il personaggio, così salendo i piani la camera sale con lui.
    /// </summary>
    public class RTSCameraRig : MonoBehaviour
    {
        public Camera cam;

        [Header("Movimento")]
        public float panSpeed = 16f;
        public float keyRotateSpeed = 100f;
        public float mouseRotateSpeed = 0.3f;

        [Header("Zoom")]
        public float distance = 20f;
        public float minDistance = 5f;
        public float maxDistance = 50f;
        public float zoomStep = 0.15f;

        [Header("Angoli")]
        public float yaw = 45f;
        public float pitch = 55f;
        public float minPitch = 25f;
        public float maxPitch = 85f;

        [Header("Inseguimento")]
        public bool follow = true;
        public float followSharpness = 6f;
        public float heightSharpness = 5f;

        public bool IsFollowing => follow;

        float targetDistance;
        Vector2 lastMouse;

        void Awake()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            targetDistance = distance;
            lastMouse = GameInput.MousePosition;
            ApplyTransform();
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var focus = SelectionController.Instance != null ? SelectionController.Instance.Focus : null;

            if (GameInput.FollowToggle) follow = !follow;
            if (GameInput.CenterCamera && focus != null)
            {
                follow = true;
                transform.position = focus.transform.position;
            }

            Vector2 pan = GameInput.PanAxis;
            if (pan.sqrMagnitude > 0.01f)
            {
                follow = false;
                Vector3 move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(pan.x, 0f, pan.y);
                transform.position += move.normalized * panSpeed * (distance / 20f) * dt;
            }

            yaw += GameInput.RotateAxis * keyRotateSpeed * dt;
            Vector2 mouse = GameInput.MousePosition;
            Vector2 delta = mouse - lastMouse;
            lastMouse = mouse;
            if (GameInput.MiddleHeld)
            {
                yaw += delta.x * mouseRotateSpeed;
                pitch = Mathf.Clamp(pitch - delta.y * mouseRotateSpeed, minPitch, maxPitch);
            }

            float scroll = GameInput.Scroll;
            if (Mathf.Abs(scroll) > 0.001f)
                targetDistance = Mathf.Clamp(targetDistance * (1f - scroll * zoomStep), minDistance, maxDistance);
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-12f * dt));

            if (focus != null)
            {
                Vector3 p = transform.position;
                Vector3 t = focus.transform.position;
                if (follow)
                {
                    float k = 1f - Mathf.Exp(-followSharpness * dt);
                    p.x = Mathf.Lerp(p.x, t.x, k);
                    p.z = Mathf.Lerp(p.z, t.z, k);
                }
                p.y = Mathf.Lerp(p.y, t.y, 1f - Mathf.Exp(-heightSharpness * dt));
                transform.position = p;
            }

            ApplyTransform();
        }

        public void ApplyTransform()
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cam == null) return;
            Quaternion tilt = Quaternion.Euler(pitch, 0f, 0f);
            cam.transform.localRotation = tilt;
            cam.transform.localPosition = tilt * new Vector3(0f, 0f, -distance);
        }
    }
}
