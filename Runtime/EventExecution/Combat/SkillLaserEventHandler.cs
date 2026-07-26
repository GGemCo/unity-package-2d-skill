using Config;
using System.Collections.Generic;
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
        /// <param name="runner">더미 Actor 해석 시 캐스터 임시 핸들의 코루틴 정리에 사용할 실행기입니다.</param>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">레이저 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        /// <param name="dummyActors">현재 스킬 실행에서 actorKey 기준으로 등록된 더미 Actor 레지스트리입니다.</param>
        /// <param name="casterActorHandle">Caster 참조를 더미 Actor처럼 다룰 때 사용하는 임시 핸들입니다.</param>
        /// <param name="ownerObject">OnHit 부가 효과 조건 확인에 사용할 실행기 GameObject입니다.</param>
        /// <param name="attackSequence">공격 식별자를 발급하고 연계 해제 정책을 저장할 시퀀스입니다.</param>
        public static void Handle(
            MonoBehaviour runner,
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            Dictionary<string, SkillDummyActorHandle> dummyActors,
            SkillDummyActorHandle casterActorHandle,
            GameObject ownerObject,
            SkillAttackSequence attackSequence)
        {
            if (payloadObj is not LaserEventDefinition def)
                return;
            if (attackSequence == null)
                return;

            if (!TryResolveLaserActor(runner, ctx, def, dummyActors, casterActorHandle, out GameObject sourceObject, out CharacterBase sourceChar))
                return;

            if (TableLoaderManager.Instance == null)
                return;

            StruckTableLaser laserInfo = TableLoaderManager.Instance.GetLaserData(def.laserUid, false);
            if (laserInfo == null)
                return;

            CharacterBase combatOwner = ResolveLaserCombatOwner(ctx, sourceChar);
            Vector3 casterPos = sourceObject.transform.position;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;
            Vector3 sourceForward = ResolveLaserSourceForward(sourceObject, ctx);

            if (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter)
            {
                // 더미 Actor가 발사 주체이면 캐스터 스냅샷 대신 더미의 현재 위치를 유지해야 시작점이 원본 캐스터로 되돌아가지 않습니다.
                casterPos = sourceObject == ctx.caster ? snapshotCasterPos : sourceObject.transform.position;
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
                sourceForward,
                sourceChar,
                casterPos,
                targetPos,
                groundPoint,
                ref targetChar,
                out bool usePosOverride,
                out Vector2 posOverride);

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
            else if (!ReferenceEquals(sourceChar, combatOwner))
            {
                // 더미 Actor는 발사 위치를 대표하고, 원 캐스터는 적대 관계와 데미지 계산을 대표합니다.
                resolvedStartPositionOverrideMode = LaserConstants.StartPositionOverrideMode.WorldPosition;
                resolvedStartPositionOverride = ResolveLaserStartPointFromActor(laserInfo, def, sourceChar);
                resolvedStartPointUpdateMode = LaserConstants.StartPointUpdateMode.SnapshotAtLaunch;
            }

            DamageFormulaRuntimeContext damageFormulaContext = BuildLaserDamageFormulaContext(
                skill,
                def,
                ctx.executionOptions,
                combatOwner);

            int attackId = attackSequence.Allocate(def.allowSkillChainOnConfirmedDamage);
            var meta = new MetadataLaser(
                uid: def.laserUid,
                damageType: ResolveLaserDamageType(skill, def),
                damage: ResolveLaserDamage(skill, def, ctx.executionOptions, combatOwner, targetChar),
                damageFormulaContext: damageFormulaContext,
                target: targetChar,
                owner: combatOwner,
                scaleMultiplier: def.scaleMultiplier,
                visualType: def.visualType,
                visualSprite: def.visualSprite,
                visualAnimatorController: def.visualAnimatorController,
                visualVfxUidOverride: def.visualVfxUidOverride,
                attachedVfxPlaybackPolicy: def.attachedVfxPlaybackPolicy,
                useTargetPositionOverride: usePosOverride,
                targetPositionOverride: posOverride,
                skillUid: skill.Uid,
                attackId: attackId,
                allowSkillChainOnConfirmedDamage: def.allowSkillChainOnConfirmedDamage,
                skillHitMpGain: ResolveSkillHitMpGain(def.skillHitMpGain, ctx.executionOptions),
                allowMultipleSkillHitMpGainPerAttack: def.allowMultipleSkillHitMpGainPerAttack,
                onHitCrowdControls: BuildLaserOnHitCrowdControls(def.onHitCrowdControls),
                guardAttackType: def.guardAttackType,
                useDurationOverride: true,
                durationOverride: Mathf.Max(0f, def.durationSeconds),
                useDamageTimingOverride: true,
                damageStartDelayOverride: Mathf.Max(0f, def.damageStartDelaySeconds),
                damageActiveDurationOverride: NormalizeLaserDamageActiveDuration(def.damageActiveDurationSeconds),
                damageTickIntervalOverride: Mathf.Max(0f, def.damageTickIntervalSeconds),
                damageTickOnStartOverride: def.damageTickOnStart,
                useMaxDistanceOverride: def.maxDistance > 0f,
                maxDistanceOverride: Mathf.Max(0f, def.maxDistance),
                // Skill Laser는 타겟의 높이를 무시하고 좌우 수평 방향을 기준으로 각도만 적용합니다.
                useRaycastDirectionModeOverride: true,
                raycastDirectionModeOverride: LaserConstants.RaycastDirectionMode.TowardTargetHorizontal,
                useRaycastAngleOverride: true,
                raycastAngleOverrideDeg: def.targetHorizontalAngleDeg,
                startPositionOverrideMode: resolvedStartPositionOverrideMode,
                startPositionOverride: resolvedStartPositionOverride,
                startPointUpdateMode: resolvedStartPointUpdateMode,
                useCasterFlipStartOffsetX: def.startAnchor == LaserStartAnchor.Caster && def.useCasterFlipStartOffsetX);
#if UNITY_EDITOR
            RegisterLaserDebugGizmo(
                sourceChar,
                targetChar,
                usePosOverride,
                posOverride,
                sourceForward,
                laserInfo,
                def,
                meta);
#endif
            sourceChar.LaunchLaser(meta);
        }

        /// <summary>
        /// 레이저 발사 주체 기준의 기본 전방 벡터를 계산합니다.
        /// </summary>
        /// <param name="sourceObject">레이저를 발사할 실제 캐릭터 오브젝트입니다.</param>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <returns>레이저 조준 실패 시 사용할 전방 벡터입니다.</returns>
        private static Vector3 ResolveLaserSourceForward(GameObject sourceObject, SkillTargetContext ctx)
        {
            if (sourceObject == ctx.caster)
                return ctx.forward;

            Vector2 currentFacing = SkillDirectionResolver.ResolveCurrentFacing2D(sourceObject);
            return currentFacing.sqrMagnitude > 1e-6f ? (Vector3)currentFacing : Vector3.right;
        }

        /// <summary>
        /// 이벤트 기본 스킬 타격 MP 획득량과 실행 옵션 보너스를 합산합니다.
        /// </summary>
        /// <param name="baseMpGain">이벤트에 설정된 기본 MP 획득량입니다.</param>
        /// <param name="options">이번 스킬 실행에 적용되는 실행 옵션입니다.</param>
        /// <returns>최종 스킬 타격 MP 획득량입니다.</returns>
        private static int ResolveSkillHitMpGain(int baseMpGain, in SkillExecutionOptions options)
        {
            return Mathf.Max(0, baseMpGain) + Mathf.Max(0, options.SkillHitMpGainBonus);
        }

        /// <summary>
        /// 레이저 데미지, 적대 관계, 데미지 공식 계산에 사용할 전투 owner를 해석합니다.
        /// </summary>
        /// <remarks>
        /// 더미 Actor는 레이저의 시작 위치와 방향을 대신 표현하는 주체이고,
        /// 실제 전투 판정은 스킬을 사용한 원 캐스터 기준으로 유지해야 합니다.
        /// </remarks>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="fallbackOwner">원 캐스터를 찾지 못했을 때 사용할 발사 Actor입니다.</param>
        /// <returns>전투 판정에 사용할 캐릭터입니다.</returns>
        private static CharacterBase ResolveLaserCombatOwner(SkillTargetContext ctx, CharacterBase fallbackOwner)
        {
            CharacterBase caster = SkillCharacterComponentResolver.ResolveCharacterBase(ctx.caster);
            return caster != null ? caster : fallbackOwner;
        }

        /// <summary>
        /// 레이저를 실제로 발사할 캐릭터를 Caster 또는 더미 Actor 설정에서 해석합니다.
        /// </summary>
        /// <param name="runner">Caster 참조 임시 핸들을 정리할 실행기입니다.</param>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="dummyActors">현재 스킬 실행에서 생성된 더미 Actor 레지스트리입니다.</param>
        /// <param name="casterActorHandle">Caster를 Actor 핸들처럼 다루기 위한 임시 핸들입니다.</param>
        /// <param name="sourceObject">해석된 레이저 발사 오브젝트입니다.</param>
        /// <param name="sourceChar">해석된 레이저 발사 캐릭터입니다.</param>
        /// <returns>레이저를 발사할 캐릭터를 찾았으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveLaserActor(
            MonoBehaviour runner,
            SkillTargetContext ctx,
            LaserEventDefinition def,
            Dictionary<string, SkillDummyActorHandle> dummyActors,
            SkillDummyActorHandle casterActorHandle,
            out GameObject sourceObject,
            out CharacterBase sourceChar)
        {
            sourceObject = null;
            sourceChar = null;

            // 더미 이벤트와 같은 Actor 참조 유틸리티를 사용해 Caster/Actor 선택 정책과 경고 정책을 공유합니다.
            if (!SkillDummyActorReferenceUtility.TryResolveActorHandle(
                    runner,
                    dummyActors,
                    casterActorHandle,
                    ctx,
                    def.actorReferenceType,
                    def.actorKey,
                    def.missingActorPolicy,
                    out SkillDummyActorHandle handle))
            {
                return false;
            }

            sourceChar = handle.Character;
            sourceObject = sourceChar != null ? sourceChar.gameObject : null;
            return sourceObject != null;
        }

        /// <summary>
        /// 스킬 타겟팅 모드와 이벤트 오버라이드 설정을 기준으로 레이저가 사용할 타겟 참조 또는 좌표 오버라이드를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="mode">최종 적용할 스킬 타겟팅 모드입니다.</param>
        /// <param name="sourceForward">레이저 발사 주체 기준의 기본 전방 벡터입니다.</param>
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
            Vector3 sourceForward,
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
                    Vector3 fwd = sourceForward.sqrMagnitude < 1e-6f ? Vector3.right : sourceForward.normalized;
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
        /// 레이저 이벤트의 피해 타입을 계산합니다.
        /// </summary>
        /// <remarks>
        /// Laser Clip의 DamageType이 None이면 skill/skill_monster 테이블의 DamageType을 사용합니다.
        /// </remarks>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <returns>레이저 메타데이터에 전달할 피해 타입입니다.</returns>
        private static ConfigCommon.DamageType ResolveLaserDamageType(
            RuntimeSkillDefinition skill,
            LaserEventDefinition def)
        {
            if (def != null && def.damageType != ConfigCommon.DamageType.None)
            {
                return def.damageType;
            }

            return skill != null ? skill.DamageType : ConfigCommon.DamageType.None;
        }

        /// <summary>
        /// 레이저가 실제 대상에 적중한 시점에 데미지를 다시 계산하기 위한 공식 입력 스냅샷을 생성합니다.
        /// </summary>
        /// <remarks>
        /// 레이저는 Raycast 결과에 따라 발사 시점의 락온 대상과 실제 피격 대상이 달라질 수 있으므로,
        /// 발사 시점에는 공식 계산 재료만 저장하고 Core 레이저 적중 처리에서 실제 target 기준으로 계산합니다.
        /// </remarks>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="options">이번 스킬 실행에 적용된 옵션 스냅샷입니다.</param>
        /// <param name="caster">공격력 기반 데미지 계산에 사용할 캐스터 캐릭터입니다.</param>
        /// <returns>Core 레이저 시스템에 전달할 공식 입력 스냅샷입니다.</returns>
        private static DamageFormulaRuntimeContext BuildLaserDamageFormulaContext(
            RuntimeSkillDefinition skill,
            LaserEventDefinition def,
            in SkillExecutionOptions options,
            CharacterBase caster)
        {
            bool useDamageFormula = skill != null && !string.IsNullOrWhiteSpace(skill.DamageFormulaKey);
            double baseDamage = ResolveBaseLaserDamage(skill, def, caster, useDamageFormula);
            double skillDamageRate = ResolveLaserDamageRate(skill, def);
            float eventMultiplier = def != null ? Mathf.Max(0f, def.multiplier) : 1f;
            float optionMultiplier = options.DamageMultiplier > 0f ? options.DamageMultiplier : 1f;

            return new DamageFormulaRuntimeContext(
                skill != null ? skill.DamageFormulaKey : string.Empty,
                baseDamage,
                skillDamageRate,
                eventMultiplier,
                optionMultiplier,
                0d,
                ResolveLaserDamageType(skill, def),
                false);
        }

        /// <summary>
        /// 레이저 이벤트의 오버라이드 피해량, 스킬 테이블 기본 피해량, 이벤트 배율을 반영한 최종 피해량을 계산합니다.
        /// </summary>
        /// <remarks>
        /// Laser Clip의 Damage가 0 이하이면 skill/skill_monster 테이블의 Damage와 DamageValueType을 사용합니다.
        /// Damage가 0보다 크면 Laser Clip의 Damage와 DamageValueType으로 테이블 값을 덮어씁니다.
        /// </remarks>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="options">이번 스킬 실행에 적용된 옵션 스냅샷입니다.</param>
        /// <param name="caster">공격력 기반 데미지 계산에 사용할 캐스터 캐릭터입니다.</param>
        /// <param name="target">레벨 차이 배율 계산에 사용할 공격 대상 캐릭터입니다.</param>
        /// <returns>레이저 메타데이터에 전달할 피해량입니다.</returns>
        private static long ResolveLaserDamage(
            RuntimeSkillDefinition skill,
            LaserEventDefinition def,
            in SkillExecutionOptions options,
            CharacterBase caster,
            CharacterBase target)
        {
            bool useDamageFormula = skill != null && !string.IsNullOrWhiteSpace(skill.DamageFormulaKey);
            double baseDamage = ResolveBaseLaserDamage(skill, def, caster, useDamageFormula);
            double skillDamageRate = ResolveLaserDamageRate(skill, def);
            float eventMultiplier = def != null ? Mathf.Max(0f, def.multiplier) : 1f;
            float optionMultiplier = options.DamageMultiplier > 0f ? options.DamageMultiplier : 1f;

            CalculateManager calculateManager = CalculateManager.GetActive();
            if (calculateManager != null)
            {
                var request = new DamageFormulaRequest(
                    caster,
                    target,
                    skill != null ? skill.DamageFormulaKey : string.Empty,
                    baseDamage,
                    skillDamageRate,
                    eventMultiplier,
                    optionMultiplier,
                    0d,
                    ResolveLaserDamageType(skill, def),
                    false);
                return calculateManager.CalculateSkillDamage(request);
            }

            double resolved = System.Math.Max(0d, baseDamage) * skillDamageRate * eventMultiplier * optionMultiplier;
            if (resolved <= 0d)
            {
                return 0L;
            }

            return resolved >= long.MaxValue
                ? long.MaxValue
                : (long)System.Math.Round(resolved);
        }

        /// <summary>
        /// 레이저 피해량 오버라이드 여부에 따라 배율 적용 전 기본 피해량을 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="caster">공격력 기반 데미지 계산에 사용할 캐스터 캐릭터입니다.</param>
        /// <param name="useDamageFormula">Poly 데미지 공식을 사용할지 여부입니다.</param>
        /// <returns>이벤트 배율과 실행 옵션 배율을 적용하기 전의 기본 피해량입니다.</returns>
        private static double ResolveBaseLaserDamage(
            RuntimeSkillDefinition skill,
            LaserEventDefinition def,
            CharacterBase caster,
            bool useDamageFormula)
        {
            if (skill == null && def == null)
            {
                return 0d;
            }

            bool useDamageOverride = def != null && def.damage > 0L;
            long damage = useDamageOverride
                ? def.damage
                : (skill != null ? skill.Damage : 0L);
            if (damage <= 0L)
            {
                return 0d;
            }

            ConfigCommonSkill.SkillDamageValueType damageValueType = useDamageOverride
                ? def.damageValueType
                : (skill != null ? skill.DamageValueType : ConfigCommonSkill.SkillDamageValueType.Fixed);

            switch (damageValueType)
            {
                case ConfigCommonSkill.SkillDamageValueType.AttackPercent:
                    if (caster == null)
                    {
                        return 0d;
                    }

                    // 공식 기반 레이저도 BaseDamage에 STAT_ATK를 포함하지 않습니다.
                    // GGemCoPlayerSettings의 Stat Point Atk 설정으로 계산된 STAT_ATK는
                    // CalculateManager가 StatStrength 변수로 공식에 별도 전달합니다.
                    long attack = useDamageFormula
                        ? System.Math.Max(0L, caster.TotalBaseAtk.Value)
                        : System.Math.Max(0L, caster.ResolvedAtk.Value);
                    return attack > 0L ? attack : 0d;

                case ConfigCommonSkill.SkillDamageValueType.Fixed:
                default:
                    return damage;
            }
        }

        /// <summary>
        /// 레이저 피해량 정의의 Damage 값을 데미지 비율로 변환합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <returns>공식과 기본 계산에 사용할 레이저 데미지 비율입니다.</returns>
        private static double ResolveLaserDamageRate(RuntimeSkillDefinition skill, LaserEventDefinition def)
        {
            bool useDamageOverride = def != null && def.damage > 0L;
            ConfigCommonSkill.SkillDamageValueType damageValueType = useDamageOverride
                ? def.damageValueType
                : (skill != null ? skill.DamageValueType : ConfigCommonSkill.SkillDamageValueType.Fixed);
            long damage = useDamageOverride
                ? def.damage
                : (skill != null ? skill.Damage : 0L);

            return damageValueType == ConfigCommonSkill.SkillDamageValueType.AttackPercent
                ? System.Math.Max(0L, damage) / 100d
                : 1d;
        }

        /// <summary>
        /// Skill 전용 OnHit Crowd Control 정의를 Core 레이저 런타임 메타데이터로 변환합니다.
        /// </summary>
        /// <remarks>
        /// Core 패키지가 Skill 타입을 참조하지 않도록, 발사 메타데이터 생성 시점에 독립 DTO로 변환합니다.
        /// </remarks>
        /// <param name="entries">스킬 레이저 이벤트에 설정된 Crowd Control 후보 목록입니다.</param>
        /// <returns>Core 레이저 시스템에서 사용할 Crowd Control 후보 목록입니다.</returns>
        private static ProjectileOnHitCrowdControlEntry[] BuildLaserOnHitCrowdControls(
            OnHitCrowdControlEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return null;
            }

            var result = new ProjectileOnHitCrowdControlEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                OnHitCrowdControlEntry entry = entries[i];
                result[i] = new ProjectileOnHitCrowdControlEntry(
                    entry.crowdControlUid,
                    entry.chance,
                    entry.requireDamageDealt,
                    ConvertOnHitCrowdControlTiming(entry.timing));
            }

            return result;
        }

        /// <summary>
        /// Skill Crowd Control 적용 시점을 Core 레이저 적용 시점으로 변환합니다.
        /// </summary>
        /// <param name="timing">Skill 이벤트에 설정된 적용 시점입니다.</param>
        /// <returns>Core 레이저 메타데이터에서 사용할 적용 시점입니다.</returns>
        private static ProjectileOnHitCrowdControlTiming ConvertOnHitCrowdControlTiming(
            OnHitCrowdControlTiming timing)
        {
            return timing == OnHitCrowdControlTiming.BeforeDamage
                ? ProjectileOnHitCrowdControlTiming.BeforeDamage
                : ProjectileOnHitCrowdControlTiming.AfterDamage;
        }

