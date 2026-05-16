using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// SetDummyAirborneState 이벤트 정의를 실제 더미 액터 공중 높이 전환 명령으로 변환하고 실행합니다.
    /// </summary>
    internal static class SkillDummyActorAirborneEventUtility
    {
        /// <summary>
        /// 더미 공중 상태 이벤트를 해석해 대상 더미를 찾고, 목표 높이와 중력 유지 정책을 계산한 뒤 전환을 시작합니다.
        /// </summary>
        /// <param name="runner">공중 높이 전환 코루틴을 실행하거나 중단할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="casterHandle">Caster 참조에 재사용할 임시 핸들입니다.</param>
        /// <param name="ctx">현재 스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">더미 공중 상태 이벤트 정의입니다.</param>
        /// <returns>공중 상태 전환 명령 시작에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryExecuteAirborneState(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle casterHandle,
            SkillTargetContext ctx,
            SetDummyAirborneStateEventDefinition def)
        {
            if (runner == null || def == null)
                return false;

            if (!SkillDummyActorReferenceUtility.TryResolveActorHandle(
                    runner,
                    registry,
                    casterHandle,
                    ctx,
                    def.actorReferenceType,
                    def.actorKey,
                    def.missingActorPolicy,
                    out var handle))
                return false;

            if (handle.Character == null)
                return false;

            ResolveTransition(
                def,
                out float targetAirHeight,
                out float durationSeconds,
                out bool keepAirborneGravity);

            SkillDummyActorMotionUtility.StartAirHeightTransition(
                runner,
                handle,
                targetAirHeight,
                durationSeconds,
                def.easing,
                def.allowReplace,
                keepAirborneGravity);
            return true;
        }

        /// <summary>
        /// 이벤트 정의에서 공중 높이 전환에 필요한 목표 높이, 지속 시간, 중력 유지 여부를 계산합니다.
        /// </summary>
        /// <param name="def">더미 공중 상태 이벤트 정의입니다.</param>
        /// <param name="targetAirHeight">계산된 목표 공중 높이입니다.</param>
        /// <param name="durationSeconds">0 이상으로 보정된 전환 시간(초)입니다.</param>
        /// <param name="keepAirborneGravity">전환 완료 후에도 공중 중력 오버라이드를 유지할지 여부입니다.</param>
        private static void ResolveTransition(
            SetDummyAirborneStateEventDefinition def,
            out float targetAirHeight,
            out float durationSeconds,
            out bool keepAirborneGravity)
        {
            targetAirHeight = ResolveTargetAirHeight(def);
            durationSeconds = Mathf.Max(0f, def.durationSeconds);
            keepAirborneGravity = ShouldKeepAirborneGravity(def, targetAirHeight);
        }

        /// <summary>
        /// 공중 상태 활성 여부에 따라 목표 공중 높이를 결정합니다.
        /// </summary>
        /// <param name="def">더미 공중 상태 이벤트 정의입니다.</param>
        /// <returns>활성 상태이면 0 이상으로 보정된 목표 높이, 비활성 상태이면 0입니다.</returns>
        private static float ResolveTargetAirHeight(SetDummyAirborneStateEventDefinition def)
        {
            return def.airborneEnabled ? Mathf.Max(0f, def.targetAirHeight) : 0f;
        }

        /// <summary>
        /// 목표 상태가 공중 유지인지 확인하여 중력 오버라이드를 유지할지 결정합니다.
        /// </summary>
        /// <param name="def">더미 공중 상태 이벤트 정의입니다.</param>
        /// <param name="targetAirHeight">계산된 목표 공중 높이입니다.</param>
        /// <returns>공중 상태를 유지해야 하면 <see langword="true"/>입니다.</returns>
        private static bool ShouldKeepAirborneGravity(
            SetDummyAirborneStateEventDefinition def,
            float targetAirHeight)
        {
            return def.airborneEnabled || targetAirHeight > 0f;
        }
    }
}
