using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 총알 동작 관리. 전방 직선 이동, 면 충돌 판정, 오브젝트 풀 반환 처리
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
            transform.Translate(speed * Time.deltaTime * Vector3.forward);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isReturning)
                return;

            if (other.TryGetComponent<ShapeFace>(out var face))
            {
                // 면의 바깥쪽 법선: 도형 중심(루트) → 면 위치 방향
                Vector3 shapeCenter = face.transform.root.position;
                Vector3 faceOutward = (face.transform.position - shapeCenter).normalized;

                // 내적 > 0.2: 총알이 완전한 뒷면에서 진입하는 경우만 무시
                // 마우스 방향 발사 시 비스듬한 각도도 유효 충돌로 인정
                float dot = Vector3.Dot(faceOutward, transform.forward);
                if (0.2f < dot)
                    return;

                face.OnHit(transform.position);
            }

            ReturnToPool();
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
