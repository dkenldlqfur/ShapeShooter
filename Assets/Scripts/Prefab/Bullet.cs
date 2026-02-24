using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 총알 동작 관리. Raycast 연속 충돌 판정 체계(Continuous Collision Detection) 기반의 표면 
    /// 타격 위치 계산 및 오브젝트 풀 반환 처리를 수행합니다.
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private float lifeTime = 5f;

        private CancellationTokenSource cts;
        private bool isReturning = false;

        private void OnEnable()
        {
            isReturning = false;
            cts = new();
            AutoReturnToPool(cts.Token).Forget();
        }

        private void OnDisable()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        /// <summary>
        /// 수명 초과 시 자동으로 풀에 반환
        /// </summary>
        private async UniTaskVoid AutoReturnToPool(CancellationToken token)
        {
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(lifeTime), cancellationToken: token).SuppressCancellationThrow();

            if (!canceled && !isReturning && null != BulletManager.Instance && gameObject.activeInHierarchy)
            {
                isReturning = true;
                BulletManager.Instance.Return(this);
            }
        }

        private void Update()
        {
            if (isReturning)
                return;

            float moveDistance = speed * Time.deltaTime;
            
            // 앞으로 이동할 거리만큼 미리 Raycast를 쏘아 연속 충돌(관통 방지)을 검사합니다.
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, moveDistance))
            {
                var polygonManager = hit.collider.GetComponentInParent<PolygonColorManager>();
                if (null != polygonManager)
                    polygonManager.OnHitAccurate(hit.point, hit.normal, transform.forward);

                transform.position = hit.point; // 표면까지만 이동시킨 뒤 즉각 반환
                ReturnToPool();
                return;
            }

            transform.Translate(moveDistance * Vector3.forward);
        }

        /// <summary>
        /// 풀에 반환하거나, 풀이 없으면 파괴
        /// </summary>
        private void ReturnToPool()
        {
            if (isReturning)
                return;

            isReturning = true;

            if (null != BulletManager.Instance)
                BulletManager.Instance.Return(this);
            else
                Destroy(gameObject);
        }
    }
}
