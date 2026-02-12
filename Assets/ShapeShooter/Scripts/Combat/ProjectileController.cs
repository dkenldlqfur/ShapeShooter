using UnityEngine;
using ShapeShooter.Shape;

namespace ShapeShooter.Combat
{
    public class ProjectileController : MonoBehaviour
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private float lifeTime = 5f;

        private void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            // 충돌한 대상이 IShapeFace 인터페이스를 구현하는지 확인
            IShapeFace face = other.GetComponent<IShapeFace>();
            if (null != face)
            {
                face.OnHit(transform.position);
                Destroy(gameObject);
            }
            else
                // 벽이나 다른 물체에 부딪혔을 때도 소멸
                Destroy(gameObject);
        }
    }
}
