using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace ShapeShooter
{
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

        private async UniTaskVoid AutoReturnToPool(CancellationToken token)
        {
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(lifeTime), cancellationToken: token).SuppressCancellationThrow();

            if (!canceled && !isReturning && null != BulletPool.Instance && gameObject.activeInHierarchy)
            {
                isReturning = true;
                BulletPool.Instance.Return(this);
            }
        }

        private void Update()
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            // 이미 반환 중이면 무시
            if (isReturning)
                return;

            // 충돌한 대상이 IShapeFace 인터페이스를 구현하는지 확인
            var face = other.GetComponent<IShapeFace>();
            if (null != face)
            {
                face.OnHit(transform.position);
                ReturnToPool();
            }
            else
            {
                // 벽이나 다른 물체에 부딪혔을 때도 소멸 (풀 반환)
                ReturnToPool();
            }
        }

        private void ReturnToPool()
        {
            if (isReturning)
                return;

            isReturning = true;

            if (null != BulletPool.Instance)
                BulletPool.Instance.Return(this);
            else
                Destroy(gameObject); // 풀이 없으면 파괴
        }
    }
}
