using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트가 시작한 카메라 줌의 실행과 종료 시 복귀 정책을 관리합니다.
    /// </summary>
    internal sealed class SkillCameraZoomController
    {
        private bool _restoreOnSkillEnd;
        private bool _restoreOnCancel;
        private float _restoreDuration;
        private Easing.EaseType _restoreEasing = Easing.EaseType.EaseOutQuad;
        private bool _restoreUseUnscaledTime;

        /// <summary>
        /// Bake된 카메라 줌 이벤트를 Core 카메라 매니저로 전달합니다.
        /// </summary>
        /// <param name="owner">카메라 줌 요청의 출처로 사용할 스킬 실행기입니다.</param>
        /// <param name="payloadObj">Bake된 카메라 줌 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        public void Play(Object owner, Object payloadObj, float eventDurationSeconds)
        {
            if (owner == null || payloadObj is not SkillCameraZoomEventDefinition def)
                return;

            CameraManager cameraManager = SceneGame.Instance != null ? SceneGame.Instance.cameraManager : null;
            if (cameraManager == null)
                return;

            float duration = def.ResolveDuration(eventDurationSeconds);
            if (def.mode == SkillCameraZoomMode.Restore)
            {
                if (cameraManager.RestoreZoomIfOwnedBy(
                        CameraZoomOwner.Skill,
                        owner,
                        duration,
                        def.easing,
                        def.useUnscaledTime))
                {
                    ResetCleanupFlags();
                }

                return;
            }

            var request = new CameraZoomRequest
            {
                Owner = CameraZoomOwner.Skill,
                Source = owner,
                EndSize = Mathf.Max(0.0001f, def.targetOrthographicSize),
                Duration = duration,
                Easing = def.easing,
                UseUnscaledTime = def.useUnscaledTime,
                ChangeOriginalSize = false,
                ReplaceMode = def.replaceMode,
            };
            if (!cameraManager.TryStartZoom(request))
                return;

            _restoreOnSkillEnd |= def.restoreOnSkillEnd;
            _restoreOnCancel |= def.restoreOnCancel;
            _restoreDuration = duration;
            _restoreEasing = def.easing;
            _restoreUseUnscaledTime = def.useUnscaledTime;
        }

        /// <summary>
        /// 스킬 종료 사유와 복귀 정책에 따라 이 실행기가 소유한 카메라 줌을 정리합니다.
        /// </summary>
        /// <param name="owner">카메라 줌 요청 출처로 사용한 스킬 실행기입니다.</param>
        /// <param name="forCancel">취소 종료이면 <see langword="true"/>입니다.</param>
        /// <param name="forceRestore">설정과 관계없이 복귀를 시도할지 여부입니다.</param>
        public void Cleanup(Object owner, bool forCancel, bool forceRestore = false)
        {
            bool shouldRestore = forceRestore || (forCancel ? _restoreOnCancel : _restoreOnSkillEnd);
            if (shouldRestore)
            {
                CameraManager cameraManager = SceneGame.Instance != null ? SceneGame.Instance.cameraManager : null;
                cameraManager?.RestoreZoomIfOwnedBy(
                    CameraZoomOwner.Skill,
                    owner,
                    forceRestore ? 0f : _restoreDuration,
                    _restoreEasing,
                    _restoreUseUnscaledTime);
            }

            ResetCleanupFlags();
        }

        /// <summary>
        /// 카메라 줌 종료 시 복귀 예약 상태를 초기화합니다.
        /// </summary>
        public void ResetCleanupFlags()
        {
            _restoreOnSkillEnd = false;
            _restoreOnCancel = false;
            _restoreDuration = 0f;
            _restoreEasing = Easing.EaseType.EaseOutQuad;
            _restoreUseUnscaledTime = false;
        }
    }
}
