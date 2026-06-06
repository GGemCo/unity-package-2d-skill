using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 실행 중 계산한 위치를 이후 이벤트에서 재사용할 수 있도록 이름 있는 위치 앵커로 저장합니다.
    /// </summary>
    internal static class SkillCaptureTargetPositionEventHandler
    {
        /// <summary>
        /// 위치 캡처 이벤트를 실행하고 현재 스킬 런에 위치 앵커를 저장합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">위치 캡처 이벤트 정의입니다.</param>
        /// <param name="snapshotCasterPos">스킬 시작 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점의 지면 기준점입니다.</param>
        public static void Handle(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (run == null || payloadObj is not CaptureTargetPositionEventDefinition def)
                return;

            if (string.IsNullOrWhiteSpace(def.anchorKey))
                return;

            Vector3 casterPos = ctx.caster != null ? ctx.caster.transform.position : snapshotCasterPos;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            CharacterBase targetChar = ctx.lockedTarget != null
                ? ctx.lockedTarget.GetComponent<CharacterBase>()
                : null;
            Vector3 resolvedForward = SkillDirectionResolver.ResolveForward2D(ctx.caster, ctx.forward);
            Vector3 position = ResolveCapturePosition(
                skill,
                ctx,
                def,
                casterPos,
                targetPos,
                groundPoint,
                snapshotTargetPos,
                resolvedForward,
                targetChar);

            // HitArea 기반 보정이 아닌 경우에는 최종 기준점에 공통 오프셋을 더합니다.
            if (def.targetPointPolicy != SkillPositionCaptureTargetPointPolicy.FixedOffsetFromTargetCenter &&
                def.targetPointPolicy != SkillPositionCaptureTargetPointPolicy.FixedNormalizedPointInTargetHitArea)
            {
                position += (Vector3)def.offset;
            }

            var snapshot = new SkillPositionAnchorSnapshot(
                position,
                resolvedForward,
                casterPos,
                targetPos,
                groundPoint,
                run.CurrentTime);

            run.SavePositionAnchor(def.anchorKey, snapshot);
        }

        /// <summary>
        /// 위치 캡처 이벤트의 기준 위치와 타겟 보정 정책을 해석합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">위치 캡처 이벤트 정의입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점의 타겟 위치입니다.</param>
        /// <param name="resolvedForward">이벤트 시점에 해석된 전방 방향입니다.</param>
        /// <param name="targetChar">현재 고정 타겟 캐릭터입니다.</param>
        /// <returns>위치 앵커에 저장할 월드 좌표입니다.</returns>
        private static Vector3 ResolveCapturePosition(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            CaptureTargetPositionEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            Vector3 snapshotTargetPos,
            Vector3 resolvedForward,
            CharacterBase targetChar)
        {
            Vector3 sourcePosition = ResolveSourcePosition(
                skill,
                ctx,
                def,
                casterPos,
                targetPos,
                groundPoint,
                snapshotTargetPos,
                resolvedForward);

            switch (def.targetPointPolicy)
            {
                case SkillPositionCaptureTargetPointPolicy.FixedOffsetFromTargetCenter:
                    return targetPos + (Vector3)def.offset;

                case SkillPositionCaptureTargetPointPolicy.FixedNormalizedPointInTargetHitArea:
                    if (TryResolveTargetHitAreaNormalizedPoint(
                            targetChar,
                            def.targetHitAreaNormalized,
                            out Vector2 hitAreaPoint))
                    {
                        return hitAreaPoint;
                    }

                    return targetPos + (Vector3)def.offset;

                case SkillPositionCaptureTargetPointPolicy.UseSourcePosition:
                default:
                    return sourcePosition;
            }
        }

        /// <summary>
        /// 캡처 소스 설정에 따라 기본 위치를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">위치 캡처 이벤트 정의입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점의 타겟 위치입니다.</param>
        /// <param name="resolvedForward">이벤트 시점에 해석된 전방 방향입니다.</param>
        /// <returns>타겟 보정 전의 기본 월드 좌표입니다.</returns>
        private static Vector3 ResolveSourcePosition(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            CaptureTargetPositionEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            Vector3 snapshotTargetPos,
            Vector3 resolvedForward)
        {
            switch (def.source)
            {
                case SkillPositionCaptureSource.Caster:
                    return casterPos;

                case SkillPositionCaptureSource.Ground:
                    return groundPoint;

                case SkillPositionCaptureSource.CurrentTargeting:
                    return ResolveCurrentTargetingPosition(skill, ctx, def, casterPos, targetPos, groundPoint, resolvedForward);

                case SkillPositionCaptureSource.SkillStartTargetSnapshot:
                    return snapshotTargetPos;

                case SkillPositionCaptureSource.Target:
                default:
                    return targetPos;
            }
        }

        /// <summary>
        /// 스킬 타겟팅 모드와 사거리 정책을 기준으로 캡처 위치를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">위치 캡처 이벤트 정의입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        /// <param name="resolvedForward">이벤트 시점에 해석된 전방 방향입니다.</param>
        /// <returns>현재 타겟팅 규칙으로 계산한 월드 좌표입니다.</returns>
        private static Vector3 ResolveCurrentTargetingPosition(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            CaptureTargetPositionEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            Vector3 resolvedForward)
        {
            ConfigCommonSkill.SkillTargetingMode mode = skill != null
                ? (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue)
                : ConfigCommonSkill.SkillTargetingMode.ForwardDirectional;

            float range = skill != null ? SkillRangeResolver.GetPlacementRange(skill) : 0f;
            if (def.targetingOverride.enabled)
            {
                mode = def.targetingOverride.mode;
                if (def.targetingOverride.rangeOverride > 0f)
                    range = def.targetingOverride.rangeOverride;
            }

            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    return groundPoint;
                case ConfigCommonSkill.SkillTargetingMode.Self:
                    return casterPos;
                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                    return targetPos;
                default:
                    Vector3 fwd = resolvedForward.sqrMagnitude < 1e-6f ? Vector3.right : resolvedForward.normalized;
                    return SkillRangeResolver.ResolveForwardPlacementPosition(casterPos, fwd, range);
            }
        }

        /// <summary>
        /// 타겟 HitArea 정규화 좌표를 월드 좌표로 변환합니다.
        /// </summary>
        /// <param name="targetChar">좌표를 계산할 타겟 캐릭터입니다.</param>
        /// <param name="normalizedPoint">HitArea 정규화 좌표입니다.</param>
        /// <param name="worldPoint">변환된 월드 좌표입니다.</param>
        /// <returns>HitArea 좌표 계산에 성공했으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveTargetHitAreaNormalizedPoint(
            CharacterBase targetChar,
            Vector2 normalizedPoint,
            out Vector2 worldPoint)
        {
            worldPoint = default;
            if (targetChar == null)
                return false;

            CharacterHitArea hitArea = targetChar.GetComponentInChildren<CharacterHitArea>();
            Collider2D collider = hitArea != null ? hitArea.GetComponent<Collider2D>() : null;
            if (collider == null)
                return false;

            Bounds bounds = collider.bounds;
            float x = Mathf.Lerp(bounds.min.x, bounds.max.x, Mathf.Clamp01(normalizedPoint.x));
            float y = Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(normalizedPoint.y));
            worldPoint = new Vector2(x, y);
            return true;
        }
    }
}
