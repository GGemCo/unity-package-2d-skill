# Skill ReferenceSnippets

작성일: 2026-02-18

목적:
- Skill 패키지에서 타임라인 이벤트 확장을 **SkillLungeClip(3단 구조)** 스타일로 자동 복제

우선순위:
1) `Docs/ReferenceSnippets.md`
2) `Docs/STYLE_CONTRACT.md`
3) `Docs/GOLDEN_REFERENCES.md`
4) `Docs/GGemCoPatterns/*`
5) Skill `CONVENTIONS/ARCHITECTURE/PLAYBOOK`

---

## EventDefinition(이동 이벤트 데이터)

- 경로: `EventDefinition/LungeEventDefinition.cs`
- 포인트:
  - 디자이너 입력: 거리/시간/Easing 우선
  - 입력 검증(범위/필수) 포함
  - 런타임 Executor가 이 데이터를 소비

```csharp
    /// - 스킬 타임라인(이벤트 구간)과 이동 구간을 정밀하게 동기화하기 위한 Payload 입니다.
    /// - Speed 기반이 아니라 "거리(Distance)" 기반으로 설계하여, 클립 시간에 따라 일관된 이동감을 제공합니다.
    /// </summary>
    public sealed class LungeEventDefinition : ScriptableObject
    {
        [Header("Motion")]
        [Tooltip("이 이벤트(클립) 구간 동안 이동할 총 거리(월드 단위)")]
        public float distance = 2.5f;

        [Tooltip("지속시간 오버라이드(<=0 이면 이벤트 구간(Start~End)을 사용)")]
        public float durationOverrideSeconds = -1f;

        [Tooltip("시간→진행률 Easing (Core의 Easing 클래스를 사용)")]
        public Easing.EaseType easing = Easing.EaseType.Linear;

        [Header("Direction")]
        [Tooltip("true면 스킬 발동 시점의 전방(캐스터 스냅샷)을 사용합니다.")]
        public bool useSnapshotForward = true;

        [Tooltip("Kinematic이면 MovePosition 기반 이동을 사용합니다.")]
        public bool useMovePosition = true;

        [Tooltip("종료 시 정지(velocity 기반 구현에서 유효)")]
        public bool stopAtEnd = true;
    }
}
```
## Runtime Executor(이벤트 실행 흐름)

- 경로: `Core/SkillExecutor.cs`
- 포인트:
  - 타임라인 이벤트 실행의 표준 진입점
  - 캔슬/중단 시 정리 포함
  - 이동/Projectile/Effect 등은 Executor에서 수행

```csharp
    /// - 애니메이션: 클립 이름 규칙 + Playables(Animator 파라미터 미사용)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillExecutor : MonoBehaviour
    {
        [Header("Hit Evaluator")]
        [SerializeField] private LayerMask hitMask = ~0;

        private IHitEvaluator _hitEvaluator;

        private SkillRun _current;
        public bool IsBusy => _current != null;

        private void Awake()
        {
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

            _current = new SkillRun(this, skill, targetCtx,
                ResolveAnimController(targetCtx.caster),
                ResolveActionController(targetCtx.caster));
            _current.Start();
            return true;
        }

        public void ExecuteEvent(
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
                case ConfigCommonSkill.SkillEventType.Lunge:
                    float lungeDuration = Mathf.Max(0f, e.EndTime - e.StartTime);
                    HandleLunge(ctx, payload, lungeDuration);
                    break;
                default:
                    break;
            }
        }

        
        private void HandleLunge(
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            if (payloadObj is not LungeEventDefinition def) return;
            if (ctx.caster == null) return;

            // 모션 컨트롤러는 캐릭터(플레이어/몬스터) 공용 컴포넌트에서 제공한다.
            var motion = ctx.caster.GetComponentInParent<ICharacterMotionController>();
            if (motion == null) return;

            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : eventDurationSeconds;
            if (duration <= 0f) return;

            // 2D 기준 방향 보정
            Vector3 fwd3 = def.useSnapshotForward ? ctx.forward : (ctx.forward);
            Vector2 dir2 = new Vector2(fwd3.x, fwd3.y);

            // ctx.forward가 기본값(Vector3.forward)인 경우(2D) localScale.x 기반으로 보정
            if (dir2.sqrMagnitude < 1e-6f || Mathf.Abs(fwd3.z) > 0.5f)
            {
                float sign = Mathf.Sign(ctx.caster.transform.localScale.x);
                if (Mathf.Approximately(sign, 0f)) sign = 1f;
                dir2 = new Vector2(sign, 0f);
            }

            var req = new LungeRequest(dir2, duration, def.distance, def.easing, def.stopAtEnd, def.useMovePosition);
            motion.TryStartLunge(in req);
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
            }
        }

        private void HandleEffect(
```
## SkillRun(실행 컨텍스트/상태)

