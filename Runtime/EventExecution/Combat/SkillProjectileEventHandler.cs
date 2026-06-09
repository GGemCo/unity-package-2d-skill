using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 투사체 스킬 이벤트의 대상 좌표 해석과 투사체 발사 메타데이터 생성을 담당합니다.
    /// </summary>
    internal static class SkillProjectileEventHandler
    {
        /// <summary>
        /// 투사체 이벤트 정의를 바탕으로 발사 대상과 좌표를 계산하고 투사체를 생성합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">투사체 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        /// <param name="ownerObject">투사체 부가 효과의 출처로 사용할 실행기 GameObject입니다.</param>
        /// <param name="attackSequence">공격 식별자를 발급하고 연계 해제 정책을 저장할 시퀀스입니다.</param>
        public static void Handle(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            GameObject ownerObject,
            SkillAttackSequence attackSequence)
        {
            if (payloadObj is not ProjectileEventDefinition def)
                return;
            if (ctx.caster == null)
                return;

            CharacterBase casterChar = ctx.caster.GetComponent<CharacterBase>();
            if (casterChar == null)
                return;

            if (TableLoaderManager.Instance == null)
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

            ResolveProjectileTarget(
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

            if (TryResolveProjectileTargetPointOverride(def, targetChar, targetPos, out Vector2 fixedTargetPoint))
            {
                usePosOverride = true;
                posOverride = fixedTargetPoint;
            }

            int attackId = attackSequence.Allocate(def.allowSkillChainOnConfirmedDamage);
            ProjectileDamageFormulaContext damageFormulaContext = BuildProjectileDamageFormulaContext(
                skill,
                def,
                ctx.executionOptions,
                casterChar);

            var meta = new MetadataProjectile(
                uid: def.projectileUid,
                damageType: ResolveProjectileDamageType(skill, def),
                damage: ResolveProjectileDamage(skill, def, ctx.executionOptions, casterChar),
                target: targetChar,
                owner: casterChar,
                speedMultiplier: def.speedMultiplier,
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
                onHitCrowdControls: BuildProjectileOnHitCrowdControls(def.onHitCrowdControls),
                guardAttackType: def.guardAttackType,
                useHitLifetimeModeOverride: def.useProjectileHitBehaviorOverride,
                hitLifetimeModeOverride: def.hitLifetimeMode,
                useDamageApplyModeOverride: def.useProjectileHitBehaviorOverride,
                damageApplyModeOverride: def.damageApplyMode,
                useTickDamageIntervalOverride: def.useProjectileHitBehaviorOverride &&
                                                   def.damageApplyMode == ProjectileConstants.DamageApplyMode.PeriodicOverlap,
                tickDamageIntervalOverride: Mathf.Max(0f, def.tickDamageIntervalSeconds),
                useArrivalPolicyOverride: def.useArrivalPolicyOverride,
                arrivalPolicyOverride: def.arrivalPolicy,
                useEnvironmentHitPolicyOverride: def.useEnvironmentHitPolicyOverride,
                environmentHitPolicyOverride: def.environmentHitPolicy,
                useEnvironmentHitLayerMaskOverride: def.useEnvironmentHitPolicyOverride &&
                                                    !def.useDefaultGroundWallEnvironmentLayers,
                environmentHitLayerMaskOverride: def.customEnvironmentHitLayerMask.value,
                hitVfxPositionPolicy: def.hitVfxPositionPolicy,
                hitVfxOffset: def.hitVfxOffset,
                hitVfxHitAreaNormalized: def.hitVfxHitAreaNormalized,
                damageFormulaContext: damageFormulaContext);

            casterChar.LaunchProjectile(meta);
        }

        /// <summary>
        /// 프로젝타일이 실제 대상에 적중한 시점에 다시 계산할 데미지 공식 컨텍스트를 생성합니다.
        /// </summary>
        /// <remarks>
        /// 발사 시점에는 실제 충돌 대상이 확정되지 않았으므로, 최종 데미지 대신 공식 입력값만 스냅샷으로 보관합니다.
        /// 이후 Core 프로젝타일 충돌 처리에서 실제 target을 넣어 <see cref="CalculateManager.CalculateSkillDamage"/>를 호출합니다.
        /// </remarks>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">프로젝타일 이벤트 정의입니다.</param>
        /// <param name="options">이번 스킬 실행에 적용된 옵션 스냅샷입니다.</param>
        /// <param name="caster">공격력 기반 데미지 계산에 사용할 캐스터 캐릭터입니다.</param>
        /// <returns>Core 프로젝타일 시스템에 전달할 적중 시점 재계산 컨텍스트입니다.</returns>
        private static ProjectileDamageFormulaContext BuildProjectileDamageFormulaContext(
            RuntimeSkillDefinition skill,
            ProjectileEventDefinition def,
            in SkillExecutionOptions options,
            CharacterBase caster)
        {
            bool useDamageFormula = skill != null && !string.IsNullOrWhiteSpace(skill.DamageFormulaKey);
            double baseDamage = ResolveBaseProjectileDamage(skill, def, caster, useDamageFormula);
            double skillDamageRate = ResolveProjectileDamageRate(skill, def);
            float eventMultiplier = def != null ? Mathf.Max(0f, def.multiplier) : 1f;
            float optionMultiplier = options.DamageMultiplier > 0f ? options.DamageMultiplier : 1f;
            ConfigCommon.DamageType damageType = ResolveProjectileDamageType(skill, def);

            return new ProjectileDamageFormulaContext(
                skill != null ? skill.DamageFormulaKey : string.Empty,
                baseDamage,
                skillDamageRate,
                eventMultiplier,
                optionMultiplier,
                0d,
                damageType,
                false);
        }

        /// <summary>
        /// 프로젝타일 이벤트의 피해 타입을 계산합니다.
        /// </summary>
        /// <remarks>
        /// Projectile Clip의 DamageType이 None이면 skill/skill_monster 테이블의 DamageType을 사용합니다.
        /// </remarks>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">프로젝타일 이벤트 정의입니다.</param>
        /// <returns>프로젝타일 발사 메타데이터에 전달할 피해 타입입니다.</returns>
        private static ConfigCommon.DamageType ResolveProjectileDamageType(
            RuntimeSkillDefinition skill,
            ProjectileEventDefinition def)
        {
            if (def != null && def.damageType != ConfigCommon.DamageType.None)
            {
                return def.damageType;
            }

            return skill != null ? skill.DamageType : ConfigCommon.DamageType.None;
        }

        /// <summary>
        /// 프로젝타일 이벤트의 오버라이드 피해량, 스킬 테이블 기본 피해량, 이벤트 배율을 반영한 최종 피해량을 계산합니다.
        /// </summary>
        /// <remarks>
        /// Projectile Clip의 Damage가 0 이하이면 skill/skill_monster 테이블의 Damage와 DamageValueType을 사용합니다.
        /// Damage가 0보다 크면 Projectile Clip의 Damage와 DamageValueType으로 테이블 값을 덮어씁니다.
        /// </remarks>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">프로젝타일 이벤트 정의입니다.</param>
        /// <param name="options">이번 스킬 실행에 적용된 옵션 스냅샷입니다.</param>
        /// <param name="caster">공격력 기반 데미지 계산에 사용할 캐스터 캐릭터입니다.</param>
        /// <returns>프로젝타일 발사 메타데이터에 전달할 피해량입니다.</returns>
        private static long ResolveProjectileDamage(
            RuntimeSkillDefinition skill,
            ProjectileEventDefinition def,
            in SkillExecutionOptions options,
            CharacterBase caster)
        {
            bool useDamageFormula = skill != null && !string.IsNullOrWhiteSpace(skill.DamageFormulaKey);
            double baseDamage = ResolveBaseProjectileDamage(skill, def, caster, useDamageFormula);
            double skillDamageRate = ResolveProjectileDamageRate(skill, def);
            float eventMultiplier = def != null ? Mathf.Max(0f, def.multiplier) : 1f;
            float optionMultiplier = options.DamageMultiplier > 0f ? options.DamageMultiplier : 1f;

            CalculateManager calculateManager = CalculateManager.GetActive();
            if (calculateManager != null)
            {
                var request = new DamageFormulaRequest(
                    caster,
                    ResolveProjectileFormulaTarget(skill, def, caster),
                    skill != null ? skill.DamageFormulaKey : string.Empty,
                    baseDamage,
                    skillDamageRate,
                    eventMultiplier,
                    optionMultiplier,
                    0d,
                    ResolveProjectileDamageType(skill, def),
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
        /// 프로젝타일 공식 계산에 사용할 대상 캐릭터를 추정합니다.
        /// </summary>
        /// <remarks>
        /// Projectile은 발사 시점에 아직 실제 피격 대상이 없으므로, 캐스터의 현재 공격 타겟을 우선 사용합니다.
        /// 실제 적중 후 대상별 보정이 필요하면 Projectile 충돌 시점에서 재계산하는 별도 확장이 필요합니다.
        /// </remarks>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">프로젝타일 이벤트 정의입니다.</param>
        /// <param name="caster">공격자 캐릭터입니다.</param>
        /// <returns>공식 계산에 사용할 대상 캐릭터입니다. 없으면 null입니다.</returns>
        private static CharacterBase ResolveProjectileFormulaTarget(
            RuntimeSkillDefinition skill,
            ProjectileEventDefinition def,
            CharacterBase caster)
        {
            return caster != null && caster.attackerTransform != null
                ? caster.attackerTransform.GetComponent<CharacterBase>()
                : null;
        }

        /// <summary>
        /// 프로젝타일 피해량 오버라이드 여부에 따라 배율 적용 전 기본 피해량을 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">프로젝타일 이벤트 정의입니다.</param>
        /// <param name="caster">공격력 기반 데미지 계산에 사용할 캐스터 캐릭터입니다.</param>
        /// <param name="useDamageFormula">Poly 데미지 공식을 사용할지 여부입니다.</param>
        /// <returns>이벤트 배율과 실행 옵션 배율을 적용하기 전의 기본 피해량입니다.</returns>
        private static double ResolveBaseProjectileDamage(
            RuntimeSkillDefinition skill,
            ProjectileEventDefinition def,
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

                    // 공식 기반 프로젝타일도 BaseDamage에 STAT_ATK를 포함하지 않습니다.
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
        /// 프로젝타일 피해량 정의의 Damage 값을 데미지 비율로 변환합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">프로젝타일 이벤트 정의입니다.</param>
        /// <returns>공식과 기본 계산에 사용할 프로젝타일 데미지 비율입니다.</returns>
        private static double ResolveProjectileDamageRate(RuntimeSkillDefinition skill, ProjectileEventDefinition def)
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
        /// Skill 전용 OnHit Crowd Control 정의를 Core 프로젝타일 런타임 메타데이터로 변환합니다.
        /// </summary>
        /// <remarks>
        /// Core 패키지가 Skill 타입을 참조하지 않도록, 발사 메타데이터 생성 시점에 독립 DTO로 변환합니다.
        /// </remarks>
        /// <param name="entries">스킬 프로젝타일 이벤트에 설정된 Crowd Control 후보 목록입니다.</param>
        /// <returns>Core 프로젝타일 시스템에서 사용할 Crowd Control 후보 목록입니다.</returns>
        private static ProjectileOnHitCrowdControlEntry[] BuildProjectileOnHitCrowdControls(
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
        /// Skill Crowd Control 적용 시점을 Core 프로젝타일 적용 시점으로 변환합니다.
        /// </summary>
        /// <param name="timing">Skill 이벤트에 설정된 적용 시점입니다.</param>
        /// <returns>Core 프로젝타일 메타데이터에서 사용할 적용 시점입니다.</returns>
        private static ProjectileOnHitCrowdControlTiming ConvertOnHitCrowdControlTiming(
            OnHitCrowdControlTiming timing)
        {
            return timing == OnHitCrowdControlTiming.BeforeDamage
                ? ProjectileOnHitCrowdControlTiming.BeforeDamage
                : ProjectileOnHitCrowdControlTiming.AfterDamage;
        }

        /// <summary>
        /// 스킬 타겟팅 모드와 이벤트 오버라이드 설정을 기준으로 투사체가 사용할 타겟 참조 또는 좌표 오버라이드를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">투사체 이벤트 정의입니다.</param>
        /// <param name="mode">최종 적용할 스킬 타겟팅 모드입니다.</param>
        /// <param name="casterChar">투사체를 발사하는 캐스터 캐릭터입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        /// <param name="targetChar">좌표 오버라이드가 필요 없을 때 사용할 타겟 캐릭터 참조입니다.</param>
        /// <param name="usePosOverride">좌표 오버라이드를 사용할지 여부입니다.</param>
        /// <param name="posOverride">투사체가 사용할 좌표 오버라이드입니다.</param>
        private static void ResolveProjectileTarget(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            ProjectileEventDefinition def,
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
        /// 프로젝타일 목표점 고정 정책을 해석하여 좌표 오버라이드 값을 계산합니다.
        /// </summary>
        /// <param name="def">프로젝타일 이벤트 정의입니다.</param>
        /// <param name="targetChar">현재 고정 타겟 캐릭터입니다.</param>
        /// <param name="targetPos">현재 해석된 타겟 중심 좌표입니다.</param>
        /// <param name="targetPointOverride">계산된 목표점 오버라이드입니다.</param>
        /// <returns>고정 정책이 활성화되어 좌표를 계산했으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveProjectileTargetPointOverride(
            ProjectileEventDefinition def,
            CharacterBase targetChar,
            Vector3 targetPos,
            out Vector2 targetPointOverride)
        {
            targetPointOverride = default;
            if (def == null || targetChar == null)
                return false;

            switch (def.targetPointPolicy)
            {
                case ProjectileTargetPointPolicy.FixedOffsetFromTargetCenter:
                    targetPointOverride = (Vector2)targetPos + def.fixedTargetOffset;
                    return true;

                case ProjectileTargetPointPolicy.FixedNormalizedPointInTargetHitArea:
                    if (TryResolveTargetHitAreaNormalizedPoint(targetChar, def.fixedTargetHitAreaNormalized, out targetPointOverride))
                        return true;

                    targetPointOverride = (Vector2)targetPos + def.fixedTargetOffset;
                    return true;

                case ProjectileTargetPointPolicy.UseDefaultTargeting:
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
    }
}
