using UnityEngine;

namespace ShapeShooter.Core
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "ShapeShooter/LevelData")]
    public class LevelData : ScriptableObject
    {
        [Header("Shape Settings")]
        public GameObject shapePrefab;
        public float rotationSpeed = 10f;
        
        [Header("Difficulty")]
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
        Chaos
    }
}