- 경로: `Core/SkillRun.cs`
- 포인트:
  - 스킬 실행 중 상태/컨텍스트 보관
  - 테스트 모드 격리/원상복구 훅을 붙일 지점

```csharp

namespace GGemCo2DSkill
{
    public sealed class SkillRun
    {
        private readonly SkillExecutor _owner;
        private readonly StruckTableSkill _skill;
        private readonly SkillTargetContext _ctx;

        private readonly ICharacterAnimationController _animController;
        private readonly ICharacterActionController _actionController;
        private readonly ICharacterMotionController _motionController;

        private SkillRuntimeSequence _sequence;
        private float _time;
        private int _nextEventIndex;

        // Casting
        private bool _didCastStart;
        private bool _didCastLoop;
        private bool _didCastEnd;
        private bool _didUse;

        private float _castElapsed;
        private Vector3 _snapshotCasterPos;
        private Vector3 _snapshotTargetPos;
        private Vector3 _snapshotGroundPoint;

        private bool _isLoading;
        private bool _isEnded;

        public bool IsDone { get; private set; }

        public SkillRun(SkillExecutor owner, StruckTableSkill skill, SkillTargetContext ctx,
            ICharacterAnimationController animController,
            ICharacterActionController actionController)
        {
            _owner = owner;
            _skill = skill;
            _ctx = ctx;
            _animController = animController;
            _actionController = actionController;
            _motionController = _ctx.caster != null ? _ctx.caster.GetComponentInParent<ICharacterMotionController>() : null;
        }

        public void Start()
        {
            SnapshotContext();

            ApplyInitialActionState();
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
                    EndRun();
                    return;
                }

                _sequence = await AddressableLoaderSkillRuntimeSequence.LoadAsync(runtimeSequenceKey);
                _nextEventIndex = 0;
                _time = 0f;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                IsDone = true;
                    EndRun();
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
                    ApplyUseActionState();
                    PlayUse();
                }
            }
            else
            {
                if (!_didUse)
                {
                    ApplyUseActionState();
                    PlayUse();
                }
            }

            // 2) 이벤트 시퀀스 재생(Use 시작부터 재생)
            if (_didUse && _sequence != null && _sequence.Events != null)
            {
                while (_nextEventIndex < _sequence.Events.Length)
                {
                    var ev = _sequence.Events[_nextEventIndex];
                    if (_time + 1e-6f < ev.StartTime) break;

                    _owner.ExecuteEvent(_skill, _ctx, _sequence, ev, _snapshotCasterPos, _snapshotTargetPos,
                        _snapshotGroundPoint);
                    _nextEventIndex++;
                }
                _time += dt;

                if (_time >= _sequence.Duration && _nextEventIndex >= _sequence.Events.Length)
                {
                    IsDone = true;
                    EndRun();
                }
            }
            else if (_didUse)
            {
                // 시퀀스가 없으면 Use 클립 길이 정도로 종료(간단 정책)
                _time += dt;
                IsDone = _time >= 0.3f;
                if (IsDone) EndRun();
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

            // Core 애니메이션 컨트롤러를 우선 사용
            if (GcLogger.IsNull(_animController, nameof(_animController))) return;

            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.CastingStart,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(_skill.CastStartClip) ? null : _skill.CastStartClip));
        }

        private void PlayCastLoop()
        {
            if (_didCastLoop) return;
            _didCastLoop = true;

            if (GcLogger.IsNull(_animController, nameof(_animController))) return;
            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.CastingLoop,
                loop: true,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(_skill.CastLoopClip) ? null : _skill.CastLoopClip));
        }

        private void PlayCastEnd()
        {
            if (_didCastEnd) return;
            _didCastEnd = true;

            if (GcLogger.IsNull(_animController, nameof(_animController))) return;
            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.CastingEnd,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(_skill.CastEndClip) ? null : _skill.CastEndClip));
        }

        private void PlayUse()
        {
            if (_didUse) return;
            _didUse = true;

            // Use 시작 기준으로 이벤트 타임라인 리셋
            _time = 0f;
            _nextEventIndex = 0;
            
            if (_actionController != null)
            {
                _actionController.RequestAction(new CharacterActionRequest(
                    CharacterConstants.CharacterStatus.UseSkill));
            }
            
            if (GcLogger.IsNull(_animController, nameof(_animController))) return;
            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.Action,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(_skill.UseClip) ? null : _skill.UseClip));
        }
        /// <summary>
        /// 스킬 런 시작 시점에 적용할 초기 액션 상태를 설정합니다.
        /// (캐스팅 유무에 따라 CastingSkill 또는 UseSkill)
        /// </summary>
```
## Clip(Authoring: 타임라인에서 편집 가능한 이벤트)

