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

            if (!canceled && !isReturning && null != BulletManager.Instance && gameObject.activeInHierarchy)
            {
                isReturning = true;
                BulletManager.Instance.Return(this);
            }
        }

        private void Update()
        {
            transform.Translate(speed * Time.deltaTime * Vector3.forward);
        }

        private void OnTriggerEnter(Collider other)
        {
            // 이미 반환 중이면 무시
            if (isReturning)
                return;

            if (other.TryGetComponent<ShapeFace>(out var face))
                face.OnHit(transform.position);                

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (isReturning)
                return;

            isReturning = true;

            if (null != BulletManager.Instance)
                BulletManager.Instance.Return(this);
            else
                Destroy(gameObject); // 풀이 없으면 파괴
        }
    }
}
