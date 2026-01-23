using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Config;
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
        [SerializeField] private AreaRegistry areaRegistry;

        // 외부 시스템(주입/참조)
        public IStatusEffectSystem StatusSystem { get; set; }

        private readonly EffectOrchestrator _effects = new();
        private IHitEvaluator _hitEvaluator;

        private SkillRun _current;
        public bool IsBusy => _current != null;

        private void Awake()
        {
            if (animationPlayer == null) animationPlayer = GetComponent<SkillAnimationPlayer>();
            _hitEvaluator = new AreaHitEvaluator(hitMask);
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
                    // RuntimeSequenceKey가 비어있으면 즉시 종료(정책)
                    // todo. 정리 필요
                    var runtimeSequenceKey = $"GGemCo_Skill_RuntimeSequences_{_skill.Uid}";
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
                    HandleDamage(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
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
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not DamageEventDefinition def) return;

            // 기본값은 skill 테이블의 값(SSOT)
            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            float range = skill.Range > 0f ? skill.Range : 3f;
            int maxTargets = skill.MaxTargets > 0 ? skill.MaxTargets : 1;
            string areaId = string.IsNullOrEmpty(skill.DefaultAreaId) ? "Area_Default" : skill.DefaultAreaId;

            // 이벤트 override 적용
            if (def.targetingOverride.enabled)
            {
                mode = def.targetingOverride.mode;
                if (def.targetingOverride.rangeOverride > 0f) range = def.targetingOverride.rangeOverride;
                if (def.targetingOverride.maxTargetsOverride > 0) maxTargets = def.targetingOverride.maxTargetsOverride;
            }

            if (def.areaOverride.enabled && !string.IsNullOrEmpty(def.areaOverride.areaId))
                areaId = def.areaOverride.areaId;

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
            if (areaRegistry == null || _hitEvaluator == null) return;
            if (!areaRegistry.TryGet(areaId, out var areaDef) || areaDef == null) return;

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

            var hits = new List<GameObject>(Mathf.Max(1, maxTargets));
            _hitEvaluator.EvaluateTargets(center, ctx.forward, areaDef, range, maxTargets, ctx.caster, hits);

            // 데미지 적용(현재는 로그/샘플 처리: 실제 데미지 모델은 프로젝트에 맞게 연동)
            // TODO: Core 전투 시스템과 연결(예: IDamageSystem, Stat/DamageType, 크리티컬 등)
            for (int i = 0; i < hits.Count; i++)
            {
                var go = hits[i];

                if (go == null) continue;
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
            // todo. 정리 필요
            // _effects.OnEffect(ctx.caster, ctx.lockedTarget, ctx.groundPoint, def, snapshotCasterPos, snapshotTargetPos,snapshotGroundPoint);
        }

        private void HandleApplyStatus(
            StruckTableSkill skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not ApplyStatusEventDefinition def) return;
            if (StatusSystem == null) return;

            // todo. 정리 필요.
            // StatusSystem.Apply(ctx.caster, ctx.lockedTarget, def, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
        }
    }
}