- 경로: `GGemCoTool/CreateSkill/Timeline/Clips/SkillLungeClip.cs`
- 포인트:
  - Definition과 1:1로 연결되는 Clip 형태
  - Inspector 노출(툴팁/가이드) 방식 유지

```csharp
    /// - Speed가 아니라 "거리(Distance)" 기반으로 설계한다.
    /// </summary>
    [Serializable]
    public sealed class SkillLungeClip : SkillEventClipBase
    {
        [SerializeField] private float distance = 2.5f;
        [SerializeField] private float durationOverrideSeconds = 0f;
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;
        [SerializeField] private bool stopAtEnd = true;
        [SerializeField] private bool useMovePosition = true;
        [SerializeField] private bool useSnapshotForward = true;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Lunge;

        public float Distance => distance;
        public float DurationOverrideSeconds => durationOverrideSeconds;
        public Easing.EaseType Easing => easing;
        public bool StopAtEnd => stopAtEnd;
        public bool UseMovePosition => useMovePosition;
        public bool UseSnapshotForward => useSnapshotForward;
    }
}
```
## Tooling(제작/테스트 툴 진입점)

- 경로: `GGemCoTool/CreateSkill/CreateSkillWindow.cs`
- 포인트:
  - 새 이벤트 추가 시 툴에서 노출/검증이 가능해야 함
  - 리로드/테스트 실행 흐름 참고

