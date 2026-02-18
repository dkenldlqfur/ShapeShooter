using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 스테이지 클리어 기록
    /// </summary>
    [Serializable]
    public struct StageRecord
    {
        public float clearTime;
        public int shotCount;
        public bool hasRecord;
    }

    /// <summary>
    /// 게임 전체 흐름 관리 (싱글톤).
    /// 스테이지 로드, 카운트다운, 게임 루프, 클리어/게임오버, 기록 저장
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager instance;
        private static bool applicationIsQuitting = false;

        public static bool HasInstance => instance != null;

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

        // 카운트다운 및 UI 표시 시간 상수 (밀리초)
        private const int COUNTDOWN_START = 3;
        private const int COUNTDOWN_INTERVAL_MS = 1000;
        private const int START_MESSAGE_DURATION_MS = 500;
        private const int STAGE_CLEAR_DISPLAY_MS = 3000;
        private const int GAME_CLEAR_DISPLAY_MS = 3000;

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
        /// Inspector 미설정 시 Resources 폴더에서 기본값 로드
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
        /// 게임 시작: 플레이어 생성, 씬 카메라 비활성화, 첫 스테이지 로드
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

                var cam = listener.GetComponent<Camera>();
                if (null != cam)
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
        /// 카운트다운 표시 후 게임 루프 시작
        /// </summary>
        private async UniTask StartCountdown()
        {
            IsGameActive = false;
            var gameUI = FindAnyObjectByType<GameUI>();

            for (int i = COUNTDOWN_START; 0 < i; i--)
            {
                if (null != gameUI)
                    gameUI.SetCountdownText($"Stage {CurrentStageIndex + 1}\n{i}");
                await UniTask.Delay(COUNTDOWN_INTERVAL_MS);
            }

            if (null != gameUI)
                gameUI.SetCountdownText("시작!");
            await UniTask.Delay(START_MESSAGE_DURATION_MS);

            if (null != gameUI)
                gameUI.SetCountdownText("");
            
            IsGameActive = true;
            GameLoop().Forget();
        }

        /// <summary>
        /// 스테이지 로드. 마지막 스테이지 초과 시 게임 클리어 처리 후 false 반환
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
        /// 게임 활성 동안 매 프레임 타이머를 갱신하는 루프
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
        /// 발사 횟수 증가 (게임 활성 상태에서만)
        /// </summary>
        public void IncrementShotCount()
        {
            if (IsGameActive)
                ShotCount++;
        }

        /// <summary>
        /// 스테이지 클리어 처리: 기록 저장, 클리어 메시지 표시, 다음 스테이지 진행
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
            await UniTask.Delay(STAGE_CLEAR_DISPLAY_MS);

            bool loaded = await LoadStage(CurrentStageIndex + 1, false);
            if (loaded)
                await StartCountdown();
        }

        /// <summary>
        /// 게임 종료 시작 (클리어 또는 게임오버)
        /// </summary>
        public void EndGame(bool isClear)
        {
            EndGameSequence(isClear).Forget();
        }

        /// <summary>
        /// 종료 시퀀스: 총알 정리, 메시지 표시, 오브젝트 파괴, 시작 화면 복귀
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
                await UniTask.Delay(GAME_CLEAR_DISPLAY_MS);
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
        /// 씬 내 모든 활성 총알을 풀에 반환하거나 파괴
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
        /// 현재 스테이지의 레벨 데이터 반환
        /// </summary>
        public LevelData GetCurrentLevelData()
        {
            if (levels.Length > CurrentStageIndex)
                return levels[CurrentStageIndex];
            return null;
        }

        /// <summary>
        /// 스테이지 클리어 기록을 PlayerPrefs에 저장 (더 빠른 기록만 갱신)
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
        /// PlayerPrefs에서 스테이지 클리어 기록 조회
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
