using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 그라운드슬램 스킬 이벤트의 착지 지점 계산과 모션 요청 생성을 담당합니다.
    /// </summary>
    internal static class SkillGroundSlamMotionResolver
    {
        /// <summary>
        /// 공중 대기 구간 동안 캐릭터를 현재 위치에 고정하는 모션을 시작합니다.
        /// </summary>
        /// <param name="motion">모션 요청을 실행할 캐릭터 모션 컨트롤러입니다.</param>
        /// <param name="def">그라운드슬램 이벤트 정의입니다.</param>
        /// <param name="holdPosition">대기 중 유지할 월드 좌표입니다.</param>
        /// <param name="holdDurationSeconds">대기 지속 시간(초)입니다.</param>
        /// <returns>위치 고정 모션을 시작했으면 <see langword="true"/>입니다.</returns>
        public static bool TryStartHoldMotion(
            ICharacterMotionController motion,
            GroundSlamEventDefinition def,
            Vector2 holdPosition,
            float holdDurationSeconds)
        {
            if (motion == null || def == null || holdDurationSeconds <= 0f)
                return false;

            var req = new MotionRequest(
                MotionChannel.Skill,
                MotionKind.PositionHold,
                Vector2.zero,
                holdDurationSeconds,
                0f,
                Easing.EaseType.Linear,
                stopAtEnd: true,
                useMovePosition: def.useMovePosition,
                allowReplace: def.allowReplace,
                startPosition: holdPosition,
                targetPosition: holdPosition,
                groundSnapDistance: 0f);

            return motion.TryStartMotion(in req);
        }

        /// <summary>
        /// 시작 지점에서 착지 지점까지 이동하는 그라운드슬램 낙하 모션을 시작합니다.
        /// </summary>
        /// <param name="motion">모션 요청을 실행할 캐릭터 모션 컨트롤러입니다.</param>
        /// <param name="def">그라운드슬램 이벤트 정의입니다.</param>
        /// <param name="startPosition">낙하 시작 월드 좌표입니다.</param>
        /// <param name="targetPosition">낙하 도착 월드 좌표입니다.</param>
        /// <param name="fallDurationSeconds">낙하 지속 시간(초)입니다.</param>
        /// <returns>낙하 모션을 시작했으면 <see langword="true"/>입니다.</returns>
        public static bool TryStartSlamMotion(
            ICharacterMotionController motion,
            GroundSlamEventDefinition def,
            Vector2 startPosition,
            Vector2 targetPosition,
            float fallDurationSeconds)
        {
            if (motion == null || def == null)
                return false;

            Vector2 travel = targetPosition - startPosition;
            if (travel.sqrMagnitude <= 1e-8f)
                return false;

            var req = new MotionRequest(
                MotionChannel.Skill,
                MotionKind.GroundSlam,
                travel.normalized,
                fallDurationSeconds,
                travel.magnitude,
                def.easing,
                stopAtEnd: def.stopAtEnd,
                useMovePosition: def.useMovePosition,
                allowReplace: true,
                startPosition: startPosition,
                targetPosition: targetPosition,
                groundSnapDistance: def.groundSnapDistance);

            return motion.TryStartMotion(in req);
        }

        /// <summary>
        /// 수평 이동 정책과 착지 모드를 기준으로 그라운드슬램 도착 위치를 계산합니다.
        /// </summary>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="def">그라운드슬램 이벤트 정의입니다.</param>
        /// <param name="startPosition">낙하 시작 월드 좌표입니다.</param>
        /// <param name="forward">캐릭터의 전방 방향입니다.</param>
        /// <param name="targetPosition">계산된 착지 월드 좌표입니다.</param>
        /// <returns>착지 위치를 사용할 수 있으면 <see langword="true"/>입니다.</returns>
        public static bool TryResolveTargetPosition(
            SkillTargetContext ctx,
            GroundSlamEventDefinition def,
            Vector2 startPosition,
            Vector2 forward,
            out Vector2 targetPosition)
        {
            float targetX = startPosition.x;
            switch (def.horizontalPolicy)
            {
                case GroundSlamHorizontalPolicy.KeepCurrentX:
                    targetX = startPosition.x;
                    break;
                case GroundSlamHorizontalPolicy.MoveToTargetX:
                    if (ctx.lockedTarget != null)
                        targetX = ctx.lockedTarget.transform.position.x;
                    else if (ctx.groundPoint != default)
                        targetX = ctx.groundPoint.x;
                    break;
                case GroundSlamHorizontalPolicy.MoveByForward:
                    targetX = startPosition.x + forward.x * Mathf.Max(0f, def.forwardDistance);
                    break;
            }

            switch (def.landingMode)
            {
                case GroundSlamLandingMode.FixedDistanceDown:
                {
                    float targetY = startPosition.y - Mathf.Max(0f, def.fixedDropDistance);
                    targetPosition = new Vector2(targetX, targetY);
                    return targetY < startPosition.y - 1e-4f;
                }
                case GroundSlamLandingMode.LockedTargetGround:
                {
                    Vector2 probeBase = ctx.lockedTarget != null
                        ? (Vector2)ctx.lockedTarget.transform.position
                        : new Vector2(targetX, startPosition.y);
                    targetX = probeBase.x;
                    return TryResolveGroundPoint(def, targetX, probeBase.y, startPosition.y, out targetPosition);
                }
                case GroundSlamLandingMode.GroundPoint:
                {
                    Vector2 probeBase = ctx.groundPoint != default
                        ? (Vector2)ctx.groundPoint
                        : new Vector2(targetX, startPosition.y);
                    targetX = probeBase.x;
                    return TryResolveGroundPoint(def, targetX, probeBase.y, startPosition.y, out targetPosition);
                }
                case GroundSlamLandingMode.CurrentGround:
                default:
                    return TryResolveGroundPoint(def, targetX, startPosition.y, startPosition.y, out targetPosition);
            }
        }

        /// <summary>
        /// 지정된 X좌표 주변의 지면을 레이캐스트로 탐색하고 실패 시 고정 낙하 거리로 대체 좌표를 계산합니다.
        /// </summary>
        /// <param name="def">그라운드슬램 이벤트 정의입니다.</param>
        /// <param name="targetX">탐색할 착지 X좌표입니다.</param>
        /// <param name="referenceY">레이캐스트 시작 높이를 계산할 기준 Y좌표입니다.</param>
        /// <param name="startY">낙하 시작 Y좌표입니다.</param>
        /// <param name="targetPosition">계산된 지면 또는 대체 착지 좌표입니다.</param>
        /// <returns>착지 좌표를 계산했으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveGroundPoint(
            GroundSlamEventDefinition def,
            float targetX,
            float referenceY,
            float startY,
            out Vector2 targetPosition)
        {
            Vector2 origin = new Vector2(targetX, Mathf.Max(referenceY, startY) + Mathf.Max(0f, def.groundProbeStartHeight));
            float probeDistance = Mathf.Max(0.1f, def.groundProbeDistance);
            var hit = Physics2D.Raycast(origin, Vector2.down, probeDistance, def.groundLayerMask);
            if (hit.collider != null)
            {
                targetPosition = new Vector2(targetX, hit.point.y);
                return true;
            }

            float fallbackY = startY - Mathf.Max(0f, def.fixedDropDistance);
            targetPosition = new Vector2(targetX, fallbackY);
            return true;
        }
    }
}
