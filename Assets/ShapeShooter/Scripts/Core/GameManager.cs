using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using System;

namespace ShapeShooter.Core
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

        [Header("Settings")]
        [SerializeField] private LevelData[] levels;

        public int CurrentStageIndex { get; private set; }
        public float StageTimer { get; private set; }
        public int ShotCount { get; private set; }
        public bool IsGameActive { get; private set; }

        private GameObject currentShapeGameObj;

        private void Awake()
        {
            if (null == Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
                Destroy(gameObject);
        }

        private void Start()
        {
            StartGame().Forget();
        }

        private async UniTaskVoid StartGame()
        {
            CurrentStageIndex = 0;
            await LoadStage(CurrentStageIndex);
        }

        public async UniTask LoadStage(int stageIndex)
        {
            if (levels.Length <= stageIndex)
            {
                EndGame(true);
                return;
            }

            IsGameActive = false;
            CurrentStageIndex = stageIndex;
            ShotCount = 0;
            StageTimer = 0f;

            Debug.Log($"스테이지 {stageIndex + 1} 시작");

            if (null != currentShapeGameObj)
                Destroy(currentShapeGameObj);

            LevelData levelData = levels[stageIndex];
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
            StageRecord existing = GetStageRecord(stageIndex);

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
            StageRecord record = new StageRecord();
            record.hasRecord = 1 == PlayerPrefs.GetInt($"Stage_{stageIndex}_HasRecord", 0);

            if (record.hasRecord)
            {
                record.clearTime = PlayerPrefs.GetFloat($"Stage_{stageIndex}_Time", 0f);
                record.shotCount = PlayerPrefs.GetInt($"Stage_{stageIndex}_Shots", 0);
            }

            return record;
        }
    }
}