```csharp
using System;
using System.IO;
using System.Linq;
using Config;
using GGemCo2DSkill;
using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 제작 툴(V2)
    /// - SSOT: skill 테이블
    /// - Marker 미사용, 이벤트 클립 기반
    /// - Bake 결과는 SkillRuntimeSequence(Addressables)로 저장
    /// </summary>
    public sealed class CreateSkillWindow : EditorWindow
    {
        private const string Title = "Skill Authoring V2";

        // Skill.txt canonical column order (TableSkill 기준)
        private static readonly string[] SkillTableHeaders =
        {
            "Uid","Name","Memo","IconFileName","CastTime","CoolTime","TargetingMode","Range","MaxTargets",
            "CastStartClip","CastLoopClip","CastEndClip","UseClip"
        };

        [MenuItem(ConfigEditorSkill.NameToolSettingTestSkill, false, (int)ConfigEditorSkill.ToolOrdering.SettingTestSkill)]
        public static void Open()
        {
            var w = GetWindow<CreateSkillWindow>();
            w.titleContent = new GUIContent(Title);
            w.minSize = new Vector2(700, 260);
        }

        // Skill selection (SearchableDropdownUtility)
        private Button _btnSelectSkill;
        private Label _labelSelectedSkill;

        // Editing UI
        private ScrollView _rightScroll;
        private VisualElement _editRoot;

        private IntegerField _fUid;
        private TextField _fName;
        private TextField _fMemo;
        private TextField _fIconFileName;
        private FloatField _fCastTime;
        private FloatField _fCoolTime;
        private EnumField _fTargetingMode;
        private FloatField _fRange;
        private IntegerField _fMaxTargets;
        private TextField _fDefaultAreaId;
        private TextField _fCastStartClip;
        private TextField _fCastLoopClip;
        private TextField _fCastEndClip;
        private TextField _fUseClip;

        private Button _btnRevert;
        private Button _btnApplyTest;
        private Button _btnSaveTable;

        // PlayMode Test
        private DropdownField _monsterDropdown;
        private Button _btnSpawnMonster;
        private Toggle _toggleAutoResetMonster;
        private Button _btnCaptureMonsterOrigin;
        private Button _btnResetMonsterOrigin;

        private readonly System.Collections.Generic.List<string> _monsterNames = new();
        private readonly System.Collections.Generic.List<int> _monsterUids = new();
        private int _selectedMonsterIndex;

        private Button _btnUseSkill;
        private HelpBox _playModeHelp;

        private ObjectField _timelineField;
        private Button _resolveTimelineByKey;
        private Button _registerTimelineKey;

        private Button _bakeButton;

        private Toggle _forceReload;

        private TableSkill _tableSkill;
        private System.Collections.Generic.List<StruckTableSkill> _skillListSource;

        private StruckTableSkill _selectedSkill;
        private StruckTableSkill _cachedSkillOriginal;
        private StruckTableSkill _editingSkill;
        private bool _editingDirty;

        private bool _lastPlayModeState;

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            var top = new Toolbar();
            _forceReload = new Toggle("ForceReload") { value = false };
            top.Add(_forceReload);

            var reload = new Button(LoadSkills) { text = "Reload" };
            top.Add(reload);
            rootVisualElement.Add(top);

            // Skill select bar (top)
            var selectBar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    // alignItems = Align.Center,
                    marginTop = 6,
                    marginBottom = 6,
                    flexGrow = 1,
                }
            };
            // PlayMode 테스트 UI
            _playModeHelp = new HelpBox(
                "Play Mode에서만 동작합니다. '스킬 사용하기'는 현재 Input Field 값 + (선택 시) Timeline 이벤트를 사용해 실행합니다.\n" +
                "- TimelineAsset이 지정되어 있으면, 런타임 시퀀스를 메모리에서 Bake하여 Addressables 로딩을 우회합니다.",
                HelpBoxMessageType.Info);
            selectBar.Add(_playModeHelp);

            _btnSelectSkill = new Button(OpenSkillSearchDropdown)
            {
                text = "스킬 선택",
                style = { marginRight = 8 }
            };
            selectBar.Add(_btnSelectSkill);

            _labelSelectedSkill = new Label("선택된 스킬: (없음)")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexGrow = 1
                }
            };
            selectBar.Add(_labelSelectedSkill);

            rootVisualElement.Add(selectBar);

            _rightScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1,
                }
            };

            _editRoot = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1,
                }
            };
            _rightScroll.Add(_editRoot);

            _timelineField = new ObjectField("TimelineAsset") { objectType = typeof(TimelineAsset) };
            _editRoot.Add(_timelineField);

            BuildEditFields();

            var editButtons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            _btnRevert = new Button(RevertEdits) { text = "되돌리기", style = { marginRight = 6 } };
            _btnApplyTest = new Button(ApplyTestEdits) { text = "테스트 적용하기", style = { marginRight = 6 } };
            _btnSaveTable = new Button(SaveEditsToSkillTxt) { text = "저장하기(skill.txt)" };
            editButtons.Add(_btnRevert);
            editButtons.Add(_btnApplyTest);
            editButtons.Add(_btnSaveTable);
            _editRoot.Add(editButtons);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _resolveTimelineByKey = new Button(ResolveTimelineByKey) { text = "Resolve Timeline by Key", style = { marginRight = 6 } };
            _registerTimelineKey = new Button(RegisterTimelineKey) { text = "Register Timeline(Key)", style = { marginRight = 6 } };
            row.Add(_resolveTimelineByKey);
            row.Add(_registerTimelineKey);
            _editRoot.Add(row);

            _bakeButton = new Button(BakeRuntimeSequence) { text = "Bake RuntimeSequence + Register Addressables" };
            _editRoot.Add(_bakeButton);

            BuildPlayModeMonsterUI();

            _btnUseSkill = new Button(UseSkillInPlayMode) { text = "스킬 사용하기(PlayMode)" };
            _editRoot.Add(_btnUseSkill);
```
