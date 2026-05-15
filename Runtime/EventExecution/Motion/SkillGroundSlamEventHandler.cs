using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 그라운드슬램 이벤트의 페이로드 검증, 착지 지점 해석, 모션 시작, 애니메이션 전환을 담당합니다.
    /// </summary>
    internal static class SkillGroundSlamEventHandler
    {
        /// <summary>
        /// 그라운드슬램 이벤트 정의에 따라 착지 지점을 계산하고 내려치기 이동 또는 즉시 착지 연출을 시작합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">그라운드슬램 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        /// <param name="animationController">그라운드슬램 애니메이션과 대기 상태를 관리할 컨트롤러입니다.</param>
        public static void Handle(
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds,
            SkillGroundSlamAnimationController animationController)
        {
            if (payloadObj is not GroundSlamEventDefinition def)
                return;
            if (ctx.caster == null)
                return;

            var motion = ResolveMotionController(ctx.caster);
            if (motion == null)
                return;

            float holdDuration = Mathf.Max(0f, def.airHoldDurationSeconds);
            float fallDuration = ResolveFallDuration(def, eventDurationSeconds);
            if (fallDuration <= 0f)
                return;

            Vector2 startPosition = ctx.caster.transform.position;
            Vector2 forward = ResolveForward(ctx.caster, ctx.forward, def.useSnapshotForward);
            if (!SkillGroundSlamMotionResolver.TryResolveTargetPosition(ctx, def, startPosition, forward, out Vector2 targetPosition))
                return;

            Vector2 travel = targetPosition - startPosition;
            if (travel.sqrMagnitude <= 1e-8f)
            {
                animationController?.BeginInstantLandSequence(ctx.caster, motion, def);
                return;
            }

            if (holdDuration > 0f)
            {
                if (def.holdPositionDuringAirHold &&
                    !SkillGroundSlamMotionResolver.TryStartHoldMotion(motion, def, startPosition, holdDuration))
                {
                    return;
                }

                animationController?.BeginPendingSlam(
                    ctx.caster,
                    motion,
                    def,
                    startPosition,
                    targetPosition,
                    fallDuration,
                    holdDuration);
                return;
            }

            if (!SkillGroundSlamMotionResolver.TryStartSlamMotion(motion, def, startPosition, targetPosition, fallDuration))
                return;

            animationController?.BeginMotionAnimation(ctx.caster, motion, def, usePhaseBasedLoopTransition: false);
        }

        /// <summary>
        /// 캐스터에서 그라운드슬램 모션을 실행할 캐릭터 모션 컨트롤러를 조회합니다.
        /// </summary>
        /// <param name="caster">모션 컨트롤러를 찾을 캐스터 오브젝트입니다.</param>
        /// <returns>캐스터 상위 계층의 모션 컨트롤러입니다.</returns>
        private static ICharacterMotionController ResolveMotionController(GameObject caster)
        {
            return caster != null ? caster.GetComponentInParent<ICharacterMotionController>() : null;
        }

        /// <summary>
        /// 그라운드슬램 이벤트 정의와 타임라인 기본값을 기준으로 낙하 지속 시간을 계산합니다.
        /// </summary>
        /// <param name="def">그라운드슬램 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        /// <returns>낙하 모션에 사용할 지속 시간(초)입니다.</returns>
        private static float ResolveFallDuration(GroundSlamEventDefinition def, float eventDurationSeconds)
        {
            if (def.fallDurationSeconds > 0f)
                return def.fallDurationSeconds;

            return def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : eventDurationSeconds;
        }

        /// <summary>
        /// 스냅샷 전방 사용 여부에 따라 그라운드슬램 착지점 계산에 사용할 전방 방향을 반환합니다.
        /// </summary>
        /// <param name="caster">방향 보정 기준이 되는 캐스터 오브젝트입니다.</param>
        /// <param name="snapshotForward">스킬 시작 시점에 기록된 전방 벡터입니다.</param>
        /// <param name="useSnapshotForward">스냅샷 전방을 우선 사용할지 여부입니다.</param>
        /// <returns>정규화된 2D 전방 방향입니다.</returns>
        private static Vector2 ResolveForward(GameObject caster, Vector3 snapshotForward, bool useSnapshotForward)
        {
            Vector2 forward = useSnapshotForward
                ? (Vector2)SkillDirectionResolver.ResolveForward2D(caster, snapshotForward)
                : SkillDirectionResolver.ResolveCurrentFacing2D(caster);

            if (forward.sqrMagnitude <= 1e-6f)
                return Vector2.right;

            forward.Normalize();
            return forward;
        }
    }
}
