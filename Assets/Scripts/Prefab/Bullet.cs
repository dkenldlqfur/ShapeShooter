using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 발사체(Bullet) 엔티티 모델. 물리 엔진의 Raycast를 선행 투사하는 연속 충돌 판정(CCD) 기법을 사용하여
    /// 빠른 속도로 이동하는 객체의 표면 관통 결함을 방지하고 오브젝트 풀링 생명주기를 관장합니다.
    /// 메모리 할당 방지(Zero-Allocation)를 위해 재생성 없는 비동기 루틴을 사용합니다.
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private float lifeTime = 3f;

        private bool isReturning = false;
        private CancellationTokenSource cts;

        private void Awake()
        {
            cts = new CancellationTokenSource();
        }

        private void OnEnable()
        {
            isReturning = false;
            MoveRoutine(cts.Token).Forget();
        }

        private void OnDisable()
        {
            isReturning = true;
        }

        private void OnDestroy()
        {
            if (null != cts)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        private async UniTaskVoid MoveRoutine(CancellationToken token)
        {
            float elapsed = 0f;

            while (elapsed < lifeTime && !isReturning)
            {
                // 생명 주기 내 비동기 프레임 대기
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                if (token.IsCancellationRequested || isReturning)
                    break;

                elapsed += Time.deltaTime;
                float moveDistance = speed * Time.deltaTime;

                // 프레임 델타에 비례한 다음 예상 이동 좌표값을 향해 Raycast를 쏘아 관통 이탈 오류를 추적합니다.
                if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, moveDistance))
                {
                    var polygonManager = hit.collider.GetComponentInParent<PolygonColorManager>();
                    if (null != polygonManager)
                        polygonManager.OnHitAccurate(hit.point, hit.normal, transform.forward, hit.triangleIndex);

                    transform.position = hit.point; // 메쉬 표면과의 거리를 강제 병합 후 트랜잭션 종료
                    ReturnToPool();
                    break;
                }

                transform.Translate(moveDistance * Vector3.forward);
            }

            // 루프 종료 시점 (시간 초과 등)
            if (!isReturning)
                ReturnToPool();
        }

        /// <summary>
        /// 현재 활성 인스턴스를 즉시 무효화하고 상위 메모리 풀 배열로 참조를 반환합니다.
        /// </summary>
        private void ReturnToPool()
        {
            if (isReturning)
                return;

            isReturning = true;

            if (null != BulletManager.Instance)
                BulletManager.Instance.Return(this);
        }
    }
}
