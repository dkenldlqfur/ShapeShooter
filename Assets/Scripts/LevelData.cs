using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 개별 스테이지 혹은 난이도별 게임플레이 설정 인자들을 정의하는 데이터 컨테이너입니다.
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
        public int requiredHitsPerPolygon = 1;
    }

    /// <summary>
    /// 대상 객체의 3차원 공간 회전 모드를 지정하는 열거형 타입입니다.
    /// </summary>
    public enum RotationPatternType
    {
        /// <summary>월드 축 상에서의 고정 형태</summary>
        Static,
        /// <summary>특정 단일 축을 기준으로 하는 스핀오프</summary>
        SingleAxis,
        /// <summary>복합 다중 축을 결합한 스핀오프</summary>
        MultiAxis,
        /// <summary>주기적 전환 임의 축 스핀오프</summary>
        Random,
        /// <summary>피격 이벤트 기반의 반응형 축 전환 스핀오프</summary>
        ReactiveAxis,
    }
}
