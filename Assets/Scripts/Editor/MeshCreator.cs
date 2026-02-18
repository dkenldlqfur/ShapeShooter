#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace ShapeShooter.Editor
{
    public class MeshCreator
    {
        [MenuItem("Tools/Create Triangle Mesh")]
        public static void CreateTriangleMesh()
        {
            var mesh = new Mesh();
            mesh.name = "Triangle";

            // 정삼각형 버텍스 (중심점 0,0,0 기준)
            float size = 1f;
            float height = size * Mathf.Sqrt(3) / 2;
            
            var vertices = new Vector3[]
            {
                new Vector3(0, height / 2, 0),          // Top
                new Vector3(-size / 2, -height / 2, 0), // Bottom Left
                new Vector3(size / 2, -height / 2, 0)   // Bottom Right
            };

            var triangles = new int[]
            {
                0, 1, 2
            };

            var uv = new Vector2[]
            {
                new Vector2(0.5f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f)
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();

            string path = "Assets/Resources/Meshes/Triangle.asset";
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
