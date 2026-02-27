using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShooter
{
    /// <summary>
    /// 플레이어 객체의 궤도 비행 렌더링 시스템 및 조작 입력을 제어하는 컴포넌트입니다.
    /// 마우스 포인터 방향(혹은 중심점) 측위 방식을 통해 발사체의 궤적을 갱신합니다.
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
        /// 카메라 하위 컴포넌트가 항상 구심점을 응시(LookAt)하도록 후기 프레임 연산을 보정합니다.
        /// </summary>
        private void LateUpdate()
        {
            if (null != playerCamera)
                playerCamera.transform.LookAt(Vector3.zero, transform.up);
        }

        /// <summary>
        /// 입력 벡터를 기반으로 대상체 주위의 구면 궤도상 이동을 수행합니다. 외적을 산출하여 회전 쿼터니언을 적용합니다.
        /// </summary>
        private void HandleMovement()
        {
            var input = moveAction.ReadValue<Vector2>();
            var moveDir = (transform.right * input.x) + (transform.up * input.y);

            if (0.001f > moveDir.sqrMagnitude)
                return;

            // 현재 위치 벡터와 이동 대상 벡터의 외적 연산(Cross) 구동을 통해 회전축을 도출합니다.
            var axis = Vector3.Cross(transform.position, moveDir).normalized;

            float speedMultiplier = 1.0f;
            if (boostAction.IsPressed())
                speedMultiplier = boostRate;
            float angle = orbitSpeed * speedMultiplier * Time.deltaTime;
            var rotation = Quaternion.AngleAxis(angle, axis);

            transform.position = rotation * transform.position;

            // 원점을 수직으로 바라보는 짐벌락 방지 및 중심 정렬 처리 시퀀스입니다.
            var forward = (Vector3.zero - transform.position).normalized;
            var stableUp = rotation * transform.up;

            // Forward 벡터 정렬 시 Up 벡터 상실을 막기 위해 교차 대체합니다.
            if (0.001f > Vector3.Cross(forward, stableUp).sqrMagnitude)
                stableUp = rotation * transform.right;

            transform.rotation = Quaternion.LookRotation(forward, stableUp);
        }

        /// <summary>
        /// 조작 부재 상태 시 짐벌 정렬을 위한 기본 방향성 보정 연산입니다.
        /// </summary>
        private void LookAtTarget()
        {
            var forward = (Vector3.zero - transform.position).normalized;
            var currentUp = transform.up;

            if (0.001f > Vector3.Cross(forward, currentUp).sqrMagnitude)
                currentUp = transform.right;

            transform.rotation = Quaternion.LookRotation(forward, currentUp);
        }

        /// <summary>
        /// 발사 이벤트 트리거 시, 스크린 스페이스상의 커서 위치를 월드 레이(Ray)로 변환해 충돌점에 투사체를 발포합니다.
        /// </summary>
        private void OnFire(InputAction.CallbackContext context)
        {
            if (null != GameManager.Instance && !GameManager.Instance.IsGameActive)
                return;

            if (null == firePoint)
                return;

            // 2D 스크린 마우스 포인터의 위치를 3D 공간상의 추상화 레이캐스팅 선분으로 치환합니다.
            var ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            
            Vector3 targetPoint = ray.GetPoint(100f);

            // 레이캐스트가 표면에 닿았을 경우 적시 충돌점을, 아닌 경우 최장 사거리를 강제 부여합니다.
            if (Physics.Raycast(ray, out var hit))
                targetPoint = hit.point;

            var direction = (targetPoint - firePoint.position).normalized;
            var lookRotation = Quaternion.LookRotation(direction);

            if (null != BulletManager.Instance)
            {
                BulletManager.Instance.Get(firePoint.position, lookRotation);
                GameManager.Instance.IncrementShotCount();

                if (null != ParticleManager.Instance)
                    ParticleManager.Instance.PlayMuzzleFlash(firePoint.position, lookRotation);
            }
        }

        /// <summary>
        /// 씬 전환 등 환경 리셋 시 원형 포지션 데이터로 복원하기 위한 호출점입니다.
        /// </summary>
        public void ResetPosition()
        {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
        }
    }
}
