using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 1회 실행의 런타임 상태를 관리합니다.
    /// </summary>
    public sealed class SkillRun
    {
        private readonly SkillExecutor _owner;
        /// <summary>
        /// 스킬 UID 기반 Runtime Temp HP 이벤트 source key와 실행 옵션 source key가 충돌하지 않도록 분리하는 오프셋입니다.
        /// </summary>
        private const int ExecutionOptionRuntimeTempHpSourceKeyOffset = 1_000_000;

        private readonly RuntimeSkillDefinition _skill;
        private readonly SkillTargetContext _ctx;

        private readonly ICharacterMotionController _motionController;
        private readonly Rigidbody2D _casterRigidbody2D;
        private readonly SkillRunChargeController _chargeController;
        private readonly SkillRunPlaybackController _playbackController;
        private readonly List<SkillBundleRuntimeEntry> _additionalBundleEntries;

        private Vector3 _snapshotCasterPos;
        private Vector3 _snapshotTargetPos;
        private Vector3 _snapshotGroundPoint;
        private readonly Dictionary<string, SkillPositionAnchorSnapshot> _positionAnchors = new(StringComparer.Ordinal);

        private bool _isLoading;
        private bool _isEnded;
        private bool _isPositionHoldActive;
        private bool _keepPositionHoldUntilSkillEnd;
        private bool _isMovementControlLockActive;
        private bool _keepMovementControlLockUntilSkillEnd;
        private float _movementControlLockRemainingSeconds;
        private CharacterBase _movementControlLockCharacter;
        private IAutoMoveSuspendService _movementControlLockAutoMoveSuspendService;
        private object _movementControlLockToken;
        private object _movementOnlyLockToken;
        private AutoMoveSuspendToken _movementControlLockAutoMoveToken;

        public bool IsDone { get; private set; }
        public int SkillUid => _skill != null ? _skill.Uid : 0;
        public GameObject Caster => _ctx.caster;

        /// <summary>
        /// 현재 스킬이 차징 단계에 머무르고 있는지 여부입니다.
        /// </summary>
        public bool IsCharging => !IsDone && _chargeController.IsCharging;

        /// <summary>
        /// Use 애니메이션 기준으로 현재 스킬 이벤트 시퀀스가 진행된 시간입니다.
        /// </summary>
        public float CurrentTime => _playbackController.CurrentTime;

        /// <summary>
        /// 스킬 1회 실행 상태를 생성하고 실행에 필요한 캐릭터 컨트롤러와 차징 컨트롤러를 연결합니다.
        /// </summary>
        /// <param name="owner">스킬 실행을 소유하고 이벤트 실행과 종료 알림을 담당하는 실행기입니다.</param>
        /// <param name="skill">실행할 런타임 스킬 정의입니다.</param>
        /// <param name="ctx">캐스터, 타겟, 지면 좌표 등을 담은 실행 컨텍스트입니다.</param>
        /// <param name="animController">스킬 단계별 애니메이션을 재생할 컨트롤러입니다.</param>
        /// <param name="actionController">스킬 사용 중 캐릭터 액션 상태를 고정할 컨트롤러입니다.</param>
        public SkillRun(SkillExecutor owner, RuntimeSkillDefinition skill, SkillTargetContext ctx,
            ICharacterAnimationController animController,
            ICharacterActionController actionController)
            : this(owner, skill, ctx, animController, actionController, null)
        {
        }

        /// <summary>
        /// 스킬 1회 실행 상태를 생성하고, 대표 스킬 애니메이션과 함께 실행할 추가 스킬 시퀀스를 연결합니다.
        /// </summary>
        /// <param name="owner">스킬 실행을 소유하고 이벤트 실행과 종료 알림을 담당하는 실행기입니다.</param>
        /// <param name="skill">대표 애니메이션과 기본 실행 상태를 담당할 스킬 정의입니다.</param>
        /// <param name="ctx">대표 스킬 실행 컨텍스트입니다.</param>
        /// <param name="animController">스킬 단계별 애니메이션을 재생할 컨트롤러입니다.</param>
        /// <param name="actionController">스킬 사용 중 캐릭터 액션 상태를 고정할 컨트롤러입니다.</param>
        /// <param name="additionalBundleEntries">대표 스킬과 동시에 이벤트를 실행할 추가 스킬 목록입니다.</param>
        internal SkillRun(
            SkillExecutor owner,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            ICharacterAnimationController animController,
            ICharacterActionController actionController,
            IReadOnlyList<SkillBundleRuntimeEntry> additionalBundleEntries)
        {
            _owner = owner;
            _skill = skill;
            _ctx = ctx;
            _additionalBundleEntries = additionalBundleEntries != null
                ? new List<SkillBundleRuntimeEntry>(additionalBundleEntries)
                : new List<SkillBundleRuntimeEntry>();
            _motionController = _ctx.caster != null ? _ctx.caster.GetComponentInParent<ICharacterMotionController>() : null;
            _casterRigidbody2D = _ctx.caster != null ? _ctx.caster.GetComponentInParent<Rigidbody2D>() : null;
            _chargeController = new SkillRunChargeController(
                _owner,
                this,
                _skill,
                _ctx,
                animController,
                () => _snapshotCasterPos);
            _playbackController = new SkillRunPlaybackController(
                _owner,
                this,
                _skill,
                _ctx,
                animController,
                actionController,
                () => _snapshotCasterPos,
                () => _snapshotTargetPos,
                () => _snapshotGroundPoint);
        }

        /// <summary>
        /// 현재 스킬 정의가 차징 단계를 사용하는지 여부입니다.
        /// </summary>
        private bool HasCharge => _chargeController.HasCharge;

        /// <summary>
        /// 같은 스킬 실행 안에서 이후 이벤트가 참조할 수 있도록 위치 앵커를 저장합니다.
        /// </summary>
        /// <param name="key">위치 앵커를 식별할 키입니다.</param>
        /// <param name="snapshot">저장할 위치 스냅샷입니다.</param>
        public void SavePositionAnchor(string key, SkillPositionAnchorSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            _positionAnchors[key.Trim()] = snapshot;
        }

        /// <summary>
        /// 같은 스킬 실행 안에서 이전 이벤트가 저장한 위치 앵커를 조회합니다.
        /// </summary>
        /// <param name="key">조회할 위치 앵커 키입니다.</param>
        /// <param name="snapshot">조회된 위치 스냅샷입니다.</param>
        /// <returns>위치 앵커를 찾았으면 <see langword="true"/>입니다.</returns>
        public bool TryGetPositionAnchor(string key, out SkillPositionAnchorSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            return _positionAnchors.TryGetValue(key.Trim(), out snapshot);
        }

        /// <summary>
        /// 스킬 실행을 시작하고 런타임 시퀀스를 비동기로 로드합니다.
        /// </summary>
        public void Start()
        {
            SnapshotContext();
            ApplyStartExecutionOptions(_skill, _ctx);
            ApplyAdditionalStartExecutionOptions();

            _playbackController.ApplyInitialActionState(HasCharge);
            _isLoading = true;
            _ = LoadSequenceAsync();
        }

        /// <summary>
        /// 스킬 시작 시점에 고정된 실행 옵션 중 즉시 적용해야 하는 효과를 처리합니다.
        /// </summary>
        /// <param name="skill">실행 옵션의 기본 source key를 제공하는 스킬 정의입니다.</param>
        /// <param name="context">즉시 적용 옵션을 포함한 실행 컨텍스트입니다.</param>
        private void ApplyStartExecutionOptions(RuntimeSkillDefinition skill, in SkillTargetContext context)
        {
            if (skill == null || context.executionOptions.RuntimeTempHpOnStart <= 0L || context.caster == null)
                return;

            CharacterBase targetCharacter =
                context.caster.GetComponent<CharacterBase>() ??
                context.caster.GetComponentInParent<CharacterBase>();
            if (targetCharacter == null)
                return;

            int sourceKey = context.executionOptions.RuntimeTempHpSourceKeyOverride != 0
                ? context.executionOptions.RuntimeTempHpSourceKeyOverride
                : ExecutionOptionRuntimeTempHpSourceKeyOffset + skill.Uid;

            targetCharacter.SetRuntimeBonusHpTemp(
                sourceKey,
                context.executionOptions.RuntimeTempHpOnStart,
                fillToMax: true);
        }

        /// <summary>
        /// 묶음 실행에 포함된 추가 스킬의 시작 즉시 실행 옵션을 적용합니다.
        /// </summary>
        private void ApplyAdditionalStartExecutionOptions()
        {
            if (_additionalBundleEntries == null)
                return;

            for (int i = 0; i < _additionalBundleEntries.Count; i++)
            {
                SkillBundleRuntimeEntry entry = _additionalBundleEntries[i];
                ApplyStartExecutionOptions(entry.Skill, entry.Context);
            }
        }

        /// <summary>
        /// 스킬 소유자 타입에 맞는 RuntimeSequence Addressables 키를 계산하고 비동기로 로드합니다.
        /// </summary>
        private async Task LoadSequenceAsync()
        {
            try
            {
                // RuntimeSequenceKey는 Addressables Key 규칙(ConfigAddressableKeySkill)을 사용한다.
                // (에디터 테스트에서는 SkillRuntimeSequenceRepository.RegisterEditorOverride로 주입 가능)
                var runtimeSequenceKey = ConfigAddressableKeySkill.GetRuntimeSequenceKeyPlayer(_skill.Uid);
                if (_skill.OwnerType == ConfigCommonSkill.SkillOwnerType.Monster)
                {
                    runtimeSequenceKey = ConfigAddressableKeySkill.GetRuntimeSequenceKeyMonster(_skill.Uid);
                }
                
                if (string.IsNullOrEmpty(runtimeSequenceKey))
                {
                    IsDone = true;
                    EndRun();
                    return;
                }

                SkillRuntimeSequence sequence = await LoadRuntimeSequenceAsync(_skill, runtimeSequenceKey);
                _playbackController.SetSequence(sequence);
                _playbackController.SetAdditionalSequences(await LoadAdditionalBundleSequencesAsync());

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


        /// <summary>
        /// 스킬 소유자 타입에 맞는 RuntimeSequence를 로드합니다.
        /// </summary>
        /// <param name="skill">시퀀스를 로드할 스킬 정의입니다.</param>
        /// <param name="runtimeSequenceKey">Addressables RuntimeSequence 키입니다.</param>
        /// <returns>로드된 RuntimeSequence입니다.</returns>
        private static async Task<SkillRuntimeSequence> LoadRuntimeSequenceAsync(
            RuntimeSkillDefinition skill,
            string runtimeSequenceKey)
        {
            if (skill == null || string.IsNullOrEmpty(runtimeSequenceKey))
                return null;

            if (skill.OwnerType == ConfigCommonSkill.SkillOwnerType.Monster)
            {
                return await AddressableLoaderSkillRuntimeSequenceMonster.LoadAsyncMonster(runtimeSequenceKey);
            }

            if (skill.OwnerType == ConfigCommonSkill.SkillOwnerType.Player)
            {
                // 플레이어 시퀀스는 시작 로딩에서 제외될 수 있으므로,
                // 실제 스킬 사용 시점에 필요한 키만 지연 로드합니다.
                var runtimeSequenceLoader = AddressableLoaderSkillRuntimeSequencePlayer.Instance;
                if (runtimeSequenceLoader == null)
                {
                    runtimeSequenceLoader = new GameObject(nameof(AddressableLoaderSkillRuntimeSequencePlayer))
                        .AddComponent<AddressableLoaderSkillRuntimeSequencePlayer>();
                }

                return await runtimeSequenceLoader.LoadByKeyAsync(runtimeSequenceKey);
            }

            return null;
        }

        /// <summary>
        /// 묶음 실행에 포함된 추가 스킬들의 RuntimeSequence를 로드합니다.
        /// </summary>
        /// <returns>추가 스킬 재생 상태 목록입니다.</returns>
        private async Task<List<SkillRunPlaybackSequence>> LoadAdditionalBundleSequencesAsync()
        {
            var sequences = new List<SkillRunPlaybackSequence>();
            if (_additionalBundleEntries == null || _additionalBundleEntries.Count == 0)
                return sequences;

            for (int i = 0; i < _additionalBundleEntries.Count; i++)
            {
                SkillBundleRuntimeEntry entry = _additionalBundleEntries[i];
                RuntimeSkillDefinition skill = entry.Skill;
                if (skill == null)
                    continue;

                string key = skill.OwnerType == ConfigCommonSkill.SkillOwnerType.Monster
                    ? ConfigAddressableKeySkill.GetRuntimeSequenceKeyMonster(skill.Uid)
                    : ConfigAddressableKeySkill.GetRuntimeSequenceKeyPlayer(skill.Uid);
                SkillRuntimeSequence sequence = string.IsNullOrEmpty(key)
                    ? null
                    : await LoadRuntimeSequenceAsync(skill, key);

                sequences.Add(new SkillRunPlaybackSequence(skill, entry.Context, sequence));
            }

            return sequences;
        }

        /// <summary>
        /// 스킬 실행 시간을 진행합니다.
        /// 차징이 있는 스킬은 차징이 완료된 뒤 기존 캐스팅/사용 단계로 진입합니다.
        /// </summary>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        public void Tick(float dt)
        {
            if (IsDone) return;
            if (_isLoading) return;

            TickMovementControlLock(dt);

            if (_chargeController.ShouldBlockSkillFlow)
            {
                _chargeController.Tick(dt);
                FinishIfChargeRequestedRunEnd();
                return;
            }

            _playbackController.Tick(dt);
            FinishIfPlaybackRequestedRunEnd();
        }

        /// <summary>
        /// 피격으로 차징 게이지를 감소시킵니다.
        /// 게이지가 0이 되면 차징 실패 상태로 전환합니다.
        /// </summary>
        /// <param name="reason">피격 또는 취소 사유입니다.</param>
        /// <param name="damageAmount">감소시킬 게이지 값입니다. 0 이하이면 스킬 설정의 기본 감소량을 사용합니다.</param>
        /// <returns>현재 차징 게이지가 피격을 처리했으면 <see langword="true"/>입니다.</returns>
        public bool TryApplyChargeGaugeDamage(SkillCancelReason reason, float damageAmount = 0f)
        {
            bool handled = _chargeController.TryApplyGaugeDamage(reason, damageAmount);
            FinishIfChargeRequestedRunEnd();
            return handled;
        }

        /// <summary>
        /// 차징 컨트롤러가 스킬 종료를 요청한 경우 SkillRun 종료 절차로 연결합니다.
        /// </summary>
        private void FinishIfChargeRequestedRunEnd()
        {
            if (!_chargeController.ShouldEndRun || IsDone)
                return;

            IsDone = true;
            EndRun();
        }

        /// <summary>
        /// 재생 컨트롤러가 스킬 종료를 요청한 경우 SkillRun 종료 절차로 연결합니다.
        /// </summary>
        private void FinishIfPlaybackRequestedRunEnd()
        {
            if (!_playbackController.ShouldEndRun || IsDone)
                return;

            IsDone = true;
            EndRun();
        }

        /// <summary>
        /// 스킬 시작 시점의 캐스터, 락온 타겟, 지면 좌표를 저장하여 이후 이벤트가 동일한 기준점을 사용할 수 있게 합니다.
        /// </summary>
        private void SnapshotContext()
        {
            _snapshotCasterPos = _ctx.caster != null ? _ctx.caster.transform.position : Vector3.zero;
            _snapshotTargetPos = _ctx.lockedTarget != null ? _ctx.lockedTarget.transform.position : _snapshotCasterPos;
            _snapshotGroundPoint = _ctx.groundPoint;
        }

        /// <summary>
        /// 스킬 이벤트가 요청한 위치 고정 모션을 Skill 채널에 시작합니다.
        /// </summary>
        /// <param name="durationSeconds">스킬 종료까지 유지하지 않을 때 적용할 고정 시간입니다.</param>
        /// <param name="keepUntilSkillEnd">스킬 종료 시점까지 위치 고정을 유지할지 여부입니다.</param>
        /// <param name="stopAtEnd">위치 고정 종료 시 속도를 정지할지 여부입니다.</param>
        /// <param name="useMovePosition">Rigidbody2D.MovePosition 기반으로 위치를 유지할지 여부입니다.</param>
        /// <param name="allowReplace">기존 Skill 채널 모션 교체 허용 여부입니다.</param>
        /// <returns>위치 고정 모션을 시작했으면 <see langword="true"/>입니다.</returns>
        public bool TryStartPositionHold(float durationSeconds, bool keepUntilSkillEnd, bool stopAtEnd, bool useMovePosition, bool allowReplace)
        {
            if (_motionController == null || _ctx.caster == null)
                return false;

            // 기존 Skill 채널 모션 잔여 영향 제거
            _motionController.CancelMotion(MotionChannel.Skill, 1001);

            if (_casterRigidbody2D != null)
            {
                _casterRigidbody2D.SetLinearVelocity(Vector2.zero);
            }

            Vector2 holdPos = _casterRigidbody2D != null
                ? _casterRigidbody2D.position
                : (Vector2)_ctx.caster.transform.position;

            var request = new MotionRequest(
                channel: MotionChannel.Skill,
                kind: MotionKind.PositionHold,
                direction: Vector2.right,
                durationSeconds: keepUntilSkillEnd ? 0f : Mathf.Max(0f, durationSeconds),
                distance: 0f,
                easeType: Easing.EaseType.Linear,
                stopAtEnd: stopAtEnd,
                useMovePosition: useMovePosition,
                allowReplace: true,
                startPosition: holdPos,
                targetPosition: holdPos);

            if (_motionController.TryStartMotion(request))
            {
                _isPositionHoldActive = true;
                _keepPositionHoldUntilSkillEnd = keepUntilSkillEnd;
                return true;
            }

            if (_casterRigidbody2D != null)
            {
                _casterRigidbody2D.SetLinearVelocity(Vector2.zero);
            }

            return false;
        }

        /// <summary>
        /// 스킬 이벤트가 요청한 이동 조작 잠금을 시작합니다.
        /// </summary>
        /// <param name="character">잠금을 적용할 캐릭터입니다.</param>
        /// <param name="durationSeconds">스킬 종료까지 유지하지 않을 때 적용할 잠금 시간입니다.</param>
        /// <param name="keepUntilSkillEnd">스킬 종료 시점까지 잠금을 유지할지 여부입니다.</param>
        /// <param name="stopImmediately">시작 시 캐릭터 이동과 속도를 즉시 정지할지 여부입니다.</param>
        /// <param name="cancelSkillMotion">시작 시 Skill 채널 모션을 취소할지 여부입니다.</param>
        /// <param name="controlLockMode">Player 조작을 차단할 범위입니다.</param>
        /// <param name="autoMovePolicy">자동 이동 처리 정책입니다.</param>
        /// <returns>잠금 요청을 적용했으면 <see langword="true"/>입니다.</returns>
        public bool TryStartMovementControlLock(
            CharacterBase character,
            float durationSeconds,
            bool keepUntilSkillEnd,
            bool stopImmediately,
            bool cancelSkillMotion,
            SkillPlayerControlLockMode controlLockMode,
            SkillAutoMoveControlPolicy autoMovePolicy)
        {
            if (character == null)
                return false;

            if (!keepUntilSkillEnd && durationSeconds <= 0f)
                return false;

            ReleaseMovementControlLock();

            if (stopImmediately)
            {
                StopTargetMovementImmediately(character, cancelSkillMotion);
            }

            _movementControlLockCharacter = character;
            _movementControlLockRemainingSeconds = keepUntilSkillEnd ? 0f : Mathf.Max(0f, durationSeconds);
            _keepMovementControlLockUntilSkillEnd = keepUntilSkillEnd;

            ApplyPlayerControlLockMode(character, controlLockMode);

            ApplyAutoMovePolicy(autoMovePolicy);

            _isMovementControlLockActive =
                _movementControlLockToken != null ||
                _movementOnlyLockToken != null ||
                _movementControlLockAutoMoveToken.IsValid ||
                keepUntilSkillEnd ||
                _movementControlLockRemainingSeconds > 0f;
            return true;
        }

        /// <summary>
        /// 이동 조작 잠금의 남은 시간을 갱신하고 만료된 잠금을 해제합니다.
        /// </summary>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        private void TickMovementControlLock(float dt)
        {
            if (!_isMovementControlLockActive || _keepMovementControlLockUntilSkillEnd)
                return;

            _movementControlLockRemainingSeconds -= Mathf.Max(0f, dt);
            if (_movementControlLockRemainingSeconds > 0f)
                return;

            ReleaseMovementControlLock();
        }

        /// <summary>
        /// 현재 스킬 런이 유지 중인 이동 조작 잠금과 자동 이동 일시정지를 해제합니다.
        /// </summary>
        public void ReleaseMovementControlLock()
        {
            if (!_isMovementControlLockActive &&
                _movementControlLockToken == null &&
                _movementOnlyLockToken == null &&
                !_movementControlLockAutoMoveToken.IsValid)
                return;

            if (_movementControlLockCharacter != null && _movementControlLockToken != null)
            {
                _movementControlLockCharacter.ReleaseControlLock(_movementControlLockToken);
            }

            if (_movementControlLockCharacter != null && _movementOnlyLockToken != null)
            {
                _movementControlLockCharacter.ReleaseMovementLock(_movementOnlyLockToken);
            }

            if (_movementControlLockAutoMoveSuspendService != null && _movementControlLockAutoMoveToken.IsValid)
            {
                _movementControlLockAutoMoveSuspendService.ReleaseSuspend(_movementControlLockAutoMoveToken);
            }

            _movementControlLockCharacter = null;
            _movementControlLockAutoMoveSuspendService = null;
            _movementControlLockToken = null;
            _movementOnlyLockToken = null;
            _movementControlLockAutoMoveToken = AutoMoveSuspendToken.None;
            _movementControlLockRemainingSeconds = 0f;
            _isMovementControlLockActive = false;
            _keepMovementControlLockUntilSkillEnd = false;
        }

        /// <summary>
        /// 이벤트 설정에 따라 Player의 이동 전용 잠금 또는 전체 조작 잠금을 획득합니다.
        /// </summary>
        /// <param name="character">잠금을 적용할 Player 캐릭터입니다.</param>
        /// <param name="controlLockMode">Player 조작을 차단할 범위입니다.</param>
        private void ApplyPlayerControlLockMode(CharacterBase character, SkillPlayerControlLockMode controlLockMode)
        {
            if (character == null)
                return;

            switch (controlLockMode)
            {
                case SkillPlayerControlLockMode.MovementOnly:
                    _movementOnlyLockToken = character.AcquireMovementLock(this);
                    break;

                case SkillPlayerControlLockMode.AllControl:
                    _movementControlLockToken = character.AcquireControlLock(this);
                    break;
            }
        }

        /// <summary>
        /// 이벤트 설정에 따라 자동 이동을 일시정지하거나 취소합니다.
        /// </summary>
        /// <param name="autoMovePolicy">자동 이동 처리 정책입니다.</param>
        private void ApplyAutoMovePolicy(SkillAutoMoveControlPolicy autoMovePolicy)
        {
            GameObject targetObject = _movementControlLockCharacter != null
                ? _movementControlLockCharacter.gameObject
                : null;
            if (targetObject == null)
                return;

            switch (autoMovePolicy)
            {
                case SkillAutoMoveControlPolicy.Suspend:
                    _movementControlLockAutoMoveSuspendService =
                        targetObject.GetComponent<IAutoMoveSuspendService>() ??
                        targetObject.GetComponentInParent<IAutoMoveSuspendService>();
                    if (_movementControlLockAutoMoveSuspendService != null)
                    {
                        _movementControlLockAutoMoveToken =
                            _movementControlLockAutoMoveSuspendService.AcquireSuspend(AutoMoveSuspendReason.Skill);
                    }
                    break;

                case SkillAutoMoveControlPolicy.Cancel:
                    PlayerAutoMoveController autoMoveController =
                        targetObject.GetComponent<PlayerAutoMoveController>() ??
                        targetObject.GetComponentInParent<PlayerAutoMoveController>();
                    autoMoveController?.Cancel();
                    break;
            }
        }

        /// <summary>
        /// 대상 캐릭터의 현재 이동 입력, 이동 애니메이션, Rigidbody2D 속도를 즉시 정지합니다.
        /// </summary>
        /// <param name="character">이동을 정지할 캐릭터입니다.</param>
        /// <param name="cancelSkillMotion">Skill 채널 모션까지 함께 취소할지 여부입니다.</param>
        private void StopTargetMovementImmediately(CharacterBase character, bool cancelSkillMotion)
        {
            if (character == null || character.IsStatusDead())
                return;

            character.directionNormalize = Vector2.zero;
            character.Stop(isForce: true);

            if (cancelSkillMotion)
            {
                ICharacterMotionController motionController =
                    character.GetComponent<ICharacterMotionController>() ??
                    character.GetComponentInParent<ICharacterMotionController>();
                motionController?.CancelMotion(MotionChannel.Skill, 2101);
            }

            Rigidbody2D rigidbody2D =
                character.GetComponent<Rigidbody2D>() ??
                character.GetComponentInParent<Rigidbody2D>();
            if (rigidbody2D != null)
            {
                rigidbody2D.SetLinearVelocity(Vector2.zero);
            }
        }

        /// <summary>
        /// 현재 스킬 런이 유지 중인 위치 고정 모션을 해제하고 잔여 속도를 제거합니다.
        /// </summary>
        public void ReleasePositionHold()
        {
            if (!_isPositionHoldActive)
                return;

            _motionController?.CancelMotion(MotionChannel.Skill, 0);

            if (_casterRigidbody2D != null)
            {
                _casterRigidbody2D.SetLinearVelocity(Vector2.zero);
            }

            _isPositionHoldActive = false;
            _keepPositionHoldUntilSkillEnd = false;
        }

        /// <summary>
        /// 스킬 런 종료 처리(상태 해제 + 정리). 중복 호출 방지 포함.
        /// </summary>
        private void EndRun()
        {
            if (_isEnded)
                return;

            _isEnded = true;

            _chargeController.CleanupForRunEnd();

            if (_keepPositionHoldUntilSkillEnd || _isPositionHoldActive)
            {
                ReleasePositionHold();
            }

            if (_keepMovementControlLockUntilSkillEnd || _isMovementControlLockActive)
            {
                ReleaseMovementControlLock();
            }

            _playbackController.CleanupActionState();

            _owner?.NotifyRunEnded(this);
        }
        
        /// <summary>
        /// 외부 요청으로 스킬 런을 취소하고 애니메이션, 액션 상태, 차징 리소스를 정리합니다.
        /// </summary>
        /// <param name="reason">스킬 취소 사유입니다.</param>
        public void Cancel(SkillCancelReason reason)
        {
            if (IsDone) return;

            // 이후 이벤트/캐스팅/차징 진행 차단
            IsDone = true;
            _chargeController.CleanupForRunEnd();

            // 체인 캔슬은 다음 스킬 애니메이션이 같은 프레임에 이어서 재생되므로
            // 대기 애니메이션으로 한 번 복귀시키지 않고 현재 스킬 재생만 끊습니다.
            if (reason != SkillCancelReason.ComboChain && reason != SkillCancelReason.ForcedBySystem)
            {
                _playbackController.StopSkillAnimation();
            }

            // 상태 해제(UseSkill/CastingSkill 등)
            EndRun();
        }
    }
}
