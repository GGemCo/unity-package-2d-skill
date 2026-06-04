using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 1회 실행에 필요한 대상 정보와 실행 옵션을 담는 컨텍스트입니다.
    /// </summary>
    public readonly struct SkillTargetContext
    {
        /// <summary>스킬을 사용하는 캐스터입니다.</summary>
        public readonly GameObject caster;

        /// <summary>락온 대상입니다. Lock-On 계열 타겟팅에서 사용합니다.</summary>
        public readonly GameObject lockedTarget;

        /// <summary>지면 대상 좌표입니다. GroundTarget 계열 타겟팅에서 사용합니다.</summary>
        public readonly Vector3 groundPoint;

        /// <summary>전방 방향입니다. ForwardDirectional 계열 타겟팅에서 사용합니다.</summary>
        public readonly Vector3 forward;

        /// <summary>이번 스킬 실행에만 적용할 옵션 스냅샷입니다.</summary>
        public readonly SkillExecutionOptions executionOptions;

        /// <summary>
        /// 스킬 대상 컨텍스트를 생성합니다.
        /// </summary>
        /// <param name="caster">스킬을 사용하는 캐스터입니다.</param>
        /// <param name="lockedTarget">락온 대상입니다.</param>
        /// <param name="groundPoint">지면 대상 좌표입니다.</param>
        /// <param name="forward">전방 방향입니다.</param>
        /// <param name="executionOptions">이번 실행에만 적용할 옵션 스냅샷입니다.</param>
        public SkillTargetContext(
            GameObject caster,
            GameObject lockedTarget,
            Vector3 groundPoint,
            Vector3 forward,
            SkillExecutionOptions executionOptions = default(SkillExecutionOptions))
        {
            this.caster = caster;
            this.lockedTarget = lockedTarget;
            this.groundPoint = groundPoint;
            this.forward = forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
            this.executionOptions = SkillExecutionOptions.None.Combine(executionOptions);
        }
    }
}
