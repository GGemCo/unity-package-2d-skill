using System.Collections.Generic;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 실행 중 생성되는 공격 식별자와 연계 해제 가능 여부를 관리합니다.
    /// </summary>
    internal sealed class SkillAttackSequence
    {
        /// <summary>
        /// 다음 공격 식별자를 만들기 위한 누적 시퀀스입니다.
        /// 스킬이 끝나도 값을 되돌리지 않아 같은 실행기 안에서 식별자가 반복되지 않게 합니다.
        /// </summary>
        private int _nextAttackId;

        /// <summary>
        /// 공격 식별자별로 실제 타격 확정 시 다음 스킬 연계를 열 수 있는지 저장합니다.
        /// </summary>
        private readonly Dictionary<int, bool> _chainUnlockByAttackId = new();

        /// <summary>
        /// 새 공격 식별자를 발급하고, 해당 공격의 연계 해제 정책을 저장합니다.
        /// </summary>
        /// <param name="allowSkillChainOnConfirmedDamage">피격 확정 시 다음 스킬 연계를 허용할지 여부입니다.</param>
        /// <returns>이번 공격에 사용할 고유 공격 식별자입니다.</returns>
        public int Allocate(bool allowSkillChainOnConfirmedDamage)
        {
            int attackId = ++_nextAttackId;
            _chainUnlockByAttackId[attackId] = allowSkillChainOnConfirmedDamage;
            return attackId;
        }

        /// <summary>
        /// 지정한 공격 식별자가 실제 데미지 확정 시 다음 스킬 연계를 열 수 있는지 확인합니다.
        /// </summary>
        /// <param name="attackId">검사할 공격 식별자입니다.</param>
        /// <returns>연계 해제 공격이면 <see langword="true"/>입니다.</returns>
        public bool IsChainUnlockAttack(int attackId)
        {
            return attackId > 0 &&
                   _chainUnlockByAttackId.TryGetValue(attackId, out bool enabled) &&
                   enabled;
        }

        /// <summary>
        /// 현재 스킬 실행에 속한 공격별 연계 해제 정보를 비웁니다.
        /// </summary>
        public void Clear()
        {
            _chainUnlockByAttackId.Clear();
        }
    }
}
