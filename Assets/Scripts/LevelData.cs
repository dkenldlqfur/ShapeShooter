using UnityEngine;

namespace ShapeShooter
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "ShapeShooter/LevelData")]
    public class LevelData : ScriptableObject
    {
        public GameObject shapePrefab;
        public float rotationSpeed = 10f;
        
        public RotationPatternType rotationPattern;
        public int requiredHitsPerFace = 1;
        public Color[] stageColors;
    }

    public enum RotationPatternType
    {
        Static,
        SingleAxis,
        MultiAxis,
        Random,
    }
}
