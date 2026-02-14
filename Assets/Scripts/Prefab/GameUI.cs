using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [SerializeField] private Button startBtn;
        [SerializeField] private GameObject gameInfoPanelObj;

        private void Start()
        {
            if (null != GameManager.Instance)
            {
                GameManager.Instance.OnStageChanged += OnStageChanged;
                GameManager.Instance.OnGameClear += ShowGameClear;
                GameManager.Instance.OnGameOver += ShowGameOver;
            }

            ShowStartBtn();
            UpdateUILoop().Forget();
        }

        public void OnStartButtonClicked()
        {
            if (null != startBtn)
                startBtn.gameObject.SetActive(false);

            if (null != gameInfoPanelObj)
                gameInfoPanelObj.SetActive(true);

            if (null != GameManager.Instance)
                GameManager.Instance.StartGame().Forget();
        }

        public void ShowStartBtn()
        {
            if (null != startBtn)
                startBtn.gameObject.SetActive(true);

            if (null != gameInfoPanelObj)
                gameInfoPanelObj.SetActive(false);
            
            if (null != gameClearPanel)
                gameClearPanel.SetActive(false);
            
            if (null != gameOverPanel)
                gameOverPanel.SetActive(false);
        }

        /// <summary>
        /// 스테이지 변경 시 호출 - 스테이지 텍스트 및 이전 기록 갱신
        /// </summary>
        private void OnStageChanged(int stageNumber)
        {
            // 스테이지 표시
            if (null != stageText)
                stageText.text = $"Stage {stageNumber}";
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
