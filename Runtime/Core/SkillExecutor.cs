using System;
using System.Collections;
using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임 스킬 실행을 담당하는 실행기입니다.
    /// 스킬 정의 조회, 이벤트 실행, 피격 판정, 이동, 이펙트, 상태이상 적용, 취소를 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillExecutor : MonoBehaviour
    {
        /// <summary>
        /// 피격 판정에 사용할 레이어 마스크입니다.
        /// </summary>
        [Header("Hit Evaluator")]
        [SerializeField] private LayerMask hitMask = ~0;

        /// <summary>
        /// 범위 기반 타겟 판정을 수행하는 평가기입니다.
        /// </summary>
        private IHitEvaluator _hitEvaluator;

        /// <summary>
        /// 현재 실행 중인 스킬 런타임입니다.
        /// </summary>
        private SkillRun _current;

        /// <summary>
        /// 현재 스킬 실행에서 생성한 취소 가능 VFX를 추적하고 정리합니다.
        /// </summary>
        private readonly SkillOwnedVfxTracker _ownedVfxTracker = new();

        /// <summary>
        /// 런타임 이벤트 타입을 실제 실행 로직으로 전달하는 디스패처입니다.
        /// </summary>
        private readonly SkillEventDispatcher _eventDispatcher = new();

        /// <summary>
        /// 스킬 화면 페이드 재생과 종료 시 정리 정책을 관리합니다.
        /// </summary>
        private readonly SkillScreenFadeController _screenFadeController = new();

        /// <summary>
        /// 스킬 캐릭터 잔상 재생과 종료 시 정리 정책을 관리합니다.
        /// </summary>
        private readonly SkillAfterimageController _afterimageController = new();

        /// <summary>
        /// 그라운드슬램 애니메이션과 공중 대기 후 낙하 전환 상태를 관리합니다.
        /// </summary>
        private readonly SkillGroundSlamAnimationController _groundSlamAnimationController = new();

        /// <summary>
        /// 아크 런지의 단계별 애니메이션 상태를 관리합니다.
        /// </summary>
        private readonly SkillArcLungeAnimationController _arcLungeAnimationController = new();

        /// <summary>
        /// 현재 스킬 실행에서 생성한 더미 캐릭터를 actorKey 기준으로 관리하는 컬렉션입니다.
        /// </summary>
        private readonly Dictionary<string, SkillDummyActorHandle> _dummyActors = new(StringComparer.Ordinal);

        /// <summary>
        /// 캐스터를 더미 액터 참조처럼 다루기 위한 내부 식별 키입니다.
        /// </summary>
        private const string CasterActorKey = "__caster__";

        /// <summary>
        /// Move/Animation 이벤트에서 Caster를 대상으로 선택했을 때 재사용하는 임시 핸들입니다.
        /// 실제 더미 레지스트리에는 등록하지 않습니다.
        /// </summary>
        private readonly SkillDummyActorHandle _casterActorHandle = new()
        {
            ActorKey = CasterActorKey,
        };

        /// <summary>
        /// 현재 스킬 실행 중인지 여부를 반환합니다.
        /// </summary>
        public bool IsBusy => _current != null;

        /// <summary>
        /// 현재 실행 중인 스킬 UID입니다. 실행 중이 아니면 0입니다.
        /// </summary>
        public int CurrentSkillUid => _current != null ? _current.SkillUid : 0;

        /// <summary>
        /// 스킬 실행 종료 리포트가 발생했을 때 외부 어댑터에 알립니다.
        /// </summary>
        public event System.Action<SkillExecutionReport> ExecutionFinished;

        /// <summary>
        /// 현재 스킬의 차징 상태가 변경될 때 UI/디버그 도구에 알립니다.
        /// </summary>
        public event System.Action<SkillChargeSnapshot> ChargeStateChanged;

        private bool _hasPendingFinishReport;
        private SkillExecutionReport _pendingFinishReport;
        private int _executionSequence;
        private readonly SkillAttackSequence _attackSequence = new();
        private CharacterHitStopController _hitStopController;

        /// <summary>
        /// 실행기에 필요한 런타임 의존성을 초기화합니다.
        /// </summary>
        private void Awake()
        {
            _hitEvaluator = new AreaHitEvaluator(hitMask);
            _hitStopController = GetComponent<CharacterHitStopController>();
        }

        private void OnDisable()
        {
            CleanupDummyActors(forceAll: true, forCancel: false);
            ResetCasterActorHandleTransientState(clearCharacter: true);
            CleanupSkillAfterimage(forCancel: false);

            if (_current == null)
                return;

            TryCancel(SkillCancelReason.ForcedBySystem);
        }

        /// <summary>
        /// 현재 실행 중인 스킬 런타임을 프레임 단위로 갱신합니다.
        /// 실행이 완료되면 현재 런타임 참조를 해제합니다.
        /// </summary>
        private void Update()
        {
            if (_hitStopController == null)
            {
                _hitStopController = GetComponent<CharacterHitStopController>();
            }

            if (_hitStopController != null && _hitStopController.IsActive)
                return;

            if (_current != null)
            {
                var run = _current;
                run.Tick(Time.deltaTime);
                if (ReferenceEquals(_current, run) && run.IsDone)
                {
                    NotifyRunEnded(run);
                }
            }

            _groundSlamAnimationController.Tick();
            _arcLungeAnimationController.Tick();
            MaintainDummyAirborneState();
        }

        /// <summary>
        /// 지정한 스킬 UID의 실행을 시도합니다.
        /// 스킬 정의를 조회하고 실행 컨텍스트를 구성한 뒤 새 <see cref="SkillRun"/>을 시작합니다.
        /// </summary>
        /// <param name="skillUid">실행할 스킬의 고유 식별자입니다.</param>
        /// <param name="targetCtx">캐스터, 타겟, 지면 위치, 방향 정보를 포함한 대상 컨텍스트입니다.</param>
        /// <param name="source">스킬 정의를 조회할 테이블 출처입니다.</param>
        /// <returns>스킬 실행이 시작되면 <see langword="true"/>, 실행할 수 없으면 <see langword="false"/>를 반환합니다.</returns>
        public bool TryUse(int skillUid, SkillTargetContext targetCtx, ConfigCommon.SkillTableSource source = ConfigCommon.SkillTableSource.Player)
        {
            if (_current != null) return false;

            if (!SkillDefinitionResolver.TryResolve(skillUid, source, out var skill) || skill == null) return false;

            _ownedVfxTracker.Cleanup();
            CleanupDummyActors(forceAll: true, forCancel: false);
            ResetCasterActorHandleTransientState(clearCharacter: true);
            _attackSequence.Clear();
            _screenFadeController.ResetCleanupFlags();
            _afterimageController.ResetCleanupFlags();
            _groundSlamAnimationController.Clear();
            _arcLungeAnimationController.Clear();

            var motion = SkillCharacterComponentResolver.ResolveMotionController(targetCtx.caster);
            motion?.CancelMotion(MotionChannel.Skill, 2001);

            if (targetCtx.caster != null)
            {
                var rb = targetCtx.caster.GetComponentInParent<Rigidbody2D>();
                if (rb != null)
                    rb.SetLinearVelocity(Vector2.zero);
            }

            _current = new SkillRun(this, skill, targetCtx,
                SkillCharacterComponentResolver.ResolveAnimationController(targetCtx.caster),
                SkillCharacterComponentResolver.ResolveActionController(targetCtx.caster));
            _hasPendingFinishReport = false;
            _current.Start();
            return true;
        }

        /// <summary>
        /// 지정한 AttackId가 실제 데미지 확정 시 다음 스킬 연계를 열 수 있는 이벤트인지 확인합니다.
        /// </summary>
        public bool IsChainUnlockAttack(int attackId)
        {
            return _attackSequence.IsChainUnlockAttack(attackId);
        }

        /// <summary>
        /// 런타임 이벤트 유형에 따라 실제 스킬 효과를 실행합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="sequence">이벤트 페이로드를 제공하는 런타임 시퀀스입니다.</param>
        /// <param name="e">실행할 런타임 이벤트 정보입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        public void ExecuteEvent(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            SkillRuntimeSequence sequence,
            in SkillRuntimeEvent e,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (!CanProcessEvent(run))
                return;

            var eventContext = new SkillEventExecutionContext(
                run,
                skill,
                ctx,
                sequence,
                in e,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint);
            _eventDispatcher.Execute(this, in eventContext);
        }


        /// <summary>
        /// 화면 페이드 이벤트 정의를 Core 공용 화면 페이드 서비스로 전달합니다.
        /// 스킬 종료 또는 취소 시 초기화 정책도 함께 기록합니다.
        /// </summary>
        /// <param name="payloadObj">Bake된 화면 페이드 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        internal void HandleScreenFade(UnityEngine.Object payloadObj, float eventDurationSeconds)
        {
            _screenFadeController.Play(this, payloadObj, eventDurationSeconds);
        }

        /// <summary>
        /// 현재 SkillExecutor가 시작한 화면 페이드를 스킬 종료 사유에 맞게 정리합니다.
        /// </summary>
        /// <param name="forCancel">취소 종료이면 true, 정상 종료이면 false입니다.</param>
        private void CleanupSkillScreenFade(bool forCancel)
        {
            _screenFadeController.Cleanup(this, forCancel);
        }

        /// <summary>
        /// 스킬 화면 페이드 정리 예약 상태를 초기화합니다.
        /// </summary>
        private void ResetSkillScreenFadeCleanupFlags()
        {
            _screenFadeController.ResetCleanupFlags();
        }

        /// <summary>
        /// 캐릭터 잔상 이벤트 정의를 대상 캐릭터의 <c>CharacterAfterimageTrail</c> 컴포넌트로 전달합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">Bake된 캐릭터 잔상 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        internal void HandleAfterimage(
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            if (payloadObj is not SkillAfterimageEventDefinition def)
                return;

            if (!TryResolveAfterimageTarget(ctx, def, out var targetObject))
                return;

            _afterimageController.Play(targetObject, payloadObj, eventDurationSeconds);
        }

        /// <summary>
        /// 현재 SkillExecutor가 시작한 캐릭터 잔상을 스킬 종료 사유에 맞게 정리합니다.
        /// </summary>
        /// <param name="forCancel">취소 종료이면 true, 정상 종료이면 false입니다.</param>
        private void CleanupSkillAfterimage(bool forCancel)
        {
            _afterimageController.Cleanup(forCancel);
        }

        /// <summary>
        /// 위치 고정 이벤트를 전용 핸들러로 위임합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">위치 고정 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        internal void HandlePositionHold(
            SkillRun run,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            SkillPositionHoldEventHandler.Handle(run, ctx, payloadObj, eventDurationSeconds);
        }

        /// <summary>
        /// 돌진 이벤트 정의에 따라 캐릭터 이동을 시작합니다.
        /// 2D 방향을 보정하고 직선 또는 포물선 이동 요청을 모션 컨트롤러에 전달합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">돌진 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        internal void HandleLunge(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            SkillLungeEventHandler.Handle(
                skill,
                ctx,
                payloadObj,
                eventDurationSeconds,
                _arcLungeAnimationController);
        }

        /// <summary>
        /// 그라운드슬램 이벤트 실행을 전용 핸들러에 위임합니다.
        /// </summary>
        internal void HandleGroundSlam(
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            SkillGroundSlamEventHandler.Handle(
                ctx,
                payloadObj,
                eventDurationSeconds,
                _groundSlamAnimationController);
        }

        /// <summary>
        /// 레이저 이벤트 정의를 바탕으로 발사 대상과 좌표를 계산하고 분리된 레이저 시스템을 호출합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">레이저 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        internal void HandleLaser(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            SkillLaserEventHandler.Handle(
                run,
                skill,
                ctx,
                payloadObj,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                gameObject,
                _attackSequence);
        }

        /// <summary>
        /// 투사체 이벤트 정의를 바탕으로 발사 대상과 좌표를 계산하고 투사체를 생성합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">투사체 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        internal void HandleProjectile(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            SkillProjectileEventHandler.Handle(
                skill,
                ctx,
                payloadObj,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                gameObject,
                _attackSequence);
        }

        /// <summary>
        /// 데미지 이벤트 정의를 전용 핸들러에 위임하여 피격 판정과 OnHit 처리를 실행합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">데미지 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        /// <param name="gizmoDurationSeconds">에디터 디버그용 데미지 영역 표시 시간입니다.</param>
        internal void HandleDamage(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            float gizmoDurationSeconds)
        {
            SkillDamageEventHandler.Handle(
                run,
                skill,
                ctx,
                payloadObj,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                gizmoDurationSeconds,
                _hitEvaluator,
                gameObject,
                _attackSequence);
        }

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
        internal void HandleVfx(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            SkillVfxEventHandler.Handle(
                run,
                skill,
                ctx,
                payloadObj,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                _ownedVfxTracker);
        }

        /// <summary>
        /// 상태 적용 이벤트 정의를 바탕으로 대상에게 Affect를 적용합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">상태 적용 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        internal void HandleApplyStatus(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            SkillStatusEventHandler.HandleApplyStatus(ctx, payloadObj);
        }


        /// <summary>
        /// 런타임 Temp HP(비저장 보호막/임시 하트)를 적용합니다.
        /// - 같은 source key가 다시 들어오면 누적하지 않고 설정값까지 다시 채웁니다.
        /// - 현재치가 모두 소모되면 Core 쪽에서 해당 source가 제거됩니다.
        /// </summary>
        internal void HandleApplyTempHp(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj)
        {
            SkillStatusEventHandler.HandleApplyTempHp(skill, ctx, payloadObj);
        }

        /// <summary>
        /// 스킬 이벤트로 더미 캐릭터를 생성합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">스킬 시작 시점 캐스터 위치 스냅샷입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점 타겟 위치 스냅샷입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점 지면 기준점 스냅샷입니다.</param>
        internal void HandleSpawnDummyCharacter(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not SpawnDummyCharacterEventDefinition def)
                return;

            var sceneGame = SceneGame.Instance;
            if (sceneGame == null || sceneGame.CharacterManager == null)
                return;

            if (def.characterUid <= 0)
            {
                Debug.LogWarning("[SkillExecutor] SpawnDummyCharacter characterUid must be greater than 0.");
                return;
            }

            string actorKey = SkillDummyEventUtility.NormalizeActorKey(def.actorKey);
            if (string.IsNullOrEmpty(actorKey))
            {
                Debug.LogWarning("[SkillExecutor] SpawnDummyCharacter actorKey is empty.");
                return;
            }

            PruneDummyActors();

            if (_dummyActors.TryGetValue(actorKey, out var existing) && existing != null)
            {
                if (!def.replaceIfExists)
                    return;

                DestroyDummyActor(existing, destroyGameObject: true, removeFromRegistry: true);
            }

            Vector3 casterPos = ctx.caster != null ? ctx.caster.transform.position : snapshotCasterPos;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            if (!SkillDummyEventUtility.TryResolveSpawnPosition(run, def, casterPos, targetPos, groundPoint, out Vector3 spawnPos))
                return;

            spawnPos += def.localOffset;

            int mapUid = sceneGame.mapManager != null ? sceneGame.mapManager.GetCurrentMapUid() : 0;
            var regenData = new CharacterRegenData(def.characterUid, spawnPos, flip: false, mapUid, defaultVisible: true);

            CharacterConstants.Type sourceType = SkillDummyEventUtility.ResolveSourceCharacterType(def.sourceType);
            GameObject dummyObject = sceneGame.CharacterManager.CreateDummyCharacter(
                sourceType,
                def.characterUid,
                regenData);

            if (dummyObject == null)
            {
                Debug.LogWarning($"[SkillExecutor] Failed to spawn dummy character. source={def.sourceType}, uid={def.characterUid}, key={actorKey}");
                return;
            }

            dummyObject.transform.position = new Vector3(spawnPos.x, spawnPos.y, dummyObject.transform.position.z);

            var character = dummyObject.GetComponent<CharacterBase>() ?? dummyObject.GetComponentInParent<CharacterBase>();
            if (character == null)
            {
                sceneGame.CharacterManager.RemoveCharacter(dummyObject);
                Debug.LogWarning($"[SkillExecutor] Spawned dummy has no CharacterBase. key={actorKey}, uid={def.characterUid}");
                return;
            }

            SkillDummyActorPresentationUtility.ConfigureRuntime(character);

            var handle = new SkillDummyActorHandle
            {
                ActorKey = actorKey,
                Character = character,
                DespawnOnSkillEnd = def.despawnOnSkillEnd,
                DespawnOnCancel = def.despawnOnCancel,
                GroundPosition = new Vector3(spawnPos.x, spawnPos.y, character.transform.position.z),
                AirHeight = 0f,
            };

            if (def.spawnFacing != CharacterConstants.FacingDirection8.None)
                character.SetFacing(def.spawnFacing);

            if (!string.IsNullOrWhiteSpace(def.initialAnimationName))
                PlayDummyAnimation(handle, def.initialAnimationName, def.initialAnimationLoop, def.initialAnimationTimeScale);

            var marker = character.GetComponent<SkillDummyCharacterMarker>();
            if (marker == null)
                marker = character.gameObject.AddComponent<SkillDummyCharacterMarker>();

            marker.Bind(actorKey, run != null ? run.SkillUid : (skill != null ? skill.Uid : 0));

            SkillDummyActorPresentationUtility.ApplyRuntimeLocks(handle);
            _dummyActors[actorKey] = handle;
            SkillDummyActorRuntimeUtility.ApplyWorldPosition(handle);

            if (def.fadeInEnabled && def.fadeInDurationSeconds > 0f)
            {
                SkillDummyActorPresentationUtility.SetVisualAlpha(character, 0f);
                handle.ActiveFadeCoroutine = StartCoroutine(FadeDummyCharacterCoroutine(
                    handle,
                    fadeIn: true,
                    durationSeconds: def.fadeInDurationSeconds,
                    destroyAfterFade: false,
                    removeFromRegistry: false));
            }
            else
            {
                SkillDummyActorPresentationUtility.SetVisualAlpha(character, 1f);
            }
        }

        /// <summary>
        /// 스킬 이벤트로 생성된 더미 캐릭터를 이동시킵니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점 타겟 위치 스냅샷입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점 지면 기준점 스냅샷입니다.</param>
        internal void HandleMoveDummyCharacter(
            SkillRun run,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not MoveDummyCharacterEventDefinition def)
                return;

            if (!TryResolveDummyActorHandle(ctx, def.actorReferenceType, def.actorKey, def.missingActorPolicy, out var handle))
                return;

            if (handle.Character == null)
                return;

            bool requiresLockedTarget =
                def.moveTargetMode == DummyMoveTargetMode.LockedTarget ||
                def.moveTargetMode == DummyMoveTargetMode.LockedTargetFront;

            if (requiresLockedTarget && !def.useSnapshotCenter && ctx.lockedTarget == null)
            {
                if (def.missingActorPolicy == DummyMissingActorPolicy.Warn)
                {
                    Debug.LogWarning($"[SkillExecutor] MoveDummyCharacter requires locked target. actor={SkillDummyEventUtility.GetActorDisplayName(def.actorReferenceType, def.actorKey)}");
                }
                return;
            }

            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.useSnapshotCenter)
            {
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            Vector3 actorPos = handle.Character.transform.position;
            if (!SkillDummyEventUtility.TryResolveMoveTargetPosition(run, def, actorPos, targetPos, groundPoint, out Vector3 moveTarget))
                return;

            moveTarget += def.localOffset;

            if (def.playMoveAnimation && !string.IsNullOrWhiteSpace(def.moveAnimationName))
            {
                PlayDummyAnimation(handle, def.moveAnimationName, def.moveAnimationLoop, def.moveAnimationTimeScale);
            }

            Transform lookTargetTransform = null;
            Vector3 fallbackLookTargetPosition = targetPos;
            if (def.lookAtTargetDuringMove && !def.useSnapshotCenter && ctx.lockedTarget != null)
            {
                lookTargetTransform = ctx.lockedTarget.transform;
            }

            StartDummyMove(handle, moveTarget, def, lookTargetTransform, fallbackLookTargetPosition);
        }

        /// <summary>
        /// 스킬 이벤트로 생성된 더미 캐릭터를 파괴(또는 비활성화)합니다.
        /// </summary>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        internal void HandleDespawnDummyCharacter(UnityEngine.Object payloadObj)
        {
            if (payloadObj is not DespawnDummyCharacterEventDefinition def)
                return;

            if (!TryGetDummyActorHandle(def.actorKey, def.missingActorPolicy, out var handle))
                return;

            BeginDummyDespawn(
                handle,
                fadeOutEnabled: def.fadeOutEnabled,
                fadeOutDurationSeconds: def.fadeOutDurationSeconds,
                destroyAfterFade: def.destroyAfterFade,
                removeFromRegistry: true);
        }

        /// <summary>
        /// 스킬 이벤트로 더미 캐릭터의 공중 상태(높이/중력)를 제어합니다.
        /// </summary>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        internal void HandleSetDummyAirborneState(UnityEngine.Object payloadObj)
        {
            if (payloadObj is not SetDummyAirborneStateEventDefinition def)
                return;

            if (!TryGetDummyActorHandle(def.actorKey, def.missingActorPolicy, out var handle))
                return;

            if (handle.Character == null)
                return;

            float targetAirHeight = def.airborneEnabled ? Mathf.Max(0f, def.targetAirHeight) : 0f;
            StartDummyAirHeightTransition(
                handle,
                targetAirHeight: targetAirHeight,
                durationSeconds: Mathf.Max(0f, def.durationSeconds),
                easing: def.easing,
                allowReplace: def.allowReplace,
                keepAirborneGravity: def.airborneEnabled || targetAirHeight > 0f);
        }

        /// <summary>
        /// 더미 캐릭터에 지정한 애니메이션을 재생하고 필요 시 후속 애니메이션 전환을 예약합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">애니메이션 이벤트 정의 페이로드입니다.</param>
        /// <param name="eventDurationSeconds">타임라인 클립 구간에서 계산한 이벤트 지속 시간(초)입니다.</param>
        internal void HandlePlayDummyCharacterAnimation(SkillTargetContext ctx, UnityEngine.Object payloadObj, float eventDurationSeconds)
        {
            if (payloadObj is not PlayDummyCharacterAnimationEventDefinition def)
                return;

            if (!TryResolveDummyActorHandle(ctx, def.actorReferenceType, def.actorKey, def.missingActorPolicy, out var handle))
                return;

            PlayDummyAnimation(handle, def.animationName, def.loop, def.timeScale);

            if (def.durationPolicy != DummyAnimationDurationPolicy.UseClipWindow)
                return;

            if (def.endPolicy == DummyAnimationEndPolicy.None)
                return;

            float duration = Mathf.Max(0f, eventDurationSeconds);
            if (duration <= 0f)
            {
                SkillDummyActorPresentationUtility.ApplyAnimationEndPolicy(
                    handle,
                    def.endPolicy,
                    def.endAnimationName,
                    def.endAnimationLoop,
                    def.endAnimationTimeScale);
                return;
            }

            handle.ActiveAnimationCoroutine = StartCoroutine(DummyAnimationFollowupCoroutine(
                handle,
                ++handle.AnimationRequestVersion,
                duration,
                def.endPolicy,
                def.endAnimationName,
                def.endAnimationLoop,
                def.endAnimationTimeScale));
        }

        /// <summary>
        /// 캐릭터 잔상 이벤트가 지정한 대상을 실제 GameObject로 해석합니다.
        /// </summary>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="def">캐릭터 잔상 이벤트 정의입니다.</param>
        /// <param name="targetObject">해석된 잔상 대상 GameObject입니다.</param>
        /// <returns>대상 해석에 성공하면 <see langword="true"/>를 반환합니다.</returns>
        private bool TryResolveAfterimageTarget(
            SkillTargetContext ctx,
            SkillAfterimageEventDefinition def,
            out GameObject targetObject)
        {
            targetObject = null;

            if (def == null)
                return false;

            switch (def.targetType)
            {
                case SkillAfterimageTargetType.Caster:
                    targetObject = ctx.caster;
                    if (targetObject != null)
                        return true;

                    if (def.missingActorPolicy == DummyMissingActorPolicy.Warn)
                        Debug.LogWarning("[SkillExecutor] Afterimage target is Caster, but caster is null.");
                    return false;

                case SkillAfterimageTargetType.LockedTarget:
                    targetObject = ctx.lockedTarget;
                    if (targetObject != null)
                        return true;

                    if (def.missingActorPolicy == DummyMissingActorPolicy.Warn)
                        Debug.LogWarning("[SkillExecutor] Afterimage target is LockedTarget, but locked target is null.");
                    return false;

                case SkillAfterimageTargetType.DummyActor:
                    if (!TryGetDummyActorHandle(def.actorKey, def.missingActorPolicy, out var handle))
                        return false;

                    targetObject = handle.Character != null ? handle.Character.gameObject : null;
                    return targetObject != null;

                default:
                    if (def.missingActorPolicy == DummyMissingActorPolicy.Warn)
                        Debug.LogWarning($"[SkillExecutor] Unsupported afterimage target type. targetType={def.targetType}");
                    return false;
            }
        }

        /// <summary>
        /// 더미 이벤트가 지정한 대상(Actor/Caster)을 실제 런타임 핸들로 해석합니다.
        /// </summary>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="actorReferenceType">대상 참조 방식입니다.</param>
        /// <param name="actorKey">Actor 참조일 때 사용할 식별 키입니다.</param>
        /// <param name="missingPolicy">대상 미존재 시 처리 정책입니다.</param>
        /// <param name="handle">해석된 런타임 핸들입니다.</param>
        /// <returns>해석에 성공하면 <see langword="true"/>를 반환합니다.</returns>
        private bool TryResolveDummyActorHandle(
            SkillTargetContext ctx,
            DummyActorReferenceType actorReferenceType,
            string actorKey,
            DummyMissingActorPolicy missingPolicy,
            out SkillDummyActorHandle handle)
        {
            switch (actorReferenceType)
            {
                case DummyActorReferenceType.Caster:
                    return TryGetCasterActorHandle(ctx, missingPolicy, out handle);
                case DummyActorReferenceType.Actor:
                default:
                    return TryGetDummyActorHandle(actorKey, missingPolicy, out handle);
            }
        }

        /// <summary>
        /// 현재 컨텍스트의 캐스터를 더미 액터 핸들 형태로 변환해 반환합니다.
        /// </summary>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="missingPolicy">캐스터 해석 실패 시 처리 정책입니다.</param>
        /// <param name="handle">캐스터에 바인딩된 임시 핸들입니다.</param>
        /// <returns>캐스터를 핸들로 해석하면 <see langword="true"/>를 반환합니다.</returns>
        private bool TryGetCasterActorHandle(
            SkillTargetContext ctx,
            DummyMissingActorPolicy missingPolicy,
            out SkillDummyActorHandle handle)
        {
            handle = null;

            if (ctx.caster == null)
            {
                if (missingPolicy == DummyMissingActorPolicy.Warn)
                    Debug.LogWarning("[SkillExecutor] Dummy actor target is Caster, but caster is null.");
                return false;
            }

            var character = SkillCharacterComponentResolver.ResolveCharacterBase(ctx.caster);
            if (character == null)
            {
                if (missingPolicy == DummyMissingActorPolicy.Warn)
                    Debug.LogWarning($"[SkillExecutor] Dummy actor target is Caster, but CharacterBase was not found. caster={ctx.caster.name}");
                return false;
            }

            if (!ReferenceEquals(_casterActorHandle.Character, character))
            {
                // 캐스터가 바뀌면 이전 캐스터에 예약된 이동/애니메이션 후속 작업을 정리합니다.
                ResetCasterActorHandleTransientState(clearCharacter: true);
                _casterActorHandle.Character = character;
            }

            _casterActorHandle.ActorKey = CasterActorKey;
            _casterActorHandle.AirHeight = 0f;
            SkillDummyActorRuntimeUtility.SyncGroundFromTransform(_casterActorHandle);
            handle = _casterActorHandle;
            return true;
        }

        /// <summary>
        /// Caster 참조 임시 핸들에 남아 있는 이동/애니메이션 후속 작업을 정리합니다.
        /// </summary>
        /// <param name="clearCharacter">
        /// <see langword="true"/>이면 캐릭터 참조까지 제거합니다.
        /// <see langword="false"/>이면 캐릭터 참조는 유지하고 코루틴/상태만 초기화합니다.
        /// </param>
        private void ResetCasterActorHandleTransientState(bool clearCharacter)
        {
            if (_casterActorHandle.ActiveMoveCoroutine != null)
            {
                StopCoroutine(_casterActorHandle.ActiveMoveCoroutine);
                _casterActorHandle.ActiveMoveCoroutine = null;
            }

            if (_casterActorHandle.ActiveFadeCoroutine != null)
            {
                StopCoroutine(_casterActorHandle.ActiveFadeCoroutine);
                _casterActorHandle.ActiveFadeCoroutine = null;
            }

            if (_casterActorHandle.ActiveAirHeightCoroutine != null)
            {
                StopCoroutine(_casterActorHandle.ActiveAirHeightCoroutine);
                _casterActorHandle.ActiveAirHeightCoroutine = null;
            }

            CancelDummyAnimationFollowup(_casterActorHandle);
            SkillDummyActorRuntimeUtility.ReleaseGravityOverride(_casterActorHandle);
            SkillDummyActorPresentationUtility.ReleaseRuntimeLocks(_casterActorHandle);

            if (!clearCharacter)
                return;

            _casterActorHandle.Character = null;
            _casterActorHandle.GroundPosition = Vector3.zero;
            _casterActorHandle.AirHeight = 0f;
        }

        /// <summary>
        /// actorKey에 해당하는 더미 핸들을 조회합니다.
        /// </summary>
        /// <param name="actorKey">조회할 더미 액터 키입니다.</param>
        /// <param name="missingPolicy">미존재 시 로깅 정책입니다.</param>
        /// <param name="handle">조회된 더미 핸들입니다.</param>
        /// <returns>조회에 성공하면 <see langword="true"/>입니다.</returns>
        private bool TryGetDummyActorHandle(string actorKey, DummyMissingActorPolicy missingPolicy, out SkillDummyActorHandle handle)
        {
            handle = null;

            string normalizedKey = SkillDummyEventUtility.NormalizeActorKey(actorKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                if (missingPolicy == DummyMissingActorPolicy.Warn)
                    Debug.LogWarning("[SkillExecutor] Dummy actorKey is empty.");
                return false;
            }

            PruneDummyActors();

            if (_dummyActors.TryGetValue(normalizedKey, out handle) && handle != null && handle.Character != null)
                return true;

            _dummyActors.Remove(normalizedKey);
            handle = null;

            if (missingPolicy == DummyMissingActorPolicy.Warn)
                Debug.LogWarning($"[SkillExecutor] Dummy actor not found. key={normalizedKey}");

            return false;
        }

        /// <summary>
        /// 더미 캐릭터의 이동을 시작합니다.
        /// </summary>
        /// <param name="handle">이동 대상 더미 핸들입니다.</param>
        /// <param name="targetPosition">이동 목표 위치입니다.</param>
        /// <param name="def">이동 이벤트 정의입니다.</param>
        /// <param name="lookTargetTransform">이동 중 실시간으로 추적할 타겟 Transform입니다.</param>
        /// <param name="fallbackLookTargetPosition">실시간 타겟이 없을 때 사용할 고정 바라보기 좌표입니다.</param>
        private void StartDummyMove(
            SkillDummyActorHandle handle,
            Vector3 targetPosition,
            MoveDummyCharacterEventDefinition def,
            Transform lookTargetTransform,
            Vector3 fallbackLookTargetPosition)
        {
            if (handle == null || handle.Character == null || def == null)
                return;

            SkillDummyActorRuntimeUtility.SyncGroundFromTransform(handle);
            var character = handle.Character;
            Vector3 targetGroundPosition = new Vector3(targetPosition.x, targetPosition.y, handle.GroundPosition.z);

            if (handle.ActiveMoveCoroutine != null)
            {
                if (!def.allowReplace)
                    return;

                StopCoroutine(handle.ActiveMoveCoroutine);
                handle.ActiveMoveCoroutine = null;
            }

            var motion = SkillCharacterComponentResolver.ResolveMotionController(character.gameObject);
            if (motion != null && motion.IsPlaying(MotionChannel.Skill))
            {
                if (!def.allowReplace)
                    return;

                motion.CancelMotion(MotionChannel.Skill, reason: 9202);
            }

            Vector3 currentGroundPosition = handle.GroundPosition;
            Vector2 delta = new Vector2(
                targetGroundPosition.x - currentGroundPosition.x,
                targetGroundPosition.y - currentGroundPosition.y);
            float distance = delta.magnitude;
            float duration = Mathf.Max(0f, def.durationSeconds);

            if (distance <= 1e-4f || duration <= 0f)
            {
                handle.GroundPosition = targetGroundPosition;
                SkillDummyActorRuntimeUtility.ApplyWorldPosition(handle);
                if (def.lookAtTargetDuringMove)
                    SkillDummyActorRuntimeUtility.UpdateFacingDuringMove(handle, lookTargetTransform, fallbackLookTargetPosition);
                return;
            }

            // 더미는 지면 좌표와 공중 높이를 분리 관리하므로, 이동 보간은 지면 좌표만 갱신합니다.
            handle.ActiveMoveCoroutine = StartCoroutine(CoMoveDummyByTransform(
                handle,
                currentGroundPosition,
                targetGroundPosition,
                duration,
                def.easing,
                def.lookAtTargetDuringMove,
                lookTargetTransform,
                fallbackLookTargetPosition));
        }

        /// <summary>
        /// 더미 캐릭터의 지면 좌표를 보간하여 이동시키고, 설정된 경우 타겟 바라보기를 함께 갱신합니다.
        /// </summary>
        /// <param name="handle">이동 대상 더미 핸들입니다.</param>
        /// <param name="from">시작 위치입니다.</param>
        /// <param name="to">도착 위치입니다.</param>
        /// <param name="durationSeconds">이동 시간(초)입니다.</param>
        /// <param name="easeType">보간 easing입니다.</param>
        /// <param name="lookAtTargetDuringMove">이동 중 타겟 바라보기 갱신 여부입니다.</param>
        /// <param name="lookTargetTransform">실시간으로 추적할 타겟 Transform입니다.</param>
        /// <param name="fallbackLookTargetPosition">실시간 타겟이 없을 때 사용할 고정 바라보기 좌표입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private IEnumerator CoMoveDummyByTransform(
            SkillDummyActorHandle handle,
            Vector3 from,
            Vector3 to,
            float durationSeconds,
            Easing.EaseType easeType,
            bool lookAtTargetDuringMove,
            Transform lookTargetTransform,
            Vector3 fallbackLookTargetPosition)
        {
            if (handle == null || handle.Character == null)
                yield break;

            float duration = Mathf.Max(0.0001f, durationSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (handle.Character == null)
                    yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Easing.Apply(t, easeType);
                handle.GroundPosition = Vector3.LerpUnclamped(from, to, eased);
                SkillDummyActorRuntimeUtility.ApplyWorldPosition(handle);
                if (lookAtTargetDuringMove)
                    SkillDummyActorRuntimeUtility.UpdateFacingDuringMove(handle, lookTargetTransform, fallbackLookTargetPosition);
                yield return null;
            }

            if (handle.Character != null)
            {
                handle.GroundPosition = to;
                SkillDummyActorRuntimeUtility.ApplyWorldPosition(handle);
                if (lookAtTargetDuringMove)
                    SkillDummyActorRuntimeUtility.UpdateFacingDuringMove(handle, lookTargetTransform, fallbackLookTargetPosition);
            }

            handle.ActiveMoveCoroutine = null;
        }

        /// <summary>
        /// 공중 상태로 유지 중인 더미의 중력/속도/좌표를 프레임마다 보정합니다.
        /// 스킬 런이 끝난 뒤에도 더미가 유지되는 구성에서 물리 오차로 서서히 내려오는 현상을 방지합니다.
        /// </summary>
        private void MaintainDummyAirborneState()
        {
            if (_dummyActors.Count == 0)
                return;

            foreach (var pair in _dummyActors)
            {
                SkillDummyActorRuntimeUtility.MaintainAirborneState(pair.Value);
            }
        }

        /// <summary>
        /// 더미 캐릭터의 공중 높이 전환을 시작합니다.
        /// </summary>
        /// <param name="handle">대상 더미 핸들입니다.</param>
        /// <param name="targetAirHeight">목표 공중 높이(+Y)입니다.</param>
        /// <param name="durationSeconds">보간 시간(초)입니다.</param>
        /// <param name="easing">보간 easing입니다.</param>
        /// <param name="allowReplace">기존 공중 보간 덮어쓰기 허용 여부입니다.</param>
        /// <param name="keepAirborneGravity">완료 후에도 공중 중력 오버라이드를 유지할지 여부입니다.</param>
        private void StartDummyAirHeightTransition(
            SkillDummyActorHandle handle,
            float targetAirHeight,
            float durationSeconds,
            Easing.EaseType easing,
            bool allowReplace,
            bool keepAirborneGravity)
        {
            if (handle == null || handle.Character == null)
                return;

            if (handle.ActiveAirHeightCoroutine != null)
            {
                if (!allowReplace)
                    return;

                StopCoroutine(handle.ActiveAirHeightCoroutine);
                handle.ActiveAirHeightCoroutine = null;
            }

            SkillDummyActorRuntimeUtility.SyncGroundFromTransform(handle);

            float startHeight = Mathf.Max(0f, handle.AirHeight);
            float endHeight = Mathf.Max(0f, targetAirHeight);
            float duration = Mathf.Max(0f, durationSeconds);

            if (keepAirborneGravity || startHeight > 0f || endHeight > 0f)
                SkillDummyActorRuntimeUtility.EnsureGravityOverride(handle);

            SkillDummyActorRuntimeUtility.ZeroRigidbodyVelocity(handle);

            if (Mathf.Abs(endHeight - startHeight) <= 1e-4f || duration <= 0f)
            {
                handle.AirHeight = endHeight;
                SkillDummyActorRuntimeUtility.ApplyWorldPosition(handle);
                SkillDummyActorRuntimeUtility.ZeroRigidbodyVelocity(handle);

                if (!keepAirborneGravity && endHeight <= 0f)
                    SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);
                return;
            }

            handle.ActiveAirHeightCoroutine = StartCoroutine(CoDummyAirHeightTransition(
                handle,
                startHeight,
                endHeight,
                duration,
                easing,
                keepAirborneGravity));
        }

        /// <summary>
        /// 더미 캐릭터 공중 높이 전환을 프레임 단위로 보간합니다.
        /// </summary>
        /// <param name="handle">대상 더미 핸들입니다.</param>
        /// <param name="startAirHeight">시작 공중 높이입니다.</param>
        /// <param name="targetAirHeight">목표 공중 높이입니다.</param>
        /// <param name="durationSeconds">보간 시간(초)입니다.</param>
        /// <param name="easing">보간 easing입니다.</param>
        /// <param name="keepAirborneGravity">완료 후 중력 오버라이드 유지 여부입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private IEnumerator CoDummyAirHeightTransition(
            SkillDummyActorHandle handle,
            float startAirHeight,
            float targetAirHeight,
            float durationSeconds,
            Easing.EaseType easing,
            bool keepAirborneGravity)
        {
            if (handle == null || handle.Character == null)
                yield break;

            float duration = Mathf.Max(0.0001f, durationSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (handle.Character == null)
                {
                    handle.ActiveAirHeightCoroutine = null;
                    SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Easing.Apply(t, easing);
                handle.AirHeight = Mathf.Lerp(startAirHeight, targetAirHeight, eased);
                SkillDummyActorRuntimeUtility.ApplyWorldPosition(handle);
                SkillDummyActorRuntimeUtility.ZeroRigidbodyVelocity(handle);
                yield return null;
            }

            if (handle.Character != null)
            {
                handle.AirHeight = targetAirHeight;
                SkillDummyActorRuntimeUtility.ApplyWorldPosition(handle);
                SkillDummyActorRuntimeUtility.ZeroRigidbodyVelocity(handle);
            }

            handle.ActiveAirHeightCoroutine = null;

            if (!keepAirborneGravity && targetAirHeight <= 0f)
                SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);
        }

        /// <summary>
        /// 더미 캐릭터 제거를 시작합니다.
        /// </summary>
        /// <param name="handle">제거할 더미 핸들입니다.</param>
        /// <param name="fadeOutEnabled">페이드 아웃 사용 여부입니다.</param>
        /// <param name="fadeOutDurationSeconds">페이드 아웃 시간(초)입니다.</param>
        /// <param name="destroyAfterFade">페이드 이후 Destroy 여부입니다.</param>
        /// <param name="removeFromRegistry">완료 후 레지스트리 제거 여부입니다.</param>
        private void BeginDummyDespawn(
            SkillDummyActorHandle handle,
            bool fadeOutEnabled,
            float fadeOutDurationSeconds,
            bool destroyAfterFade,
            bool removeFromRegistry)
        {
            if (handle == null)
                return;

            if (handle.ActiveMoveCoroutine != null)
            {
                StopCoroutine(handle.ActiveMoveCoroutine);
                handle.ActiveMoveCoroutine = null;
            }

            if (handle.ActiveAirHeightCoroutine != null)
            {
                StopCoroutine(handle.ActiveAirHeightCoroutine);
                handle.ActiveAirHeightCoroutine = null;
            }

            CancelDummyAnimationFollowup(handle);

            if (handle.Character == null)
            {
                SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);
                if (removeFromRegistry && !string.IsNullOrEmpty(handle.ActorKey))
                    _dummyActors.Remove(handle.ActorKey);
                return;
            }

            var motion = SkillCharacterComponentResolver.ResolveMotionController(handle.Character.gameObject);
            motion?.CancelMotion(MotionChannel.Skill, reason: 9203);
            SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);

            float duration = Mathf.Max(0f, fadeOutDurationSeconds);
            if (fadeOutEnabled && duration > 0f)
            {
                if (handle.ActiveFadeCoroutine != null)
                {
                    StopCoroutine(handle.ActiveFadeCoroutine);
                    handle.ActiveFadeCoroutine = null;
                }

                handle.ActiveFadeCoroutine = StartCoroutine(FadeDummyCharacterCoroutine(
                    handle,
                    fadeIn: false,
                    durationSeconds: duration,
                    destroyAfterFade: destroyAfterFade,
                    removeFromRegistry: removeFromRegistry));
                return;
            }

            DestroyDummyActor(handle, destroyGameObject: destroyAfterFade, removeFromRegistry: removeFromRegistry);
        }

        /// <summary>
        /// 더미 캐릭터 페이드 인/아웃을 처리합니다.
        /// </summary>
        /// <param name="handle">페이드를 적용할 더미 핸들입니다.</param>
        /// <param name="fadeIn">페이드 인이면 <see langword="true"/>입니다.</param>
        /// <param name="durationSeconds">페이드 시간(초)입니다.</param>
        /// <param name="destroyAfterFade">페이드 아웃 완료 후 Destroy 여부입니다.</param>
        /// <param name="removeFromRegistry">완료 후 레지스트리 제거 여부입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private IEnumerator FadeDummyCharacterCoroutine(
            SkillDummyActorHandle handle,
            bool fadeIn,
            float durationSeconds,
            bool destroyAfterFade,
            bool removeFromRegistry)
        {
            if (handle == null || handle.Character == null)
            {
                if (handle != null && removeFromRegistry && !string.IsNullOrEmpty(handle.ActorKey))
                    _dummyActors.Remove(handle.ActorKey);
                yield break;
            }

            var character = handle.Character;
            var anim = SkillCharacterComponentResolver.ResolveAnimationController(character.gameObject);
            float duration = Mathf.Max(0f, durationSeconds);

            if (duration <= 0f)
            {
                SkillDummyActorPresentationUtility.SetVisualAlpha(character, fadeIn ? 1f : 0f);
            }
            else if (anim != null)
            {
                if (fadeIn)
                    SkillDummyActorPresentationUtility.SetVisualAlpha(character, 0f);

                yield return anim.FadeEffect(duration, fadeIn);
                SkillDummyActorPresentationUtility.SetVisualAlpha(character, fadeIn ? 1f : 0f);
            }
            else
            {
                float startAlpha = fadeIn ? 0f : 1f;
                float endAlpha = fadeIn ? 1f : 0f;
                float elapsed = 0f;
                SkillDummyActorPresentationUtility.SetVisualAlpha(character, startAlpha);

                while (elapsed < duration)
                {
                    if (handle.Character == null)
                        yield break;

                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    SkillDummyActorPresentationUtility.SetVisualAlpha(handle.Character, Mathf.Lerp(startAlpha, endAlpha, t));
                    yield return null;
                }

                if (handle.Character != null)
                    SkillDummyActorPresentationUtility.SetVisualAlpha(handle.Character, endAlpha);
            }

            handle.ActiveFadeCoroutine = null;

            if (fadeIn)
                yield break;

            DestroyDummyActor(handle, destroyGameObject: destroyAfterFade, removeFromRegistry: removeFromRegistry);
        }

        /// <summary>
        /// 더미 캐릭터 애니메이션 요청을 시작합니다.
        /// 기존에 예약된 후속 애니메이션 전환이 있으면 취소한 뒤 새 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="handle">애니메이션을 재생할 더미 핸들입니다.</param>
        /// <param name="animationName">재생할 애니메이션 이름입니다.</param>
        /// <param name="loop">루프 재생 여부입니다.</param>
        /// <param name="timeScale">재생 속도 배율입니다.</param>
        private void PlayDummyAnimation(SkillDummyActorHandle handle, string animationName, bool loop, float timeScale)
        {
            if (handle == null)
                return;

            CancelDummyAnimationFollowup(handle);
            SkillDummyActorPresentationUtility.PlayAnimation(handle.Character, animationName, loop, timeScale);
        }

        /// <summary>
        /// 더미 캐릭터에 예약된 애니메이션 후속 전환을 취소합니다.
        /// </summary>
        /// <param name="handle">취소할 더미 핸들입니다.</param>
        private void CancelDummyAnimationFollowup(SkillDummyActorHandle handle)
        {
            if (handle == null)
                return;

            if (handle.ActiveAnimationCoroutine != null)
            {
                StopCoroutine(handle.ActiveAnimationCoroutine);
                handle.ActiveAnimationCoroutine = null;
            }

            handle.AnimationRequestVersion++;
        }

        /// <summary>
        /// 더미 캐릭터 애니메이션 유지 시간이 끝난 뒤 후속 애니메이션 전환을 처리합니다.
        /// </summary>
        /// <param name="handle">후속 전환을 적용할 더미 핸들입니다.</param>
        /// <param name="requestVersion">예약 당시의 애니메이션 요청 버전입니다.</param>
        /// <param name="durationSeconds">대기 시간(초)입니다.</param>
        /// <param name="endPolicy">대기 완료 후 적용할 종료 정책입니다.</param>
        /// <param name="endAnimationName">커스텀 종료 애니메이션 이름입니다.</param>
        /// <param name="endAnimationLoop">커스텀 종료 애니메이션 루프 여부입니다.</param>
        /// <param name="endAnimationTimeScale">커스텀 종료 애니메이션 재생 속도 배율입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private IEnumerator DummyAnimationFollowupCoroutine(
            SkillDummyActorHandle handle,
            int requestVersion,
            float durationSeconds,
            DummyAnimationEndPolicy endPolicy,
            string endAnimationName,
            bool endAnimationLoop,
            float endAnimationTimeScale)
        {
            float remaining = Mathf.Max(0f, durationSeconds);
            while (remaining > 0f)
            {
                if (handle == null || handle.Character == null)
                    yield break;

                if (handle.AnimationRequestVersion != requestVersion)
                    yield break;

                remaining -= Time.deltaTime;
                yield return null;
            }

            if (handle == null || handle.Character == null)
                yield break;

            if (handle.AnimationRequestVersion != requestVersion)
                yield break;

            handle.ActiveAnimationCoroutine = null;
            SkillDummyActorPresentationUtility.ApplyAnimationEndPolicy(handle, endPolicy, endAnimationName, endAnimationLoop, endAnimationTimeScale);
        }

        /// <summary>
        /// 더미 캐릭터를 즉시 정리합니다.
        /// </summary>
        /// <param name="handle">정리할 더미 핸들입니다.</param>
        /// <param name="destroyGameObject">Destroy 수행 여부입니다.</param>
        /// <param name="removeFromRegistry">레지스트리 제거 여부입니다.</param>
        private void DestroyDummyActor(SkillDummyActorHandle handle, bool destroyGameObject, bool removeFromRegistry)
        {
            if (handle == null)
                return;

            if (handle.ActiveMoveCoroutine != null)
            {
                StopCoroutine(handle.ActiveMoveCoroutine);
                handle.ActiveMoveCoroutine = null;
            }

            if (handle.ActiveFadeCoroutine != null)
            {
                StopCoroutine(handle.ActiveFadeCoroutine);
                handle.ActiveFadeCoroutine = null;
            }

            if (handle.ActiveAirHeightCoroutine != null)
            {
                StopCoroutine(handle.ActiveAirHeightCoroutine);
                handle.ActiveAirHeightCoroutine = null;
            }

            CancelDummyAnimationFollowup(handle);

            var character = handle.Character;
            if (character != null)
            {
                var motion = SkillCharacterComponentResolver.ResolveMotionController(character.gameObject);
                motion?.CancelMotion(MotionChannel.Skill, reason: 9201);

                SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);
                SkillDummyActorPresentationUtility.ReleaseRuntimeLocks(handle);

                if (destroyGameObject)
                {
                    var sceneGame = SceneGame.Instance;
                    if (sceneGame != null && sceneGame.CharacterManager != null)
                        sceneGame.CharacterManager.RemoveCharacter(character.gameObject);
                    else
                        Destroy(character.gameObject);
                }
                else
                {
                    character.gameObject.SetActive(false);
                }
            }
            else
            {
                SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);
                SkillDummyActorPresentationUtility.ReleaseRuntimeLocks(handle);
            }

            if (removeFromRegistry && !string.IsNullOrEmpty(handle.ActorKey))
                _dummyActors.Remove(handle.ActorKey);

            handle.Character = null;
            handle.AirHeight = 0f;
        }

        /// <summary>
        /// 레지스트리에서 파괴된 더미 핸들을 정리합니다.
        /// </summary>
        private void PruneDummyActors()
        {
            if (_dummyActors.Count == 0)
                return;

            var keysToRemove = new List<string>();
            foreach (var pair in _dummyActors)
            {
                if (pair.Value == null || pair.Value.Character == null)
                {
                    if (pair.Value != null)
                        SkillDummyActorRuntimeUtility.ReleaseGravityOverride(pair.Value);
                    keysToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                _dummyActors.Remove(keysToRemove[i]);
            }
        }

        /// <summary>
        /// 현재 등록된 더미 캐릭터를 종료 정책에 맞게 정리합니다.
        /// </summary>
        /// <param name="forceAll">모든 더미를 강제 정리할지 여부입니다.</param>
        /// <param name="forCancel">취소 종료 기준(<see langword="true"/>) 또는 정상 종료 기준(<see langword="false"/>)을 선택합니다.</param>
        private void CleanupDummyActors(bool forceAll, bool forCancel)
        {
            if (_dummyActors.Count == 0)
                return;

            var keys = new List<string>(_dummyActors.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (!_dummyActors.TryGetValue(key, out var handle) || handle == null)
                {
                    _dummyActors.Remove(key);
                    continue;
                }

                bool shouldCleanup = forceAll || (forCancel ? handle.DespawnOnCancel : handle.DespawnOnSkillEnd);
                if (!shouldCleanup)
                    continue;

                DestroyDummyActor(handle, destroyGameObject: true, removeFromRegistry: true);
            }

            PruneDummyActors();
        }

        /// <summary>
        /// 지정한 런이 현재 활성 런과 동일하고 이벤트를 처리 가능한 상태인지 반환합니다.
        /// </summary>
        internal bool CanProcessEvent(SkillRun run)
        {
            return run != null && ReferenceEquals(_current, run) && !run.IsDone;
        }

        /// <summary>
        /// 차징 상태 변경 스냅샷을 외부 UI/디버그 도구로 전달합니다.
        /// </summary>
        /// <param name="snapshot">현재 차징 상태 스냅샷입니다.</param>
        internal void NotifyChargeStateChanged(SkillChargeSnapshot snapshot)
        {
            ChargeStateChanged?.Invoke(snapshot);
        }

        /// <summary>
        /// 차징 실패가 발생했을 때 현재 실행 결과를 Failed로 예약합니다.
        /// 실패 애니메이션이 끝난 뒤 <see cref="NotifyRunEnded"/>에서 실제 리포트가 발행됩니다.
        /// </summary>
        /// <param name="run">실패한 스킬 런타임입니다.</param>
        internal void NotifyChargeFailed(SkillRun run)
        {
            if (run == null || !ReferenceEquals(_current, run))
                return;

            _pendingFinishReport = new SkillExecutionReport(run.SkillUid, MonsterSkillExecutionState.Failed, ++_executionSequence, Time.time);
            _hasPendingFinishReport = true;
        }

        /// <summary>
        /// 스킬 런 종료 시 현재 실행 참조와 실행 중 생성한 리소스를 정리하고 종료 리포트를 발행합니다.
        /// </summary>
        /// <param name="run">종료된 스킬 런타임입니다.</param>
        internal void NotifyRunEnded(SkillRun run)
        {
            if (run == null || !ReferenceEquals(_current, run))
                return;

            var report = _hasPendingFinishReport
                ? _pendingFinishReport
                : new SkillExecutionReport(run.SkillUid, MonsterSkillExecutionState.Succeeded, ++_executionSequence, Time.time);

            _hasPendingFinishReport = false;
            _current = null;
            _attackSequence.Clear();
            CleanupDummyActors(forceAll: false, forCancel: false);
            CleanupSkillScreenFade(forCancel: false);
            CleanupSkillAfterimage(forCancel: false);
            ExecutionFinished?.Invoke(report);
        }

        /// <summary>
        /// 현재 스킬 실행이 생성한 VFX를 모두 정리합니다.
        /// </summary>
        private void CleanupSpawnedVfxs()
        {
            _ownedVfxTracker.Cleanup();
        }

        private static void ClearDamageAreaGizmo(GameObject caster)
        {
#if UNITY_EDITOR
            SkillTestRuntimeHub.Instance?.ClearDamageAreas(caster);
            SkillTestRuntimeHub.Instance?.ClearLasers(caster);
#endif
        }

        /// <summary>
        /// 현재 실행 중인 스킬이 차징 중이면 피격을 차징 게이지 감소로 처리합니다.
        /// 게이지가 0이 되면 내부적으로 실패 애니메이션과 Failed 리포트를 예약합니다.
        /// </summary>
        /// <param name="reason">피격/인터럽트 사유입니다.</param>
        /// <param name="gaugeDamage">감소시킬 차징 게이지 값입니다. 0 이하이면 스킬 설정값을 사용합니다.</param>
        /// <returns>차징 게이지가 해당 피격을 처리했으면 <see langword="true"/>입니다.</returns>
        public bool TryApplyIncomingHitToChargeGauge(SkillCancelReason reason, float gaugeDamage = 0f)
        {
            if (_current == null)
                return false;

            return _current.TryApplyChargeGaugeDamage(reason, gaugeDamage);
        }

        /// <summary>
        /// 현재 실행 중인 스킬의 취소를 시도합니다.
        /// </summary>
        /// <param name="reason">스킬 취소 사유입니다.</param>
        /// <returns>실행 중인 스킬을 취소하면 <see langword="true"/>, 취소할 대상이 없으면 <see langword="false"/>를 반환합니다.</returns>
        public bool TryCancel(SkillCancelReason reason)
        {
            if (_current == null) return false;

            var run = _current;
            ClearDamageAreaGizmo(run.Caster);
            CleanupSpawnedVfxs();
            CleanupDummyActors(forceAll: false, forCancel: true);
            CleanupSkillScreenFade(forCancel: true);
            CleanupSkillAfterimage(forCancel: true);
            _groundSlamAnimationController.Clear();
            _arcLungeAnimationController.Clear();

            _pendingFinishReport = new SkillExecutionReport(run.SkillUid, MonsterSkillExecutionState.Canceled, ++_executionSequence, Time.time);
            _hasPendingFinishReport = true;

            run.Cancel(reason);
            if (ReferenceEquals(_current, run))
            {
                _current = null; // 즉시 종료(추가 Tick/이벤트 전달 방지)
                _attackSequence.Clear();
                if (_hasPendingFinishReport)
                {
                    var report = _pendingFinishReport;
                    _hasPendingFinishReport = false;
                    ExecutionFinished?.Invoke(report);
                }
            }

            return true;
        }
    }
}
