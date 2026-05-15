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

        public SkillExecutionReport(int skillUid, MonsterSkillExecutionState state, int sequence, float endTime)
        {
            SkillUid = skillUid;
            State = state;
            Sequence = sequence;
            EndTime = endTime;
        }

        public MonsterSkillExecutionResult ToMonsterSkillExecutionResult()
        {
            return new MonsterSkillExecutionResult(SkillUid, State, Sequence, EndTime);
        }
    }
}
