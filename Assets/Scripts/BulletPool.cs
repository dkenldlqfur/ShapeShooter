using UnityEngine;
using UnityEngine.Pool;

namespace ShapeShooter
{
    public class BulletPool : MonoBehaviour
    {
        public static BulletPool Instance { get; private set; }

        [SerializeField] private Bullet bulletPrefab;
        [SerializeField] private int defaultCapacity = 20;
        [SerializeField] private int maxSize = 100;

        private IObjectPool<Bullet> pool;

        private void Awake()
        {
            if (null == Instance)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            InitPool();
        }

        private void InitPool()
        {
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
            var bullet = Instantiate(bulletPrefab, transform);
            return bullet;
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

        public Bullet Get(Vector3 position, Quaternion rotation)
        {
            var bullet = pool.Get();
            bullet.transform.position = position;
            bullet.transform.rotation = rotation;
            return bullet;
        }

        public void Return(Bullet bullet)
        {
            if (gameObject.activeInHierarchy)
                pool.Release(bullet);
        }
    }
}
