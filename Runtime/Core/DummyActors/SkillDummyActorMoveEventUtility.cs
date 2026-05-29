using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// MoveDummyCharacter 이벤트 정의를 실제 더미 액터 이동 명령으로 변환하고 실행합니다.
    /// </summary>
    internal static class SkillDummyActorMoveEventUtility
    {
        /// <summary>
        /// 더미 이동 이벤트를 해석해 대상 더미를 찾고, 목표 좌표와 바라보기 기준을 계산한 뒤 이동을 시작합니다.
        /// </summary>
        /// <param name="runner">코루틴 실행과 캐스터 임시 핸들 정리에 사용할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="casterHandle">Caster 참조에 재사용할 임시 핸들입니다.</param>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">더미 이동 이벤트 정의입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점 타겟 위치 스냅샷입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점 지면 기준점 스냅샷입니다.</param>
        /// <returns>이동 명령 시작에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryExecuteMove(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle casterHandle,
            SkillRun run,
            SkillTargetContext ctx,
            MoveDummyCharacterEventDefinition def,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (runner == null || def == null)
                return false;

            if (!SkillDummyActorReferenceUtility.TryResolveActorHandle(
                    runner,
                    registry,
                    casterHandle,
                    ctx,
                    def.actorReferenceType,
                    def.actorKey,
                    def.missingActorPolicy,
                    out var handle))
                return false;

            if (handle.Character == null)
                return false;

            if (!TryResolveMoveCommand(
                    run,
                    ctx,
                    handle,
                    def,
                    snapshotTargetPos,
                    snapshotGroundPoint,
                    out Vector3 moveTarget,
                    out Transform lookTargetTransform,
                    out Vector3 fallbackLookTargetPosition))
                return false;

            PlayMoveAnimationIfNeeded(runner, handle, def);
            SkillDummyActorMotionUtility.StartMove(
                runner,
                handle,
                moveTarget,
                def,
                lookTargetTransform,
                fallbackLookTargetPosition);
            return true;
        }

        /// <summary>
        /// 더미 이동 이벤트에서 실제 이동 목표 좌표와 이동 중 바라보기 기준을 계산합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="handle">이동 대상 더미 핸들입니다.</param>
        /// <param name="def">더미 이동 이벤트 정의입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점 타겟 위치 스냅샷입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점 지면 기준점 스냅샷입니다.</param>
        /// <param name="moveTarget">계산된 최종 이동 목표 좌표입니다.</param>
        /// <param name="lookTargetTransform">이동 중 실시간으로 추적할 바라보기 타겟 Transform입니다.</param>
        /// <param name="fallbackLookTargetPosition">실시간 타겟이 없을 때 사용할 고정 바라보기 좌표입니다.</param>
        /// <returns>이동 명령 계산에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveMoveCommand(
            SkillRun run,
            SkillTargetContext ctx,
            SkillDummyActorHandle handle,
            MoveDummyCharacterEventDefinition def,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            out Vector3 moveTarget,
            out Transform lookTargetTransform,
            out Vector3 fallbackLookTargetPosition)
        {
            moveTarget = Vector3.zero;
            lookTargetTransform = null;
            fallbackLookTargetPosition = snapshotTargetPos;

            if (!ValidateLockedTarget(ctx, def))
                return false;

            ResolveReferencePositions(
                ctx,
                def.useSnapshotCenter,
                snapshotTargetPos,
                snapshotGroundPoint,
                out Vector3 targetPos,
                out Vector3 groundPoint);

            Vector3 actorPos = handle.Character.transform.position;
            if (!SkillDummyEventUtility.TryResolveMoveTargetPosition(run, def, actorPos, targetPos, groundPoint, out moveTarget))
                return false;

            moveTarget += def.localOffset;
            moveTarget = ApplyScreenClampPolicy(handle, def, moveTarget);
            ResolveLookTarget(ctx, def, targetPos, out lookTargetTransform, out fallbackLookTargetPosition);
            return true;
        }

        /// <summary>
        /// 화면 경계 보정 정책이 활성화된 경우, 더미의 최종 표시 위치가 화면을 벗어나지 않도록 목표 지면 좌표를 보정합니다.
        /// </summary>
        /// <param name="handle">이동 대상 더미 핸들입니다.</param>
        /// <param name="def">더미 이동 이벤트 정의입니다.</param>
        /// <param name="moveTarget">정책 적용 전 목표 지면 좌표입니다.</param>
        /// <returns>화면 경계 정책이 반영된 목표 지면 좌표입니다.</returns>
        private static Vector3 ApplyScreenClampPolicy(
            SkillDummyActorHandle handle,
            MoveDummyCharacterEventDefinition def,
            Vector3 moveTarget)
        {
            if (handle == null || def == null)
                return moveTarget;

            if (def.screenClampPolicy != SkillLungeScreenClampPolicy.ClampToViewportEdge)
                return moveTarget;

            if (!SkillScreenClampUtility.TryClampGroundPositionWithAirHeight(
                    moveTarget,
                    handle.AirHeight,
                    Mathf.Max(0f, def.screenEdgePadding),
                    out Vector3 clampedGroundPosition))
                return moveTarget;

            return clampedGroundPosition;
        }

        /// <summary>
        /// 현재 이벤트가 실시간 lockedTarget을 요구하는데 대상이 없는지 검증합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">더미 이동 이벤트 정의입니다.</param>
        /// <returns>이동을 계속 진행해도 되면 <see langword="true"/>입니다.</returns>
        private static bool ValidateLockedTarget(SkillTargetContext ctx, MoveDummyCharacterEventDefinition def)
        {
            if (!RequiresLockedTarget(def) || def.useSnapshotCenter || ctx.lockedTarget != null)
                return true;

            if (def.missingActorPolicy == DummyMissingActorPolicy.Warn)
            {
                Debug.LogWarning($"[SkillExecutor] MoveDummyCharacter requires locked target. actor={SkillDummyEventUtility.GetActorDisplayName(def.actorReferenceType, def.actorKey)}");
            }

            return false;
        }

        /// <summary>
        /// 이동 목표 해석 방식이 lockedTarget을 필수로 요구하는지 반환합니다.
        /// </summary>
        /// <param name="def">더미 이동 이벤트 정의입니다.</param>
        /// <returns>lockedTarget 기반 이동이면 <see langword="true"/>입니다.</returns>
        private static bool RequiresLockedTarget(MoveDummyCharacterEventDefinition def)
        {
            return def.moveTargetMode == DummyMoveTargetMode.LockedTarget ||
                   def.moveTargetMode == DummyMoveTargetMode.LockedTargetFront;
        }

        /// <summary>
        /// 현재 컨텍스트 또는 스냅샷 기준으로 이동 목표 계산에 사용할 참조 좌표를 결정합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="useSnapshotCenter">스냅샷 좌표를 사용할지 여부입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점 타겟 위치 스냅샷입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점 지면 기준점 스냅샷입니다.</param>
        /// <param name="targetPos">해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">해석된 지면 기준점입니다.</param>
        private static void ResolveReferencePositions(
            SkillTargetContext ctx,
            bool useSnapshotCenter,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            out Vector3 targetPos,
            out Vector3 groundPoint)
        {
            if (useSnapshotCenter)
            {
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
                return;
            }

            targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            groundPoint = ctx.groundPoint;
        }

        /// <summary>
        /// 이동 중 바라보기 갱신에 사용할 실시간 타겟과 fallback 좌표를 결정합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">더미 이동 이벤트 정의입니다.</param>
        /// <param name="targetPos">해석된 타겟 위치입니다.</param>
        /// <param name="lookTargetTransform">실시간으로 추적할 바라보기 타겟 Transform입니다.</param>
        /// <param name="fallbackLookTargetPosition">실시간 타겟이 없을 때 사용할 고정 바라보기 좌표입니다.</param>
        private static void ResolveLookTarget(
            SkillTargetContext ctx,
            MoveDummyCharacterEventDefinition def,
            Vector3 targetPos,
            out Transform lookTargetTransform,
            out Vector3 fallbackLookTargetPosition)
        {
            lookTargetTransform = null;
            fallbackLookTargetPosition = targetPos;

            if (def.lookAtTargetDuringMove && !def.useSnapshotCenter && ctx.lockedTarget != null)
                lookTargetTransform = ctx.lockedTarget.transform;
        }

        /// <summary>
        /// 이벤트 설정에 따라 이동 시작 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="runner">예약된 애니메이션 후속 전환을 취소할 MonoBehaviour입니다.</param>
        /// <param name="handle">애니메이션을 재생할 더미 핸들입니다.</param>
        /// <param name="def">더미 이동 이벤트 정의입니다.</param>
        private static void PlayMoveAnimationIfNeeded(
            MonoBehaviour runner,
            SkillDummyActorHandle handle,
            MoveDummyCharacterEventDefinition def)
        {
            if (!def.playMoveAnimation || string.IsNullOrWhiteSpace(def.moveAnimationName))
                return;

            SkillDummyActorAnimationEventUtility.PlayAnimation(
                runner,
                handle,
                def.moveAnimationName,
                def.moveAnimationLoop,
                def.moveAnimationTimeScale);
        }
    }
}
