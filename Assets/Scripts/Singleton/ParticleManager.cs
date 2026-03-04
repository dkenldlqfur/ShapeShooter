using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ShapeShooter
{
    /// <summary>
    /// 피격 시 발생하는 폴리곤 형상 파편 파티클의 오브젝트 풀 관리 및 이펙트 재생을 전담하는 싱글톤 매니저입니다.
    /// </summary>
    public class ParticleManager : Singleton<ParticleManager>
    {
        [SerializeField] private int defaultCapacity = 30;
        [SerializeField] private int maxSize = 100;

        private readonly Dictionary<string, IObjectPool<ParticleSystem>> pools = new();
        private readonly Dictionary<string, GameObject> prefabs = new();

        /// <summary>
        /// 싱글톤 초기화 시점에 기본 파티클 프리팹들을 비동기로 미리 로드하고 풀을 구성합니다.
        /// </summary>
        protected override void Init()
        {
            InitAsync().Forget();
        }

        private async UniTaskVoid InitAsync()
        {
            await PreloadPrefab("Prefabs/Particles/HitParticle");
            await PreloadPrefab("Prefabs/Particles/MuzzleFlash");
        }

        private async UniTask PreloadPrefab(string prefabPath)
        {
            if (!prefabs.ContainsKey(prefabPath))
            {
                var prefab = await AssetManager.Instance.LoadAssetAsync<GameObject>(prefabPath);
                if (null != prefab)
                {
                    prefabs[prefabPath] = prefab;
                    GetPool(prefabPath);
                }
            }
        }

        private IObjectPool<ParticleSystem> GetPool(string prefabPath)
        {
            if (pools.TryGetValue(prefabPath, out var pool))
                return pool;

            if (!prefabs.TryGetValue(prefabPath, out var prefab) || null == prefab)
                return null;
            
            var objPool = new ObjectPool<ParticleSystem>(
                createFunc: () => {
                    var go = Instantiate(prefabs[prefabPath], transform);
                    var sys = go.GetComponent<ParticleSystem>();
                    go.AddComponent<ParticlePoolReturner>();
                    // 필요 시 파티클을 콜백으로 호출할 때 풀을 지연 할당하거나,
                    // 순환 참조를 끊기 위한 설정 단계에 의존하여 할당합니다.
                    return sys;
                },
                actionOnGet: ps => {
                    ps.gameObject.SetActive(true);
                    var returner = ps.GetComponent<ParticlePoolReturner>();
                    if (null != returner && null == returner.Pool)
                        returner.Pool = pools[prefabPath]; // ObjectPool 생성자 직후에 pools[prefabPath]가 할당되므로 참조에 안전합니다.
                },
                actionOnRelease: ps => ps.gameObject.SetActive(false),
                actionOnDestroy: ps => Destroy(ps.gameObject),
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );

            pools[prefabPath] = objPool;
            return objPool;
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

            var center = (v0 + v1 + v2) / 3f;
            ps.transform.position = center;

            // 발사체 방향과 표면 노멀의 반사 벡터를 산출하여 파편 비산 방향을 결정합니다.
            var reflectDir = Vector3.Reflect(bulletForward, hitNormal).normalized;
            // 반사 벡터와 표면 노멀을 혼합하여 자연스러운 파편 비산 각도를 산출합니다.
            var debrisDir = Vector3.Slerp(hitNormal, reflectDir, 0.5f).normalized;
            ps.transform.rotation = Quaternion.LookRotation(debrisDir);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var returner = ps.GetComponent<ParticlePoolReturner>();

            if (null == returner.SharedMesh)
            {
                // 인스턴스별로 최초 1회만 메쉬를 생성합니다.
                returner.SharedMesh = BuildTriangleMesh(v0 - center, v1 - center, v2 - center);
            }
            else
            {
                // 이미 존재한다면 기존 메쉬의 정점만 업데이트하여 메모리(GC) 누적을 방지합니다.
                UpdateTriangleMesh(returner.SharedMesh, v0 - center, v1 - center, v2 - center);
            }

            renderer.mesh = returner.SharedMesh;

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

        /// <summary>
        /// 생성된 메쉬의 정점 데이터만 업데이트하여 메모리 재할당을 방지합니다.
        /// </summary>
        private void UpdateTriangleMesh(Mesh mesh, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            mesh.SetVertices(new[] { v0, v1, v2 });
            mesh.RecalculateBounds();
        }
    }

    /// <summary>
    /// ParticleSystem의 StopAction 콜백을 수신하여 풀로 반환하는 래퍼 컴포넌트입니다.
    /// 메모리 할당(GC) 없이 안전하게 재사용 사이클을 구성합니다.
    /// </summary>
    public class ParticlePoolReturner : MonoBehaviour
    {
        public IObjectPool<ParticleSystem> Pool { get; set; }
        public Mesh SharedMesh { get; set; }
        
        private ParticleSystem ps;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
        }

        private void OnDestroy()
        {
            if (null != SharedMesh)
                Destroy(SharedMesh);
        }

        private void OnParticleSystemStopped()
        {
            if (null != Pool)
                Pool.Release(ps);
        }
    }
}
