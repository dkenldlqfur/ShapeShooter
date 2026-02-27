using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ShapeShooter
{
    /// <summary>
    /// 피격 시 발생하는 폴리곤 형상 파편 파티클의 오브젝트 풀 관리 및 이펙트 재생을 전담하는 싱글톤 매니저입니다.
    /// </summary>
    public class ParticleManager : MonoBehaviour
    {
        private static ParticleManager instance;

        public static ParticleManager Instance
        {
            get
            {
                if (null == instance)
                {
                    instance = FindAnyObjectByType<ParticleManager>();
                    if (null == instance)
                    {
                        var go = new GameObject("ParticleManager");
                        instance = go.AddComponent<ParticleManager>();
                        instance.InitPool();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [SerializeField] private int defaultCapacity = 30;
        [SerializeField] private int maxSize = 100;

        private readonly Dictionary<string, IObjectPool<ParticleSystem>> pools = new();
        private readonly Dictionary<string, GameObject> prefabs = new();

        private IObjectPool<ParticleSystem> GetPool(string prefabPath)
        {
            if (pools.TryGetValue(prefabPath, out var pool))
                return pool;

            var prefab = Resources.Load<GameObject>(prefabPath);
            if (null == prefab)
                return null;

            prefabs[prefabPath] = prefab;
            
            var objPool = new ObjectPool<ParticleSystem>(
                createFunc: () => {
                    var go = Instantiate(prefabs[prefabPath], transform);
                    var sys = go.GetComponent<ParticleSystem>();
                    var returner = go.AddComponent<ParticlePoolReturner>();
                    // We assign the pool lazily when the particles are fetched if needed, 
                    // or rely on a setup method to break the circular dependency.
                    return sys;
                },
                actionOnGet: ps => {
                    ps.gameObject.SetActive(true);
                    var returner = ps.GetComponent<ParticlePoolReturner>();
                    if (returner != null && returner.Pool == null)
                    {
                        returner.Pool = pools[prefabPath]; // Safe because pools[prefabPath] is assigned right after ObjectPool constructor
                    }
                },
                actionOnRelease: ps => ps.gameObject.SetActive(false),
                actionOnDestroy: ps => Destroy(ps.gameObject),
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );

            pools[prefabPath] = objPool;
            return objPool;
        }

        private void InitPool()
        {
            GetPool("Prefabs/Particles/HitParticle");
            GetPool("Prefabs/Particles/MuzzleFlash");
        }


        /// <summary>
        /// 피격된 삼각형의 월드 좌표 꼭짓점 3개와 표면 노멀, 색상을 받아 기본 파편 파티클(`HitParticle`)을 재생합니다.
        /// </summary>
        public void PlayHitEffect(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 hitNormal, Vector3 bulletForward, Color color)
        {
            PlayEffect("Prefabs/Particles/HitParticle", v0, v1, v2, hitNormal, bulletForward, color);
        }

        /// <summary>
        /// 지정된 프리팹 경로의 파티클 풀을 확장하여 범용적으로 이펙트를 재생합니다.
        /// </summary>
        public void PlayEffect(string prefabPath, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 hitNormal, Vector3 bulletForward, Color color)
        {
            var targetPool = GetPool(prefabPath);
            if (null == targetPool)
                return;

            var ps = targetPool.Get();

            Vector3 center = (v0 + v1 + v2) / 3f;
            ps.transform.position = center;

            // 발사체 방향과 표면 노멀의 반사 벡터를 산출하여 파편 비산 방향을 결정합니다.
            Vector3 reflectDir = Vector3.Reflect(bulletForward, hitNormal).normalized;
            // 반사 벡터와 표면 노멀을 혼합하여 자연스러운 파편 비산 각도를 산출합니다.
            Vector3 debrisDir = Vector3.Slerp(hitNormal, reflectDir, 0.5f).normalized;
            ps.transform.rotation = Quaternion.LookRotation(debrisDir);

            var mesh = BuildTriangleMesh(v0 - center, v1 - center, v2 - center);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.mesh = mesh;

            var main = ps.main;
            main.startColor = color;
            main.stopAction = ParticleSystemStopAction.Callback;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }

        /// <summary>
        /// 발사체 생성 지점에서 짧은 시간 유지되는 총구 화염 파티클을 재생합니다.
        /// </summary>
        public void PlayMuzzleFlash(Vector3 position, Quaternion rotation)
        {
            var mPool = GetPool("Prefabs/Particles/MuzzleFlash");
            if (null == mPool)
                return;

            var ps = mPool.Get();
            ps.transform.SetPositionAndRotation(position, rotation);
            
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Callback;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }

        /// <summary>
        /// 3개의 로컬 좌표 꼭짓점으로 양면(Double-sided) 삼각형 메쉬를 동적 생성합니다.
        /// </summary>
        private Mesh BuildTriangleMesh(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            var mesh = new Mesh
            {
                vertices = new[] { v0, v1, v2 },
                triangles = new[] { 0, 1, 2, 2, 1, 0 },
                normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward }
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// ParticleSystem의 StopAction 콜백을 수신하여 풀로 반환하는 래퍼 컴포넌트입니다.
    /// 메모리 할당(GC) 없이 안전하게 재사용 사이클을 구성합니다.
    /// </summary>
    public class ParticlePoolReturner : MonoBehaviour
    {
        public IObjectPool<ParticleSystem> Pool { get; set; }
        private ParticleSystem ps;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
        }

        private void OnParticleSystemStopped()
        {
            if (null != Pool)
                Pool.Release(ps);
        }
    }
}
