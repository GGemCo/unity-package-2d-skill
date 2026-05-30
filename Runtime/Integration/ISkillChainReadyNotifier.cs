using System;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 실제 타격 확정으로 현재 스킬을 다음 스킬로 체인할 수 있는 상태가 되었음을 알리는 포트입니다.
    /// </summary>
    /// <remarks>
    /// 입력 계층은 이 포트를 통해 <c>enableSkillChainOnConfirmedDamage</c>와
    /// 각 스킬 이벤트의 <c>allowSkillChainOnConfirmedDamage</c> 결과를 직접 의존하지 않고 전달받습니다.
    /// </remarks>
    public interface ISkillChainReadyNotifier
    {
        /// <summary>
        /// 현재 실행 중인 스킬이 실제 타격을 확정하여 다음 스킬 체인을 받을 수 있을 때 발생합니다.
        /// </summary>
        event Action<int> SkillChainReady;

        /// <summary>
        /// 현재 실행 중인 스킬이 다음 스킬로 체인 취소될 수 있는 상태인지 반환합니다.
        /// </summary>
        bool IsSkillChainReady { get; }
    }
}
