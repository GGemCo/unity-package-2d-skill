using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// SkillExecutor가 외부 어댑터로 전달하는 실행 종료 리포트입니다.
    /// </summary>
    public readonly struct SkillExecutionReport
    {
        public readonly int SkillUid;
        public readonly MonsterSkillExecutionState State;
        public readonly int Sequence;
        public readonly float EndTime;
        public readonly SkillCancelReason? CancelReason;

        /// <summary>
        /// 스킬 실행 종료 리포트를 생성합니다.
        /// </summary>
        /// <param name="skillUid">종료된 스킬 UID입니다.</param>
        /// <param name="state">스킬 종료 상태입니다.</param>
        /// <param name="sequence">실행 종료 순서입니다.</param>
        /// <param name="endTime">종료 시각입니다.</param>
        /// <param name="cancelReason">취소 종료인 경우 취소 사유입니다.</param>
        public SkillExecutionReport(
            int skillUid,
            MonsterSkillExecutionState state,
            int sequence,
            float endTime,
            SkillCancelReason? cancelReason = null)
        {
            SkillUid = skillUid;
            State = state;
            Sequence = sequence;
            EndTime = endTime;
            CancelReason = cancelReason;
        }

        public MonsterSkillExecutionResult ToMonsterSkillExecutionResult()
        {
            return new MonsterSkillExecutionResult(SkillUid, State, Sequence, EndTime);
        }
    }
}
