using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임 Temp HP(비저장 보호막/임시 하트)를 적용하는 이벤트 정의입니다.
    /// - 같은 source key가 다시 적용되면 누적하지 않고 설정값만큼 다시 채웁니다.
    /// - 현재치가 모두 소모되면 해당 Temp HP는 최대치와 함께 제거됩니다.
    /// </summary>
    public sealed class ApplyTempHpEventDefinition : ScriptableObject
    {
        [Header("Target")]
        public ApplyAffectTarget applyTo = ApplyAffectTarget.Caster;

        [Header("Temp HP")]
        [Min(0)] public long tempHpValue = 0;

        [Tooltip("0이면 현재 skill uid를 source key로 사용합니다.")]
        public int sourceKeyOverride = 0;
    }
}
