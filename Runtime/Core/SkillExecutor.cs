// Assets/GGemCo/Skills/Runtime/Core/SkillExecutor.cs
using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임에서 Timeline 없이 SkillDefinition(SO)의 Baked EventSequence만 재생한다.
    /// 애니메이션은 클립 이름 규칙 + Playables로 재생(Animator 파라미터 미사용).
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

        private readonly List<GameObject> _targets = new(16);

        private SkillRun _current;

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

        public bool TryUse(SkillDefinition skill, SkillTargetContext targetCtx)
        {
            if (skill == null) return false;
            if (_current != null) return false; // MVP: 동시에 하나만. (확장: 채널/우선순위)

            _current = new SkillRun(this, skill, targetCtx);
            _current.Start();
            return true;
        }

        private sealed class SkillRun
        {
            private readonly SkillExecutor _owner;
            private readonly SkillDefinition _skill;
            private readonly SkillTargetContext _ctx;

            private float _time;
            private int _nextKeyframeIndex;

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
            
            public bool IsDone { get; private set; }

            public SkillRun(SkillExecutor owner, SkillDefinition skill, SkillTargetContext ctx)
            {
                _owner = owner;
                _skill = skill;
                _ctx = ctx;
            }

            public void Start()
            {
                _time = 0f;
                _nextKeyframeIndex = 0;
                _castElapsed = 0f;
                _snapshotCasterPos = _ctx.caster != null ? _ctx.caster.transform.position : Vector3.zero;
                _snapshotTargetPos = _ctx.lockedTarget != null ? _ctx.lockedTarget.transform.position : Vector3.zero;
                _snapshotGroundPoint = _ctx.groundPoint;
                
                // 캐스팅이 있으면 캐스팅부터, 아니면 즉시 Use로
                if (_skill.castTimeSeconds > 0f)
                {
                    PlayCastStart();
                }
                else
                {
                    PlayUse();
                }
            }

            public void Tick(float dt)
            {
                if (IsDone) return;

                _time += dt;

                // 캐스팅 진행
                if (_skill.castTimeSeconds > 0f && !_didUse)
                {
                    _castElapsed += dt;

                    if (!_didCastLoop && _castElapsed >= 0.01f)
                        PlayCastLoop();

                    if (_castElapsed >= _skill.castTimeSeconds)
                    {
                        PlayCastEnd();
                        PlayUse();
                    }
                }

                // 이벤트 시퀀스 재생 (Use가 시작된 시점부터 재생하고 싶다면, offset을 둘 수도 있음)
                var seq = _skill.eventSequence;
                if (seq != null && seq.keyframes != null)
                {
                    while (_nextKeyframeIndex < seq.keyframes.Count)
                    {
                        var kf = seq.keyframes[_nextKeyframeIndex];
                        if (_time + 1e-6f < kf.time) break;

                        ExecuteKeyframe(kf);
                        _nextKeyframeIndex++;
                    }

                    if (_time >= seq.totalDuration && _nextKeyframeIndex >= seq.keyframes.Count)
                    {
                        IsDone = true;
                    }
                }
                else
                {
                    // 시퀀스가 없으면 Use 클립 길이 정도로 종료(간단 정책)
                    IsDone = _time >= 0.3f && _didUse;
                }
            }

            private void PlayCastStart()
            {
                if (_didCastStart) return;
                _didCastStart = true;

                if (_owner.TryResolveClip(_skill.castStartClipName, out var clip))
                {
                    _owner.animationPlayer.PlayOneShot(clip, 0.05f);
                }
            }

            private void PlayCastLoop()
            {
                if (_didCastLoop) return;
                _didCastLoop = true;

                if (_owner.TryResolveClip(_skill.castLoopClipName, out var clip))
                {
                    _castLoopHandle = _owner.animationPlayer.PlayLoop(clip, 0.05f);
                }
            }

            private void PlayCastEnd()
            {
                if (_didCastEnd) return;
                _didCastEnd = true;

                if (_castLoopHandle.IsValid) _castLoopHandle.Stop(0.05f);

                if (_owner.TryResolveClip(_skill.castEndClipName, out var clip))
                {
                    _owner.animationPlayer.PlayOneShot(clip, 0.05f);
                }
            }

            private void PlayUse()
            {
                if (_didUse) return;
                _didUse = true;

                if (_owner.TryResolveClip(_skill.useClipName, out var clip))
                {
                    _owner.animationPlayer.PlayOneShot(clip, 0.03f);
                }
            }

            private void ExecuteKeyframe(SkillEventKeyframe kf)
            {
                switch (kf.type)
                {
                    case SkillEventType.Damage:
                        _owner.HandleDamage(_skill, _ctx, kf.payload, _snapshotCasterPos, _snapshotTargetPos, _snapshotGroundPoint);
                        break;
                    case SkillEventType.SpawnEffect:
                        _owner.HandleEffect(_skill, _ctx, kf.payload, _snapshotCasterPos, _snapshotTargetPos, _snapshotGroundPoint);
                        break;
                    case SkillEventType.ApplyStatus:
                        _owner.HandleApplyStatus(_skill, _ctx, kf.payload, _snapshotCasterPos, _snapshotTargetPos, _snapshotGroundPoint);
                        break;
                    default:
                        // 확장 이벤트는 여기에 라우팅
                        break;
                }
            }
        }

        private bool TryResolveClip(string clipName, out AnimationClip clip)
        {
            clip = null;
            if (clipLibrary == null) return false;
            return clipLibrary.TryGetClip(clipName, out clip);
        }

        private void HandleDamage(SkillDefinition skill, SkillTargetContext ctx, UnityEngine.Object payload,
            Vector3 snapshotCasterPos, Vector3 snapshotTargetPos, Vector3 snapshotGroundPoint)
        {
            var def = payload as DamageEventDefinition;
            if (def == null) return;

            ResolveEventTargeting(
                skill, ctx, def.targetingOverride,
                snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint,
                out var resolvedCtx, out var resolvedRange, out var resolvedMaxTargets, out var center);

            // Area 선택(스킬 기본 or 이벤트 오버라이드)
            var areaId = (def.areaOverride.enabled && !string.IsNullOrWhiteSpace(def.areaOverride.areaId))
                ? def.areaOverride.areaId
                : skill.defaultAreaId;

            if (areaRegistry == null || !areaRegistry.TryGet(areaId, out var areaDef) || areaDef == null)
                return;

            // scale 적용(복제 없이 런타임 가산)
            float scale = (def.areaOverride.enabled && def.areaOverride.scale > 0f) ? def.areaOverride.scale : 1f;

            // 단순 스케일: Circle radius / Box width,length / Cone length,angle 등(정교화 가능)
            // 여기서는 runtime용 임시 스케일을 적용하기 위해 "가상 파라미터"로만 처리(복제 X)
            // 필요 시 AreaDefinitionRuntimeView 구조체로 분리 권장.
            var forward = resolvedCtx.forward;

            // 후보 타겟 평가
            _hitEvaluator.EvaluateTargets(
                center,
                forward,
                MakeScaledArea(areaDef, scale),
                resolvedRange,
                resolvedMaxTargets,
                resolvedCtx.caster,
                _targets);

            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                if (t == null) continue;
                Debug.Log(
                    $"[Skill] Damage: skill={skill.skillId}, target={t.name}, model={def.damageModelId}, mul={def.multiplier}");
            }
        }

        private static AreaDefinition MakeScaledArea(AreaDefinition src, float scale)
        {
            if (Mathf.Approximately(scale, 1f)) return src;

            // 런타임에서 ScriptableObject를 변경하면 안 되므로 "임시 복제" 대신
            // 평가 함수가 파라미터를 따로 받는 구조가 더 좋습니다.
            // MVP 편의상 'hidden clone'을 만들지 않고, scale=1만 허용해도 됩니다.
            // 여기서는 안전을 위해 scale을 무시하고 src 반환(권장: 추후 AreaRuntimeParam로 개선).
            return src;
        }

        private void HandleEffect(SkillDefinition skill, SkillTargetContext ctx, UnityEngine.Object payload,
            Vector3 snapshotCasterPos, Vector3 snapshotTargetPos, Vector3 snapshotGroundPoint)
        {
            var def = payload as EffectEventDefinition;
            if (def == null) return;

            ResolveEventTargeting(
                skill, ctx, def.targetingOverride,
                snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint,
                out var resolvedCtx, out _, out _, out var center);

            var caster = resolvedCtx.caster;
            var target = resolvedCtx.lockedTarget;

            // center 기준으로 스폰(attachToTarget이면 target에 부착)
            _effects.Play(def, caster, target, center);
        }

        private void HandleApplyStatus(SkillDefinition skill, SkillTargetContext ctx, UnityEngine.Object payload,
            Vector3 snapshotCasterPos, Vector3 snapshotTargetPos, Vector3 snapshotGroundPoint)
        {
            var def = payload as ApplyStatusEventDefinition;
            if (def == null) return;

            if (StatusSystem == null)
            {
                Debug.LogWarning("[Skill] StatusSystem is null. ApplyStatus ignored.");
                return;
            }

            ResolveEventTargeting(
                skill, ctx, def.targetingOverride,
                snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint,
                out var resolvedCtx, out var resolvedRange, out var resolvedMaxTargets, out var center);

            // 단일 타겟(락온) 또는 범위 적용(오버라이드 enabled이면 areaOverride로 판단)
            if (def.areaOverride.enabled)
            {
                var areaId = !string.IsNullOrWhiteSpace(def.areaOverride.areaId) ? def.areaOverride.areaId : skill.defaultAreaId;
                if (areaRegistry == null || !areaRegistry.TryGet(areaId, out var areaDef) || areaDef == null)
                    return;

                _hitEvaluator.EvaluateTargets(
                    center, resolvedCtx.forward, areaDef,
                    resolvedRange, resolvedMaxTargets,
                    resolvedCtx.caster, _targets);

                for (int i = 0; i < _targets.Count; i++)
                {
                    var t = _targets[i];
                    if (t == null) continue;
                    StatusSystem.Apply(resolvedCtx.caster, t, def.statusId, def.stacks, def.durationOverrideSeconds, def.chance01);
                }
            }
            else
            {
                // 기본: lockedTarget에만
                var target = resolvedCtx.lockedTarget;
                if (target != null)
                    StatusSystem.Apply(resolvedCtx.caster, target, def.statusId, def.stacks, def.durationOverrideSeconds, def.chance01);
            }
        }

        // SkillExecutor.cs 내부(메서드)
        private void ResolveEventTargeting(
            SkillDefinition skill,
            SkillTargetContext baseCtx,
            TargetingOverride ov,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            out SkillTargetContext resolvedCtx,
            out float resolvedRange,
            out int resolvedMaxTargets,
            out Vector3 resolvedCenter)
        {
            resolvedCtx = baseCtx;

            // 기본값
            var mode = skill.targetingMode;
            resolvedRange = skill.range;
            resolvedMaxTargets = skill.maxTargets;

            if (ov.enabled)
            {
                mode = ov.mode;
                if (ov.rangeOverride > 0f) resolvedRange = ov.rangeOverride;
                if (ov.maxTargetsOverride > 0) resolvedMaxTargets = ov.maxTargetsOverride;
            }

            // 중심점 계산 정책
            bool useSnapshot = ov.enabled && ov.useSnapshotCenter;
            bool follow = ov.enabled && ov.followTarget;

            var caster = baseCtx.caster;
            var target = baseCtx.lockedTarget;

            Vector3 casterPosNow = caster != null ? caster.transform.position : snapshotCasterPos;
            Vector3 targetPosNow = target != null ? target.transform.position : snapshotTargetPos;

            // 모드별 center 결정
            switch (mode)
            {
                case SkillTargetingMode.LockOnGuaranteedHit:
                    resolvedCenter = useSnapshot ? snapshotTargetPos : targetPosNow;
                    break;

                case SkillTargetingMode.TargetCenteredArea:
                    resolvedCenter = useSnapshot ? snapshotTargetPos : targetPosNow;
                    break;

                case SkillTargetingMode.FollowTargetArea:
                    if (follow && target != null) resolvedCenter = target.transform.position;
                    else resolvedCenter = useSnapshot ? snapshotTargetPos : targetPosNow;
                    break;

                case SkillTargetingMode.GroundTarget:
                    resolvedCenter = useSnapshot ? snapshotGroundPoint : baseCtx.groundPoint;
                    break;

                case SkillTargetingMode.ForwardDirectional:
                    resolvedCenter = useSnapshot ? snapshotCasterPos : casterPosNow;
                    break;

                default:
                    resolvedCenter = useSnapshot ? snapshotCasterPos : casterPosNow;
                    break;
            }

            // resolvedCtx에 mode를 직접 넣지는 않지만, 필요하면 SkillTargetContext 확장(추후) 가능
        }

    }
}
