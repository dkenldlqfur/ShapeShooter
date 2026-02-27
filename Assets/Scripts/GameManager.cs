using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 개별 스테이지 클리어에 대한 시간 및 소모 탄환 등의 성과 기록을 저장하는 구조체입니다.
    /// </summary>
    [Serializable]
    public struct StageRecord
    {
        public float clearTime;
        public int shotCount;
        public bool hasRecord;
    }

    /// <summary>
    /// 전반적인 게임의 흐름(레벨 전환, 시작/종료 처리, 기록 저장)을 제어하는 최상위 싱글톤 매니저입니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager instance;
        private static bool applicationIsQuitting = false;

        public static bool HasInstance => null != instance;

        public static GameManager Instance
        {
            get
            {
                if (applicationIsQuitting)
                    return null;

                if (null == instance)
                {
                    instance = FindAnyObjectByType<GameManager>();
                    if (null == instance)
                    {
                        var go = new GameObject("GameManager");
                        instance = go.AddComponent<GameManager>();
                        instance.InitializeDefaults();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        public event Action<int> OnStageChanged;
        public event Action OnGameClear;
        public event Action OnGameOver;

        [Header("Prefabs")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Levels")]
        [SerializeField] private LevelData[] levels;

        public int CurrentStageIndex { get; private set; }
        public float StageTimer { get; private set; }
        public int ShotCount { get; private set; }
        public bool IsGameActive { get; private set; }

        private GameObject currentPlayer;
        private GameObject currentShapeGameObj;
        private Camera sceneMainCamera;

        [Header("Game Settings")]
        [SerializeField] private GameSettings gameSettings;


        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>
        /// 에디터의 Inspector를 통해 초기 값이 할당되지 않았을 경우, `Resources` 폴더 내 기본값으로 자동 대체 초기화합니다.
        /// </summary>
        private void InitializeDefaults()
        {
            if (null == playerPrefab)
                playerPrefab = Resources.Load<GameObject>("Prefabs/Player");

            if (null == levels || 0 == levels.Length)
            {
                var loadedLevels = Resources.LoadAll<LevelData>("LevelData");
                
                if (null != loadedLevels && 0 < loadedLevels.Length)
                {
                    System.Array.Sort(loadedLevels, (a, b) => a.name.CompareTo(b.name));
                    levels = loadedLevels;
                }
            }
        }

        #region 게임 흐름

        /// <summary>
        /// 신규 게임 세션을 가동시킵니다. 플레이어 인스턴스를 생성하고 초기 스테이지를 비동기적으로 로딩합니다.
        /// 물리 카메라 오버랩 방지를 위해 씬 전역 카메라를 우회 처리합니다.
        /// </summary>
        public async UniTaskVoid StartGame()
        {
            if (null != playerPrefab)
                currentPlayer = Instantiate(playerPrefab);

            // 플레이어 카메라와 씬 카메라의 AudioListener 중복 방지
            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var listener in listeners)
            {
                if (null != currentPlayer && listener.transform.IsChildOf(currentPlayer.transform))
                    continue;

                listener.enabled = false;

                if (listener.TryGetComponent<Camera>(out var cam))
                {
                    sceneMainCamera = cam;
                    cam.gameObject.SetActive(false);
                }
            }

            CurrentStageIndex = 0;
            
            bool loaded = await LoadStage(CurrentStageIndex, false);
            if (loaded)
                await StartCountdown();
        }

        /// <summary>
        /// UI 시스템과 연동하여 스테이지 시작 전 카운트다운 연출을 수행하는 비동기 함수입니다.
        /// 연출 종료 후 본격적인 게임 루프를 진입시킵니다.
        /// </summary>
        private async UniTask StartCountdown()
        {
            IsGameActive = false;
            var gameUI = FindAnyObjectByType<GameUI>();

            int currentCountdownStart = 3;
            if (null != gameSettings)
                currentCountdownStart = gameSettings.countdownStart;

            int currentCountdownIntervalMs = 1000;
            if (null != gameSettings)
                currentCountdownIntervalMs = gameSettings.countdownIntervalMs;

            int currentStartMessageDurationMs = 500;
            if (null != gameSettings)
                currentStartMessageDurationMs = gameSettings.startMessageDurationMs;

            for (int i = currentCountdownStart; 0 < i; i--)
            {
                if (null != gameUI)
                    gameUI.SetCountdownText($"Stage {CurrentStageIndex + 1}\n{i}");
                
                await UniTask.Delay(currentCountdownIntervalMs);
            }

            if (null != gameUI)
                gameUI.SetCountdownText("시작!");
            
            await UniTask.Delay(currentStartMessageDurationMs);

            if (null != gameUI)
                gameUI.SetCountdownText("");
            
            IsGameActive = true;
            GameLoop().Forget();
        }

        /// <summary>
        /// 지정된 인덱스의 스테이지 생태계를 구성합니다. 기존 총알과 타겟을 제거하고 플레이어 위치를 리셋한 뒤
        /// 새로운 도형 개체를 비동기 대기로 인스턴스화합니다.
        /// </summary>
        public async UniTask<bool> LoadStage(int stageIndex, bool autoStart = true)
        {
            if (levels.Length <= stageIndex)
            {
                EndGame(true);
                return false;
            }

            ClearAllBullets();

            IsGameActive = false;
            CurrentStageIndex = stageIndex;
            ShotCount = 0;
            StageTimer = 0f;

            if (null != currentShapeGameObj)
                Destroy(currentShapeGameObj);

            if (null != currentPlayer)
            {
                if (currentPlayer.TryGetComponent<Player>(out var playerComp))
                    playerComp.ResetPosition();
            }

            var levelData = levels[stageIndex];
            if (null != levelData && null != levelData.shapePrefab)
                currentShapeGameObj = Instantiate(levelData.shapePrefab, Vector3.zero, Quaternion.identity);

            OnStageChanged?.Invoke(stageIndex + 1);

            await UniTask.Yield();

            if (autoStart)
            {
                IsGameActive = true;
                GameLoop().Forget();
            }

            return true;
        }

        /// <summary>
        /// 인게임 루프가 동작하는 동안 프레임별로 플레이 타이머를 누적시키는 비동기 백그라운드 태스크입니다.
        /// </summary>
        private async UniTaskVoid GameLoop()
        {
            while (IsGameActive)
            {
                StageTimer += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        #endregion

        #region 스테이지 클리어/게임 종료

        /// <summary>
        /// 게임이 활성화된 상태일 경우에 한해 플레이어의 시전 횟수 데이터를 갱신합니다.
        /// </summary>
        public void IncrementShotCount()
        {
            if (IsGameActive)
                ShotCount++;
        }

        /// <summary>
        /// 현재 활성화된 스테이지를 완료 처리하며 기록을 갱신하고 다음 단계로 진입 대기를 트리거합니다.
        /// </summary>
        public void CompleteStage()
        {
            if (!IsGameActive)
                return;

            IsGameActive = false;

            SaveStageRecord(CurrentStageIndex, StageTimer, ShotCount);

            var gameUI = FindAnyObjectByType<GameUI>();
            if (null != gameUI)
                gameUI.SetCountdownText("스테이지 클리어!");

            ProceedToNextStage().Forget();
        }

        private async UniTaskVoid ProceedToNextStage()
        {
            int currentStageClearDisplayMs = 3000;
            if (null != gameSettings)
                currentStageClearDisplayMs = gameSettings.stageClearDisplayMs;

            await UniTask.Delay(currentStageClearDisplayMs);

            bool loaded = await LoadStage(CurrentStageIndex + 1, false);
            if (loaded)
                await StartCountdown();
        }

        /// <summary>
        /// 게임의 전반적인 세션이 클리어 또는 실패로 완전히 정지되는 시퀀스를 호출합니다.
        /// </summary>
        public void EndGame(bool isClear)
        {
            EndGameSequence(isClear).Forget();
        }

        /// <summary>
        /// 게임 종료의 코어 루틴입니다. 씬 내 부산물을 제거하고 UI 및 카메라 상태를 초기 모드로 복원합니다.
        /// </summary>
        private async UniTaskVoid EndGameSequence(bool isClear)
        {
            IsGameActive = false;
            ClearAllBullets();

            var gameUI = FindAnyObjectByType<GameUI>();

            if (isClear)
            {
                if (null != gameUI)
                    gameUI.SetCountdownText("게임 클리어!");
                
                OnGameClear?.Invoke();
                
                int currentGameClearDisplayMs = 3000;
                if (null != gameSettings)
                    currentGameClearDisplayMs = gameSettings.gameClearDisplayMs;

                await UniTask.Delay(currentGameClearDisplayMs);
            }
            else
            {
                OnGameOver?.Invoke();
            }

            if (null != currentPlayer) 
                Destroy(currentPlayer);
            
            if (null != currentShapeGameObj) 
                Destroy(currentShapeGameObj);

            if (null != gameUI)
                gameUI.ShowStartBtn();

            if (null != sceneMainCamera)
                sceneMainCamera.gameObject.SetActive(true);
        }

        /// <summary>
        /// 현재 씬 내에 활성화되어 있는 모든 발사체를 식별하여, 즉시 메모리 풀로 환원하거나 파괴 처리합니다.
        /// </summary>
        private void ClearAllBullets()
        {
            var bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
            foreach (var bullet in bullets)
            {
                if (null != bullet && bullet.gameObject.activeInHierarchy)
                {
                    if (null != bullet.GetComponent<Player>())
                        continue;

                    if (null != BulletManager.Instance)
                        BulletManager.Instance.Return(bullet);
                    else
                        Destroy(bullet.gameObject);
                }
            }
        }

        #endregion

        #region 레벨 데이터 및 기록

        /// <summary>
        /// 현재 인덱스에 대응되는 고유 스테이지 메타데이터 모델을 참조 반환합니다.
        /// </summary>
        public LevelData GetCurrentLevelData()
        {
            if (levels.Length > CurrentStageIndex)
                return levels[CurrentStageIndex];
            return null;
        }

        /// <summary>
        /// 입력된 시간과 횟수가 과거 최고 기록보다 우수할 경우 로컬 스토리지 볼륨에 영구 보존합니다.
        /// </summary>
        public void SaveStageRecord(int stageIndex, float time, int shots)
        {
            var existing = GetStageRecord(stageIndex);

            if (!existing.hasRecord || time < existing.clearTime)
            {
                PlayerPrefs.SetFloat($"Stage_{stageIndex}_Time", time);
                PlayerPrefs.SetInt($"Stage_{stageIndex}_Shots", shots);
                PlayerPrefs.SetInt($"Stage_{stageIndex}_HasRecord", 1);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 저장소에 기록된 스테이지별 세부 퍼포먼스 내역을 복원합니다.
        /// </summary>
        public StageRecord GetStageRecord(int stageIndex)
        {
            var record = new StageRecord
            {
                hasRecord = 1 == PlayerPrefs.GetInt($"Stage_{stageIndex}_HasRecord", 0)
            };

            if (record.hasRecord)
            {
                record.clearTime = PlayerPrefs.GetFloat($"Stage_{stageIndex}_Time", 0f);
                record.shotCount = PlayerPrefs.GetInt($"Stage_{stageIndex}_Shots", 0);
            }

            return record;
        }

        #endregion
    }
}
