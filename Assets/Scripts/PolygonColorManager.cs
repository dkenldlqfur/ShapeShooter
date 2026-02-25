using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShapeShooter
{
    public enum FaceColorType
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
    /// 단일 원본 메쉬를 폴리곤(삼각형) 단위로 분할하여 독립적인 정점/색상을 갖도록 재구성합니다.
    /// 각 폴리곤을 독립 그룹으로 관리하며, Raycast 타격 지점 기반의 충돌 색상 변경 체계를 담당합니다.
    /// </summary>
    public class PolygonColorManager : MonoBehaviour
    {
        public static Color GetColorByHP(int hp)
        {
            var colorType = (FaceColorType)Mathf.Clamp(hp, 0, (int)FaceColorType.Purple);
            return GetColor(colorType);
        }

        public static Color GetColor(FaceColorType type)
        {
            return type switch
            {
                FaceColorType.White     => Color.white,
                FaceColorType.Red       => Color.red,
                FaceColorType.Orange    => new(1f, 0.5f, 0f),
                FaceColorType.Yellow    => Color.yellow,
                FaceColorType.Green     => Color.green,
                FaceColorType.Blue      => Color.blue,
                FaceColorType.Purple    => new(0.6f, 0.2f, 0.8f),
                _ => Color.white
            };
        }

        public event Action OnFaceHit;
        public event Action OnFaceCompleted;
        public event Action OnFaceRestored;

        private MeshCollider meshCollider;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private Mesh separatedMesh;
        private Vector3[] vertices;
        private Vector3[] normals;
        private Color[] colors;

        private int maxHP = 1;

        /// <summary>
        /// 개별 폴리곤(삼각형) 단위의 색상/HP 관리 그룹
        /// </summary>
        private class PolygonGroup
        {
            public int currentHP;
            public int maxHP;
            public bool isCompleted;
            public int vertexBase; // 해당 삼각형의 첫 번째 정점 인덱스 (vertexBase, +1, +2)
            public Color originalColor;
        }

        private PolygonGroup[] polygonGroups;

        public int TotalFaces 
        {
            get
            {
                if (null != polygonGroups)
                {
                    return polygonGroups.Length;
                }
                return 0;
            }
        }
        public int CompletedFaces { get; private set; }

        public void Initialize(LevelData levelData)
        {
            if (null != levelData)
                maxHP = levelData.requiredHitsPerFace;
            else
                maxHP = 1;

            meshCollider = GetComponent<MeshCollider>();
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            if (null == meshFilter || null == meshFilter.sharedMesh)
                return;

            CreateIndependentPolygons();

            // 분리된 메쉬를 MeshCollider에 할당하여 triangleIndex가 polygonGroups 인덱스와 일치하도록 보장
            if (null != meshCollider)
                meshCollider.sharedMesh = separatedMesh;

            BuildPolygonGroups();
            UpdateAllColors();
        }

        private void CreateIndependentPolygons()
        {
            Mesh originalMesh = meshFilter.sharedMesh;
            if (null != meshCollider)
            {
                if (null != meshCollider.sharedMesh)
                {
                    originalMesh = meshCollider.sharedMesh;
                }
            }
            int[] origTris = originalMesh.triangles;
            Vector3[] origVerts = originalMesh.vertices;
            Vector3[] origNorms = originalMesh.normals;

            vertices = new Vector3[origTris.Length];
            normals = new Vector3[origTris.Length];
            colors = new Color[origTris.Length];
            var newTris = new int[origTris.Length];

            bool hasNormals = origNorms.Length > 0;
            bool hasColors = originalMesh.colors.Length > 0;

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
            }

            separatedMesh = new Mesh()
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = vertices,
                normals = normals,
                colors = colors,
                triangles = newTris
            };

            meshFilter.mesh = separatedMesh;

            // 깊이(Z) 버퍼에 쓰고 Z 테스트가 정상적으로 동작하는 3D 정점 색상 전용 커스텀 머티리얼 적용
            var mat = new Material(Shader.Find("Custom/VertexColorUnlit"));
            meshRenderer.material = mat;
        }

        /// <summary>
        /// 각 삼각형을 독립 폴리곤 그룹으로 초기화합니다. (면 그룹핑 없음)
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

                colors[group.vertexBase] = polygonColor;
                colors[group.vertexBase + 1] = polygonColor;
                colors[group.vertexBase + 2] = polygonColor;
            }
            separatedMesh.colors = colors;
        }

        /// <summary>
        /// Möller–Trumbore ray-triangle intersection algorithm
        /// Returns true if the ray intersects the triangle, and outputs the distance t
        /// </summary>
        private bool IntersectRayTriangle(Vector3 rayOrigin, Vector3 rayDir, Vector3 v0, Vector3 v1, Vector3 v2, out float t)
        {
            t = 0f;
            const float EPSILON = 0.0000001f;
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(rayDir, edge2);
            float a = Vector3.Dot(edge1, h);

            if (a > -EPSILON && a < EPSILON)
                return false; // Ray is parallel to this triangle.

            float f = 1.0f / a;
            Vector3 s = rayOrigin - v0;
            float u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(rayDir, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            t = f * Vector3.Dot(edge2, q);
            return t > EPSILON;
        }

        private int FindHitTriangleByRaycast(Vector3 hitPointWorld, Vector3 rayForwardWorld)
        {
            // 발사체의 궤적을 로컬 공간 선분(Ray)으로 변환
            // 충돌 지점에서 약간 뒤에서 출발하는 Ray 생성
            Vector3 originWorld = hitPointWorld - rayForwardWorld * 2.0f;
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

                if (IntersectRayTriangle(localOrigin, localDir, v0, v1, v2, out float t))
                {
                    if (t < closestT)
                    {
                        closestT = t;
                        bestTriangle = i;
                    }
                }
            }

            // 레이캐스트가 실패한 경우, 거리를 이용한 최후의 보루 (기존 방식 개선: 폴리곤 중심점 거리 비교)
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
        /// Raycast의 triangleIndex를 사용하여 정확히 맞힌 폴리곤의 색상을 변경합니다.
        /// </summary>
        public void OnHitAccurate(Vector3 hitPointWorld, Vector3 hitNormalWorld, Vector3 bulletForward, int colliderTriangleIndex)
        {
            // 뒷면 타격 무시 (내적 > 0 이면 안쪽에서 바깥쪽으로 향하는 경우)
            if (0.2f < Vector3.Dot(hitNormalWorld, bulletForward))
                return;

            if (null == polygonGroups || 0 == polygonGroups.Length)
                return;

            int triIndex = colliderTriangleIndex;
            string method = "Raycast TriangleIndex";
            
            if (-1 == triIndex || triIndex >= polygonGroups.Length)
            {
                triIndex = FindHitTriangleByRaycast(hitPointWorld, bulletForward);
                method = "Ray-Triangle Fallback";
            }

            if (-1 == triIndex)
                return;


            OnFaceHit?.Invoke();
            ApplyHitToPolygon(polygonGroups[triIndex]);
        }

        /// <summary>
        /// (구버전 하위 호환) 가장 가까운 정점을 통해 타격 폴리곤을 판별하고 HP를 깎습니다.
        /// </summary>
        public void OnHit(Vector3 hitPointWorld, Vector3 bulletForward)
        {
            int triIndex = FindHitTriangleByRaycast(hitPointWorld, bulletForward);
            if (-1 == triIndex)
                return;

            Vector3 hitNormalLocal = normals[triIndex * 3];
            Vector3 worldNormal = transform.TransformDirection(hitNormalLocal);

            // 뒷면 타격 무시 (내적 > 0 이면 안쪽에서 바깥쪽으로 향하는 경우)
            if (0.2f < Vector3.Dot(worldNormal, bulletForward))
                return;

            OnFaceHit?.Invoke();
            ApplyHitToPolygon(polygonGroups[triIndex]);
        }

        private void ApplyHitToPolygon(PolygonGroup group)
        {
            bool wasCompleted = group.isCompleted;

            if (0 == group.currentHP)
            {
                group.currentHP = 1;
                group.isCompleted = false;
            }
            else
            {
                group.currentHP--;
            }

            Color newColor = group.originalColor;
            if (0 != group.currentHP)
                newColor = GetColorByHP(group.currentHP);

            colors[group.vertexBase] = newColor;
            colors[group.vertexBase + 1] = newColor;
            colors[group.vertexBase + 2] = newColor;

            separatedMesh.colors = colors;

            if (wasCompleted)
            {
                CompletedFaces--;
                OnFaceRestored?.Invoke();
            }
            else if (0 == group.currentHP)
            {
                group.isCompleted = true;
                CompletedFaces++;
                OnFaceCompleted?.Invoke();
            }
        }
    }
}






