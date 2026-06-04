using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이동 조작 잠금 이벤트를 현재 스킬 런타임에 적용합니다.
    /// </summary>
    internal static class SkillMovementControlLockEventHandler
    {
        /// <summary>
        /// Bake된 이동 조작 잠금 이벤트 정의를 해석하여 캐스터의 이동과 조작을 제한합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">Bake된 이동 조작 잠금 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        public static void Handle(
            SkillRun run,
            SkillTargetContext ctx,
            Object payloadObj,
            float eventDurationSeconds)
        {
            if (run == null || ctx.caster == null)
                return;

            if (payloadObj is not MovementControlLockEventDefinition def)
                return;

            CharacterBase character =
                ctx.caster.GetComponent<CharacterBase>() ??
                ctx.caster.GetComponentInParent<CharacterBase>();
            if (character == null)
                return;

            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : eventDurationSeconds;
            bool keepUntilSkillEnd = def.durationPolicy == MovementControlLockDurationPolicy.UntilSkillEnd;
            if (!keepUntilSkillEnd && duration <= 0f)
                return;

            run.TryStartMovementControlLock(
                character,
                Mathf.Max(0f, duration),
                keepUntilSkillEnd,
                def.stopImmediately,
                def.cancelSkillMotion,
                def.lockControl,
                def.autoMovePolicy);
        }
    }
}
