using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// VFX 스킬 이벤트의 생성 위치 계산, 위치 앵커 저장, VFX 생성 요청을 처리합니다.
    /// </summary>
    internal static class SkillVfxEventHandler
    {
        /// <summary>
        /// 이펙트 이벤트 정의를 바탕으로 생성 위치를 계산하고 이펙트를 생성합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">이펙트 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        /// <param name="ownedVfxTracker">생성한 VFX를 추적할 소유 VFX 추적기입니다.</param>
        public static void Handle(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            SkillOwnedVfxTracker ownedVfxTracker)
        {
            if (payloadObj is not VfxEventDefinition def)
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

            Vector3 spawnPos = ResolveVfxSpawnPosition(skill, ctx, def, casterPos, targetPos, groundPoint);
            spawnPos += def.localOffset;

            Vector2 resolvedForward = SkillDirectionResolver.ResolveForward2D(ctx.caster, ctx.forward);
            SaveVfxPositionAnchorIfNeeded(
                run,
                def,
                spawnPos,
                resolvedForward,
                casterPos,
                targetPos,
                groundPoint);

            Vector2 visualDirection = ResolveVfxVisualDirection(def, spawnPos, casterPos, targetPos, resolvedForward);

            VfxBehaviourBase vfx = null;
            SceneGame sceneGame = SceneGame.Instance;
            if (sceneGame != null && sceneGame.VfxManager != null)
            {
                vfx = sceneGame.VfxManager.CreateVfx(BuildVfxSpawnRequest(def, spawnPos, visualDirection, resolvedForward));
            }

            if (vfx == null)
                return;

            if (ShouldAttachVfxToTarget(def) && ctx.lockedTarget != null)
            {
                vfx.transform.SetParent(ctx.lockedTarget.transform, worldPositionStays: true);
            }

            vfx.transform.position = spawnPos;
            ownedVfxTracker?.Register(vfx);
        }

        /// <summary>
        /// VFX 수명 설정을 Core VFX 생성 요청에 전달할 지속 시간 값으로 변환합니다.
        /// </summary>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="duration">계산된 VFX 지속 시간입니다.</param>
        /// <returns>명시적 지속 시간 오버라이드가 있으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveVfxDuration(VfxEventDefinition def, out float duration)
        {
            duration = 0f;
            if (def == null)
                return false;

            switch (def.lifetimeMode)
            {
                case VfxLifetimeMode.UseVfxDefault:
                    return false;
                case VfxLifetimeMode.OneShot:
                    duration = 0f;
                    return true;
                case VfxLifetimeMode.FixedDuration:
                    duration = Mathf.Max(0f, def.lifetimeSeconds);
                    return true;
                case VfxLifetimeMode.Infinite:
                    duration = -1f;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 스킬 이벤트 정의와 계산된 생성 위치를 Core VFX 생성 요청으로 변환합니다.
        /// </summary>
        /// <param name="def">스킬 Timeline에서 Bake된 VFX 이벤트 정의입니다.</param>
        /// <param name="spawnPos">타겟팅 규칙과 오프셋을 반영한 월드 생성 위치입니다.</param>
        /// <returns>VFX 매니저에 전달할 생성 요청입니다.</returns>
        private static VfxSpawnRequest BuildVfxSpawnRequest(
            VfxEventDefinition def,
            Vector3 spawnPos,
            Vector2 visualDirection,
            Vector2 sourceDirection)
        {
            TryResolveVfxDuration(def, out float vfxDuration);
            bool hasDirection = visualDirection.sqrMagnitude > 0.0001f || sourceDirection.sqrMagnitude > 0.0001f;

            return new VfxSpawnRequest
            {
                VfxUid = def != null ? def.vfxUid : 0,
                WorldPosition = spawnPos,
                DurationOverride = vfxDuration,
                UseDirection = hasDirection,
                Direction = visualDirection,
                SourceDirection = sourceDirection,
                SortingLayerOverride = def != null && def.overrideSortingLayer
                    ? def.sortingLayerOverride
                    : (ConfigSortingLayer.Keys?)null,
                SortingOrderOverride = def != null && def.overrideSortingOrder
                    ? def.sortingOrderOverride
                    : (int?)null,
            };
        }

        /// <summary>
        /// VFX가 실제로 바라볼 방향을 이벤트 앵커와 타겟팅 정책 기준으로 계산합니다.
        /// </summary>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="spawnPos">최종 생성 위치입니다.</param>
        /// <param name="casterPos">이벤트 시점의 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점의 타겟 위치입니다.</param>
        /// <param name="resolvedForward">캐스터 전방으로 해석된 fallback 방향입니다.</param>
        /// <returns>VFX 방향 보정에 사용할 2D 방향입니다.</returns>
        private static Vector2 ResolveVfxVisualDirection(
            VfxEventDefinition def,
            Vector3 spawnPos,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector2 resolvedForward)
        {
            Vector2 fallbackForward = NormalizeOrDefault(resolvedForward);
            if (def == null)
                return fallbackForward;

            if (def.targetingOverride.enabled)
            {
                switch (def.targetingOverride.mode)
                {
                    case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                    case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                    case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                        return NormalizeOrFallback((Vector2)(targetPos - casterPos), fallbackForward);
                    case ConfigCommonSkill.SkillTargetingMode.Self:
                        return fallbackForward;
                    case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    default:
                        return NormalizeOrFallback((Vector2)(spawnPos - casterPos), fallbackForward);
                }
            }

            switch (ResolveVfxSpawnAnchor(def))
            {
                case VfxSpawnAnchor.Target:
                    return NormalizeOrFallback((Vector2)(targetPos - casterPos), fallbackForward);
                case VfxSpawnAnchor.Ground:
                    return NormalizeOrFallback((Vector2)(spawnPos - casterPos), fallbackForward);
                case VfxSpawnAnchor.Caster:
                default:
                    return fallbackForward;
            }
        }

        /// <summary>
        /// 방향 값이 유효하면 정규화하고, 아니면 fallback 방향을 사용합니다.
        /// </summary>
        /// <param name="direction">검사할 방향입니다.</param>
        /// <param name="fallback">대체 방향입니다.</param>
        /// <returns>정규화된 방향입니다.</returns>
        private static Vector2 NormalizeOrFallback(Vector2 direction, Vector2 fallback)
        {
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : NormalizeOrDefault(fallback);
        }

        /// <summary>
        /// 방향 값이 비어 있으면 오른쪽 방향을 기본값으로 사용합니다.
        /// </summary>
        /// <param name="direction">검사할 방향입니다.</param>
        /// <returns>정규화된 방향입니다.</returns>
        private static Vector2 NormalizeOrDefault(Vector2 direction)
        {
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        }

        /// <summary>
        /// VFX 이벤트가 계산한 최종 생성 위치를 같은 스킬 실행 안의 이름 있는 위치 앵커로 저장합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="spawnPos">VFX가 생성될 최종 월드 위치입니다.</param>
        /// <param name="resolvedForward">이벤트 시점에 해석된 2D 전방 방향입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        private static void SaveVfxPositionAnchorIfNeeded(
            SkillRun run,
            VfxEventDefinition def,
            Vector3 spawnPos,
            Vector3 resolvedForward,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint)
        {
            if (run == null || def == null || !def.positionAnchorWrite.enabled)
                return;

            if (string.IsNullOrWhiteSpace(def.positionAnchorWrite.key))
                return;

            var snapshot = new SkillPositionAnchorSnapshot(
                spawnPos,
                resolvedForward,
                casterPos,
                targetPos,
                groundPoint,
                run.CurrentTime);

            run.SavePositionAnchor(def.positionAnchorWrite.key, snapshot);
        }

        /// <summary>
        /// VFX 이벤트의 앵커 설정을 런타임 생성 위치 기준점으로 해석합니다.
        /// </summary>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <returns>이벤트가 사용해야 할 생성 위치 기준점입니다.</returns>
        private static VfxSpawnAnchor ResolveVfxSpawnAnchor(VfxEventDefinition def)
        {
            if (def == null)
                return VfxSpawnAnchor.Caster;

            if (def.attachToTarget)
                return VfxSpawnAnchor.Target;

            return def.spawnAnchor;
        }

        /// <summary>
        /// VFX 이벤트에 명시된 타겟팅 오버라이드로 생성 월드 위치를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        /// <returns>타겟팅 오버라이드가 가리키는 VFX 생성 월드 위치입니다.</returns>
        private static Vector3 ResolveVfxTargetingOverridePosition(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            VfxEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint)
        {
            switch (def.targetingOverride.mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    return groundPoint;
                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                    return targetPos;
                case ConfigCommonSkill.SkillTargetingMode.Self:
                    return casterPos;
                default:
                    Vector3 fwd = SkillDirectionResolver.ResolveForward2D(ctx.caster, ctx.forward);
                    float range = def.targetingOverride.rangeOverride > 0f
                        ? def.targetingOverride.rangeOverride
                        : SkillRangeResolver.GetPlacementRange(skill);
                    return SkillRangeResolver.ResolveForwardPlacementPosition(casterPos, fwd, range);
            }
        }

        /// <summary>
        /// VFX 이벤트의 앵커 설정에 따라 실제 생성 월드 위치를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        /// <returns>VFX를 생성할 월드 위치입니다.</returns>
        private static Vector3 ResolveVfxSpawnPosition(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            VfxEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint)
        {
            if (def != null && def.targetingOverride.enabled)
                return ResolveVfxTargetingOverridePosition(skill, ctx, def, casterPos, targetPos, groundPoint);

            switch (ResolveVfxSpawnAnchor(def))
            {
                case VfxSpawnAnchor.Target:
                    return targetPos;
                case VfxSpawnAnchor.Ground:
                    return groundPoint;
                case VfxSpawnAnchor.Caster:
                default:
                    return casterPos;
            }
        }

        /// <summary>
        /// 생성된 VFX를 타겟 Transform에 부착해야 하는지 확인합니다.
        /// </summary>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <returns>타겟에 부착해야 하면 <see langword="true"/>입니다.</returns>
        private static bool ShouldAttachVfxToTarget(VfxEventDefinition def)
        {
            return ResolveVfxSpawnAnchor(def) == VfxSpawnAnchor.Target;
        }
    }
}
