using UnityEngine;
using ShapeShooter.Core;

namespace ShapeShooter.Combat
{
    public class Shooter : MonoBehaviour
    {
        [SerializeField] private ProjectileController projectilePrefab;
        [SerializeField] private Transform firePoint;

        public void Fire()
        {
            if (null == projectilePrefab || null == firePoint)
                return;

            Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            
            // 발사 횟수 기록
            if (null != GameManager.Instance)
                GameManager.Instance.IncrementShotCount();
        }
    }
}
