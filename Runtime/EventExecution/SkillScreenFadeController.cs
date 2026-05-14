using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트가 시작한 화면 페이드의 재생과 종료 시 정리 정책을 관리합니다.
    /// </summary>
    internal sealed class SkillScreenFadeController
    {
        /// <summary>
        /// 정상 종료 시 화면 페이드를 강제로 초기화해야 하는지 여부입니다.
        /// </summary>
        private bool _clearOnSkillEnd;

        /// <summary>
        /// 취소 종료 시 화면 페이드를 강제로 초기화해야 하는지 여부입니다.
        /// </summary>
        private bool _clearOnCancel;

        /// <summary>
        /// 화면 페이드 이벤트 정의를 Core 공용 화면 페이드 서비스로 전달합니다.
        /// </summary>
        /// <param name="owner">페이드 요청 출처로 사용할 실행기 객체입니다.</param>
        /// <param name="payloadObj">Bake된 화면 페이드 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        public void Play(Object owner, Object payloadObj, float eventDurationSeconds)
        {
            if (payloadObj is not SkillScreenFadeEventDefinition def)
                return;

            ScreenFadeRuntimeService service = ScreenFadeRuntimeService.GetOrCreate(SceneGame.Instance);
            if (service == null)
                return;

            float duration = def.ResolveDuration(eventDurationSeconds);
            var request = new ScreenFadeRequest
            {
                owner = ScreenFadeOwner.Skill,
                source = owner,
                color = def.color,
                fromAlpha = def.fromAlpha,
                toAlpha = def.toAlpha,
                durationSeconds = duration,
                holdFinalState = def.holdFinalState,
                useUnscaledTime = def.useUnscaledTime,
                easing = def.easing,
                renderMode = def.renderMode,
                sortingLayerName = def.sortingLayerName,
                orderInLayer = def.orderInLayer,
                planeDistance = def.planeDistance,
                replaceMode = def.replaceMode,
            };

            if (!service.Play(request))
                return;

            _clearOnSkillEnd |= def.clearOnSkillEnd;
            _clearOnCancel |= def.clearOnCancel;
        }

        /// <summary>
        /// 스킬 종료 사유에 맞게 실행기가 시작한 화면 페이드를 정리합니다.
        /// </summary>
        /// <param name="owner">페이드 요청 출처로 사용했던 실행기 객체입니다.</param>
        /// <param name="forCancel">취소 종료이면 <see langword="true"/>, 정상 종료이면 <see langword="false"/>입니다.</param>
        public void Cleanup(Object owner, bool forCancel)
        {
            bool shouldClear = forCancel ? _clearOnCancel : _clearOnSkillEnd;
            if (!shouldClear)
            {
                ResetCleanupFlags();
                return;
            }

            ScreenFadeRuntimeService service = ScreenFadeRuntimeService.GetOrCreate(SceneGame.Instance);
            service?.StopIfOwnedBy(ScreenFadeOwner.Skill, owner, forceClear: true);
            ResetCleanupFlags();
        }

        /// <summary>
        /// 화면 페이드 정리 예약 상태를 초기화합니다.
        /// </summary>
        public void ResetCleanupFlags()
        {
            _clearOnSkillEnd = false;
            _clearOnCancel = false;
        }
    }
}
