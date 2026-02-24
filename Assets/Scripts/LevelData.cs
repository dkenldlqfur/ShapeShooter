using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 스테이지별 레벨 설정 데이터
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "ShapeShooter/LevelData")]
    public class LevelData : ScriptableObject
    {
        [Tooltip("스테이지에 사용할 도형 프리팹")]
        public GameObject shapePrefab;

        [Tooltip("도형 회전 속도 (도/초)")]
        public float rotationSpeed = 10f;

        [Tooltip("도형 회전 패턴")]
        public RotationPatternType rotationPattern;

        [Header("면 체력")]
        [Tooltip("면의 초기 HP (총알에 맞을 때마다 1 감소, 0이 되면 원래 색상으로 복구)")]
        public int requiredHitsPerFace = 1;
    }

    /// <summary>
    /// 도형 회전 패턴 유형
    /// </summary>
    public enum RotationPatternType
    {
        /// <summary>회전 없음</summary>
        Static,
        /// <summary>X/Y/Z 중 랜덤 단일 축 회전</summary>
        SingleAxis,
        /// <summary>랜덤 조합 축 회전</summary>
        MultiAxis,
        /// <summary>일정 시간마다 랜덤 축 변경</summary>
        Random,
        /// <summary>MultiAxis 기반이지만, 총알 충돌마다 새 축으로 전환</summary>
        ReactiveAxis,
    }
}
