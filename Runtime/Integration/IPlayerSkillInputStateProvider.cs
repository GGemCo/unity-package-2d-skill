namespace GGemCo2DSkill
{
    /// <summary>
    /// 입력 규칙 계층에서 플레이어 스킬 실행 상태를 조회하기 위한 포트입니다.
    /// </summary>
    /// <remarks>
    /// 프로젝트 전용 입력 규칙은 이 포트를 통해 SkillExecutor나 PlayerSkillDriverAdapter의 구체 구현에 직접 의존하지 않고,
    /// 스킬 실행 중 일반 공격을 소비할지 또는 확정 피해 기반 체인 입력을 통과시킬지 판단합니다.
    /// </remarks>
    public interface IPlayerSkillInputStateProvider
    {
        /// <summary>
        /// 현재 플레이어 스킬 실행기가 스킬을 처리 중인지 여부입니다.
        /// </summary>
        bool IsSkillBusy { get; }

        /// <summary>
        /// 확정 피해 기반 체인 입력을 받을 수 있는 상태인지 여부입니다.
        /// </summary>
        bool IsSkillChainReady { get; }
    }
}
