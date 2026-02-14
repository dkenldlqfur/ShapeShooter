using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// 스테이지 클리어 기록 구조체
    /// </summary>
    [Serializable]
    public struct StageRecord
    {
        public float clearTime;
        public int shotCount;
        public bool hasRecord;
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public event Action<int> OnStageChanged;
        public event Action OnGameClear;
        public event Action OnGameOver;

        [Header("Prefabs")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject bulletPoolPrefab;

        [Header("Levels")]
        [SerializeField] private LevelData[] levels;

        public int CurrentStageIndex { get; private set; }
        public float StageTimer { get; private set; }
        public int ShotCount { get; private set; }
        public bool IsGameActive { get; private set; }

        private GameObject currentPlayer;
        private GameObject currentBulletManager;
        private GameObject currentShapeGameObj;

        private void Awake()
        {
            if (null == Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public async UniTaskVoid StartGame()
        {
            // 필수 오브젝트 동적 생성
            if (null != playerPrefab)
                currentPlayer = Instantiate(playerPrefab);

            if (null != bulletPoolPrefab)
                currentBulletManager = Instantiate(bulletPoolPrefab);

            CurrentStageIndex = 0;
            await LoadStage(CurrentStageIndex);
        }

        public async UniTask LoadStage(int stageIndex)
        {
            // 멀티라인 블록이므로 유지
            if (levels.Length <= stageIndex)
            {
                EndGame(true);
                return;
            }

            // 잔여 총알 제거
            ClearAllBullets();

            IsGameActive = false;
            CurrentStageIndex = stageIndex;
            ShotCount = 0;
            StageTimer = 0f;

            Debug.Log($"스테이지 {stageIndex + 1} 시작");

            if (null != currentShapeGameObj)
                Destroy(currentShapeGameObj);

            // 플레이어 위치 초기화
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

            IsGameActive = true;
            GameLoop().Forget();
        }

        private async UniTaskVoid GameLoop()
        {
            while (IsGameActive)
            {
                StageTimer += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        public void IncrementShotCount()
        {
            if (IsGameActive)
                ShotCount++;
        }

        public void CompleteStage()
        {
            if (!IsGameActive)
                return;

            IsGameActive = false;
            Debug.Log($"스테이지 {CurrentStageIndex + 1} 클리어! 시간: {StageTimer:F2}, 발사 횟수: {ShotCount}");

            // 기록 저장
            SaveStageRecord(CurrentStageIndex, StageTimer, ShotCount);

            // 다음 스테이지로 이동
            ProceedToNextStage().Forget();
        }

        private async UniTaskVoid ProceedToNextStage()
        {
            await UniTask.Delay(3000);
            await LoadStage(CurrentStageIndex + 1);
        }

        public void EndGame(bool isClear)
        {
            IsGameActive = false;
            if (isClear)
            {
                Debug.Log("모든 스테이지 클리어!");
                OnGameClear?.Invoke();
            }
            else
            {
                Debug.Log("게임 오버");
                OnGameOver?.Invoke();
            }

            // 게임 종료 시 동적 생성된 오브젝트 정리
            if (null != currentPlayer) 
                Destroy(currentPlayer);
            
            if (null != currentBulletManager) 
                Destroy(currentBulletManager);
            
            if (null != currentShapeGameObj) 
                Destroy(currentShapeGameObj);

            // 타이틀 화면 다시 표시 (GameUI.ShowTitle 호출)
            var gameUI = FindAnyObjectByType<GameUI>();
            if (null != gameUI)
                gameUI.ShowStartBtn();
        }

        private void ClearAllBullets()
        {
            var bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
            foreach (var bullet in bullets)
            {
                if (null != bullet && bullet.gameObject.activeInHierarchy)
                {
                    // 안전장치: 플레이어에게 Bullet 컴포넌트가 붙어있는 경우 파괴 방지
                    if (null != bullet.GetComponent<Player>())
                    {
                        Debug.LogWarning("경고: 플레이어 오브젝트에 'Bullet' 스크립트가 붙어있습니다! Inspector에서 제거해주세요.");
                        continue;
                    }

                    if (null != BulletManager.Instance)
                        BulletManager.Instance.Return(bullet);
                    else
                        Destroy(bullet.gameObject);
                }
            }
        }

        public LevelData GetCurrentLevelData()
        {
            if (levels.Length > CurrentStageIndex)
                return levels[CurrentStageIndex];
            return null;
        }

        // ============================================
        // 스테이지 기록 관리 (PlayerPrefs 기반)
        // ============================================

        /// <summary>
        /// 스테이지 클리어 기록 저장
        /// 최고 기록(짧은 시간, 적은 탄환)만 저장
        /// </summary>
        public void SaveStageRecord(int stageIndex, float time, int shots)
        {
            var existing = GetStageRecord(stageIndex);

            // 기록이 없거나 더 빠른 시간이면 갱신
            if (!existing.hasRecord || time < existing.clearTime)
            {
                PlayerPrefs.SetFloat($"Stage_{stageIndex}_Time", time);
                PlayerPrefs.SetInt($"Stage_{stageIndex}_Shots", shots);
                PlayerPrefs.SetInt($"Stage_{stageIndex}_HasRecord", 1);
                PlayerPrefs.Save();
                Debug.Log($"스테이지 {stageIndex + 1} 최고 기록 갱신! 시간: {time:F2}, 탄환: {shots}");
            }
        }

        /// <summary>
        /// 스테이지 클리어 기록 조회
        /// </summary>
        public StageRecord GetStageRecord(int stageIndex)
        {
            StageRecord record = new()
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
    }
}
