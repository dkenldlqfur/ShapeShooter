#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ShapeShooter.Editor
{
    /// <summary>
    /// Bullet 프리팹 Rigidbody의 CollisionDetection을 Continuous로 설정
    /// </summary>
    public static class BulletPrefabFixer
    {
        [MenuItem("Tools/ShapeShooter/Fix Bullet Collision Detection")]
        public static void Fix()
        {
            const string path = "Assets/Resources/Prefabs/Bullet.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (null == prefab)
            {
                Debug.LogError("[BulletPrefabFixer] Bullet.prefab을 찾을 수 없습니다.");
                return;
            }

            var rb = prefab.GetComponent<Rigidbody>();
            if (null == rb)
            {
                Debug.LogError("[BulletPrefabFixer] Rigidbody 컴포넌트가 없습니다.");
                return;
            }

            // Continuous 설정으로 터널링 방지
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();

            Debug.Log("[BulletPrefabFixer] CollisionDetectionMode → Continuous 적용 완료!");
        }
    }
}
#endif
