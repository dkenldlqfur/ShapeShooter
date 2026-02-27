using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeShooter
{
    /// <summary>
    /// 게임 내 모든 최상단 Canvas UI(타이머, 탄환 표기 정보, 카운트다운 및 최고/최신 기록 패널)를 시각적으로 제어합니다.
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
        /// 카운트다운에 쓰일 메인 텍스트 컴포넌트의 가시성 및 렌더링 스타일을 초기화합니다.
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
            countdownText.enableAutoSizing = true;
            countdownText.fontSizeMin = 30;
            countdownText.fontSizeMax = 80;

            countdownText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 시작 트리거 이벤트 콜백입니다. 대기 상태를 파기하고 메인 게임세션을 지시합니다.
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
        /// 메인 타이틀(대기) 화면 상태의 가시성을 복원합니다.
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
        /// 매니저로부터 스테이지 갱신 이벤트를 수신하였을 때 과거 기록 표시부를 병합 출력합니다.
        /// </summary>
        private void OnStageChanged(int stageNumber)
        {
            if (null != stageText)
                stageText.text = $"Stage {stageNumber}";

            // 뷰 모델 표기 인덱스와 데이터베이스 탐색 인덱스의 오프셋 동기화입니다.
            UpdateRecordUI(stageNumber - 1);
        }

        /// <summary>
        /// 내부 스토리지에서 인출한 퍼포먼스 기록 데이터를 UI 파이프라인에 시각적으로 반영합니다.
        /// </summary>
        private void UpdateRecordUI(int stageIndex)
        {
            if (null == GameManager.Instance)
                return;

            var record = GameManager.Instance.GetStageRecord(stageIndex);

            if (null != recordTimeText)
            {
                if (record.hasRecord)
                    recordTimeText.text = $"최고 시간: {record.clearTime:F2}초";
                else
                    recordTimeText.text = "최고 시간: --";
            }

            if (null != recordShotsText)
            {
                if (record.hasRecord)
                    recordShotsText.text = $"최소 탄환: {record.shotCount}발";
                else
                    recordShotsText.text = "최소 탄환: --";
            }
        }

        /// <summary>
        /// 메인 스크린 타겟 텍스트를 인젝션합니다. 빈 값을 전송 시 해당 컴포넌트를 숨김 처리합니다.
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
        /// 싱글턴 매니저에 누적된 시간/자원 소모량 변동치를 매 프레임별로 UI 노드에 동기화시키는 비동기 작업입니다.
        /// </summary>
        private async UniTaskVoid UpdateUILoop()
        {
            while (null != this)
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