#if UNITY_EDITOR
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
#endif
        
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
        /// 더미 Actor 기준으로 레이저 시작점을 계산합니다.
        /// </summary>
        /// <remarks>
        /// Core 레이저 메타데이터의 Owner를 원 캐스터로 유지하면 기본 Caster 시작점이 원 캐스터 위치로 돌아가므로,
        /// Skill 레이어에서 더미 기준 시작점을 월드 좌표로 미리 계산해 전달합니다.
        /// </remarks>
        /// <param name="laserInfo">레이저 테이블 정보입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="sourceChar">레이저 시작점 기준으로 사용할 더미 Actor 캐릭터입니다.</param>
        /// <returns>더미 Actor 기준으로 계산된 레이저 시작점 월드 좌표입니다.</returns>
        private static Vector2 ResolveLaserStartPointFromActor(
            StruckTableLaser laserInfo,
            LaserEventDefinition def,
            CharacterBase sourceChar)
        {
            Vector2 sourcePosition = sourceChar != null ? sourceChar.transform.position : Vector2.zero;
            Vector2 tableOffset = laserInfo != null ? laserInfo.StartPosition : Vector2.zero;
            if (def == null)
                return sourcePosition + tableOffset;

            switch (def.startPositionOverrideMode)
            {
                case LaserConstants.StartPositionOverrideMode.ReplaceTableOffset:
                    return sourcePosition + ResolveActorFlipStartOffset(def.startPositionOverride, def, sourceChar);

                case LaserConstants.StartPositionOverrideMode.AddToTableOffset:
                    return sourcePosition + ResolveActorFlipStartOffset(tableOffset + def.startPositionOverride, def, sourceChar);

                case LaserConstants.StartPositionOverrideMode.WorldPosition:
                    return def.startPositionOverride;

                case LaserConstants.StartPositionOverrideMode.UseLaserTable:
                default:
                    return sourcePosition + ResolveActorFlipStartOffset(tableOffset, def, sourceChar);
            }
        }

        /// <summary>
        /// 더미 Actor의 좌우 반전 상태를 기준으로 시작점 오프셋 X 값을 보정합니다.
        /// </summary>
        /// <param name="offset">더미 Actor 기준 오프셋입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="sourceChar">좌우 반전 상태를 확인할 더미 Actor 캐릭터입니다.</param>
        /// <returns>반전 정책이 적용된 오프셋입니다.</returns>
        private static Vector2 ResolveActorFlipStartOffset(
            Vector2 offset,
            LaserEventDefinition def,
            CharacterBase sourceChar)
        {
            if (def == null || !def.useCasterFlipStartOffsetX)
                return offset;

            if (sourceChar == null || !sourceChar.IsFlipped())
                return offset;

            return new Vector2(-offset.x, offset.y);
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
