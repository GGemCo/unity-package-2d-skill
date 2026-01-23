using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Config;
// using GGemCo2DAffect;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임 스킬 실행기(Authoring V2).
    /// - SSOT: Core의 skill 테이블(Uid 기반)
    /// - 연출 타이밍: Addressables로 로드한 <see cref="SkillRuntimeSequence"/>
    /// - 애니메이션: 클립 이름 규칙 + Playables(Animator 파라미터 미사용)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillExecutor : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private AnimationClipLibrary clipLibrary;
        [SerializeField] private SkillAnimationPlayer animationPlayer;
        [Header("Hit Evaluator")]
        [SerializeField] private LayerMask hitMask = ~0;

        // 외부 시스템(주입/참조)
        public IStatusEffectSystem StatusSystem { get; set; }

        private readonly EffectOrchestrator _effects = new();
        private IHitEvaluator _hitEvaluator;

        // Core EffectManager(존재할 때만 사용)
        private EffectManager _effectManager;

        private SkillRun _current;
        public bool IsBusy => _current != null;

        private void Awake()
        {
            if (animationPlayer == null) animationPlayer = GetComponent<SkillAnimationPlayer>();
            _hitEvaluator = new AreaHitEvaluator(hitMask);

            // SceneGame이 존재하는 환경에서는 Core EffectManager를 사용한다.
            if (SceneGame.Instance != null)
                _effectManager = SceneGame.Instance.EffectManager;
        }

        private void Update()
        {
            if (_current == null) return;

            _current.Tick(Time.deltaTime);
            if (_current.IsDone) _current = null;
        }

        /// <summary>
        /// 스킬 사용을 시도합니다(테이블 Uid 기반).
        /// </summary>
        public bool TryUse(int skillUid, SkillTargetContext targetCtx)
        {
            if (_current != null) return false;

            var table = TableLoaderManager.Instance != null ? TableLoaderManagerSkill.Instance.TableSkill : null;
            if (table == null) return false;

            if (!table.GetDatas().TryGetValue(skillUid, out var skill) || skill == null) return false;

            _current = new SkillRun(this, skill, targetCtx);
            _current.Start();
            return true;
        }

        private sealed class SkillRun
        {
            private readonly SkillExecutor _owner;
            private readonly StruckTableSkill _skill;
            private readonly SkillTargetContext _ctx;

            private SkillRuntimeSequence _sequence;
            private float _time;
            private int _nextEventIndex;

            // Casting
            private bool _didCastStart;
            private bool _didCastLoop;
            private bool _didCastEnd;
            private bool _didUse;

            private float _castElapsed;
            private SkillAnimationPlayer.LoopHandle _castLoopHandle;

            private Vector3 _snapshotCasterPos;
            private Vector3 _snapshotTargetPos;
            private Vector3 _snapshotGroundPoint;

            private bool _isLoading;

            public bool IsDone { get; private set; }

            public SkillRun(SkillExecutor owner, StruckTableSkill skill, SkillTargetContext ctx)
            {
                _owner = owner;
                _skill = skill;
                _ctx = ctx;
            }

            public void Start()
            {
                SnapshotContext();
                _isLoading = true;
                _ = LoadSequenceAsync();
            }

            private async Task LoadSequenceAsync()
            {
                try
                {
                    // RuntimeSequenceKey는 Addressables Key 규칙(ConfigAddressableKeySkill)을 사용한다.
                    // (에디터 테스트에서는 SkillRuntimeSequenceRepository.RegisterEditorOverride로 주입 가능)
                    // todo. 정리 필요
                    var runtimeSequenceKey = ConfigAddressableKeySkill.GetRuntimeSequenceKey(_skill.Uid);
                    if (string.IsNullOrEmpty(runtimeSequenceKey))
                    {
                        IsDone = true;
                        return;
                    }

                    _sequence = await SkillRuntimeSequenceRepository.LoadAsync(runtimeSequenceKey);
                    _nextEventIndex = 0;
                    _time = 0f;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    IsDone = true;
                }
                finally
                {
                    _isLoading = false;
                }
            }

            public void Tick(float dt)
            {
                if (IsDone) return;
                if (_isLoading) return;

                // 1) 캐스팅 처리
                float castTime = Mathf.Max(0f, _skill.CastTime);

                if (castTime > 0f && !_didUse)
                {
                    _castElapsed += dt;

                    if (!_didCastStart) PlayCastStart();
                    if (!_didCastLoop) PlayCastLoop();

                    if (_castElapsed >= castTime)
                    {
                        PlayCastEnd();
                        PlayUse();
                    }
                }
                else
                {
                    if (!_didUse) PlayUse();
                }

                // 2) 이벤트 시퀀스 재생(Use 시작부터 재생)
                if (_sequence != null && _sequence.Events != null)
                {
                    _time += dt;

                    while (_nextEventIndex < _sequence.Events.Length)
                    {
                        var ev = _sequence.Events[_nextEventIndex];
                        if (_time + 1e-6f < ev.StartTime) break;

                        _owner.ExecuteEvent(_skill, _ctx, _sequence, ev, _snapshotCasterPos, _snapshotTargetPos, _snapshotGroundPoint);
                        _nextEventIndex++;
                    }

                    if (_time >= _sequence.Duration && _nextEventIndex >= _sequence.Events.Length)
                        IsDone = true;
                }
                else
                {
                    // 시퀀스가 없으면 Use 클립 길이 정도로 종료(간단 정책)
                    _time += dt;
                    IsDone = _time >= 0.3f && _didUse;
                }
            }

            private void SnapshotContext()
            {
                _snapshotCasterPos = _ctx.caster != null ? _ctx.caster.transform.position : Vector3.zero;
                _snapshotTargetPos = _ctx.lockedTarget != null ? _ctx.lockedTarget.transform.position : _snapshotCasterPos;
                _snapshotGroundPoint = _ctx.groundPoint;
            }

            private void PlayCastStart()
            {
                if (_didCastStart) return;
                _didCastStart = true;

                if (!_owner.TryResolveClip(_skill.CastStartClip, out var clip)) return;
                _owner.animationPlayer.PlayOneShot(clip, 1f);
            }

            private void PlayCastLoop()
            {
                if (_didCastLoop) return;
                _didCastLoop = true;

                if (!_owner.TryResolveClip(_skill.CastLoopClip, out var clip)) return;
                _castLoopHandle = _owner.animationPlayer.PlayLoop(clip, 1f);
            }

            private void PlayCastEnd()
            {
                if (_didCastEnd) return;
                _didCastEnd = true;

                _castLoopHandle.Stop();
                if (!_owner.TryResolveClip(_skill.CastEndClip, out var clip)) return;
                _owner.animationPlayer.PlayOneShot(clip, 1f);
            }

            private void PlayUse()
            {
                if (_didUse) return;
                _didUse = true;

                if (!_owner.TryResolveClip(_skill.UseClip, out var clip)) return;
                _owner.animationPlayer.PlayOneShot(clip, 1f);
            }
        }

        private bool TryResolveClip(string clipName, out AnimationClip clip)
        {
            clip = null;
            if (string.IsNullOrEmpty(clipName)) return false;
            if (clipLibrary == null) return false;
            return clipLibrary.TryGetClip(clipName, out clip);
        }

        private void ExecuteEvent(
            StruckTableSkill skill,
            SkillTargetContext ctx,
            SkillRuntimeSequence sequence,
            in SkillRuntimeEvent e,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            var payload = sequence != null ? sequence.GetPayload(e.PayloadIndex) : null;

            switch (e.Type)
            {
                case ConfigCommonSkill.SkillEventType.Damage:
                    // Damage 클립 구간 동안(Start~End) Gizmo 표시가 가능하도록 duration을 전달합니다.
                    // EndTime이 비정상(=StartTime)인 경우에도 최소 1프레임은 보이도록 보정합니다.
                    float damageGizmoDuration = Mathf.Max(0.05f, e.EndTime - e.StartTime);
                    HandleDamage(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint, damageGizmoDuration);
                    break;
                case ConfigCommonSkill.SkillEventType.SpawnEffect:
                    HandleEffect(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
                    break;
                case ConfigCommonSkill.SkillEventType.ApplyAffect:
                    HandleApplyStatus(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
                    break;
                default:
                    break;
            }
        }

        private void HandleDamage(
            StruckTableSkill skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            float gizmoDurationSeconds)
        {
            if (payloadObj is not DamageEventDefinition def) return;

            // 기본값은 skill 테이블의 값(SSOT)
            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            float range = skill.Range > 0f ? skill.Range : 3f;
            int maxTargets = skill.MaxTargets > 0 ? skill.MaxTargets : 1;

            // 이벤트 override 적용
            if (def.targetingOverride.enabled)
            {
                mode = def.targetingOverride.mode;
                if (def.targetingOverride.rangeOverride > 0f) range = def.targetingOverride.rangeOverride;
                if (def.targetingOverride.maxTargetsOverride > 0) maxTargets = def.targetingOverride.maxTargetsOverride;
            }

            // 타겟/중심점 결정
            Vector3 casterPos = ctx.caster != null ? ctx.caster.transform.position : snapshotCasterPos;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            // 히트 평가
            if (_hitEvaluator == null) return;
            var areaSpec = def.area;
            areaSpec.EnsureSaneDefaults();

            Vector3 center = casterPos;
            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    center = groundPoint;
                    break;
                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                    center = targetPos;
                    break;
                default:
                    // Forward / Fallback
                    var fwd = ctx.forward.sqrMagnitude < 1e-6f ? Vector3.right : ctx.forward.normalized;
                    center = casterPos + fwd * Mathf.Max(0.1f, range);
                    break;
            }

#if UNITY_EDITOR
            // SkillDamageClip이 처리되는 동안만 데미지 영역을 Gizmo로 표시합니다.
            // (에디터 PlayMode 테스트 및 씬 디버깅 용도)
            if (ctx.caster != null)
            {
                var gizmo = ctx.caster.GetComponent<GGemCo2DSkillEditor.SkillDamageAreaGizmo>();
                if (gizmo != null)
                {
                    gizmo.Show(center, ctx.forward, areaSpec, range, gizmoDurationSeconds);
                }
            }
#endif

            var hits = new List<GameObject>(Mathf.Max(1, maxTargets));
            _hitEvaluator.EvaluateTargets(center, ctx.forward, areaSpec, range, maxTargets, ctx.caster, hits);

            // 데미지 적용(현재는 로그/샘플 처리: 실제 데미지 모델은 프로젝트에 맞게 연동)
            // TODO: 정리 필요
            var castCharacterBase = ctx.caster.GetComponent<CharacterBase>();
            long totalDamage = 10;
            for (int i = 0; i < hits.Count; i++)
            {
                var go = hits[i];
                if (go == null) continue;
                
                // if (castCharacterBase.IsPlayer() && go.CompareTag(ConfigTags.GetValue(ConfigTags.Keys.Player))) continue;
                // if (castCharacterBase.IsMonster() && go.CompareTag(ConfigTags.GetValue(ConfigTags.Keys.Monster))) continue;
                //
                CharacterHitArea characterHitArea = go.GetComponent<CharacterHitArea>();
                if (characterHitArea == null) continue;
                
                // GcLogger.Log("Player attacked the monster after animation!");
                CharacterBase target = characterHitArea.target;
                
                MetadataDamage metadataDamage = new MetadataDamage
                {
                    damage = totalDamage,
                    attacker = gameObject,
                    damageType = ConfigCommon.DamageType.Physic,
                    affectUid = 0
                };

                // 몬스터와 마주보고 있으면 공격 
                if (castCharacterBase.AreFacingEachOther(target.transform))
                {
                    target.TakeDamage(metadataDamage);
                }
                // 몬스터와 같은 곳을 바라보고 있으면,
                else if (castCharacterBase.CurrentFacing == target.CurrentFacing)
                {
                    switch (castCharacterBase.CurrentFacing)
                    {
                        case CharacterConstants.FacingDirection8.Right:
                        {
                            if (target.transform.position.x >= transform.position.x)
                            {
                                target.TakeDamage(metadataDamage);
                            }
                            break;
                        }
                        case CharacterConstants.FacingDirection8.Left:
                        {
                            if (target.transform.position.x <= transform.position.x)
                            {
                                target.TakeDamage(metadataDamage);
                            }
                            break;
                        }
                    }
                }
                // 샘플: EffectOrchestrator에 이벤트 전달
                // _effects.OnDamage(ctx.caster, go, def);
            }
        }

        private void HandleEffect(
            StruckTableSkill skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not EffectEventDefinition def) return;

            // ----------------------
            // Spawn position resolve
            // ----------------------
            Vector3 casterPos = ctx.caster != null ? ctx.caster.transform.position : snapshotCasterPos;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            // 기본은 caster 기준. attachToTarget이면 target 기준.
            Vector3 spawnPos = def.attachToTarget ? targetPos : casterPos;
            if (!def.attachToTarget)
            {
                var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
                if (def.targetingOverride.enabled)
                    mode = def.targetingOverride.mode;

                switch (mode)
                {
                    case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                        spawnPos = groundPoint;
                        break;
                    case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                    case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                        spawnPos = targetPos;
                        break;
                    default:
                        // Forward / fallback
                        var fwd = ctx.forward.sqrMagnitude < 1e-6f ? Vector3.right : ctx.forward.normalized;
                        float range = skill.Range > 0f ? skill.Range : 3f;
                        spawnPos = casterPos + fwd * Mathf.Max(0.1f, range);
                        break;
                }
            }

            // localOffset은 월드 오프셋으로 처리(2D 프로젝트 기준: z는 그대로)
            spawnPos += def.localOffset;

            // ----------------------
            // Effect create
            // ----------------------
            DefaultEffect effect = null;
            var sceneGame = SceneGame.Instance;

            // 1) Core EffectManager 기반 생성(권장)
            if (sceneGame != null && sceneGame.EffectManager != null)
            {
                effect = sceneGame.EffectManager.CreateEffect(def.effectUid);
            }

            // 2) 폴백: 프리팹 직접 Instantiate
            if (effect == null) return;

            // lifetimeSeconds가 지정되면 Core DefaultEffect의 duration을 사용
            if (def.lifetimeSeconds > 0f)
                effect.SetDuration(def.lifetimeSeconds);

            if (def.attachToTarget && ctx.lockedTarget != null)
            {
                effect.transform.SetParent(ctx.lockedTarget.transform, worldPositionStays: true);
            }

            effect.transform.position = spawnPos;
        }

        private void HandleApplyStatus(
            StruckTableSkill skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            /*
            if (payloadObj is not ApplyStatusEventDefinition def) return;

            // Affect 패키지 기반 처리.
            // - statusId.id: AffectUid(숫자 문자열)로 간주
            // - stacks: ApplyAffect를 stacks 만큼 반복 호출
            // - durationOverrideSeconds: AffectApplyContext.DurationOverride로 전달
            // - chance01: 확률 체크

            if (!TryParseAffectUid(def.statusId, out int affectUid))
                return;

            // 확률(0~1)
            float chance = Mathf.Clamp01(def.chance01);
            if (chance <= 0f) return;

            // 적용 대상 선정(단일 타겟 또는 영역)
            var targets = s_applyTargets;
            targets.Clear();

            // 스냅샷/중심점 결정
            Vector3 casterPos = ctx.caster != null ? ctx.caster.transform.position : snapshotCasterPos;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

                if (_hitEvaluator == null) return;

                // AreaDefinition을 사용하지 않으므로, ApplyAffect의 범위는 기본 원형 범위를 사용한다.
                // (range를 radius로 사용)
                var areaSpec = new SkillAreaSpec
                {
                    shape = ConfigCommonSkill.SkillAreaShape.Circle,
                    radius = Mathf.Max(0.1f, range),
                    length = Mathf.Max(0.1f, range),
                    width = Mathf.Max(0.1f, range),
                    angle = 60f,
                    localOffset = Vector3.zero
                };

                Vector3 center = casterPos;
                switch (mode)
                {
                    case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                        center = groundPoint;
                        break;
                    case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                    case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                        center = targetPos;
                        break;
                    default:
                        var fwd = ctx.forward.sqrMagnitude < 1e-6f ? Vector3.right : ctx.forward.normalized;
                        center = casterPos + fwd * Mathf.Max(0.1f, range);
                        break;
                }

                _hitEvaluator.EvaluateTargets(center, ctx.forward, areaSpec, range, maxTargets, ctx.caster, targets);
            }
            else
            {
                // 단일 타겟: lockedTarget 우선
                if (ctx.lockedTarget != null)
                    targets.Add(ctx.lockedTarget);
            }

            if (targets.Count == 0) return;

            var applyCtx = new AffectApplyContext
            {
                Source = ctx.caster,
                DurationOverride = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : 0f
            };

            int stacks = Mathf.Max(1, def.stacks);

            for (int i = 0; i < targets.Count; i++)
            {
                var targetGo = targets[i];
                if (targetGo == null) continue;

                // 확률 체크(대상별)
                if (chance < 0.9999f && UnityEngine.Random.value > chance)
                    continue;

                if (!TryEnsureAffectComponent(targetGo, out var affectComponent))
                    continue;

                for (int s = 0; s < stacks; s++)
                    affectComponent.ApplyAffect(affectUid, applyCtx);
            }
            */
        }

        private static readonly List<GameObject> s_applyTargets = new(32);

        private static bool TryParseAffectUid(StatusEffectId id, out int affectUid)
        {
            affectUid = 0;
            if (string.IsNullOrWhiteSpace(id.id)) return false;
            return int.TryParse(id.id, out affectUid) && affectUid > 0;
        }
/*
        private static bool TryEnsureAffectComponent(GameObject target, out AffectComponent affectComponent)
        {
            affectComponent = null;
            if (target == null) return false;

            // AffectComponent가 없으면 추가(타겟 계약 IAffectTarget이 필요)
            affectComponent = target.GetComponent<AffectComponent>();

            // IAffectTarget 브리지(CoreAffectTargetAdapter)가 없으면 추가 시도
            // (CharacterBase 기반 캐릭터만 지원)
            if (target.GetComponent<IAffectTarget>() == null)
            {
                if (target.GetComponent<CharacterBase>() != null)
                {
                    if (target.GetComponent<CoreAffectTargetAdapter>() == null)
                        target.AddComponent<CoreAffectTargetAdapter>();
                }
                else
                {
                    // 캐릭터가 아니면 현재는 지원하지 않음
                    return false;
                }
            }

            if (affectComponent == null)
                affectComponent = target.AddComponent<AffectComponent>();

            return affectComponent != null;
        }
*/
    }
}
