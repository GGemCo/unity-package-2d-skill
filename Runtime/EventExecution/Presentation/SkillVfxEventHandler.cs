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

            Vector3 anchorSpawnPos = ResolveVfxSpawnPosition(skill, ctx, def, casterPos, targetPos, groundPoint);
            anchorSpawnPos = ResolveAxisOverrideSpawnPosition(def, anchorSpawnPos, casterPos, targetPos, groundPoint);
            Transform bindingParent = ResolveVfxBindingParentTransform(def, ctx);
            Vector3 spawnPos = ResolveFinalVfxSpawnPosition(def, anchorSpawnPos, bindingParent, ctx.caster);

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
                vfx = sceneGame.VfxManager.CreateVfx(
                    BuildVfxSpawnRequest(def, spawnPos, visualDirection, resolvedForward, bindingParent));
            }

            if (vfx == null)
                return;

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
        /// <param name="parent">생성된 VFX를 결합할 부모 Transform입니다.</param>
        /// <returns>VFX 매니저에 전달할 생성 요청입니다.</returns>
        private static VfxSpawnRequest BuildVfxSpawnRequest(
            VfxEventDefinition def,
            Vector3 spawnPos,
            Vector2 visualDirection,
            Vector2 sourceDirection,
            Transform parent)
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
                Parent = parent,
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
        /// VFX 생성 기준점에 축별 위치 합성 정책을 적용합니다.
        /// </summary>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="anchorSpawnPos">기존 Anchor/TargetingOverride 규칙으로 계산된 기준 위치입니다.</param>
        /// <param name="casterPos">이벤트 시점의 Caster 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점의 Target 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점의 GroundPoint 위치입니다.</param>
        /// <returns>축별 합성 정책이 반영된 VFX 생성 기준 위치입니다.</returns>
        private static Vector3 ResolveAxisOverrideSpawnPosition(
            VfxEventDefinition def,
            Vector3 anchorSpawnPos,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint)
        {
            if (def == null || !def.axisOverride.enabled)
                return anchorSpawnPos;

            VfxPositionAxisOverrideOptions options = def.axisOverride;
            return new Vector3(
                ResolveAxisValue(options.xSource, anchorSpawnPos.x, casterPos.x, targetPos.x, groundPoint.x, options.fixedWorldPosition.x),
                ResolveAxisValue(options.ySource, anchorSpawnPos.y, casterPos.y, targetPos.y, groundPoint.y, options.fixedWorldPosition.y),
                ResolveAxisValue(options.zSource, anchorSpawnPos.z, casterPos.z, targetPos.z, groundPoint.z, options.fixedWorldPosition.z));
        }

        /// <summary>
        /// 축별 위치 소스 정책에 따라 단일 축 값을 해석합니다.
        /// </summary>
        /// <param name="source">축 값을 가져올 기준점입니다.</param>
        /// <param name="anchorValue">기존 Anchor 계산 결과의 축 값입니다.</param>
        /// <param name="casterValue">Caster 위치의 축 값입니다.</param>
        /// <param name="targetValue">Target 위치의 축 값입니다.</param>
        /// <param name="groundValue">GroundPoint 위치의 축 값입니다.</param>
        /// <param name="fixedWorldValue">월드 고정 좌표의 축 값입니다.</param>
        /// <returns>선택된 기준점에서 가져온 축 값입니다.</returns>
        private static float ResolveAxisValue(
            VfxPositionAxisSource source,
            float anchorValue,
            float casterValue,
            float targetValue,
            float groundValue,
            float fixedWorldValue)
        {
            switch (source)
            {
                case VfxPositionAxisSource.Caster:
                    return casterValue;
                case VfxPositionAxisSource.Target:
                    return targetValue;
                case VfxPositionAxisSource.Ground:
                    return groundValue;
                case VfxPositionAxisSource.FixedWorld:
                    return fixedWorldValue;
                case VfxPositionAxisSource.Anchor:
                default:
                    return anchorValue;
            }
        }

        /// <summary>
        /// 결합 정책에 따라 VFX의 부모 Transform을 해석합니다.
        /// </summary>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <returns>결합할 부모 Transform입니다. 결합하지 않으면 <see langword="null"/>입니다.</returns>
        private static Transform ResolveVfxBindingParentTransform(VfxEventDefinition def, SkillTargetContext ctx)
        {
            if (def == null)
                return null;

            switch (def.targetBindingPolicy)
            {
                case VfxTargetBindingPolicy.AttachToCaster:
                    return ctx.caster != null ? ctx.caster.transform : null;
                case VfxTargetBindingPolicy.AttachToTarget:
                    return ctx.lockedTarget != null ? ctx.lockedTarget.transform : null;
                case VfxTargetBindingPolicy.None:
                default:
                    return null;
            }
        }

        /// <summary>
        /// Anchor 위치와 결합 정책이 결정된 뒤 Offset까지 반영한 최종 월드 생성 위치를 계산합니다.
        /// </summary>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="anchorSpawnPos">Anchor 규칙으로 계산된 기본 월드 위치입니다.</param>
        /// <param name="bindingParent">결합할 부모 Transform입니다.</param>
        /// <param name="caster">현재 이벤트를 실행한 캐스터 오브젝트입니다.</param>
        /// <returns>Offset이 반영된 최종 월드 위치입니다.</returns>
        private static Vector3 ResolveFinalVfxSpawnPosition(
            VfxEventDefinition def,
            Vector3 anchorSpawnPos,
            Transform bindingParent,
            GameObject caster)
        {
            if (def == null)
                return anchorSpawnPos;

            Vector3 offset = ResolveCasterFlipOffsetXIfNeeded(def, def.localOffset, caster);
            if (def.offsetSpace == VfxOffsetSpace.ParentLocal && bindingParent != null)
            {
                Vector3 localBase = bindingParent.InverseTransformPoint(anchorSpawnPos);
                Vector3 localWithOffset = localBase + offset;
                return bindingParent.TransformPoint(localWithOffset);
            }

            // ParentLocal인데 부모가 없는 경우에는 안전하게 월드 오프셋으로 처리한다.
            return anchorSpawnPos + offset;
        }

        /// <summary>
        /// 캐스터 좌우 반전 정책에 따라 Offset의 X 값을 보정합니다.
        /// - 정책이 꺼져 있으면 원본 Offset을 그대로 사용합니다.
        /// - 정책이 켜져 있고 캐스터가 좌우 반전 상태이면 X 부호를 반전합니다.
        /// </summary>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="offset">원본 오프셋입니다.</param>
        /// <param name="caster">좌우 반전 상태를 확인할 캐스터 오브젝트입니다.</param>
        /// <returns>좌우 반전 정책이 반영된 오프셋입니다.</returns>
        private static Vector3 ResolveCasterFlipOffsetXIfNeeded(
            VfxEventDefinition def,
            Vector3 offset,
            GameObject caster)
        {
            if (def == null || !def.useCasterFlipOffsetX)
                return offset;

            if (!IsCasterFlipped(caster))
                return offset;

            offset.x *= -1f;
            return offset;
        }

        /// <summary>
        /// 캐스터가 기본 방향 대비 좌우 반전된 상태인지 확인합니다.
        /// - CharacterBase가 있으면 <see cref="CharacterBase.IsFlipped"/>를 우선 사용합니다.
        /// - CharacterBase가 없으면 Transform의 localScale.x 부호를 보조 기준으로 사용합니다.
        /// </summary>
        /// <param name="caster">판정할 캐스터 오브젝트입니다.</param>
        /// <returns>좌우 반전 상태로 판단되면 <see langword="true"/>입니다.</returns>
        private static bool IsCasterFlipped(GameObject caster)
        {
            if (caster == null)
                return false;

            CharacterBase characterBase = caster.GetComponent<CharacterBase>();
            if (characterBase != null)
                return characterBase.IsFlipped();

            return caster.transform.localScale.x < 0f;
        }
    }
}
