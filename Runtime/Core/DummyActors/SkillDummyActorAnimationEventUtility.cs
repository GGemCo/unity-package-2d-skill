using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// PlayDummyCharacterAnimation 이벤트 정의를 실제 더미 액터 애니메이션 명령으로 변환하고 실행합니다.
    /// </summary>
    internal static class SkillDummyActorAnimationEventUtility
    {
        /// <summary>
        /// 더미 애니메이션 이벤트를 해석해 대상 더미를 찾고, 애니메이션 재생과 후속 전환 예약을 수행합니다.
        /// </summary>
        /// <param name="runner">후속 애니메이션 코루틴 실행과 취소에 사용할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="casterHandle">Caster 참조에 재사용할 임시 핸들입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">더미 애니메이션 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">타임라인 클립 구간에서 계산한 이벤트 지속 시간(초)입니다.</param>
        /// <returns>애니메이션 이벤트 실행에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryExecuteAnimation(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle casterHandle,
            SkillTargetContext ctx,
            PlayDummyCharacterAnimationEventDefinition def,
            float eventDurationSeconds)
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

            PlayAnimation(runner, handle, def.animationName, def.loop, def.timeScale);
            ApplyDurationEndPolicy(runner, handle, def, eventDurationSeconds);
            return true;
        }

        /// <summary>
        /// 더미 캐릭터 애니메이션 요청을 시작합니다.
        /// 기존에 예약된 후속 애니메이션 전환이 있으면 취소한 뒤 새 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="runner">예약된 후속 애니메이션 전환을 취소할 MonoBehaviour입니다.</param>
        /// <param name="handle">애니메이션을 재생할 더미 핸들입니다.</param>
        /// <param name="animationName">재생할 애니메이션 이름입니다.</param>
        /// <param name="loop">루프 재생 여부입니다.</param>
        /// <param name="timeScale">재생 속도 배율입니다.</param>
        public static void PlayAnimation(
            MonoBehaviour runner,
            SkillDummyActorHandle handle,
            string animationName,
            bool loop,
            float timeScale)
        {
            if (handle == null)
                return;

            SkillDummyActorLifecycleUtility.CancelAnimationFollowup(runner, handle);
            SkillDummyActorPresentationUtility.PlayAnimation(handle.Character, animationName, loop, timeScale);
        }

        /// <summary>
        /// 이벤트 지속 시간 정책에 따라 후속 애니메이션 전환을 즉시 적용하거나 예약합니다.
        /// </summary>
        /// <param name="runner">후속 애니메이션 코루틴을 실행할 MonoBehaviour입니다.</param>
        /// <param name="handle">후속 전환을 적용할 더미 핸들입니다.</param>
        /// <param name="def">더미 애니메이션 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">타임라인 클립 구간에서 계산한 이벤트 지속 시간(초)입니다.</param>
        private static void ApplyDurationEndPolicy(
            MonoBehaviour runner,
            SkillDummyActorHandle handle,
            PlayDummyCharacterAnimationEventDefinition def,
            float eventDurationSeconds)
        {
            if (handle == null)
                return;

            if (def.durationPolicy != DummyAnimationDurationPolicy.UseClipWindow)
                return;

            if (def.endPolicy == DummyAnimationEndPolicy.None)
                return;

            float duration = Mathf.Max(0f, eventDurationSeconds);
            if (duration <= 0f)
            {
                ApplyEndPolicyImmediately(handle, def);
                return;
            }

            ScheduleEndPolicy(runner, handle, def, duration);
        }

        /// <summary>
        /// 지속 시간이 없을 때 후속 애니메이션 종료 정책을 즉시 적용합니다.
        /// </summary>
        /// <param name="handle">종료 정책을 적용할 더미 핸들입니다.</param>
        /// <param name="def">더미 애니메이션 이벤트 정의입니다.</param>
        private static void ApplyEndPolicyImmediately(
            SkillDummyActorHandle handle,
            PlayDummyCharacterAnimationEventDefinition def)
        {
            SkillDummyActorPresentationUtility.ApplyAnimationEndPolicy(
                handle,
                def.endPolicy,
                def.endAnimationName,
                def.endAnimationLoop,
                def.endAnimationTimeScale);
        }

        /// <summary>
        /// 지정한 지속 시간이 끝난 뒤 후속 애니메이션 종료 정책을 적용하도록 예약합니다.
        /// </summary>
        /// <param name="runner">후속 애니메이션 코루틴을 실행할 MonoBehaviour입니다.</param>
        /// <param name="handle">후속 전환을 예약할 더미 핸들입니다.</param>
        /// <param name="def">더미 애니메이션 이벤트 정의입니다.</param>
        /// <param name="durationSeconds">후속 전환 전 대기 시간(초)입니다.</param>
        private static void ScheduleEndPolicy(
            MonoBehaviour runner,
            SkillDummyActorHandle handle,
            PlayDummyCharacterAnimationEventDefinition def,
            float durationSeconds)
        {
            handle.ActiveAnimationCoroutine = SkillDummyActorLifecycleUtility.StartAnimationFollowup(
                runner,
                handle,
                durationSeconds,
                def.endPolicy,
                def.endAnimationName,
                def.endAnimationLoop,
                def.endAnimationTimeScale);
        }
    }
}
