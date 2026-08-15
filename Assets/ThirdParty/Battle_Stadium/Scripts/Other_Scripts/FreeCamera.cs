using UnityEngine;

namespace ithappy.Battle_Stadium
{
    public class FreeCamera : MonoBehaviour
    {
        [SerializeField] 
        private float m_LookSpeedMouse = 30f;
        [SerializeField] 
        private float m_MoveSpeed = 10f;
        [SerializeField] 
        private float m_Sprint = 15f;

        private Vector2 m_Rotation;

        private void Start()
        {
            m_Rotation = Vector2.zero;
            transform.rotation = Quaternion.identity;
        }

        private void Update()
        {
            MouseLook();
            Move();
        }

        private void OnValidate()
        {
            m_Sprint = m_MoveSpeed >= m_Sprint ? m_MoveSpeed * 1.5f : m_Sprint;
        }

        private void MouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * m_LookSpeedMouse * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * m_LookSpeedMouse * Time.deltaTime;

            m_Rotation.y += mouseX;
            m_Rotation.x -= mouseY;

            m_Rotation.x = Mathf.Clamp(m_Rotation.x, -90, 90);

            transform.rotation = Quaternion.Euler(new(m_Rotation.x, m_Rotation.y, 0));
        }

        private void Move()
        {
            float horizontal = Input.GetAxis("Horizontal") * m_MoveSpeed * Time.deltaTime;
            float vertical = Input.GetAxis("Vertical") * Time.deltaTime;

            vertical *= Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? m_Sprint : m_MoveSpeed;
            transform.Translate(horizontal, 0, vertical);
        }
    }
}
