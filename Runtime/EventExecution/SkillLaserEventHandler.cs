using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 레이저 스킬 이벤트의 대상 좌표 해석, 시작점 앵커 계산, 메타데이터 생성, 디버그 프리뷰 등록을 담당합니다.
    /// </summary>
    internal static class SkillLaserEventHandler
    {
        /// <summary>
        /// 레이저 이벤트 정의를 바탕으로 발사 대상과 좌표를 계산하고 Core 레이저 시스템을 호출합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">레이저 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        /// <param name="ownerObject">OnHit 부가 효과 조건 확인에 사용할 실행기 GameObject입니다.</param>
        /// <param name="attackSequence">공격 식별자를 발급하고 연계 해제 정책을 저장할 시퀀스입니다.</param>
        public static void Handle(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            GameObject ownerObject,
            SkillAttackSequence attackSequence)
        {
            if (payloadObj is not LaserEventDefinition def)
                return;
            if (ctx.caster == null || attackSequence == null)
                return;

            CharacterBase casterChar = ctx.caster.GetComponent<CharacterBase>();
            if (casterChar == null)
                return;

            if (TableLoaderManager.Instance == null)
                return;

            StruckTableLaser laserInfo = TableLoaderManager.Instance.GetLaserData(def.laserUid, false);
            if (laserInfo == null)
                return;

            Vector3 casterPos = ctx.caster.transform.position;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            ConfigCommonSkill.SkillTargetingMode mode =
                (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            if (def.targetingOverride.enabled)
                mode = def.targetingOverride.mode;

            CharacterBase targetChar = ctx.lockedTarget != null
                ? ctx.lockedTarget.GetComponent<CharacterBase>()
                : null;

            ResolveLaserTarget(
                skill,
                ctx,
                def,
                mode,
                casterChar,
                casterPos,
                targetPos,
                groundPoint,
                ref targetChar,
                out bool usePosOverride,
                out Vector2 posOverride);

            if (TryResolveLaserTargetPointOverride(def, targetChar, targetPos, out Vector2 fixedTargetPoint))
            {
                usePosOverride = true;
                posOverride = fixedTargetPoint;
                targetChar = null;
            }

            LaserConstants.StartPositionOverrideMode resolvedStartPositionOverrideMode = def.startPositionOverrideMode;
            Vector2 resolvedStartPositionOverride = def.startPositionOverride;
            LaserConstants.StartPointUpdateMode resolvedStartPointUpdateMode = def.startPointUpdateMode;

            if (def.startAnchor != LaserStartAnchor.Caster)
            {
                if (!TryResolveLaserStartAnchorPosition(run, def, casterPos, targetPos, groundPoint, out Vector3 startAnchorPosition))
                    return;

                resolvedStartPositionOverrideMode = LaserConstants.StartPositionOverrideMode.WorldPosition;
                resolvedStartPositionOverride = ResolveLaserStartPointByAnchor(laserInfo, def, startAnchorPosition);
                resolvedStartPointUpdateMode = LaserConstants.StartPointUpdateMode.SnapshotAtLaunch;
            }

            int attackId = attackSequence.Allocate(def.allowSkillChainOnConfirmedDamage);
            var meta = new MetadataLaser(
                uid: def.laserUid,
                damageType: def.damageType,
                damage: def.damage,
                target: targetChar,
                owner: casterChar,
                scaleMultiplier: def.scaleMultiplier,
                visualType: def.visualType,
                visualSprite: def.visualSprite,
                visualAnimatorController: def.visualAnimatorController,
                visualVfxUidOverride: def.visualVfxUidOverride,
                useTargetPositionOverride: usePosOverride,
                targetPositionOverride: posOverride,
                skillUid: skill.Uid,
                attackId: attackId,
                allowSkillChainOnConfirmedDamage: def.allowSkillChainOnConfirmedDamage,
                elementGaugeApplications: SkillOnHitEffectUtility.BuildElementGaugeApplications(
                    def.onHitElementGauges,
                    ownerObject,
                    damageApplied: true),
                useDurationOverride: true,
                durationOverride: Mathf.Max(0f, def.durationSeconds),
                useDamageTimingOverride: true,
                damageStartDelayOverride: Mathf.Max(0f, def.damageStartDelaySeconds),
                damageActiveDurationOverride: NormalizeLaserDamageActiveDuration(def.damageActiveDurationSeconds),
                damageTickIntervalOverride: Mathf.Max(0f, def.damageTickIntervalSeconds),
                damageTickOnStartOverride: def.damageTickOnStart,
                useMaxDistanceOverride: def.maxDistance > 0f,
                maxDistanceOverride: Mathf.Max(0f, def.maxDistance),
                updateAimContinuously: def.updateAimContinuously,
                useRaycastDirectionModeOverride: def.useRaycastDirectionModeOverride,
                raycastDirectionModeOverride: def.raycastDirectionModeOverride,
                useRaycastAngleOverride: def.useRaycastAngleOverride,
                raycastAngleOverrideDeg: def.raycastAngleOverrideDeg,
                useVfxAngleSyncModeOverride: def.useVfxAngleSyncModeOverride,
                vfxAngleSyncModeOverride: def.vfxAngleSyncModeOverride,
                startPositionOverrideMode: resolvedStartPositionOverrideMode,
                startPositionOverride: resolvedStartPositionOverride,
                startPointUpdateMode: resolvedStartPointUpdateMode);

            RegisterLaserDebugGizmo(
                casterChar,
                targetChar,
                usePosOverride,
                posOverride,
                ctx.forward,
                laserInfo,
                def,
                meta);

            casterChar.LaunchLaser(meta);
        }

        /// <summary>
        /// 스킬 타겟팅 모드와 이벤트 오버라이드 설정을 기준으로 레이저가 사용할 타겟 참조 또는 좌표 오버라이드를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="mode">최종 적용할 스킬 타겟팅 모드입니다.</param>
        /// <param name="casterChar">레이저를 발사하는 캐스터 캐릭터입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        /// <param name="targetChar">좌표 오버라이드가 필요 없을 때 사용할 타겟 캐릭터 참조입니다.</param>
        /// <param name="usePosOverride">좌표 오버라이드를 사용할지 여부입니다.</param>
        /// <param name="posOverride">레이저가 사용할 좌표 오버라이드입니다.</param>
        private static void ResolveLaserTarget(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            LaserEventDefinition def,
            ConfigCommonSkill.SkillTargetingMode mode,
            CharacterBase casterChar,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            ref CharacterBase targetChar,
            out bool usePosOverride,
            out Vector2 posOverride)
        {
            usePosOverride = false;
            posOverride = default;

            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    usePosOverride = true;
                    posOverride = new Vector2(groundPoint.x, groundPoint.y);
                    break;

                case ConfigCommonSkill.SkillTargetingMode.Self:
                    targetChar = casterChar;
                    usePosOverride = false;
                    break;

                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                    if (targetChar != null)
                    {
                        usePosOverride = false;
                    }
                    else
                    {
                        usePosOverride = true;
                        posOverride = new Vector2(targetPos.x, targetPos.y);
                    }
                    break;

                default:
                    Vector3 fwd = ctx.forward.sqrMagnitude < 1e-6f ? Vector3.right : ctx.forward.normalized;
                    float range = SkillRangeResolver.GetPlacementRange(skill);
                    if (def.targetingOverride.enabled && def.targetingOverride.rangeOverride > 0f)
                        range = def.targetingOverride.rangeOverride;

                    Vector3 p = SkillRangeResolver.ResolveForwardPlacementPosition(casterPos, fwd, range);
                    usePosOverride = true;
                    posOverride = new Vector2(p.x, p.y);
                    break;
            }
        }

        /// <summary>
        /// 레이저 데미지 활성 지속 시간을 Core 레이저 시스템에 전달할 값으로 보정합니다.
        /// </summary>
        /// <param name="value">스킬 이벤트 정의에 저장된 데미지 활성 지속 시간입니다.</param>
        /// <returns>0 이하이면 레이저 종료까지 유지하는 의미의 -1, 양수이면 해당 값을 반환합니다.</returns>
        private static float NormalizeLaserDamageActiveDuration(float value)
        {
            return value <= 0f ? -1f : value;
        }

        /// <summary>
        /// 레이저 목표점 고정 정책을 해석하여 좌표 오버라이드 값을 계산합니다.
        /// </summary>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="targetChar">현재 고정 타겟 캐릭터입니다.</param>
        /// <param name="targetPos">현재 해석된 타겟 중심 좌표입니다.</param>
        /// <param name="targetPointOverride">계산된 목표점 오버라이드입니다.</param>
        /// <returns>고정 정책이 활성화되어 좌표를 계산했으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveLaserTargetPointOverride(
            LaserEventDefinition def,
            CharacterBase targetChar,
            Vector3 targetPos,
            out Vector2 targetPointOverride)
        {
            targetPointOverride = default;
            if (def == null || targetChar == null)
                return false;

            switch (def.targetPointPolicy)
            {
                case LaserTargetPointPolicy.FixedOffsetFromTargetCenter:
                    targetPointOverride = (Vector2)targetPos + def.fixedTargetOffset;
                    return true;

                case LaserTargetPointPolicy.FixedNormalizedPointInTargetHitArea:
                    if (TryResolveTargetHitAreaNormalizedPoint(targetChar, def.fixedTargetHitAreaNormalized, out targetPointOverride))
                        return true;

                    targetPointOverride = (Vector2)targetPos + def.fixedTargetOffset;
                    return true;

                case LaserTargetPointPolicy.UseDefaultTargeting:
                default:
                    return false;
            }
        }

        /// <summary>
        /// 타겟 HitArea 정규화 좌표(0~1)를 월드 좌표로 변환합니다.
        /// </summary>
        /// <param name="targetChar">좌표를 계산할 타겟 캐릭터입니다.</param>
        /// <param name="normalizedPoint">HitArea 정규화 좌표입니다. (0,0)=좌하단, (1,1)=우상단입니다.</param>
        /// <param name="worldPoint">변환된 월드 좌표입니다.</param>
        /// <returns>HitArea 좌표 계산에 성공했으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveTargetHitAreaNormalizedPoint(
            CharacterBase targetChar,
            Vector2 normalizedPoint,
            out Vector2 worldPoint)
        {
            worldPoint = default;
            if (targetChar == null || targetChar.colliderHitArea == null)
                return false;

            CapsuleCollider2D hitArea = targetChar.colliderHitArea;
            Vector2 clamped = new Vector2(
                Mathf.Clamp01(normalizedPoint.x),
                Mathf.Clamp01(normalizedPoint.y));

            float halfWidth = hitArea.size.x * 0.5f;
            float halfHeight = hitArea.size.y * 0.5f;
            float minLocalX = hitArea.offset.x - halfWidth;
            float maxLocalX = hitArea.offset.x + halfWidth;
            float minLocalY = hitArea.offset.y - halfHeight;
            float maxLocalY = hitArea.offset.y + halfHeight;

            Vector3 localPoint = new Vector3(
                Mathf.Lerp(minLocalX, maxLocalX, clamped.x),
                Mathf.Lerp(minLocalY, maxLocalY, clamped.y),
                0f);

            worldPoint = hitArea.transform.TransformPoint(localPoint);
            return true;
        }

        /// <summary>
        /// Skill 테스트 허브에 레이저 예상 범위 기즈모를 등록합니다.
        /// 실제 LaserBeam과 동일한 조준 정책을 사용하여 Raycast 선분과 시각 회전 가이드를 함께 기록합니다.
        /// </summary>
        /// <param name="casterChar">캐스터 캐릭터입니다.</param>
        /// <param name="targetChar">고정 타겟 캐릭터입니다.</param>
        /// <param name="usePosOverride">좌표 오버라이드 사용 여부입니다.</param>
        /// <param name="posOverride">좌표 오버라이드 값입니다.</param>
        /// <param name="forward">캐스터 전방 방향입니다.</param>
        /// <param name="laserInfo">레이저 테이블 정보입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="meta">실제 런타임 발사에 사용할 레이저 메타데이터입니다.</param>
        private static void RegisterLaserDebugGizmo(
            CharacterBase casterChar,
            CharacterBase targetChar,
            bool usePosOverride,
            Vector2 posOverride,
            Vector3 forward,
            StruckTableLaser laserInfo,
            LaserEventDefinition def,
            MetadataLaser meta)
        {
            if (casterChar == null || SkillTestRuntimeHub.Instance == null || laserInfo == null)
                return;

            Vector3 start = LaserStartPointResolver.ResolveCurrentStartPoint(
                laserInfo,
                meta,
                casterChar.transform.position);
            Vector2 direction = ResolveLaserPreviewDirection(casterChar, targetChar, usePosOverride, posOverride, forward, laserInfo, meta, start);
            float maxDistance = def.maxDistance > 0f ? def.maxDistance : Mathf.Max(0f, laserInfo.MaxDistance);
            if (maxDistance <= 0f)
                return;

            Vector2 visualDirection = ResolveLaserPreviewVisualDirection(laserInfo, meta, direction);
            LaserConstants.VfxAngleSyncMode vfxAngleSyncMode = LaserAimPolicyUtility.ResolveVfxAngleSyncMode(laserInfo, meta);
            Vector3 end = start + (Vector3)(direction * maxDistance);
            bool hasBlockHit = TryResolveLaserPreviewEnd(casterChar, laserInfo, start, direction, maxDistance, out Vector3 blockedEnd, out Vector3 blockPoint);
            if (hasBlockHit)
                end = blockedEnd;

            float duration = def.durationSeconds > 0f
                ? def.durationSeconds
                : SkillTestRuntimeHub.CurrentSettings != null ? SkillTestRuntimeHub.CurrentSettings.defaultLaserGizmoDuration : 0.2f;

            SkillTestRuntimeHub.Instance.RegisterLaser(
                start,
                end,
                duration,
                casterChar.gameObject,
                hasBlockHit,
                blockPoint,
                direction,
                visualDirection,
                vfxAngleSyncMode);
        }

        /// <summary>
        /// 레이저 프리뷰용 Raycast 방향 벡터를 계산합니다.
        /// 실제 LaserBeam과 동일하게 RaycastDirectionMode, RaycastAngleDeg, 타겟/좌표 오버라이드 우선순위를 따릅니다.
        /// </summary>
        /// <param name="casterChar">캐스터 캐릭터입니다.</param>
        /// <param name="targetChar">고정 타겟 캐릭터입니다.</param>
        /// <param name="usePosOverride">좌표 오버라이드 사용 여부입니다.</param>
        /// <param name="posOverride">좌표 오버라이드 값입니다.</param>
        /// <param name="forward">캐스터 전방 방향입니다.</param>
        /// <param name="laserInfo">레이저 테이블 정보입니다.</param>
        /// <param name="meta">실제 런타임 발사에 사용할 레이저 메타데이터입니다.</param>
        /// <param name="start">프리뷰 시작점입니다.</param>
        /// <returns>정책이 반영된 정규화 Raycast 방향입니다.</returns>
        private static Vector2 ResolveLaserPreviewDirection(
            CharacterBase casterChar,
            CharacterBase targetChar,
            bool usePosOverride,
            Vector2 posOverride,
            Vector3 forward,
            StruckTableLaser laserInfo,
            MetadataLaser meta,
            Vector3 start)
        {
            Vector2 fallbackTargetPoint = default;
            bool hasFallbackTargetPoint = false;

            if (targetChar == null && usePosOverride)
            {
                fallbackTargetPoint = posOverride;
                hasFallbackTargetPoint = true;
            }
            else if (targetChar == null && forward.sqrMagnitude > 1e-6f)
            {
                Vector2 normalizedForward = new Vector2(forward.x, forward.y).normalized;
                fallbackTargetPoint = (Vector2)start + normalizedForward;
                hasFallbackTargetPoint = true;
            }

            return LaserAimPolicyUtility.ResolveRaycastDirection(
                laserInfo,
                meta,
                casterChar,
                targetChar,
                hasFallbackTargetPoint,
                fallbackTargetPoint,
                start,
                true);
        }

        /// <summary>
        /// 레이저 프리뷰용 시각 회전 가이드 방향을 계산합니다.
        /// FollowRaycast/LockAtLaunch는 현재 Raycast 방향을 사용하고, None은 월드 +X 축을 가이드로 사용합니다.
        /// </summary>
        /// <param name="laserInfo">레이저 테이블 정보입니다.</param>
        /// <param name="meta">실제 런타임 발사에 사용할 레이저 메타데이터입니다.</param>
        /// <param name="raycastDirection">프리뷰 시점의 Raycast 방향입니다.</param>
        /// <returns>프리뷰용 시각 회전 가이드 방향입니다.</returns>
        private static Vector2 ResolveLaserPreviewVisualDirection(
            StruckTableLaser laserInfo,
            MetadataLaser meta,
            Vector2 raycastDirection)
        {
            return LaserAimPolicyUtility.ResolvePreviewVisualDirection(laserInfo, meta, raycastDirection);
        }

        /// <summary>
        /// 레이저 시작점 기준 앵커를 해석하여 월드 위치를 계산합니다.
        /// startAnchor가 Caster가 아니면 Skill 계층에서 먼저 월드 위치를 확정한 뒤 Core 레이저에 전달합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="casterPos">해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">해석된 지면 기준점입니다.</param>
        /// <param name="anchorPosition">계산된 기준 앵커의 월드 위치입니다.</param>
        /// <returns>기준 앵커 해석에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveLaserStartAnchorPosition(
            SkillRun run,
            LaserEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            out Vector3 anchorPosition)
        {
            anchorPosition = casterPos;
            if (def == null)
                return false;

            switch (def.startAnchor)
            {
                case LaserStartAnchor.Target:
                    anchorPosition = targetPos;
                    return true;
                case LaserStartAnchor.Ground:
                    anchorPosition = groundPoint;
                    return true;
                case LaserStartAnchor.NamedPositionAnchor:
                    if (TryResolveNamedAnchorPosition(run, def.namedAnchorKey, out anchorPosition))
                        return true;

                    Debug.LogWarning($"[SkillExecutor] Laser named start anchor not found. key={def.namedAnchorKey}");
                    return false;
                case LaserStartAnchor.Caster:
                default:
                    anchorPosition = casterPos;
                    return true;
            }
        }

        /// <summary>
        /// 기준 앵커 위치와 레이저 시작점 오버라이드 정책을 조합하여 최종 월드 시작점을 계산합니다.
        /// startAnchor가 Caster가 아닌 경우 Core 레이저에는 이 계산 결과를 WorldPosition으로 전달합니다.
        /// </summary>
        /// <param name="laserInfo">레이저 테이블 정보입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="anchorPosition">해석된 기준 앵커의 월드 위치입니다.</param>
        /// <returns>기준 앵커와 오버라이드 정책이 반영된 최종 월드 시작점입니다.</returns>
        private static Vector2 ResolveLaserStartPointByAnchor(
            StruckTableLaser laserInfo,
            LaserEventDefinition def,
            Vector3 anchorPosition)
        {
            Vector2 anchorPosition2D = anchorPosition;
            Vector2 tableOffset = laserInfo != null ? laserInfo.StartPosition : Vector2.zero;
            if (def == null)
                return anchorPosition2D + tableOffset;

            switch (def.startPositionOverrideMode)
            {
                case LaserConstants.StartPositionOverrideMode.ReplaceTableOffset:
                    return anchorPosition2D + def.startPositionOverride;
                case LaserConstants.StartPositionOverrideMode.AddToTableOffset:
                    return anchorPosition2D + tableOffset + def.startPositionOverride;
                case LaserConstants.StartPositionOverrideMode.WorldPosition:
                    return def.startPositionOverride;
                case LaserConstants.StartPositionOverrideMode.UseLaserTable:
                default:
                    return anchorPosition2D + tableOffset;
            }
        }

        /// <summary>
        /// 같은 스킬 런에 저장된 이름 있는 위치 앵커를 조회합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="anchorKey">조회할 앵커 키입니다.</param>
        /// <param name="position">조회된 위치입니다.</param>
        /// <returns>앵커 조회에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveNamedAnchorPosition(SkillRun run, string anchorKey, out Vector3 position)
        {
            position = Vector3.zero;
            if (run == null || string.IsNullOrWhiteSpace(anchorKey))
                return false;

            if (!run.TryGetPositionAnchor(anchorKey, out SkillPositionAnchorSnapshot snapshot))
                return false;

            position = snapshot.Position;
            return true;
        }

        /// <summary>
        /// 레이저 정책에 맞춰 프리뷰 종료점을 계산합니다.
        /// </summary>
        /// <param name="casterChar">레이저를 발사하는 캐스터 캐릭터입니다.</param>
        /// <param name="laserInfo">레이저 테이블 정보입니다.</param>
        /// <param name="start">프리뷰 시작점입니다.</param>
        /// <param name="direction">프리뷰 Raycast 방향입니다.</param>
        /// <param name="maxDistance">프리뷰 최대 거리입니다.</param>
        /// <param name="end">계산된 프리뷰 종료점입니다.</param>
        /// <param name="blockPoint">차단 지점입니다.</param>
        /// <returns>차단 지점을 찾았으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveLaserPreviewEnd(
            CharacterBase casterChar,
            StruckTableLaser laserInfo,
            Vector2 start,
            Vector2 direction,
            float maxDistance,
            out Vector3 end,
            out Vector3 blockPoint)
        {
            end = start + direction * maxDistance;
            blockPoint = Vector3.zero;

            int layerMask = Physics2D.GetLayerCollisionMask(casterChar.gameObject.layer);
            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true,
            };
            filter.SetLayerMask(layerMask);

            RaycastHit2D[] hits = new RaycastHit2D[32];
            int count = Physics2D.Raycast(start, direction, filter, hits, maxDistance);
            if (count <= 0)
                return false;

            bool hasNearestGround = false;
            RaycastHit2D nearestGround = default;
            bool hasNearestHostile = false;
            RaycastHit2D nearestHostile = default;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                Collider2D col = hit.collider;
                if (!col)
                    continue;

                CharacterBase hitCharacter = CombatHitTargetUtility.ResolveTargetCharacter(col);
                if (hitCharacter != null && hitCharacter == casterChar)
                    continue;

                bool isGround = col.CompareTag(ConfigTags.GetValue(ConfigTags.Keys.MapGround));
                if (isGround)
                {
                    if (!hasNearestGround || hit.distance < nearestGround.distance)
                    {
                        hasNearestGround = true;
                        nearestGround = hit;
                    }

                    continue;
                }

                if (!CombatHitTargetUtility.TryResolveHostileTarget(casterChar, col, out _))
                    continue;

                if (!hasNearestHostile || hit.distance < nearestHostile.distance)
                {
                    hasNearestHostile = true;
                    nearestHostile = hit;
                }
            }

            switch (laserInfo.BlockMode)
            {
                case LaserConstants.BlockMode.StopAtGround:
                    if (hasNearestGround)
                    {
                        blockPoint = nearestGround.point != Vector2.zero
                            ? (Vector3)nearestGround.point
                            : (Vector3)(start + direction * nearestGround.distance);
                        end = blockPoint;
                        return true;
                    }
                    break;

                case LaserConstants.BlockMode.StopAtHostile:
                    if (laserInfo.HitMode == LaserConstants.HitMode.FirstHitOnly && hasNearestHostile)
                    {
                        blockPoint = nearestHostile.point != Vector2.zero
                            ? (Vector3)nearestHostile.point
                            : (Vector3)(start + direction * nearestHostile.distance);
                        end = blockPoint;
                        return true;
                    }
                    break;

                case LaserConstants.BlockMode.StopAtGroundOrHostile:
                    if (laserInfo.HitMode == LaserConstants.HitMode.FirstHitOnly &&
                        hasNearestHostile &&
                        (!hasNearestGround || nearestHostile.distance <= nearestGround.distance))
                    {
                        blockPoint = nearestHostile.point != Vector2.zero
                            ? (Vector3)nearestHostile.point
                            : (Vector3)(start + direction * nearestHostile.distance);
                        end = blockPoint;
                        return true;
                    }

                    if (hasNearestGround)
                    {
                        blockPoint = nearestGround.point != Vector2.zero
                            ? (Vector3)nearestGround.point
                            : (Vector3)(start + direction * nearestGround.distance);
                        end = blockPoint;
                        return true;
                    }
                    break;
            }

            return false;
        }
    }
}
