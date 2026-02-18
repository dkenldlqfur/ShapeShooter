using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeShooter
{
    /// <summary>
    /// 게임 UI 관리. 카운트다운, 스테이지 정보, 실시간 타이머/탄환 표시, 이전 기록 표시
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("기록 표시")]
        [SerializeField] private TextMeshProUGUI recordTitleText;
        [SerializeField] private TextMeshProUGUI recordTimeText;
        [SerializeField] private TextMeshProUGUI recordShotsText;

        [Header("현재 스테이지 정보")]
        [SerializeField] private TextMeshProUGUI stageText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI shotCountText;

        [Header("카운트다운")]
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("시작 화면")]
        [SerializeField] private Button startBtn;
        [SerializeField] private GameObject gameInfoPanelObj;

        private void Start()
        {
            if (null != startBtn)
            {
                startBtn.onClick.RemoveAllListeners();
                startBtn.onClick.AddListener(OnStartButtonClicked);
            }

            if (null != GameManager.Instance)
                GameManager.Instance.OnStageChanged += OnStageChanged;

            ShowStartBtn();
            InitCountdownText();
            UpdateUILoop().Forget();
        }

        /// <summary>
        /// 카운트다운 텍스트 스타일 초기 설정
        /// </summary>
        private void InitCountdownText()
        {
            if (null == countdownText)
                return;

            countdownText.text = "";
            countdownText.fontSize = 80;
            countdownText.alignment = TextAlignmentOptions.Center;
            countdownText.color = Color.yellow;
            countdownText.fontStyle = FontStyles.Bold;
            countdownText.enableWordWrapping = false;
            countdownText.enableAutoSizing = true;
            countdownText.fontSizeMin = 30;
            countdownText.fontSizeMax = 80;

            countdownText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 시작 버튼 클릭 시 게임 시작
        /// </summary>
        public void OnStartButtonClicked()
        {
            if (null != startBtn)
                startBtn.gameObject.SetActive(false);

            if (null != gameInfoPanelObj)
                gameInfoPanelObj.SetActive(true);

            if (null != GameManager.Instance)
            {
                UpdateRecordUI(0);
                GameManager.Instance.StartGame().Forget();
            }
        }

        /// <summary>
        /// 시작 화면으로 전환 (시작 버튼 표시, 게임 정보 패널 숨김)
        /// </summary>
        public void ShowStartBtn()
        {
            if (null != startBtn)
                startBtn.gameObject.SetActive(true);

            if (null != gameInfoPanelObj)
                gameInfoPanelObj.SetActive(false);

            if (null != countdownText)
                countdownText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 스테이지 변경 시 스테이지 텍스트 및 이전 기록 갱신
        /// </summary>
        private void OnStageChanged(int stageNumber)
        {
            if (null != stageText)
                stageText.text = $"Stage {stageNumber}";

            // stageNumber는 1부터 시작하므로 인덱스 변환
            UpdateRecordUI(stageNumber - 1);
        }

        /// <summary>
        /// 해당 스테이지의 최고 기록을 UI에 반영
        /// </summary>
        private void UpdateRecordUI(int stageIndex)
        {
            if (null == GameManager.Instance)
                return;

            var record = GameManager.Instance.GetStageRecord(stageIndex);

            if (null != recordTimeText)
            {
                recordTimeText.text = record.hasRecord
                    ? $"최고 시간: {record.clearTime:F2}초"
                    : "최고 시간: --";
            }

            if (null != recordShotsText)
            {
                recordShotsText.text = record.hasRecord
                    ? $"최소 탄환: {record.shotCount}발"
                    : "최소 탄환: --";
            }
        }

        /// <summary>
        /// 카운트다운 텍스트 설정. 빈 문자열이면 비활성화
        /// </summary>
        public void SetCountdownText(string text)
        {
            if (null != countdownText)
            {
                countdownText.text = text;
                countdownText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        /// <summary>
        /// 매 프레임 타이머 및 탄환 수를 실시간 갱신하는 루프
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

        private void OnDestroy()
        {
            if (null != GameManager.Instance)
                GameManager.Instance.OnStageChanged -= OnStageChanged;
        }
    }
}
