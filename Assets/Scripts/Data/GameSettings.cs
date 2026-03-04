using UnityEngine;

namespace ShapeShooter
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "ShapeShooter/GameSettings", order = 1)]
    public class GameSettings : ScriptableObject
    {
        [Header("카운트다운 UI 설정")]
        [Tooltip("카운트다운 시작 숫자 (초단위)")]
        public int countdownStart = 3;

        [Tooltip("카운트다운 간격 시간 (밀리초)")]
        public int countdownIntervalMs = 1000;

        [Tooltip("'시작!' 메시지가 화면에 유지되는 시간 (밀리초)")]
        public int startMessageDurationMs = 500;

        [Header("스테이지/게임 클리어 UI 설정")]
        [Tooltip("스테이지 클리어 텍스트가 표시되는 시간 (밀리초)")]
        public int stageClearDisplayMs = 3000;

        [Tooltip("게임 클리어 텍스트가 표시되는 시간 (밀리초)")]
        public int gameClearDisplayMs = 3000;
    }
}
