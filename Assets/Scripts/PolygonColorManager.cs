using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeShooter
{
    public enum PolygonColorType
    {
        White   = 0,
        Red     = 1,
        Orange  = 2,
        Yellow  = 3,
        Green   = 4,
        Blue    = 5,
        Purple  = 6,
    }

    /// <summary>
    /// 메쉬 데이터를 폴리곤 단위의 하위 엔티티로 분석 및 분할하여 독립적인 그래픽스와 상태 데이터를 부여하는 매니저 클래스입니다.
    /// Ray 충돌 기반의 정확한 타격 지점 산출과 이에 따른 정점 색상 갱신 파이프라인을 일괄 제어합니다.
    /// </summary>
    public class PolygonColorManager : MonoBehaviour
    {
        private const float RESTORE_DURATION = 0.8f;

        public static Color GetColorByHP(int hp)
        {
            var colorType = (PolygonColorType)Mathf.Clamp(hp, 0, (int)PolygonColorType.Purple);
            return GetColor(colorType);
        }

        public static Color GetColor(PolygonColorType type)
        {
            return type switch
            {
                PolygonColorType.White     => Color.white,
                PolygonColorType.Red       => Color.red,
                PolygonColorType.Orange    => new(1f, 0.5f, 0f),
                PolygonColorType.Yellow    => Color.yellow,
                PolygonColorType.Green     => Color.green,
                PolygonColorType.Blue      => Color.blue,
                PolygonColorType.Purple    => new(0.6f, 0.2f, 0.8f),
                _ => Color.white
            };
        }

        public event Action OnPolygonHit;
        public event Action OnPolygonCompleted;
        public event Action OnPolygonRestored;

        private MeshCollider meshCollider;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private Mesh separatedMesh;
        private Vector3[] vertices;
        private Vector3[] normals;
        private Color[] colors;

        private int maxHP = 1;

        /// <summary>
        /// 분할된 독립 폴리곤에 대한 상태 추적 및 메타데이터 컨테이너입니다.
        /// </summary>
        private class PolygonGroup
        {
            public int currentHP;
            public int maxHP;
            public bool isCompleted;
            public int vertexBase; // 해당 삼각형의 첫 번째 정점 인덱스 (vertexBase, +1, +2)
            public Color originalColor;
        }

        /// <summary>
        /// HP 0→1 복원 시 색상이 서서히 번져가는 애니메이션의 상태 데이터입니다.
        /// </summary>
        private class RestoreAnimation
        {
            public int groupIndex;
            public float progress;  // 0 → 1
            public Color targetColor;
        }

        private PolygonGroup[] polygonGroups;
        private readonly List<RestoreAnimation> activeRestoreAnimations = new();

        public int TotalPolygons
        {
            get
            {
                if (null != polygonGroups)
                    return polygonGroups.Length;
                return 0;
            }
        }
        public int CompletedPolygons { get; private set; }

        public void Initialize(LevelData levelData)
        {
            if (null != levelData)
                maxHP = levelData.requiredHitsPerPolygon;
            else
                maxHP = 1;

            meshCollider = GetComponent<MeshCollider>();
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            if (null == meshFilter || null == meshFilter.sharedMesh)
                return;

            CreateIndependentPolygons();

            // 분리된 메쉬를 물리 계층에 편입시킴으로써 충돌 인덱스와 내부 데이터 모델의 동기화를 무결하게 보장합니다.
            if (null != meshCollider)
                meshCollider.sharedMesh = separatedMesh;

            BuildPolygonGroups();
            UpdateAllColors();
        }

        private void CreateIndependentPolygons()
        {
            Mesh originalMesh = meshFilter.sharedMesh;
            if (null != meshCollider && null != meshCollider.sharedMesh)
                originalMesh = meshCollider.sharedMesh;
            int[] origTris = originalMesh.triangles;
            Vector3[] origVerts = originalMesh.vertices;
            Vector3[] origNorms = originalMesh.normals;

            vertices = new Vector3[origTris.Length];
            normals = new Vector3[origTris.Length];
            colors = new Color[origTris.Length];
            var newTris = new int[origTris.Length];

            // UV0: 정점당 무게중심(Barycentric) 좌표
            var baryUVs = new Vector2[origTris.Length];
            // UV1: 정점당 원본 메쉬 색상 (Vector3 RGB 형태로 저장)
            var origColorUVs = new Vector3[origTris.Length];

            bool hasNormals = 0 < origNorms.Length;
            bool hasColors = 0 < originalMesh.colors.Length;

            for (int i = 0; i < origTris.Length; i++)
            {
                int origIndex = origTris[i];
                vertices[i] = origVerts[origIndex];

                if (hasNormals)
                    normals[i] = origNorms[origIndex];
                else
                    normals[i] = Vector3.up;

                if (hasColors)
                    colors[i] = originalMesh.colors[origIndex];
                else
                    colors[i] = Color.white;

                newTris[i] = i;

                // 삼각형의 3개 정점에 무게중심 좌표 (1,0), (0,1), (0,0) 을 각각 할당합니다.
                int vertInTri = i % 3;
                baryUVs[i] = vertInTri switch
                {
                    0 => new Vector2(1f, 0f),
                    1 => new Vector2(0f, 1f),
                    _ => new Vector2(0f, 0f),
                };

                // 셰이더에서 참조할 원본 색상을 UV1 채널에 저장합니다.
                origColorUVs[i] = new Vector3(colors[i].r, colors[i].g, colors[i].b);
            }

            separatedMesh = new Mesh()
            {
                indexFormat = IndexFormat.UInt32,
                vertices = vertices,
                normals = normals,
                colors = colors,
                triangles = newTris
            };

            // 무게중심 좌표 및 원본 색상 UV 채널을 메쉬에 주입합니다.
            separatedMesh.SetUVs(0, baryUVs);
            separatedMesh.SetUVs(1, origColorUVs);

            meshFilter.mesh = separatedMesh;

            // 렌더링 파이프라인 최적화 및 Z-Buffer 오류 억제를 위해 정점 단위의 채색 셰이더 기반 머티리얼을 연동합니다.
            var mat = new Material(Shader.Find("Custom/VertexColorUnlit"));
            meshRenderer.material = mat;
        }

        /// <summary>
        /// 분할된 서브 메쉬 단위별로 별개의 논리형 생명주기를 주입하고 초기화합니다.
        /// </summary>
        private void BuildPolygonGroups()
        {
            int triCount = vertices.Length / 3;
            polygonGroups = new PolygonGroup[triCount];

            for (int t = 0; t < triCount; t++)
            {
                int baseIdx = t * 3;
                polygonGroups[t] = new PolygonGroup()
                {
                    maxHP = this.maxHP,
                    currentHP = this.maxHP,
                    isCompleted = false,
                    vertexBase = baseIdx,
                    originalColor = colors[baseIdx]
                };
            }
        }

        private void UpdateAllColors()
        {
            foreach (var group in polygonGroups)
            {
                Color polygonColor = group.originalColor;
                if (0 != group.currentHP)
                    polygonColor = GetColorByHP(group.currentHP);

                // Alpha=1은 셰이더가 스프레드 애니메이션 없이 색상을 직접 렌더링하도록 지시합니다.
                polygonColor.a = 1f;

                colors[group.vertexBase] = polygonColor;
                colors[group.vertexBase + 1] = polygonColor;
                colors[group.vertexBase + 2] = polygonColor;
            }
            separatedMesh.colors = colors;
        }

        private void Update()
        {
            if (0 == activeRestoreAnimations.Count)
                return;

            bool isDirty = false;

            for (int i = activeRestoreAnimations.Count - 1; 0 <= i; i--)
            {
                var anim = activeRestoreAnimations[i];
                anim.progress += Time.deltaTime * 0.5f;

                if (1f <= anim.progress)
                {
                    // 애니메이션 완료: 최종 색상을 전체 Alpha 값으로 확정합니다.
                    anim.progress = 1f;
                    polygonGroups[anim.groupIndex].currentHP = polygonGroups[anim.groupIndex].maxHP;
                    SetPolygonColor(polygonGroups[anim.groupIndex].vertexBase, anim.targetColor, 1f);
                    activeRestoreAnimations.RemoveAt(i);
                }
                else
                {
                    // 정점 Alpha를 통해 채움 진행률을 갱신합니다 (셰이더가 스프레드 이펙트에 사용).
                    float easedProgress = EaseOutCubic(anim.progress);
                    SetPolygonColor(polygonGroups[anim.groupIndex].vertexBase, anim.targetColor, easedProgress);
                }

                isDirty = true;
            }

            if (isDirty)
                separatedMesh.colors = colors;
        }

        private static float EaseOutCubic(float t)
        {
            float f = 1f - t;
            return 1f - f * f * f;
        }

        private void SetPolygonColor(int vertexBase, Color color, float alpha)
        {
            color.a = alpha;
            colors[vertexBase] = color;
            colors[vertexBase + 1] = color;
            colors[vertexBase + 2] = color;
        }

        /// <summary>
        /// Möller-Trumbore 광선-삼각형 교차 판독 알고리즘입니다. 평면상의 교차 여부 및 거리 t를 산출합니다.
        /// </summary>
        private bool IntersectRayTriangle(Vector3 rayOrigin, Vector3 rayDir, Vector3 v0, Vector3 v1, Vector3 v2, out float t)
        {
            t = 0f;
            const float EPSILON = 0.0000001f;
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(rayDir, edge2);
            float a = Vector3.Dot(edge1, h);

            if (-EPSILON < a && EPSILON > a)
                return false; // 해당 광선 벡터가 삼면과 평행 구도에 있어 교차불능(Parallel) 상태임을 시사합니다.

            float f = 1.0f / a;
            Vector3 s = rayOrigin - v0;
            float u = f * Vector3.Dot(s, h);

            if (0.0f > u || 1.0f < u)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(rayDir, q);

            if (0.0f > v || 1.0f < u + v)
                return false;

            t = f * Vector3.Dot(edge2, q);
            return t > EPSILON;
        }

        private int FindHitTriangleByRaycast(Vector3 hitPointWorld, Vector3 rayForwardWorld)
        {
            // 투사체의 로컬-월드 좌표 트랜스폼 역순행을 도출하고 연장선상의 광선을 재구성합니다.
            Vector3 originWorld = hitPointWorld - 2.0f * rayForwardWorld;
            Vector3 localOrigin = transform.InverseTransformPoint(originWorld);
            Vector3 localDir = transform.InverseTransformDirection(rayForwardWorld).normalized;

            int bestTriangle = -1;
            float closestT = float.MaxValue;
            int triangleCount = vertices.Length / 3;

            for (int i = 0; i < triangleCount; i++)
            {
                int vIdx = i * 3;
                Vector3 v0 = vertices[vIdx];
                Vector3 v1 = vertices[vIdx + 1];
                Vector3 v2 = vertices[vIdx + 2];

                if (IntersectRayTriangle(localOrigin, localDir, v0, v1, v2, out float t) && t < closestT)
                {
                    closestT = t;
                    bestTriangle = i;
                }
            }

            // 광선 역추적 실패 시, 지리적 원점을 기준으로 한 백업 폴백(Fallback) 보정 루틴입니다.
            if (-1 == bestTriangle)
            {
                Vector3 localHit = transform.InverseTransformPoint(hitPointWorld);
                float minCenterDist = float.MaxValue;
                for (int i = 0; i < triangleCount; i++)
                {
                    int vIdx = i * 3;
                    Vector3 center = (vertices[vIdx] + vertices[vIdx + 1] + vertices[vIdx + 2]) / 3f;
                    float dist = (center - localHit).sqrMagnitude;
                    if (dist < minCenterDist)
                    {
                        minCenterDist = dist;
                        bestTriangle = i;
                    }
                }
            }

            return bestTriangle;
        }

        /// <summary>
        /// 물리 연산 엔진에 의해 판독 완료된 인덱스 레퍼런스 값에 대응하는 도형 데이터의 색상 변화 이벤트를 트리거합니다.
        /// </summary>
        public void OnHitAccurate(Vector3 hitPointWorld, Vector3 hitNormalWorld, Vector3 bulletForward, int colliderTriangleIndex)
        {
            // 폴리곤의 Normal 벡터와 투척물의 진행 방향간의 내적(Dot)이 양수일 경우 메쉬 배면(Culling 뒷쪽) 피격이므로 연산을 차단합니다.
            if (0.2f < Vector3.Dot(hitNormalWorld, bulletForward))
                return;

            if (null == polygonGroups || 0 == polygonGroups.Length)
                return;

            int triIndex = colliderTriangleIndex;

            if (-1 == triIndex || triIndex >= polygonGroups.Length)
                triIndex = FindHitTriangleByRaycast(hitPointWorld, bulletForward);

            if (-1 == triIndex)
                return;


            OnPolygonHit?.Invoke();
            ApplyHitToPolygon(triIndex, hitNormalWorld, bulletForward);
        }

        /// <summary>
        /// 레거시 근사적 공간 보정 방식의 타격 폴리곤 판정 및 생명력 차감 호출 기능입니다.
        /// </summary>
        public void OnHit(Vector3 hitPointWorld, Vector3 bulletForward)
        {
            int triIndex = FindHitTriangleByRaycast(hitPointWorld, bulletForward);
            if (-1 == triIndex)
                return;

            Vector3 hitNormalLocal = normals[triIndex * 3];
            Vector3 worldNormal = transform.TransformDirection(hitNormalLocal);

            // 배면 피격 연산 차단 블록입니다.
            if (0.2f < Vector3.Dot(worldNormal, bulletForward))
                return;

            OnPolygonHit?.Invoke();
            ApplyHitToPolygon(triIndex, worldNormal, bulletForward);
        }

        private void ApplyHitToPolygon(int groupIndex, Vector3 hitNormalWorld, Vector3 bulletForward)
        {
            var group = polygonGroups[groupIndex];
            bool wasCompleted = group.isCompleted;

            // 파티클 이펙트 트리거
            if (null != ParticleManager.Instance)
            {
                Vector3 v0 = transform.TransformPoint(vertices[group.vertexBase]);
                Vector3 v1 = transform.TransformPoint(vertices[group.vertexBase + 1]);
                Vector3 v2 = transform.TransformPoint(vertices[group.vertexBase + 2]);

                if (0 != group.currentHP)
                {
                    Color currentColor = GetColorByHP(group.currentHP);
                    ParticleManager.Instance.PlayHitEffect(v0, v1, v2, hitNormalWorld, bulletForward, currentColor);
                }
            }

            if (0 == group.currentHP)
            {
                // HP 0→1 복원: 서서히 번져가는 스프레드 애니메이션 개시
                group.currentHP = 1;
                group.isCompleted = false;
                StartRestoreAnimation(groupIndex, GetColorByHP(1));
            }
            else
            {
                // 스프레드 애니메이션 도중 재피격 시, 진행 중인 복원 애니메이션을 취소합니다.
                CancelRestoreAnimation(groupIndex);

                group.currentHP--;

                // 즉시 색상 변경 (HP 감소는 기존처럼 즉시 반영)
                Color newColor = group.originalColor;
                if (0 != group.currentHP)
                    newColor = GetColorByHP(group.currentHP);

                newColor.a = 1f;
                colors[group.vertexBase] = newColor;
                colors[group.vertexBase + 1] = newColor;
                colors[group.vertexBase + 2] = newColor;
                separatedMesh.colors = colors;
            }

            if (wasCompleted)
            {
                CompletedPolygons--;
                OnPolygonRestored?.Invoke();
            }
            else if (0 == group.currentHP)
            {
                group.isCompleted = true;
                CompletedPolygons++;
                OnPolygonCompleted?.Invoke();
            }
        }

        /// <summary>
        /// HP 0→1 복원 시 색상이 삼각형 중심에서 바깥쪽으로 서서히 번져가는 애니메이션을 시작합니다.
        /// </summary>
        private void StartRestoreAnimation(int groupIndex, Color targetColor)
        {
            // 동일 면의 기존 복원 애니메이션을 선제적으로 취소합니다.
            CancelRestoreAnimation(groupIndex);

            // 초기 상태: RGB는 목표 색상, Alpha=0 (셰이더가 원본 색상을 표시)
            SetPolygonColor(polygonGroups[groupIndex].vertexBase, targetColor, 0f);
            separatedMesh.colors = colors;

            var animation = new RestoreAnimation
            {
                groupIndex = groupIndex,
                progress = 0f,
                targetColor = targetColor
            };

            activeRestoreAnimations.Add(animation);
        }

        /// <summary>
        /// 지정된 폴리곤의 진행 중인 복원 애니메이션을 취소합니다.
        /// </summary>
        private void CancelRestoreAnimation(int groupIndex)
        {
            for (int i = activeRestoreAnimations.Count - 1; 0 <= i; i--)
            {
                if (activeRestoreAnimations[i].groupIndex == groupIndex)
                {
                    activeRestoreAnimations.RemoveAt(i);
                    break;
                }
            }
        }
    }
}
