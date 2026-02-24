using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShapeShooter
{
    public enum FaceColorType
    {
        White = 0,
        Red = 1,
        Orange = 2,
        Yellow = 3,
        Green = 4,
        Blue = 5,
        Purple = 6,
    }

    /// <summary>
    /// 단일 원본 메쉬를 면(Face) 단위로 분할하여 독립적인 정점/색상을 갖도록 재구성합니다.
    /// 분할된 다각형들을 평면(법선과 오프셋 기준) 단위로 그룹화하고, Raycast 타격 지점 기반의 정밀한 충돌 색상 변경 체계를 관리합니다.
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
                FaceColorType.White => Color.white,
                FaceColorType.Red => Color.red,
                FaceColorType.Orange => new Color(1f, 0.5f, 0f),
                FaceColorType.Yellow => Color.yellow,
                FaceColorType.Green => Color.green,
                FaceColorType.Blue => Color.blue,
                FaceColorType.Purple => new Color(0.6f, 0.2f, 0.8f),
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

        private class FaceGroup
        {
            public int currentHP;
            public int maxHP;
            public bool isCompleted;
            public List<int> vertexIndices = new List<int>(); // 각 다각형의 정점 인덱스 저장
            public Vector3 normal;
            public float offset;
            public Color originalColor;
        }

        private List<FaceGroup> faceGroups = new List<FaceGroup>();

        public int TotalFaces => faceGroups.Count;
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

            if (null != meshCollider && null == meshCollider.sharedMesh && null != meshFilter)
            {
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }

            if (null == meshCollider || null == meshCollider.sharedMesh)
            {
                Debug.LogWarning($"{name}??MeshCollider ?는 ?본 메쉬가 ?습?다.");
                return;
            }

            CreateIndependentPolygons();
            GroupFaces();
            UpdateAllColors();
        }

        private void CreateIndependentPolygons()
        {
            Mesh originalMesh = meshCollider.sharedMesh;
            int[] origTris = originalMesh.triangles;
            Vector3[] origVerts = originalMesh.vertices;
            Vector3[] origNorms = originalMesh.normals;

            vertices = new Vector3[origTris.Length];
            normals = new Vector3[origTris.Length];
            colors = new Color[origTris.Length];
            int[] newTris = new int[origTris.Length];

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

            separatedMesh = new Mesh();
            separatedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            separatedMesh.vertices = vertices;
            separatedMesh.normals = normals;
            separatedMesh.colors = colors;
            separatedMesh.triangles = newTris;

            meshFilter.mesh = separatedMesh;

            // 깊이(Z) 버퍼에 쓰고 Z 테스트가 정상적으로 동작하는 3D 정점 색상 전용 커스텀 머티리얼 적용
            Material mat = new Material(Shader.Find("Custom/VertexColorUnlit"));
            meshRenderer.material = mat;
        }

        private void GroupFaces()
        {
            faceGroups.Clear();

            for (int i = 0; i < normals.Length; i += 3)
            {
                Vector3 normal = normals[i];
                Vector3 point = vertices[i];
                float offset = Vector3.Dot(normal, point);

                FaceGroup targetGroup = null;

                foreach (var group in faceGroups)
                {
                    // 방향?거리가 ?치?는 ?찾기 (?일 ?면 조건)
                    if (Vector3.Angle(group.normal, normal) < 1.0f && Mathf.Abs(group.offset - offset) < 0.001f)
                    {
                        targetGroup = group;
                        break;
                    }
                }

                if (null == targetGroup)
                {
                    targetGroup = new FaceGroup
                    {
                        maxHP = this.maxHP,
                        currentHP = this.maxHP,
                        isCompleted = false,
                        normal = normal,
                        offset = offset,
                        originalColor = colors[i]
                    };
                    faceGroups.Add(targetGroup);
                }

                // ?나???각?을 ?루??3개의 ?점 ?덱?????
                targetGroup.vertexIndices.Add(i);
                targetGroup.vertexIndices.Add(i + 1);
                targetGroup.vertexIndices.Add(i + 2);
            }
        }

        private void UpdateAllColors()
        {
            foreach (var group in faceGroups)
            {
                Color faceColor;
                if (0 == group.currentHP)
                    faceColor = group.originalColor;
                else
                    faceColor = GetColorByHP(group.currentHP);
                foreach (int vIndex in group.vertexIndices)
                {
                    colors[vIndex] = faceColor;
                }
            }
            separatedMesh.colors = colors;
        }

        private int FindClosestTriangleIndex(Vector3 hitPointWorld)
        {
            Vector3 localHitPoint = transform.InverseTransformPoint(hitPointWorld);
            int closestTriangle = -1;
            float minSqrDistance = float.MaxValue;      
            int triangleCount = vertices.Length / 3;    

            for (int i = 0; i < triangleCount; i++)     
            {
                int v1 = i * 3;
                int v2 = i * 3 + 1;
                int v3 = i * 3 + 2;

                float dist1 = (vertices[v1] - localHitPoint).sqrMagnitude;
                float dist2 = (vertices[v2] - localHitPoint).sqrMagnitude;
                float dist3 = (vertices[v3] - localHitPoint).sqrMagnitude;

                float minVertexDist = Mathf.Min(dist1, Mathf.Min(dist2, dist3));

                if (minVertexDist < minSqrDistance)     
                {
                    minSqrDistance = minVertexDist;     
                    closestTriangle = i;
                }
            }
            return closestTriangle;
        }

        /// <summary>
        /// Raycast의 정확한 법선(Normal)과 충돌 좌표(Point)를 기반으로 타격할 면 그룹을 즉시 결정합니다.
        /// </summary>
        public void OnHitAccurate(Vector3 hitPointWorld, Vector3 hitNormalWorld, Vector3 bulletForward)
        {
            // 뒷면 타격 무시 (내적 > 0 이면 안쪽에서 바깥쪽으로 향하는 경우)
            if (0.2f < Vector3.Dot(hitNormalWorld, bulletForward))
                return;

            Vector3 localHitPoint = transform.InverseTransformPoint(hitPointWorld);
            Vector3 localHitNormal = transform.InverseTransformDirection(hitNormalWorld).normalized;
            float hitOffset = Vector3.Dot(localHitNormal, localHitPoint);

            FaceGroup hitGroup = null;

            foreach (var group in faceGroups)
            {
                // 법선 방향이 일치하고, 평면 오프셋이 일치하는 그룹을 찾습니다.
                if (Vector3.Angle(group.normal, localHitNormal) < 1.0f && Mathf.Abs(group.offset - hitOffset) < 0.001f)
                {
                    hitGroup = group;
                    break;
                }
            }

            if (null == hitGroup)
                return;

            OnFaceHit?.Invoke();
            ApplyHitToFaceGroup(hitGroup);
        }

        /// <summary>
        /// (구버전 하위 호환) 가장 가까운 정점을 통해 타격 면을 판별하고 HP를 깎습니다.
        /// </summary>
        public void OnHit(Vector3 hitPointWorld, Vector3 bulletForward)
        {
            int triIndex = FindClosestTriangleIndex(hitPointWorld);
            if (-1 == triIndex)
                return;

            Vector3 hitNormalLocal = normals[triIndex * 3];
            Vector3 worldNormal = transform.TransformDirection(hitNormalLocal);

            // ?면 ??무시 (?적 > 0 ?면 ?쪽?서 바깥쪽으??하??경우)
            if (0.2f < Vector3.Dot(worldNormal, bulletForward))
                return;

            OnFaceHit?.Invoke();
            ApplyHitToFace(triIndex);
        }

        private void ApplyHitToFace(int triIndex)
        {
            Vector3 hitNormal = normals[triIndex * 3];
            Vector3 hitPointLocal = vertices[triIndex * 3];
            float hitOffset = Vector3.Dot(hitNormal, hitPointLocal);

            FaceGroup hitGroup = null;

            foreach (var group in faceGroups)
            {
                if (Vector3.Angle(group.normal, hitNormal) < 1.0f && Mathf.Abs(group.offset - hitOffset) < 0.001f)
                {
                    hitGroup = group;
                    break;
                }
            }

            if (null == hitGroup)
                return;

            ApplyHitToFaceGroup(hitGroup);
        }

        private void ApplyHitToFaceGroup(FaceGroup hitGroup)
        {
            bool wasCompleted = hitGroup.isCompleted;

            if (0 == hitGroup.currentHP)
            {
                hitGroup.currentHP = 1;
                hitGroup.isCompleted = false;
            }
            else
            {
                hitGroup.currentHP--;
            }

            Color newColor;
            if (0 == hitGroup.currentHP)
                newColor = hitGroup.originalColor;
            else
                newColor = GetColorByHP(hitGroup.currentHP);
            foreach (int vIndex in hitGroup.vertexIndices)
            {
                colors[vIndex] = newColor;
            }
            separatedMesh.colors = colors;

            if (wasCompleted)
            {
                CompletedFaces--;
                OnFaceRestored?.Invoke();
            }
            else if (0 == hitGroup.currentHP)
            {
                hitGroup.isCompleted = true;
                CompletedFaces++;
                OnFaceCompleted?.Invoke();
            }
        }
    }
}






