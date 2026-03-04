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
    /// PlayerPrefs 기반의 스테이지별 플레이 기록 영속 저장소입니다.
    /// 저장/조회 로직을 캡슐화하여 향후 JSON/DB 전환을 용이하게 합니다.
    /// </summary>
    public static class StageRecordRepository
    {
        /// <summary>
        /// 입력된 시간과 횟수가 과거 최고 기록보다 우수할 경우 로컬 스토리지에 영구 보존합니다.
        /// </summary>
        public static void Save(int stageIndex, float time, int shots)
        {
            var existing = Load(stageIndex);

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
        public static StageRecord Load(int stageIndex)
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
    }
}
