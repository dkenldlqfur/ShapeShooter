using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

namespace ShapeShooter
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private float orbitSpeed = 50f;
        [SerializeField] private float boostRate = 1.2f;

        [SerializeField] private Transform firePoint;

        private Camera mainCamera;
        private InputAction moveAction;
        private InputAction fireAction;
        private InputAction boostAction;

        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private void Awake()
        {
            mainCamera = Camera.main;

            // Input System 초기화 (코드 기반)
            moveAction = new("Move", binding: "<Gamepad>/leftStick");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            fireAction = new("Fire", binding: "<Keyboard>/space");
            fireAction.AddBinding("<Mouse>/leftButton");

            boostAction = new("Sprint", binding: "<Keyboard>/leftShift");

            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        private void OnEnable()
        {
            moveAction.Enable();
            fireAction.Enable();
            boostAction.Enable();
            fireAction.performed += OnFire;
        }

        private void OnDisable()
        {
            moveAction.Disable();
            fireAction.Disable();
            boostAction.Disable();
            fireAction.performed -= OnFire;
        }

        private void Update()
        {
            HandleMovement();
            LookAtTarget();
        }

        private void HandleMovement()
        {
            var input = moveAction.ReadValue<Vector2>();

            // 1. 이동 방향 벡터 계산 (현재 회전 기준)
            // transform.right/up은 현재 플레이어의 로컬 방향 (구면 접선)
            var moveDir = (transform.right * input.x) + (transform.up * input.y);

            // 입력이 없거나 너무 작으면 패스
            if (moveDir.sqrMagnitude < 0.001f)
                return;

            // 2. 회전축 계산: 위치 벡터와 이동 방향의 외적
            var axis = Vector3.Cross(transform.position, moveDir).normalized;

            // 3. 쿼터니언 회전 적용
            float speedMultiplier = boostAction.IsPressed() ? boostRate : 1.0f;
            float angle = orbitSpeed * speedMultiplier * Time.deltaTime;
            var rotation = Quaternion.AngleAxis(angle, axis);

            // 위치 갱신
            transform.position = rotation * transform.position;

            // 4. 회전(방향) 갱신: 짐벌 락 방지
            // 원점을 바라보는 방향 계산
            var forward = (Vector3.zero - transform.position).normalized;

            // Up 벡터를 현재 회전에서 유지하여 극점(pole)에서도 안정적으로 동작
            // 현재 Up에 회전을 동일하게 적용하여 일관성 유지
            var stableUp = rotation * transform.up;

            // forward와 stableUp이 평행해지는 극단적 상황 방지
            if (Vector3.Cross(forward, stableUp).sqrMagnitude < 0.001f)
                stableUp = rotation * transform.right;

            transform.rotation = Quaternion.LookRotation(forward, stableUp);
        }

        private void LookAtTarget()
        {
            // HandleMovement에서 회전을 직접 관리하므로 입력이 없을 때만 보정
            var forward = (Vector3.zero - transform.position).normalized;
            var currentUp = transform.up;

            // forward와 현재 Up이 평행하면 Right 벡터를 사용
            if (Vector3.Cross(forward, currentUp).sqrMagnitude < 0.001f)
                currentUp = transform.right;

            transform.rotation = Quaternion.LookRotation(forward, currentUp);
        }

        private void OnFire(CallbackContext context)
        {
            if (null == firePoint)
                return;

            // (0,0,0)을 향하는 회전 계산
            var direction = (Vector3.zero - firePoint.position).normalized;
            var lookRotation = Quaternion.LookRotation(direction);

            if (null != BulletManager.Instance)
                BulletManager.Instance.Get(firePoint.position, lookRotation);

            // 발사 횟수 기록
            if (null != GameManager.Instance)
                GameManager.Instance.IncrementShotCount();
        }

        public void ResetPosition()
        {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
        }
    }
}
