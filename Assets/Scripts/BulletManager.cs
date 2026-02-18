using UnityEngine;
using UnityEngine.Pool;

namespace ShapeShooter
{
    /// <summary>
    /// 총알 오브젝트 풀 관리 (싱글톤). 총알 생성/회수/파괴 담당
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
        [SerializeField] private int defaultCapacity = 20;
        [SerializeField] private int maxSize = 100;

        private IObjectPool<Bullet> pool;

        private void InitPool()
        {
            if (null == bulletPrefab)
                return;

            pool = new ObjectPool<Bullet>(
                createFunc: CreateBullet,
                actionOnGet: OnGetBullet,
                actionOnRelease: OnReleaseBullet,
                actionOnDestroy: OnDestroyBullet,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );
        }

        private Bullet CreateBullet()
        {
            return Instantiate(bulletPrefab, transform);
        }

        private void OnGetBullet(Bullet bullet)
        {
            bullet.gameObject.SetActive(true);
        }

        private void OnReleaseBullet(Bullet bullet)
        {
            bullet.gameObject.SetActive(false);
        }

        private void OnDestroyBullet(Bullet bullet)
        {
            Destroy(bullet.gameObject);
        }

        /// <summary>
        /// 풀에서 총알을 꺼내 지정 위치/회전으로 배치
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
        /// 총알을 풀에 반환
        /// </summary>
        public void Return(Bullet bullet)
        {
            if (gameObject.activeInHierarchy && null != pool)
                pool.Release(bullet);
        }
    }
}
