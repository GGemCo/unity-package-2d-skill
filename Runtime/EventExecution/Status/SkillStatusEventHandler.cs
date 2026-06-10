using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트가 요청한 Affect 적용과 런타임 임시 HP 적용을 처리합니다.
    /// </summary>
    internal static class SkillStatusEventHandler
    {
        /// <summary>
        /// 상태 적용 이벤트 정의를 바탕으로 대상에게 Affect를 적용합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">Bake된 상태 적용 이벤트 정의입니다.</param>
        public static void HandleApplyStatus(SkillTargetContext ctx, Object payloadObj)
        {
            if (payloadObj is not ApplyStatusEventDefinition def)
                return;

            if (ctx.caster == null)
                return;

            if (!TryParseAffectUid(def.statusId, out int affectUid))
                return;

            float chance = Mathf.Clamp01(def.chance01);
            if (chance <= 0f)
                return;
            if (chance < 0.9999f && Random.value > chance)
                return;

            GameObject applyTarget = ResolveApplyTarget(ctx, def.applyTo);
            int stacks = Mathf.Max(1, def.stacks);
            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : 0f;
            SkillExecutionOptions executionOptions = ctx.executionOptions;

            for (int s = 0; s < stacks; s++)
            {
                AffectApi.Apply(
                    applyTarget,
                    affectUid,
                    ctx.caster,
                    duration,
                    executionOptions.StatusDurationBonusSeconds,
                    executionOptions.HealHpBonus,
                    executionOptions.HealHpMultiplier);
            }
        }

        /// <summary>
        /// 런타임 Temp HP(비저장 보호막/임시 하트)를 적용하거나 제거합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">Bake된 임시 HP 이벤트 정의입니다.</param>
        public static void HandleApplyTempHp(RuntimeSkillDefinition skill, SkillTargetContext ctx, Object payloadObj)
        {
            if (payloadObj is not ApplyTempHpEventDefinition def)
                return;

            if (ctx.caster == null)
                return;

            GameObject applyTarget = ResolveApplyTarget(ctx, def.applyTo);
            if (applyTarget == null)
                return;

            CharacterBase targetCharacter =
                applyTarget.GetComponent<CharacterBase>() ??
                applyTarget.GetComponentInParent<CharacterBase>();
            if (targetCharacter == null)
                return;

            long tempHpValue = def.tempHpValue > 0 ? def.tempHpValue : 0;
            int sourceKey = def.sourceKeyOverride != 0 ? def.sourceKeyOverride : skill.Uid;

            if (tempHpValue <= 0)
            {
                targetCharacter.ClearRuntimeBonusHpTemp(sourceKey);
                return;
            }

            targetCharacter.SetRuntimeBonusHpTemp(sourceKey, tempHpValue, fillToMax: true);
        }

        /// <summary>
        /// 상태 적용 대상 정책을 실제 GameObject 참조로 변환합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="applyTo">상태 적용 대상 정책입니다.</param>
        /// <returns>상태 또는 임시 HP를 적용할 대상입니다.</returns>
        private static GameObject ResolveApplyTarget(SkillTargetContext ctx, ApplyAffectTarget applyTo)
        {
            switch (applyTo)
            {
                case ApplyAffectTarget.LockedTarget:
                    return ctx.lockedTarget != null ? ctx.lockedTarget : ctx.caster;
                case ApplyAffectTarget.Caster:
                default:
                    return ctx.caster;
            }
        }

        /// <summary>
        /// 상태 식별자 문자열을 Affect UID로 변환합니다.
        /// </summary>
        /// <param name="id">파싱할 상태 식별자입니다.</param>
        /// <param name="affectUid">파싱에 성공한 Affect UID입니다.</param>
        /// <returns>유효한 양의 정수 UID로 변환되면 <see langword="true"/>입니다.</returns>
        private static bool TryParseAffectUid(StatusVfxId id, out int affectUid)
        {
            affectUid = 0;
            if (string.IsNullOrWhiteSpace(id.id))
                return false;

            return int.TryParse(id.id, out affectUid) && affectUid > 0;
        }
    }
}
