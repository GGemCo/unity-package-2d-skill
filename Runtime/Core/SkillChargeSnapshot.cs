namespace GGemCo2DSkill
{
    /// <summary>
    /// 차징 UI와 디버그 툴이 읽을 수 있는 차징 상태 스냅샷입니다.
    /// </summary>
    public readonly struct SkillChargeSnapshot
    {
        /// <summary>차징 중인 스킬 UID입니다.</summary>
        public readonly int SkillUid;

        /// <summary>차징 UI를 표시해야 하는 활성 상태인지 여부입니다.</summary>
        public readonly bool IsActive;

        /// <summary>현재 차징 진행 중인지 여부입니다.</summary>
        public readonly bool IsCharging;

        /// <summary>차징 실패 상태인지 여부입니다.</summary>
        public readonly bool IsFailed;

        /// <summary>현재 차징 단계 인덱스입니다.</summary>
        public readonly int StageIndex;

        /// <summary>전체 차징 단계 수입니다.</summary>
        public readonly int StageCount;

        /// <summary>현재 단계 경과 시간입니다.</summary>
        public readonly float StageElapsedSeconds;

        /// <summary>현재 단계 총 시간입니다.</summary>
        public readonly float StageDurationSeconds;

        /// <summary>전체 차징 경과 시간입니다.</summary>
        public readonly float TotalElapsedSeconds;

        /// <summary>전체 차징 시간입니다.</summary>
        public readonly float TotalDurationSeconds;

        /// <summary>전체 차징 진행률입니다.</summary>
        public readonly float Progress01;

        /// <summary>현재 차징 게이지 값입니다.</summary>
        public readonly float GaugeCurrent;

        /// <summary>차징 게이지 최대값입니다.</summary>
        public readonly float GaugeMax;

        /// <summary>차징 게이지 비율입니다.</summary>
        public readonly float Gauge01;

        public SkillChargeSnapshot(
            int skillUid,
            bool isActive,
            bool isCharging,
            bool isFailed,
            int stageIndex,
            int stageCount,
            float stageElapsedSeconds,
            float stageDurationSeconds,
            float totalElapsedSeconds,
            float totalDurationSeconds,
            float progress01,
            float gaugeCurrent,
            float gaugeMax,
            float gauge01)
        {
            SkillUid = skillUid;
            IsActive = isActive;
            IsCharging = isCharging;
            IsFailed = isFailed;
            StageIndex = stageIndex;
            StageCount = stageCount;
            StageElapsedSeconds = stageElapsedSeconds;
            StageDurationSeconds = stageDurationSeconds;
            TotalElapsedSeconds = totalElapsedSeconds;
            TotalDurationSeconds = totalDurationSeconds;
            Progress01 = progress01;
            GaugeCurrent = gaugeCurrent;
            GaugeMax = gaugeMax;
            Gauge01 = gauge01;
        }

        /// <summary>
        /// 차징 UI를 비활성화하기 위한 빈 스냅샷을 생성합니다.
        /// </summary>
        /// <param name="skillUid">비활성화 대상 스킬 UID입니다.</param>
        /// <returns>비활성 스냅샷입니다.</returns>
        public static SkillChargeSnapshot Inactive(int skillUid)
        {
            return new SkillChargeSnapshot(
                skillUid,
                isActive: false,
                isCharging: false,
                isFailed: false,
                stageIndex: 0,
                stageCount: 0,
                stageElapsedSeconds: 0f,
                stageDurationSeconds: 0f,
                totalElapsedSeconds: 0f,
                totalDurationSeconds: 0f,
                progress01: 0f,
                gaugeCurrent: 0f,
                gaugeMax: 0f,
                gauge01: 0f);
        }
    }
}
