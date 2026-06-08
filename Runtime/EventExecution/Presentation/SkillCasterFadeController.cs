using System.Collections;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트가 시작한 캐스터 페이드의 재생과 종료 시 복구 정책을 관리합니다.
    /// </summary>
    internal sealed class SkillCasterFadeController
    {
        /// <summary>
        /// 현재 페이드를 실행 중인 캐스터입니다.
        /// </summary>
        private GameObject _activeCaster;

        /// <summary>
        /// 실행 중인 페이드 코루틴입니다.
        /// </summary>
        private Coroutine _activeCoroutine;

        /// <summary>
        /// 정상 종료 시 캐스터 알파를 복구해야 하는지 여부입니다.
        /// </summary>
        private bool _restoreOnSkillEnd;

        /// <summary>
        /// 취소 종료 시 캐스터 알파를 복구해야 하는지 여부입니다.
        /// </summary>
        private bool _restoreOnCancel;

        /// <summary>
        /// 캐스터 페이드 이벤트 정의를 해석하여 현재 스킬 캐스터에 적용합니다.
        /// </summary>
        /// <param name="runner">코루틴 실행과 중단에 사용할 스킬 실행기입니다.</param>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="payloadObj">Bake된 캐스터 페이드 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        public void Play(MonoBehaviour runner, SkillTargetContext ctx, Object payloadObj, float eventDurationSeconds)
        {
            if (runner == null)
                return;

            if (ctx.caster == null)
                return;

            if (payloadObj is not SkillCasterFadeEventDefinition def)
                return;

            StopActiveCoroutine(runner);

            _activeCaster = ctx.caster;
            _restoreOnSkillEnd |= def.restoreOnSkillEnd;
            _restoreOnCancel |= def.restoreOnCancel;

            float duration = def.ResolveDuration(eventDurationSeconds);
            bool fadeIn = def.mode == SkillCasterFadeMode.FadeIn;

            // Core 애니메이션 컨트롤러의 페이드 구현을 우선 사용해 Sprite/Spine 표현 방식을 모두 지원합니다.
            _activeCoroutine = runner.StartCoroutine(FadeCasterCoroutine(_activeCaster, duration, fadeIn));
        }

        /// <summary>
        /// 스킬 종료 사유에 맞게 캐스터 페이드 코루틴을 정리하고, 필요한 경우 알파를 복구합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단에 사용할 스킬 실행기입니다.</param>
        /// <param name="forCancel">취소 종료이면 <see langword="true"/>, 정상 종료이면 <see langword="false"/>입니다.</param>
        /// <param name="forceRestore">종료 정책과 무관하게 캐스터 알파를 복구할지 여부입니다.</param>
        public void Cleanup(MonoBehaviour runner, bool forCancel, bool forceRestore = false)
        {
            StopActiveCoroutine(runner);

            bool shouldRestore = forceRestore || (forCancel ? _restoreOnCancel : _restoreOnSkillEnd);
            if (shouldRestore)
                SetCasterAlpha(_activeCaster, 1f);

            ResetCleanupFlags();
        }

        /// <summary>
        /// 캐스터 페이드 정리 예약 상태를 초기화합니다.
        /// </summary>
        public void ResetCleanupFlags()
        {
            _activeCaster = null;
            _activeCoroutine = null;
            _restoreOnSkillEnd = false;
            _restoreOnCancel = false;
        }

        /// <summary>
        /// 실행 중인 페이드 코루틴이 있으면 중단합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단에 사용할 스킬 실행기입니다.</param>
        private void StopActiveCoroutine(MonoBehaviour runner)
        {
            if (runner == null || _activeCoroutine == null)
                return;

            runner.StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }

        /// <summary>
        /// 캐스터 애니메이션 컨트롤러를 통해 페이드 효과를 실행하고 마지막 알파를 보정합니다.
        /// </summary>
        /// <param name="caster">페이드 대상 캐스터 오브젝트입니다.</param>
        /// <param name="duration">페이드 지속 시간(초)입니다.</param>
        /// <param name="fadeIn">페이드 인이면 <see langword="true"/>, 페이드 아웃이면 <see langword="false"/>입니다.</param>
        /// <returns>페이드 완료까지 대기하는 코루틴입니다.</returns>
        private static IEnumerator FadeCasterCoroutine(GameObject caster, float duration, bool fadeIn)
        {
            if (caster == null)
                yield break;

            ICharacterAnimationController animationController =
                SkillCharacterComponentResolver.ResolveAnimationController(caster);

            if (animationController != null && duration > 0f)
            {
                yield return animationController.FadeEffect(duration, fadeIn);
                animationController.SetCharacterColor(new Color(1f, 1f, 1f, fadeIn ? 1f : 0f));
                yield break;
            }

            // 애니메이션 컨트롤러가 없거나 즉시 전환이면 Renderer 알파를 직접 보정합니다.
            SetCasterAlpha(caster, fadeIn ? 1f : 0f);
        }

        /// <summary>
        /// 캐스터 오브젝트 계층의 애니메이션 컨트롤러와 SpriteRenderer 알파를 지정한 값으로 맞춥니다.
        /// </summary>
        /// <param name="caster">알파를 적용할 캐스터 오브젝트입니다.</param>
        /// <param name="alpha">적용할 알파값입니다.</param>
        private static void SetCasterAlpha(GameObject caster, float alpha)
        {
            if (caster == null)
                return;

            float clampedAlpha = Mathf.Clamp01(alpha);
            ICharacterAnimationController animationController =
                SkillCharacterComponentResolver.ResolveAnimationController(caster);
            animationController?.SetCharacterColor(new Color(1f, 1f, 1f, clampedAlpha));

            SpriteRenderer[] renderers = caster.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Color color = renderer.color;
                color.a = clampedAlpha;
                renderer.color = color;
            }
        }
    }
}
