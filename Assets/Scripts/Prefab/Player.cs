using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShooter
{
    /// <summary>
    /// 플레이어 조작 관리. 원점을 중심으로 구면 궤도를 회전 이동하며, 마우스가 위치한 곳(혹은 전방)으로 총알을 발사합니다.
    /// </summary>
    public class Player : MonoBehaviour
    {
        [SerializeField] private float orbitSpeed = 50f;
        [SerializeField] private float boostRate = 1.2f;
        [SerializeField] private Transform firePoint;

        private Camera playerCamera;
        private InputAction moveAction;
        private InputAction fireAction;
        private InputAction boostAction;

        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private void Awake()
        {
            playerCamera = GetComponentInChildren<Camera>();

            var inputAsset = Resources.Load<InputActionAsset>("InputSystem_Actions");
            var playerMap = inputAsset.FindActionMap("Player");
            
            moveAction = playerMap.FindAction("Move");
            fireAction = playerMap.FindAction("Attack");
            boostAction = playerMap.FindAction("Sprint");

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
            if (null != GameManager.Instance && !GameManager.Instance.IsGameActive)
                return;

            HandleMovement();
            LookAtTarget();
        }

        /// <summary>
        /// 카메라가 항상 원점을 바라보도록 LateUpdate에서 갱신
        /// </summary>
        private void LateUpdate()
        {
            if (null != playerCamera)
                playerCamera.transform.LookAt(Vector3.zero, transform.up);
        }

        /// <summary>
        /// 입력 기반 구면 궤도 이동. 외적으로 회전축을 구하고 쿼터니언 회전 적용
        /// </summary>
        private void HandleMovement()
        {
            var input = moveAction.ReadValue<Vector2>();
            var moveDir = (transform.right * input.x) + (transform.up * input.y);

            if (moveDir.sqrMagnitude < 0.001f)
                return;

            // 위치 벡터 × 이동 방향 = 회전축
            var axis = Vector3.Cross(transform.position, moveDir).normalized;

            float speedMultiplier = 1.0f;
            if (boostAction.IsPressed())
                speedMultiplier = boostRate;
            float angle = orbitSpeed * speedMultiplier * Time.deltaTime;
            var rotation = Quaternion.AngleAxis(angle, axis);

            transform.position = rotation * transform.position;

            // 원점을 바라보는 방향 계산 + 극점 근처 짐벌락 방지
            var forward = (Vector3.zero - transform.position).normalized;
            var stableUp = rotation * transform.up;

            // forward와 up이 평행하면 right 벡터로 대체
            if (Vector3.Cross(forward, stableUp).sqrMagnitude < 0.001f)
                stableUp = rotation * transform.right;

            transform.rotation = Quaternion.LookRotation(forward, stableUp);
        }

        /// <summary>
        /// 이동 입력이 없을 때 원점 방향으로 회전 보정
        /// </summary>
        private void LookAtTarget()
        {
            var forward = (Vector3.zero - transform.position).normalized;
            var currentUp = transform.up;

            if (Vector3.Cross(forward, currentUp).sqrMagnitude < 0.001f)
                currentUp = transform.right;

            transform.rotation = Quaternion.LookRotation(forward, currentUp);
        }

        /// <summary>
        /// 발사 입력 시 화면(스크린 좌표)으로 Raycast를 쏘아 타겟을 조준하고 총알을 생성합니다.
        /// </summary>
        private void OnFire(InputAction.CallbackContext context)
        {
            if (null != GameManager.Instance && !GameManager.Instance.IsGameActive)
                return;

            if (null == firePoint)
                return;

            // 마우스 스크린 좌표 → 월드 레이 생성
            var ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Vector3 targetPoint;

            // 레이가 오브젝트에 맞으면 히트 지점, 아니면 레이 방향 기준 원거리 지점 사용
            if (Physics.Raycast(ray, out var hit))
                targetPoint = hit.point;
            else
                targetPoint = ray.GetPoint(100f);

            var direction = (targetPoint - firePoint.position).normalized;
            var lookRotation = Quaternion.LookRotation(direction);

            if (null != BulletManager.Instance)
            {
                BulletManager.Instance.Get(firePoint.position, lookRotation);
                GameManager.Instance.IncrementShotCount();
            }
        }

        /// <summary>
        /// 위치/회전을 초기값으로 리셋 (스테이지 전환 시 사용)
        /// </summary>
        public void ResetPosition()
        {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
        }
    }
}
