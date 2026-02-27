using UnityEngine;
using UnityEngine.Pool;

namespace ShapeShooter
{
    /// <summary>
    /// 발사체(Bullet) 객체들의 생성, 반환, 제거 및 재사용을 담당하는 오브젝트 풀 매니저입니다.
    /// 메모리 단편화를 방지하고 투사체 생성의 최적화를 수행합니다.
    /// </summary>
    public class BulletManager : MonoBehaviour
    {
        private static BulletManager instance;

        public static BulletManager Instance
        {
            get
            {
                if (null == instance)
                {
                    instance = FindAnyObjectByType<BulletManager>();
                    if (null == instance)
                    {
                        var go = new GameObject("BulletManager");
                        instance = go.AddComponent<BulletManager>();
                        
                        var bPrefab = Resources.Load<Bullet>("Prefabs/Bullet");
                        if (null != bPrefab)
                        {
                            instance.bulletPrefab = bPrefab;
                            instance.InitPool();
                        }

                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [SerializeField] private Bullet bulletPrefab;
        [SerializeField] private int defaultCapacity = 50;
        [SerializeField] private int maxSize = 300;

        private IObjectPool<Bullet> pool;

        private void InitPool()
        {
            if (null == bulletPrefab)
                return;

            pool = new ObjectPool<Bullet>(
                createFunc: () => Instantiate(bulletPrefab, transform),
                actionOnGet: b => b.gameObject.SetActive(true),
                actionOnRelease: b => b.gameObject.SetActive(false),
                actionOnDestroy: b => Destroy(b.gameObject),
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );
        }

        /// <summary>
        /// 풀 내부에서 가용한 발사체 객체를 찾아 지정된 위치 및 회전값으로 배치한 뒤 반환합니다.
        /// </summary>
        public Bullet Get(Vector3 position = default, Quaternion rotation = default)
        {
            if (null == pool)
                return null;

            var bullet = pool.Get();
            bullet.transform.SetPositionAndRotation(position, rotation);
            return bullet;
        }

        /// <summary>
        /// 수명이 다하거나 충돌이 완료된 대상을 다시 내부 풀 보관소로 환원합니다.
        /// </summary>
        public void Return(Bullet bullet)
        {
            if (gameObject.activeInHierarchy && null != pool)
                pool.Release(bullet);
        }
    }
}
