using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트의 위치 고정 요청을 현재 스킬 런타임에 적용합니다.
    /// </summary>
    internal static class SkillPositionHoldEventHandler
    {
        /// <summary>
        /// 위치 고정 이벤트 정의를 해석하여 스킬 런타임에 위치 고정 상태를 시작합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">Bake된 위치 고정 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        public static void Handle(
            SkillRun run,
            SkillTargetContext ctx,
            Object payloadObj,
            float eventDurationSeconds)
        {
            if (run == null)
                return;

            if (payloadObj is not PositionHoldEventDefinition def)
                return;

            if (ctx.caster == null)
                return;

            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : eventDurationSeconds;
            bool keepUntilSkillEnd = def.durationPolicy == PositionHoldDurationPolicy.UntilSkillEnd;
            if (!keepUntilSkillEnd && duration <= 0f)
                return;

            run.TryStartPositionHold(duration, keepUntilSkillEnd, def.stopAtEnd, def.useMovePosition, def.allowReplace);
        }
    }
}
