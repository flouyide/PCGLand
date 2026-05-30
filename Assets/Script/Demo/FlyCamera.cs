using UnityEngine;
using UnityEngine.InputSystem;

namespace PCGLand
{
    /// <summary>
    /// 简易自由飞行相机（新版 Input System，直接读设备，无需 Input 资产）。
    /// WASD 平移，QE 升降，按住右键鼠标视角，Shift 加速。作为流式加载中心。
    /// </summary>
    public sealed class FlyCamera : MonoBehaviour
    {
        public float moveSpeed = 30f;
        public float boostMultiplier = 4f;
        public float lookSensitivity = 0.12f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // 视角（按住右键）
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * lookSensitivity;
                _pitch -= delta.y * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            // 平移
            Vector3 dir = Vector3.zero;
            if (kb.wKey.isPressed) dir += transform.forward;
            if (kb.sKey.isPressed) dir -= transform.forward;
            if (kb.dKey.isPressed) dir += transform.right;
            if (kb.aKey.isPressed) dir -= transform.right;
            if (kb.eKey.isPressed) dir += Vector3.up;
            if (kb.qKey.isPressed) dir -= Vector3.up;

            float speed = moveSpeed * (kb.leftShiftKey.isPressed ? boostMultiplier : 1f);
            transform.position += dir.normalized * speed * Time.deltaTime;
        }
    }
}
