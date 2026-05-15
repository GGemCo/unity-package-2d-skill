using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런지 계열 스킬 이벤트의 이동 방향, 이동 거리, 충돌 정책을 계산합니다.
    /// </summary>
    internal static class SkillLungeMotionResolver
    {
        /// <summary>
        /// 아크 런지 이벤트 정의를 기준으로 최종 이동 방향과 거리를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="def">아크 런지 이벤트 정의입니다.</param>
        /// <param name="fallbackDirection">타겟 해석 실패 시 사용할 기본 방향입니다.</param>
        /// <param name="resolvedDirection">계산된 이동 방향입니다.</param>
        /// <param name="resolvedDistance">계산된 이동 거리입니다.</param>
        /// <returns>이동 방향과 거리를 사용할 수 있으면 <see langword="true"/>입니다.</returns>
        public static bool TryResolveArcLungeMotion(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            ArcLungeEventDefinition def,
            Vector2 fallbackDirection,
            out Vector2 resolvedDirection,
            out float resolvedDistance)
        {
            resolvedDirection = fallbackDirection;
            resolvedDistance = Mathf.Max(0f, def.distance);

            switch (def.resolveMode)
            {
                case SkillLungeResolveMode.FixedDistance:
                    return EnsureFallbackDirection(ctx, def.horizontalOnly, ref resolvedDirection);

                case SkillLungeResolveMode.ToLockedTarget:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: false, fallbackToFixedDistance: false, out resolvedDirection, out resolvedDistance);

                case SkillLungeResolveMode.ToLockedTargetIfWithinResolveRange:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: true, fallbackToFixedDistance: false, out resolvedDirection, out resolvedDistance);

                case SkillLungeResolveMode.ToLockedTargetElseFixedDistance:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: true, fallbackToFixedDistance: true, out resolvedDirection, out resolvedDistance);

                default:
                    return EnsureFallbackDirection(ctx, def.horizontalOnly, ref resolvedDirection);
            }
        }

        /// <summary>
        /// 아크 런지의 상승, 정점 대기, 하강 시간을 전체 구간 대비 비율로 정규화합니다.
        /// </summary>
        /// <param name="riseDuration">상승 시간(초)입니다.</param>
        /// <param name="apexHoldDuration">정점 대기 시간(초)입니다.</param>
        /// <param name="fallDuration">하강 시간(초)입니다.</param>
        /// <param name="totalDuration">전체 이동 시간(초)입니다.</param>
        /// <param name="normalizedRise">정규화된 상승 비율입니다.</param>
        /// <param name="normalizedApex">정규화된 정점 대기 비율입니다.</param>
        /// <param name="normalizedFall">정규화된 하강 비율입니다.</param>
        public static void NormalizeArcDurations(
            float riseDuration,
            float apexHoldDuration,
            float fallDuration,
            float totalDuration,
            out float normalizedRise,
            out float normalizedApex,
            out float normalizedFall)
        {
            float rise = Mathf.Max(0f, riseDuration);
            float apex = Mathf.Max(0f, apexHoldDuration);
            float fall = Mathf.Max(0f, fallDuration);
            float sum = rise + apex + fall;
            if (sum <= 1e-6f)
            {
                if (totalDuration > 0f)
                {
                    rise = totalDuration * 0.5f;
                    fall = totalDuration * 0.5f;
                }
                else
                {
                    rise = 0.5f;
                    fall = 0.5f;
                }

                sum = rise + fall;
            }

            normalizedRise = rise / sum;
            normalizedApex = apex / sum;
            normalizedFall = fall / sum;
        }

        /// <summary>
        /// 직선 런지 이벤트 정의를 기준으로 최종 이동 방향과 거리를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="fallbackDirection">타겟 해석 실패 시 사용할 기본 방향입니다.</param>
        /// <param name="resolvedDirection">계산된 이동 방향입니다.</param>
        /// <param name="resolvedDistance">계산된 이동 거리입니다.</param>
        /// <returns>이동 방향과 거리를 사용할 수 있으면 <see langword="true"/>입니다.</returns>
        public static bool TryResolveLungeMotion(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            LungeEventDefinition def,
            Vector2 fallbackDirection,
            out Vector2 resolvedDirection,
            out float resolvedDistance)
        {
            resolvedDirection = fallbackDirection;
            resolvedDistance = Mathf.Max(0f, def.distance);

            switch (def.resolveMode)
            {
                case SkillLungeResolveMode.FixedDistance:
                    return EnsureFallbackDirection(ctx, def, ref resolvedDirection);

                case SkillLungeResolveMode.ToLockedTarget:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: false, fallbackToFixedDistance: false, out resolvedDirection, out resolvedDistance);

                case SkillLungeResolveMode.ToLockedTargetIfWithinResolveRange:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: true, fallbackToFixedDistance: false, out resolvedDirection, out resolvedDistance);

                case SkillLungeResolveMode.ToLockedTargetElseFixedDistance:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: true, fallbackToFixedDistance: true, out resolvedDirection, out resolvedDistance);

                default:
                    return EnsureFallbackDirection(ctx, def, ref resolvedDirection);
            }
        }

        /// <summary>
        /// 스킬 런지 충돌 정책을 코어 모션 충돌 정책으로 변환합니다.
        /// </summary>
        /// <param name="collisionPolicy">스킬 이벤트에 설정된 충돌 정책입니다.</param>
        /// <returns>모션 컨트롤러에 전달할 충돌 정책입니다.</returns>
        public static MotionCollisionPolicy ResolveMotionCollisionPolicy(SkillLungeCollisionPolicy collisionPolicy)
        {
            return collisionPolicy == SkillLungeCollisionPolicy.IgnoreLockedTargetCharacter
                ? MotionCollisionPolicy.IgnoreTargetCharacter
                : MotionCollisionPolicy.Default;
        }

        /// <summary>
        /// 직선 런지 이벤트 정의의 충돌 정책을 코어 모션 충돌 정책으로 변환합니다.
        /// </summary>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <returns>모션 컨트롤러에 전달할 충돌 정책입니다.</returns>
        public static MotionCollisionPolicy ResolveMotionCollisionPolicy(LungeEventDefinition def)
        {
            return ResolveMotionCollisionPolicy(def.collisionPolicy);
        }

        /// <summary>
        /// 충돌 정책에 따라 모션 컨트롤러가 무시할 잠금 타겟 오브젝트를 반환합니다.
        /// </summary>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="collisionPolicy">스킬 이벤트에 설정된 충돌 정책입니다.</param>
        /// <returns>무시할 타겟 오브젝트입니다. 정책이 기본값이면 <see langword="null"/>입니다.</returns>
        public static GameObject ResolveMotionCollisionTarget(SkillTargetContext ctx, SkillLungeCollisionPolicy collisionPolicy)
        {
            if (collisionPolicy != SkillLungeCollisionPolicy.IgnoreLockedTargetCharacter)
                return null;

            return ctx.lockedTarget;
        }

        /// <summary>
        /// 직선 런지 이벤트 정의의 충돌 정책에 따라 무시할 잠금 타겟 오브젝트를 반환합니다.
        /// </summary>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <returns>무시할 타겟 오브젝트입니다. 정책이 기본값이면 <see langword="null"/>입니다.</returns>
        public static GameObject ResolveMotionCollisionTarget(SkillTargetContext ctx, LungeEventDefinition def)
        {
            return ResolveMotionCollisionTarget(ctx, def.collisionPolicy);
        }

        /// <summary>
        /// 아크 런지의 잠금 타겟 기준 이동 방향과 거리를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="def">아크 런지 이벤트 정의입니다.</param>
        /// <param name="fallbackDirection">타겟 해석 실패 시 사용할 기본 방향입니다.</param>
        /// <param name="requireWithinResolveRange">타겟이 해석 범위 안에 있을 때만 이동할지 여부입니다.</param>
        /// <param name="fallbackToFixedDistance">해석 실패 시 고정 거리 이동으로 대체할지 여부입니다.</param>
        /// <param name="resolvedDirection">계산된 이동 방향입니다.</param>
        /// <param name="resolvedDistance">계산된 이동 거리입니다.</param>
        /// <returns>이동 방향과 거리를 사용할 수 있으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveLockedTargetMotion(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            ArcLungeEventDefinition def,
            Vector2 fallbackDirection,
            bool requireWithinResolveRange,
            bool fallbackToFixedDistance,
            out Vector2 resolvedDirection,
            out float resolvedDistance)
        {
            resolvedDirection = fallbackDirection;
            resolvedDistance = Mathf.Max(0f, def.distance);

            if (ctx.caster == null || ctx.lockedTarget == null)
                return fallbackToFixedDistance && EnsureFallbackDirection(ctx, def.horizontalOnly, ref resolvedDirection);

            Vector2 delta = ResolveCasterToTargetDelta(ctx.caster.transform.position, ctx.lockedTarget.transform.position, def.horizontalOnly);
            float targetDistance = delta.magnitude;
            float resolveRange = ResolveTargetResolveRange(skill, def.targetResolveRange, def.distance);

            bool withinResolveRange = !requireWithinResolveRange || resolveRange <= 0f || targetDistance <= resolveRange;
            if (!withinResolveRange)
                return fallbackToFixedDistance && EnsureFallbackDirection(ctx, def.horizontalOnly, ref resolvedDirection);

            if (targetDistance <= 1e-4f)
            {
                if (def.targetRelationMode == SkillLungeTargetRelationMode.PassThroughTarget)
                {
                    resolvedDistance = Mathf.Max(0f, def.passThroughExtraDistance);
                    return resolvedDistance > 0f && EnsureFallbackDirection(ctx, def.horizontalOnly, ref resolvedDirection);
                }

                resolvedDistance = 0f;
                return false;
            }

            resolvedDirection = delta / targetDistance;
            if (def.horizontalOnly)
                resolvedDirection = new Vector2(Mathf.Sign(resolvedDirection.x), 0f);

            switch (def.targetRelationMode)
            {
                case SkillLungeTargetRelationMode.ReachTargetCenter:
                    resolvedDistance = targetDistance;
                    break;
                case SkillLungeTargetRelationMode.PassThroughTarget:
                    resolvedDistance = targetDistance + Mathf.Max(0f, def.passThroughExtraDistance);
                    break;
                default:
                    resolvedDistance = Mathf.Max(0f, targetDistance - Mathf.Max(0f, def.stopOffset));
                    break;
            }

            return resolvedDistance > 0f;
        }

        /// <summary>
        /// 직선 런지의 잠금 타겟 기준 이동 방향과 거리를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="fallbackDirection">타겟 해석 실패 시 사용할 기본 방향입니다.</param>
        /// <param name="requireWithinResolveRange">타겟이 해석 범위 안에 있을 때만 이동할지 여부입니다.</param>
        /// <param name="fallbackToFixedDistance">해석 실패 시 고정 거리 이동으로 대체할지 여부입니다.</param>
        /// <param name="resolvedDirection">계산된 이동 방향입니다.</param>
        /// <param name="resolvedDistance">계산된 이동 거리입니다.</param>
        /// <returns>이동 방향과 거리를 사용할 수 있으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveLockedTargetMotion(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            LungeEventDefinition def,
            Vector2 fallbackDirection,
            bool requireWithinResolveRange,
            bool fallbackToFixedDistance,
            out Vector2 resolvedDirection,
            out float resolvedDistance)
        {
            resolvedDirection = fallbackDirection;
            resolvedDistance = Mathf.Max(0f, def.distance);

            if (ctx.caster == null || ctx.lockedTarget == null)
                return fallbackToFixedDistance && EnsureFallbackDirection(ctx, def, ref resolvedDirection);

            Vector2 delta = ResolveCasterToTargetDelta(ctx.caster.transform.position, ctx.lockedTarget.transform.position, def.horizontalOnly);
            float targetDistance = delta.magnitude;
            float resolveRange = ResolveTargetResolveRange(skill, def);

            bool withinResolveRange = !requireWithinResolveRange || resolveRange <= 0f || targetDistance <= resolveRange;
            if (!withinResolveRange)
                return fallbackToFixedDistance && EnsureFallbackDirection(ctx, def, ref resolvedDirection);

            if (targetDistance <= 1e-4f)
            {
                if (def.targetRelationMode == SkillLungeTargetRelationMode.PassThroughTarget)
                {
                    resolvedDistance = Mathf.Max(0f, def.passThroughExtraDistance);
                    return resolvedDistance > 0f && EnsureFallbackDirection(ctx, def, ref resolvedDirection);
                }

                resolvedDistance = 0f;
                return false;
            }

            resolvedDirection = delta / targetDistance;
            if (def.horizontalOnly)
                resolvedDirection = new Vector2(Mathf.Sign(resolvedDirection.x), 0f);

            resolvedDistance = ResolveLockedTargetDistance(def, targetDistance);
            return resolvedDistance > 0f;
        }

        /// <summary>
        /// 직선 런지의 타겟 관계 정책에 따라 최종 이동 거리를 계산합니다.
        /// </summary>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="targetDistance">캐스터와 타겟 사이의 거리입니다.</param>
        /// <returns>정책이 반영된 이동 거리입니다.</returns>
        private static float ResolveLockedTargetDistance(LungeEventDefinition def, float targetDistance)
        {
            targetDistance = Mathf.Max(0f, targetDistance);
            switch (def.targetRelationMode)
            {
                case SkillLungeTargetRelationMode.ReachTargetCenter:
                    return targetDistance;

                case SkillLungeTargetRelationMode.PassThroughTarget:
                    return targetDistance + Mathf.Max(0f, def.passThroughExtraDistance);

                case SkillLungeTargetRelationMode.StopBeforeTarget:
                default:
                    return Mathf.Max(0f, targetDistance - Mathf.Max(0f, def.stopOffset));
            }
        }

        /// <summary>
        /// 기본 방향 벡터가 유효하지 않을 때 캐스터 바라보기 기반 방향으로 보정합니다.
        /// </summary>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="horizontalOnly">수평 방향만 사용할지 여부입니다.</param>
        /// <param name="direction">보정할 방향 벡터입니다.</param>
        /// <returns>방향 벡터를 사용할 수 있으면 <see langword="true"/>입니다.</returns>
        private static bool EnsureFallbackDirection(SkillTargetContext ctx, bool horizontalOnly, ref Vector2 direction)
        {
            if (horizontalOnly)
            {
                if (Mathf.Abs(direction.x) < 1e-4f)
                {
                    float sign = 1f;
                    if (ctx.caster != null)
                    {
                        sign = Mathf.Sign(ctx.caster.transform.localScale.x);
                        if (Mathf.Approximately(sign, 0f))
                            sign = 1f;
                    }

                    direction = new Vector2(sign, 0f);
                }
                else
                {
                    direction = new Vector2(Mathf.Sign(direction.x), 0f);
                }

                return true;
            }

            if (direction.sqrMagnitude <= 1e-6f)
            {
                direction = Vector2.right;
            }
            else
            {
                direction.Normalize();
            }

            return true;
        }

        /// <summary>
        /// 직선 런지 정의의 수평 제한 설정을 사용해 기본 방향 벡터를 보정합니다.
        /// </summary>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="direction">보정할 방향 벡터입니다.</param>
        /// <returns>방향 벡터를 사용할 수 있으면 <see langword="true"/>입니다.</returns>
        private static bool EnsureFallbackDirection(SkillTargetContext ctx, LungeEventDefinition def, ref Vector2 direction)
        {
            return EnsureFallbackDirection(ctx, def.horizontalOnly, ref direction);
        }

        /// <summary>
        /// 잠금 타겟 해석에 사용할 최대 유효 거리를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="targetResolveRange">이벤트에서 지정한 해석 거리입니다.</param>
        /// <param name="distance">이벤트에서 지정한 기본 이동 거리입니다.</param>
        /// <returns>잠금 타겟 해석에 사용할 거리입니다.</returns>
        private static float ResolveTargetResolveRange(RuntimeSkillDefinition skill, float targetResolveRange, float distance)
        {
            if (targetResolveRange > 0f)
                return targetResolveRange;

            float castRange = SkillRangeResolver.GetCastRange(skill);
            if (castRange > 0f)
                return castRange;

            return Mathf.Max(0f, distance);
        }

        /// <summary>
        /// 직선 런지 정의를 기준으로 잠금 타겟 해석에 사용할 최대 유효 거리를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <returns>잠금 타겟 해석에 사용할 거리입니다.</returns>
        private static float ResolveTargetResolveRange(RuntimeSkillDefinition skill, LungeEventDefinition def)
        {
            return ResolveTargetResolveRange(skill, def.targetResolveRange, def.distance);
        }

        /// <summary>
        /// 캐스터와 타겟 사이의 2D 이동 벡터를 계산합니다.
        /// </summary>
        /// <param name="casterPosition">캐스터 월드 위치입니다.</param>
        /// <param name="targetPosition">타겟 월드 위치입니다.</param>
        /// <param name="horizontalOnly">수평 방향만 사용할지 여부입니다.</param>
        /// <returns>캐스터에서 타겟으로 향하는 2D 벡터입니다.</returns>
        private static Vector2 ResolveCasterToTargetDelta(Vector3 casterPosition, Vector3 targetPosition, bool horizontalOnly)
        {
            if (horizontalOnly)
                return new Vector2(targetPosition.x - casterPosition.x, 0f);

            return new Vector2(targetPosition.x - casterPosition.x, targetPosition.y - casterPosition.y);
        }
    }
}
