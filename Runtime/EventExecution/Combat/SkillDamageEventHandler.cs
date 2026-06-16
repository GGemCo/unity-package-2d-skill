using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 데미지 스킬 이벤트의 판정 위치 계산, 타겟 평가, 데미지 메타데이터 구성, OnHit 후속 효과 적용을 담당합니다.
    /// </summary>
    internal static class SkillDamageEventHandler
    {
        /// <summary>
        /// 데미지 이벤트 정의를 바탕으로 타격 대상을 평가하고 실제 데미지 및 OnHit 효과를 적용합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">데미지 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        /// <param name="gizmoDurationSeconds">에디터 디버그용 데미지 영역 표시 시간입니다.</param>
        /// <param name="hitEvaluator">범위 기반 타겟 판정을 수행하는 평가기입니다.</param>
        /// <param name="ownerObject">데미지 메타데이터의 대체 공격자와 위치 기준으로 사용할 실행기 오브젝트입니다.</param>
        /// <param name="attackSequence">공격 식별자를 발급하고 연계 해제 정책을 저장할 시퀀스입니다.</param>
        public static void Handle(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            float gizmoDurationSeconds,
            IHitEvaluator hitEvaluator,
            GameObject ownerObject,
            SkillAttackSequence attackSequence)
        {
            if (payloadObj is not DamageEventDefinition def)
                return;
            if (hitEvaluator == null || attackSequence == null)
                return;

            ConfigCommonSkill.SkillTargetingMode mode =
                (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            float range = SkillRangeResolver.GetPlacementRange(skill);
            int maxTargets = skill.MaxTargets > 0 ? skill.MaxTargets : 1;

            if (def.targetingOverride.enabled)
            {
                mode = def.targetingOverride.mode;
                if (def.targetingOverride.rangeOverride > 0f)
                    range = def.targetingOverride.rangeOverride;
                if (def.targetingOverride.maxTargetsOverride > 0)
                    maxTargets = def.targetingOverride.maxTargetsOverride;
            }

            Vector3 casterPos = ctx.caster != null ? ctx.caster.transform.position : snapshotCasterPos;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;
            bool useDamageStartSnapshot =
                def.damageCenterReference.mode == SkillPositionReferenceMode.SkillStartSnapshot ||
                (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter);

            if (useDamageStartSnapshot)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            Vector3 resolvedForward = SkillDirectionResolver.ResolveForward2D(ctx.caster, ctx.forward);
            SkillAreaSpec areaSpec = def.area;
            areaSpec.EnsureSaneDefaults();

            Vector3 center = ResolveDamageCenter(mode, casterPos, targetPos, groundPoint, resolvedForward, range);
            if (!TryApplyDamagePositionReference(run, def, ref center, ref resolvedForward, skill))
                return;

#if UNITY_EDITOR
            RegisterDamageDebugGizmo(ctx.caster, center, resolvedForward, areaSpec, gizmoDurationSeconds);
#endif

            var hits = new List<GameObject>(Mathf.Max(1, maxTargets));
            hitEvaluator.EvaluateTargets(center, resolvedForward, areaSpec, range, maxTargets, ctx.caster, hits);

            CharacterBase castCharacterBase = ctx.caster != null ? ctx.caster.GetComponent<CharacterBase>() : null;
            int attackId = attackSequence.Allocate(def.allowSkillChainOnConfirmedDamage);
            var resolvedOnHitCrowdControls = new List<int>(8);
            bool[] onHitSoundPlayedByEntry = null;

            for (int i = 0; i < hits.Count; i++)
            {
                GameObject go = hits[i];
                if (go == null)
                    continue;

                CharacterHitArea characterHitArea = go.GetComponentInChildren<CharacterHitArea>();
                if (characterHitArea == null)
                    continue;

                CharacterBase target = characterHitArea.target;
                if (target == null)
                    continue;

                if (!IsDamageTargetStateAllowed(def, target))
                    continue;

                ApplyOnHitAffects(
                    def.onHitAffects,
                    ctx.caster,
                    target,
                    damageApplied: false,
                    timing: OnHitAffectTiming.BeforeDamage,
                    executionOptions: ctx.executionOptions);
                int crowdControlUid = ResolveOnHitCrowdControlUid(
                    def.onHitCrowdControls,
                    damageApplied: false,
                    timing: OnHitCrowdControlTiming.BeforeDamage,
                    resolvedOnHitCrowdControls);

                bool hasPendingAfterDamageCrowdControl = HasPendingAfterDamageCrowdControl(
                    def.onHitCrowdControls,
                    damageApplied: true,
                    timing: OnHitCrowdControlTiming.AfterDamage);

                long totalDamage = ResolveSkillDamage(skill, def, ctx.executionOptions, castCharacterBase, target);

                var metadataDamage = new MetadataDamage
                {
                    damage = totalDamage,
                    attacker = ctx.caster != null ? ctx.caster : ownerObject,
                    damageType = skill.DamageType,
                    affectUid = 0,
                    crowdControlUid = crowdControlUid,
                    AttackId = attackId,
                    SkillUid = skill.Uid,
                    HasPendingAfterDamageCrowdControl = hasPendingAfterDamageCrowdControl,
                    DamageCameraShakePreset = def.useCameraShakeOnHit ? def.cameraShakePreset : null,
                    DamageCameraShakeDirectionSource = def.cameraShakeDirectionSource,
                    DamageCameraShakeFixedDirection = def.cameraShakeFixedDirection,
                    DamageCameraShakeHorizontalOnly = def.cameraShakeHorizontalOnly,
                    GuardAttackType = def.guardAttackType,
                    GuardInteractionMode = def.guardInteractionMode,
                    GuardBreakJustGuardPolicy = def.guardBreakJustGuardPolicy,
                    GuardBreakDamageMultiplier = def.guardBreakDamageMultiplier,
                    GuardBreakStaminaCost = def.guardBreakStaminaCost,
                    GuardBreakVfxUid = def.guardBreakVfxUid,
                    GuardBreakFeedbackText = def.guardBreakFeedbackText,
                };

                bool didApplyDamage = totalDamage > 0L &&
                                      ShouldApplyDamageByFacingPolicy(def, castCharacterBase, target, ownerObject);

                CollectOnHitCrowdControlUids(
                    def.onHitCrowdControls,
                    didApplyDamage,
                    OnHitCrowdControlTiming.AfterDamage,
                    resolvedOnHitCrowdControls);

                metadataDamage.ElementGaugeApplications = ResolveElementGaugeApplications(
                    def.onHitElementGauges,
                    ownerObject,
                    target,
                    didApplyDamage);

                if (didApplyDamage)
                {
                    PlayOnHitSounds(
                        def.onHitSounds,
                        OnHitSoundTiming.BeforeDamage,
                        ref onHitSoundPlayedByEntry);

                    metadataDamage.ResolvedOnHitCrowdControls = resolvedOnHitCrowdControls;
                    target.TakeDamage(metadataDamage);
                    ApplyConfiguredHitStop(def, skill, castCharacterBase, target);

                    PlayOnHitSounds(
                        def.onHitSounds,
                        OnHitSoundTiming.AfterDamage,
                        ref onHitSoundPlayedByEntry);
                }

                if (target.IsStatusDead())
                    continue;

                ApplyOnHitAffects(
                    def.onHitAffects,
                    ctx.caster,
                    target,
                    didApplyDamage,
                    OnHitAffectTiming.AfterDamage,
                    ctx.executionOptions);
            }
        }

        /// <summary>
        /// Damage 이벤트의 실제 피해 확정 시점에 맞춰 OnHit 사운드를 재생합니다.
        /// </summary>
        /// <param name="entries">재생 후보 OnHit 사운드 목록입니다.</param>
        /// <param name="timing">현재 처리 중인 사운드 재생 시점입니다.</param>
        /// <param name="playedByEntry">OncePerDamageEvent 정책을 항목별로 추적하는 배열입니다.</param>
        private static void PlayOnHitSounds(
            OnHitSoundEntry[] entries,
            OnHitSoundTiming timing,
            ref bool[] playedByEntry)
        {
            if (entries == null || entries.Length == 0)
                return;

            SceneGame sceneGame = SceneGame.Instance;
            if (sceneGame == null || sceneGame.soundManager == null)
                return;

            if (playedByEntry == null || playedByEntry.Length != entries.Length)
                playedByEntry = new bool[entries.Length];

            for (int i = 0; i < entries.Length; i++)
            {
                OnHitSoundEntry entry = entries[i];
                if (entry.timing != timing)
                    continue;
                if (!entry.IsValid())
                    continue;
                if (entry.playMode == OnHitSoundPlayMode.OncePerDamageEvent && playedByEntry[i])
                    continue;

                // 새 항목을 추가하고 soundUid만 입력해도 동작하도록 0 이하는 항상 재생으로 취급합니다.
                float chance = entry.chance <= 0f ? 1f : Mathf.Clamp01(entry.chance);
                if (chance < 0.9999f && Random.value > chance)
                    continue;

                SoundPlayRequest request = entry.ResolveRequest();
                if (request == null || !request.IsValid)
                    continue;

                sceneGame.soundManager.Play(request);

                if (entry.playMode == OnHitSoundPlayMode.OncePerDamageEvent)
                    playedByEntry[i] = true;
            }
        }

        /// <summary>
        /// 피격 대상의 패시브 정책을 반영하여 Damage 이벤트의 OnHitElementGauge 적용 목록을 생성합니다.
        /// 대상 패시브가 특정 원소 게이지 수신을 차단하면 해당 항목은 전투 메타데이터에 전달하지 않습니다.
        /// </summary>
        /// <param name="entries">스킬 이벤트에 설정된 OnHit 원소 게이지 항목입니다.</param>
        /// <param name="caster">Affect 조건 확인에 사용할 캐스터 오브젝트입니다.</param>
        /// <param name="target">원소 게이지 수신 차단 패시브를 확인할 피격 대상입니다.</param>
        /// <param name="damageApplied">이번 타격에서 실제 데미지가 적용되었는지 여부입니다.</param>
        /// <returns>적용 가능한 원소 게이지 목록입니다. 적용할 항목이 없으면 null입니다.</returns>
        private static ElementGaugeApplication[] ResolveElementGaugeApplications(
            OnHitElementGaugeEntry[] entries,
            GameObject caster,
            CharacterBase target,
            bool damageApplied)
        {
            if (entries == null || entries.Length == 0)
            {
                return null;
            }

            CharacterPassiveSkillController passiveController =
                target != null ? target.GetComponent<CharacterPassiveSkillController>() : null;
            if (passiveController == null)
            {
                return SkillOnHitEffectUtility.BuildElementGaugeApplications(entries, caster, damageApplied);
            }

            bool suppressedAny = false;
            for (int i = 0; i < entries.Length; i++)
            {
                if (passiveController.SuppressesOnHitElementGauge(entries[i].damageType))
                {
                    suppressedAny = true;
                    break;
                }
            }

            if (!suppressedAny)
            {
                return SkillOnHitEffectUtility.BuildElementGaugeApplications(entries, caster, damageApplied);
            }

            List<OnHitElementGaugeEntry> allowedEntries = new List<OnHitElementGaugeEntry>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                OnHitElementGaugeEntry entry = entries[i];
                if (passiveController.SuppressesOnHitElementGauge(entry.damageType))
                {
                    continue;
                }

                allowedEntries.Add(entry);
            }

            return allowedEntries != null && allowedEntries.Count > 0
                ? SkillOnHitEffectUtility.BuildElementGaugeApplications(allowedEntries.ToArray(), caster, damageApplied)
                : null;
        }

        /// <summary>
        /// 스킬 테이블 기본 데미지를 해석한 뒤 전역 계산 매니저로 최종 스킬 데미지를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="def">현재 데미지 이벤트 정의입니다.</param>
        /// <param name="options">이번 스킬 실행에 적용된 옵션 스냅샷입니다.</param>
        /// <param name="caster">공격력 기반 데미지 계산에 사용할 캐스터 캐릭터입니다.</param>
        /// <param name="target">레벨 차이 배율 계산에 사용할 피격 대상 캐릭터입니다.</param>
        /// <returns>전역 계산 정책이 반영된 최종 스킬 데미지입니다.</returns>
        private static long ResolveSkillDamage(
            RuntimeSkillDefinition skill,
            DamageEventDefinition def,
            in SkillExecutionOptions options,
            CharacterBase caster,
            CharacterBase target)
        {
            if (skill == null)
                return 0L;

            bool useDamageFormula = !string.IsNullOrWhiteSpace(skill.DamageFormulaKey);
            double baseDamage = ResolveBaseSkillDamage(skill, caster, useDamageFormula);
            double skillDamageRate = ResolveSkillDamageRate(skill);
            float eventMultiplier = def != null ? Mathf.Max(0f, def.multiplier) : 1f;
            float optionMultiplier = options.DamageMultiplier > 0f ? options.DamageMultiplier : 1f;

            CalculateManager calculateManager = CalculateManager.GetActive();
            if (calculateManager != null)
            {
                var request = new DamageFormulaRequest(
                    caster,
                    target,
                    skill.DamageFormulaKey,
                    baseDamage,
                    skillDamageRate,
                    eventMultiplier,
                    optionMultiplier,
                    0d,
                    skill.DamageType,
                    false);
                return calculateManager.CalculateSkillDamage(request);
            }

            double resolved = System.Math.Max(0d, baseDamage) * skillDamageRate * eventMultiplier * optionMultiplier;
            if (resolved <= 0d)
                return 0L;

            return resolved >= long.MaxValue
                ? long.MaxValue
                : (long)System.Math.Round(resolved);
        }

        /// <summary>
        /// 스킬 테이블에 설정된 데미지 해석 방식에 따라 최종 배율 적용 전 기본 데미지를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="caster">공격력 기반 데미지 계산에 사용할 캐스터 캐릭터입니다.</param>
        /// <param name="useDamageFormula">Poly 데미지 공식을 사용할지 여부입니다.</param>
        /// <returns>이벤트 배율과 실행 옵션 배율을 적용하기 전의 기본 데미지입니다.</returns>
        private static double ResolveBaseSkillDamage(RuntimeSkillDefinition skill, CharacterBase caster, bool useDamageFormula)
        {
            if (skill == null)
                return 0d;

            long damage = System.Math.Max(0L, skill.Damage);
            if (damage <= 0L)
                return 0d;

            switch (skill.DamageValueType)
            {
                case ConfigCommonSkill.SkillDamageValueType.AttackPercent:
                    if (caster == null)
                        return 0d;

                    // 공식 기반 스킬은 BaseDamage에 STAT_ATK를 포함하지 않습니다.
                    // GGemCoPlayerSettings의 Stat Point Atk 설정으로 계산된 STAT_ATK는
                    // CalculateManager가 StatStrength 변수로 공식에 별도 전달합니다.
                    long attack = useDamageFormula
                        ? System.Math.Max(0L, caster.TotalBaseAtk.Value)
                        : System.Math.Max(0L, caster.ResolvedAtk.Value);
                    if (attack <= 0L)
                        return 0d;

                    return attack;

                case ConfigCommonSkill.SkillDamageValueType.Fixed:
                default:
                    return damage;
            }
        }

        /// <summary>
        /// skill 테이블의 Damage 값을 데미지 비율로 변환합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <returns>공식과 기본 계산에 사용할 스킬 데미지 비율입니다.</returns>
        private static double ResolveSkillDamageRate(RuntimeSkillDefinition skill)
        {
            if (skill == null)
                return 1d;

            return skill.DamageValueType == ConfigCommonSkill.SkillDamageValueType.AttackPercent
                ? System.Math.Max(0L, skill.Damage) / 100d
                : 1d;
        }

        /// <summary>
        /// 타겟팅 모드에 따라 데미지 판정 중심 좌표를 계산합니다.
        /// </summary>
        /// <param name="mode">최종 적용할 스킬 타겟팅 모드입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석한 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석한 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석한 지면 기준점입니다.</param>
        /// <param name="resolvedForward">2D 기준으로 보정된 전방 방향입니다.</param>
        /// <param name="range">전방 배치에 사용할 거리입니다.</param>
        /// <returns>데미지 판정 중심 좌표입니다.</returns>
        private static Vector3 ResolveDamageCenter(
            ConfigCommonSkill.SkillTargetingMode mode,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            Vector3 resolvedForward,
            float range)
        {
            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    return groundPoint;
                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                    return targetPos;
                default:
                    Vector3 fwd = resolvedForward.sqrMagnitude < 1e-6f ? Vector3.right : resolvedForward.normalized;
                    return SkillRangeResolver.ResolveForwardPlacementPosition(casterPos, fwd, range);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 테스트 허브에 데미지 영역 기즈모 정보를 등록합니다.
        /// </summary>
        /// <param name="caster">기즈모 소유자로 사용할 캐스터 오브젝트입니다.</param>
        /// <param name="center">데미지 영역 중심입니다.</param>
        /// <param name="forward">데미지 영역 방향입니다.</param>
        /// <param name="areaSpec">데미지 영역 스펙입니다.</param>
        /// <param name="durationSeconds">기즈모 표시 시간입니다.</param>
        private static void RegisterDamageDebugGizmo(
            GameObject caster,
            Vector3 center,
            Vector3 forward,
            SkillAreaSpec areaSpec,
            float durationSeconds)
        {
            if (caster == null || SkillTestRuntimeHub.Instance == null)
                return;

            SkillTestRuntimeHub.Instance.RegisterDamageArea(
                center,
                forward,
                areaSpec,
                durationSeconds,
                caster);
        }
#endif

        /// <summary>
        /// 데미지 이벤트 정의의 바라보기 정책에 따라 실제 데미지 적용 여부를 결정합니다.
        /// </summary>
        /// <param name="def">데미지 이벤트 정의입니다.</param>
        /// <param name="caster">공격 캐릭터입니다.</param>
        /// <param name="target">피격 후보 캐릭터입니다.</param>
        /// <param name="ownerObject">캐스터 위치 대체값으로 사용할 실행기 오브젝트입니다.</param>
        /// <returns>현재 정책에서 실제 데미지를 적용해야 하면 <see langword="true"/>입니다.</returns>
        private static bool ShouldApplyDamageByFacingPolicy(
            DamageEventDefinition def,
            CharacterBase caster,
            CharacterBase target,
            GameObject ownerObject)
        {
            if (caster == null || target == null)
                return false;

            if (def != null && def.facingDamagePolicy == DamageFacingPolicy.IgnoreFacing)
                return true;

            return ShouldApplyFacingDamage(caster, target, ownerObject);
        }

        /// <summary>
        /// 바라보기 정책이 <see cref="DamageFacingPolicy.RespectFacing"/>일 때,
        /// 캐스터와 타겟의 바라보기 상태를 기준으로 실제 데미지를 적용할지 결정합니다.
        /// </summary>
        /// <param name="caster">공격 캐릭터입니다.</param>
        /// <param name="target">피격 후보 캐릭터입니다.</param>
        /// <param name="ownerObject">캐스터 위치 대체값으로 사용할 실행기 오브젝트입니다.</param>
        /// <returns>실제 데미지를 적용해야 하면 <see langword="true"/>입니다.</returns>
        private static bool ShouldApplyFacingDamage(CharacterBase caster, CharacterBase target, GameObject ownerObject)
        {
            if (caster == null || target == null)
                return false;

            if (caster.AreFacingEachOther(target))
                return true;

            if (caster.CurrentFacing != target.CurrentFacing)
                return false;

            float ownerX = ownerObject != null ? ownerObject.transform.position.x : caster.transform.position.x;
            switch (caster.CurrentFacing)
            {
                case CharacterConstants.FacingDirection8.Right:
                    return target.transform.position.x >= ownerX;
                case CharacterConstants.FacingDirection8.Left:
                    return target.transform.position.x <= ownerX;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 데미지 이벤트 정의에 설정된 HitStop 정책을 캐스터와 타겟에게 적용합니다.
        /// </summary>
        /// <param name="def">데미지 이벤트 정의입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="caster">공격 캐릭터입니다.</param>
        /// <param name="target">피격 캐릭터입니다.</param>
        private static void ApplyConfiguredHitStop(
            DamageEventDefinition def,
            RuntimeSkillDefinition skill,
            CharacterBase caster,
            CharacterBase target)
        {
            if (def == null || caster == null)
                return;

            CharacterBase.HitStopConfig hitStopConfig = caster.GetResolvedHitStopConfig();
            if (def.useHitStopSelf && hitStopConfig.Enabled)
            {
                float selfSeconds = def.useDefaultSelfHitStop
                    ? hitStopConfig.DefaultSelfSeconds
                    : Mathf.Max(0f, def.selfHitStopSeconds);
                if (selfSeconds > 0f)
                {
                    caster.ApplyHitStop(new HitStopRequest(
                        selfSeconds,
                        pauseAnimation: hitStopConfig.PauseAnimation,
                        freezePhysics: hitStopConfig.FreezePhysics,
                        sourceSkillUid: skill != null ? skill.Uid : 0));
                }
            }

            if (target != null && def.useHitStopTarget && hitStopConfig.Enabled)
            {
                float targetSeconds = def.useDefaultTargetHitStop
                    ? hitStopConfig.DefaultReceiveSeconds
                    : Mathf.Max(0f, def.targetHitStopSeconds);
                if (targetSeconds > 0f)
                {
                    target.ApplyHitStop(new HitStopRequest(
                        targetSeconds,
                        pauseAnimation: hitStopConfig.PauseAnimation,
                        freezePhysics: hitStopConfig.FreezePhysics,
                        sourceSkillUid: skill != null ? skill.Uid : 0));
                }
            }
        }

        /// <summary>
        /// 데미지 이벤트의 위치 참조 설정에 따라 판정 중심과 방향을 이름 있는 위치 앵커로 교체합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="def">데미지 이벤트 정의입니다.</param>
        /// <param name="center">현재 계산된 데미지 영역 중심입니다.</param>
        /// <param name="resolvedForward">현재 계산된 데미지 영역 방향입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <returns>데미지 처리를 계속할 수 있으면 <see langword="true"/>입니다.</returns>
        private static bool TryApplyDamagePositionReference(
            SkillRun run,
            DamageEventDefinition def,
            ref Vector3 center,
            ref Vector3 resolvedForward,
            RuntimeSkillDefinition skill)
        {
            if (def == null)
                return false;

            SkillPositionReference reference = def.damageCenterReference;
            if (reference.mode != SkillPositionReferenceMode.NamedPositionAnchor &&
                reference.mode != SkillPositionReferenceMode.NamedPositionAnchorOrCurrent)
            {
                return true;
            }

            if (run != null && run.TryGetPositionAnchor(reference.key, out SkillPositionAnchorSnapshot snapshot))
            {
                center = snapshot.Position;
                if (snapshot.Forward.sqrMagnitude > 1e-6f)
                    resolvedForward = snapshot.Forward.normalized;
                return true;
            }

            if (reference.mode == SkillPositionReferenceMode.NamedPositionAnchorOrCurrent)
                return true;

            Debug.LogWarning(
                $"[SkillExecutor] Damage position anchor not found. skillUid={skill?.Uid ?? 0}, key={reference.key}");
            return false;
        }

        /// <summary>
        /// 데미지 이벤트의 지상/공중 필터 설정에 따라 타겟 상태가 허용되는지 확인합니다.
        /// </summary>
        /// <param name="def">데미지 이벤트 정의입니다.</param>
        /// <param name="target">검사할 피격 후보 캐릭터입니다.</param>
        /// <returns>타겟 상태가 데미지 필터를 통과하면 <see langword="true"/>입니다.</returns>
        private static bool IsDamageTargetStateAllowed(DamageEventDefinition def, CharacterBase target)
        {
            if (def == null || target == null)
                return false;

            if (def.isGroundOnly && def.isAirOnly)
            {
                Debug.LogWarning(
                    $"[SkillExecutor] DamageEventDefinition has both isGroundOnly and isAirOnly enabled. The target will be skipped. target={target.name}",
                    target);
                return false;
            }

            if (!def.isGroundOnly && !def.isAirOnly)
                return true;

            bool isGrounded = target.IsCurrentlyGrounded();
            if (def.isGroundOnly)
                return isGrounded;

            if (def.isAirOnly)
                return !isGrounded;

            return true;
        }

        /// <summary>
        /// OnHit 설정 목록을 순회하며 조건에 맞는 Affect를 대상에게 적용합니다.
        /// </summary>
        /// <param name="entries">적용할 OnHit Affect 목록입니다.</param>
        /// <param name="caster">효과의 출처가 되는 캐스터입니다.</param>
        /// <param name="target">효과를 적용할 대상입니다.</param>
        /// <param name="damageApplied">실제 데미지가 적용되었는지 여부입니다.</param>
        /// <param name="timing">현재 처리 중인 OnHit 적용 시점입니다.</param>
        /// <param name="executionOptions">Affect 적용 시 전달할 스킬 실행 옵션입니다.</param>
        private static void ApplyOnHitAffects(
            OnHitAffectEntry[] entries,
            GameObject caster,
            CharacterBase target,
            bool damageApplied,
            OnHitAffectTiming timing,
            SkillExecutionOptions executionOptions)
        {
            if (entries == null || entries.Length == 0)
                return;
            if (caster == null || target == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                OnHitAffectEntry entry = entries[i];
                if (entry.affectUid <= 0)
                    continue;
                if (entry.timing != timing)
                    continue;
                if (entry.requireDamageDealt && !damageApplied)
                    continue;

                float chance = Mathf.Clamp01(entry.chance);
                if (chance <= 0f)
                    continue;
                if (chance < 0.9999f && Random.value > chance)
                    continue;

                int stacks = Mathf.Max(1, entry.stacks);
                float duration = entry.durationOverrideSeconds > 0f ? entry.durationOverrideSeconds : 0f;

                for (int s = 0; s < stacks; s++)
                {
                    AffectApi.Apply(
                        target.gameObject,
                        entry.affectUid,
                        caster,
                        duration,
                        executionOptions.StatusDurationBonusSeconds,
                        executionOptions.HealHpBonus,
                        executionOptions.HealHpMultiplier);
                }
            }
        }

        /// <summary>
        /// 데미지 이후 적용할 Crowd Control 후보가 있는지 확인합니다.
        /// </summary>
        /// <param name="entries">검사할 OnHit Crowd Control 목록입니다.</param>
        /// <param name="damageApplied">실제 데미지가 적용되었는지 여부입니다.</param>
        /// <param name="timing">현재 처리 중인 OnHit 적용 시점입니다.</param>
        /// <returns>조건을 만족하는 Crowd Control 항목이 있으면 <see langword="true"/>입니다.</returns>
        private static bool HasPendingAfterDamageCrowdControl(
            OnHitCrowdControlEntry[] entries,
            bool damageApplied,
            OnHitCrowdControlTiming timing)
        {
            if (entries == null || entries.Length == 0)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                OnHitCrowdControlEntry entry = entries[i];
                if (entry.crowdControlUid <= 0)
                    continue;
                if (entry.timing != timing)
                    continue;
                if (entry.requireDamageDealt && !damageApplied)
                    continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 현재 시점에 적용 가능한 OnHit Crowd Control UID를 순서대로 수집합니다.
        /// 배열에 등록된 순서가 실행 순서가 됩니다.
        /// </summary>
        /// <param name="entries">수집할 OnHit Crowd Control 목록입니다.</param>
        /// <param name="damageApplied">실제 데미지가 적용되었는지 여부입니다.</param>
        /// <param name="timing">현재 처리 중인 OnHit 적용 시점입니다.</param>
        /// <param name="results">수집 결과를 저장할 목록입니다.</param>
        private static void CollectOnHitCrowdControlUids(
            OnHitCrowdControlEntry[] entries,
            bool damageApplied,
            OnHitCrowdControlTiming timing,
            List<int> results)
        {
            results?.Clear();

            if (entries == null || entries.Length == 0 || results == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                OnHitCrowdControlEntry entry = entries[i];
                if (entry.crowdControlUid <= 0)
                    continue;
                if (entry.timing != timing)
                    continue;
                if (entry.requireDamageDealt && !damageApplied)
                    continue;

                float chance = Mathf.Clamp01(entry.chance);
                if (chance <= 0f)
                    continue;
                if (chance < 0.9999f && Random.value > chance)
                    continue;

                results.Add(entry.crowdControlUid);
            }
        }

        /// <summary>
        /// BeforeDamage처럼 단일 Crowd Control 전달이 필요한 구간에서 첫 번째 UID를 선택합니다.
        /// </summary>
        /// <param name="entries">검사할 OnHit Crowd Control 목록입니다.</param>
        /// <param name="damageApplied">실제 데미지가 적용되었는지 여부입니다.</param>
        /// <param name="timing">현재 처리 중인 OnHit 적용 시점입니다.</param>
        /// <param name="scratch">임시 수집에 사용할 목록입니다.</param>
        /// <returns>선택한 Crowd Control UID입니다. 없으면 0입니다.</returns>
        private static int ResolveOnHitCrowdControlUid(
            OnHitCrowdControlEntry[] entries,
            bool damageApplied,
            OnHitCrowdControlTiming timing,
            List<int> scratch)
        {
            CollectOnHitCrowdControlUids(entries, damageApplied, timing, scratch);
            return scratch != null && scratch.Count > 0 ? scratch[0] : 0;
        }
    }
}
