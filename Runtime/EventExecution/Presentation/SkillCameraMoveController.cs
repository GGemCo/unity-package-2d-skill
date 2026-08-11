using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 카메라 이동 요청을 실행하고 정상 종료·취소 시 복귀 정책을 관리합니다.
    /// </summary>
    internal sealed class SkillCameraMoveController
    {
        private bool _restoreOnSkillEnd;
        private bool _restoreOnCancel;
        private float _restoreDuration;
        private Easing.EaseType _restoreEasing = Easing.EaseType.EaseOutQuad;
        private bool _restoreUseUnscaledTime;

        /// <summary>
        /// Bake된 카메라 이동 이벤트를 Core 카메라 포커스 API로 전달합니다.
        /// </summary>
        /// <param name="owner">카메라 포커스 요청 출처로 사용할 스킬 실행기입니다.</param>
        /// <param name="context">Caster와 Target 참조가 포함된 스킬 대상 컨텍스트입니다.</param>
        /// <param name="snapshotCasterPosition">이벤트 스냅샷 시점의 Caster 위치입니다.</param>
        /// <param name="snapshotTargetPosition">이벤트 스냅샷 시점의 Target 위치입니다.</param>
        /// <param name="payloadObj">Bake된 카메라 이동 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        public void Play(
            Object owner,
            SkillTargetContext context,
            Vector3 snapshotCasterPosition,
            Vector3 snapshotTargetPosition,
            Object payloadObj,
            float eventDurationSeconds)
        {
            if (owner == null || payloadObj is not SkillCameraMoveEventDefinition def)
                return;

            CameraManager cameraManager = SceneGame.Instance != null ? SceneGame.Instance.cameraManager : null;
            if (cameraManager == null)
                return;

            float duration = def.ResolveDuration(eventDurationSeconds);
            if (def.mode == SkillCameraMoveMode.Restore)
            {
                if (cameraManager.RestoreCameraFocusIfOwnedBy(
                        CameraFocusOwner.Skill,
                        owner,
                        duration,
                        def.easing,
                        def.useUnscaledTime))
                {
                    ResetCleanupFlags();
                }

                return;
            }

            if (!TryResolveTarget(
                    context,
                    snapshotCasterPosition,
                    snapshotTargetPosition,
                    def,
                    out Transform target,
                    out Vector2 snapshotPosition))
            {
                return;
            }

            var request = new CameraFocusRequest
            {
                Owner = CameraFocusOwner.Skill,
                Source = owner,
                TrackingMode = def.trackingMode,
                Target = target,
                SnapshotPosition = snapshotPosition,
                Offset = def.offset,
                Duration = duration,
                Easing = def.easing,
                UseUnscaledTime = def.useUnscaledTime,
                RespectMapBounds = def.respectMapBounds,
                ReplaceMode = def.replaceMode,
            };
            if (!cameraManager.TryStartCameraFocus(request))
                return;

            _restoreOnSkillEnd |= def.restoreOnSkillEnd;
            _restoreOnCancel |= def.restoreOnCancel;
            _restoreDuration = duration;
            _restoreEasing = def.easing;
            _restoreUseUnscaledTime = def.useUnscaledTime;
        }

        /// <summary>
        /// 스킬 종료 사유와 복귀 정책에 따라 이 실행기가 소유한 카메라 포커스를 정리합니다.
        /// </summary>
        /// <param name="owner">카메라 포커스 요청 출처로 사용한 스킬 실행기입니다.</param>
        /// <param name="forCancel">취소 종료이면 <see langword="true"/>입니다.</param>
        /// <param name="forceRestore">설정과 관계없이 즉시 복귀할지 여부입니다.</param>
        public void Cleanup(Object owner, bool forCancel, bool forceRestore = false)
        {
            bool shouldRestore = forceRestore || (forCancel ? _restoreOnCancel : _restoreOnSkillEnd);
            if (shouldRestore)
            {
                CameraManager cameraManager = SceneGame.Instance != null ? SceneGame.Instance.cameraManager : null;
                cameraManager?.RestoreCameraFocusIfOwnedBy(
                    CameraFocusOwner.Skill,
                    owner,
                    forceRestore ? 0f : _restoreDuration,
                    _restoreEasing,
                    _restoreUseUnscaledTime);
            }

            ResetCleanupFlags();
        }

        /// <summary>카메라 이동 종료 시 복귀 예약 상태를 초기화합니다.</summary>
        public void ResetCleanupFlags()
        {
            _restoreOnSkillEnd = false;
            _restoreOnCancel = false;
            _restoreDuration = 0f;
            _restoreEasing = Easing.EaseType.EaseOutQuad;
            _restoreUseUnscaledTime = false;
        }

        /// <summary>
        /// 이벤트 설정에 따라 실제 대상 Transform과 스냅샷 위치를 결정합니다.
        /// </summary>
        private static bool TryResolveTarget(
            SkillTargetContext context,
            Vector3 snapshotCasterPosition,
            Vector3 snapshotTargetPosition,
            SkillCameraMoveEventDefinition def,
            out Transform target,
            out Vector2 snapshotPosition)
        {
            GameObject targetObject = def.targetSource == SkillCameraMoveTargetSource.Caster
                ? context.caster
                : context.lockedTarget;
            Vector3 resolvedSnapshot = def.targetSource == SkillCameraMoveTargetSource.Caster
                ? snapshotCasterPosition
                : snapshotTargetPosition;

            if (targetObject == null &&
                def.targetSource == SkillCameraMoveTargetSource.Target &&
                def.missingTargetPolicy == SkillCameraMoveMissingTargetPolicy.FallbackToCaster)
            {
                targetObject = context.caster;
                resolvedSnapshot = snapshotCasterPosition;
            }

            if (targetObject == null)
            {
                if (def.missingTargetPolicy == SkillCameraMoveMissingTargetPolicy.Warn)
                {
                    GcLogger.LogWarning(
                        $"[{nameof(SkillCameraMoveController)}] 카메라 이동 대상을 찾지 못했습니다. targetSource: {def.targetSource}");
                }

                target = null;
                snapshotPosition = default;
                return false;
            }

            target = targetObject.transform;
            snapshotPosition = resolvedSnapshot;
            return true;
        }
    }
}
