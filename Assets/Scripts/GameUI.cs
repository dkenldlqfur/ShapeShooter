using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace ShapeShooter
{
    public class GameUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI recordTitleText;
        [SerializeField] private TextMeshProUGUI recordTimeText;
        [SerializeField] private TextMeshProUGUI recordShotsText;

        [SerializeField] private TextMeshProUGUI stageText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI shotCountText;

        [SerializeField] private GameObject gameClearPanel;
        [SerializeField] private GameObject gameOverPanel;

        private void Start()
        {
            if (null != GameManager.Instance)
            {
                GameManager.Instance.OnStageChanged += OnStageChanged;
                GameManager.Instance.OnGameClear += ShowGameClear;
                GameManager.Instance.OnGameOver += ShowGameOver;
            }

            UpdateUILoop().Forget();
        }

        /// <summary>
        /// 스테이지 변경 시 호출 - 스테이지 텍스트 및 이전 기록 갱신
        /// </summary>
        private void OnStageChanged(int stageNumber)
        {
            // 스테이지 표시
            if (null != stageText)
                stageText.text = $"Stage {stageNumber}";

            // 이전 기록 표시
            UpdateRecordDisplay(stageNumber - 1); // stageNumber는 1-based, index는 0-based
        }

        /// <summary>
        /// 이전 클리어 기록 표시 갱신
        /// </summary>
        private void UpdateRecordDisplay(int stageIndex)
        {
            if (null == GameManager.Instance)
                return;

            var record = GameManager.Instance.GetStageRecord(stageIndex);

            if (null != recordTitleText)
                recordTitleText.text = "이전 기록";

            if (record.hasRecord)
            {
                if (null != recordTimeText)
                    recordTimeText.text = $"기록 시간: {record.clearTime:F2}초";

                if (null != recordShotsText)
                    recordShotsText.text = $"사용 탄환: {record.shotCount}발";
            }
            else
            {
                if (null != recordTimeText)
                    recordTimeText.text = "기록 시간: --";

                if (null != recordShotsText)
                    recordShotsText.text = "사용 탄환: --";
            }
        }

        /// <summary>
        /// 현재 플레이 타임 및 탄환 수 실시간 갱신
        /// </summary>
        private async UniTaskVoid UpdateUILoop()
        {
            while (this != null)
            {
                if (null != GameManager.Instance && GameManager.Instance.IsGameActive)
                {
                    if (null != timerText)
                        timerText.text = $"시간: {GameManager.Instance.StageTimer:F2}초";

                    if (null != shotCountText)
                        shotCountText.text = $"탄환: {GameManager.Instance.ShotCount}발";
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private void ShowGameClear()
        {
            if (null != gameClearPanel)
                gameClearPanel.SetActive(true);
        }

        private void ShowGameOver()
        {
            if (null != gameOverPanel)
                gameOverPanel.SetActive(true);
        }

        private void OnDestroy()
        {
            if (null != GameManager.Instance)
            {
                GameManager.Instance.OnStageChanged -= OnStageChanged;
                GameManager.Instance.OnGameClear -= ShowGameClear;
                GameManager.Instance.OnGameOver -= ShowGameOver;
            }
        }
    }
}
