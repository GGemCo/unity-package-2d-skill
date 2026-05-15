using System.Collections;
using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 더미 액터의 코루틴 중단, 페이드 제거, Destroy, 레지스트리 정리 같은 생명주기 처리를 담당합니다.
    /// </summary>
    internal static class SkillDummyActorLifecycleUtility
    {
        /// <summary>
        /// Caster 참조 임시 핸들에 남아 있는 이동, 페이드, 공중 높이, 애니메이션 후속 작업을 정리합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단을 실행할 MonoBehaviour입니다.</param>
        /// <param name="handle">정리할 Caster 임시 핸들입니다.</param>
        /// <param name="clearCharacter">캐릭터 참조까지 제거할지 여부입니다.</param>
        public static void ResetCasterHandleTransientState(
            MonoBehaviour runner,
            SkillDummyActorHandle handle,
            bool clearCharacter)
        {
            if (handle == null)
                return;

            StopActiveCoroutines(runner, handle);
            CancelAnimationFollowup(runner, handle);
            SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);
            SkillDummyActorPresentationUtility.ReleaseRuntimeLocks(handle);

            if (!clearCharacter)
                return;

            handle.Character = null;
            handle.GroundPosition = Vector3.zero;
            handle.AirHeight = 0f;
        }

        /// <summary>
        /// 더미 캐릭터 제거 절차를 시작하고 설정에 따라 페이드 아웃 후 Destroy를 예약합니다.
        /// </summary>
        /// <param name="runner">코루틴 실행과 중단을 담당할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="handle">제거할 더미 핸들입니다.</param>
        /// <param name="fadeOutEnabled">페이드 아웃 사용 여부입니다.</param>
        /// <param name="fadeOutDurationSeconds">페이드 아웃 시간(초)입니다.</param>
        /// <param name="destroyAfterFade">페이드 이후 Destroy 여부입니다.</param>
        /// <param name="removeFromRegistry">완료 후 레지스트리 제거 여부입니다.</param>
        public static void BeginDespawn(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle handle,
            bool fadeOutEnabled,
            float fadeOutDurationSeconds,
            bool destroyAfterFade,
            bool removeFromRegistry)
        {
            if (handle == null)
                return;

            StopCoroutineIfRunning(runner, handle.ActiveMoveCoroutine);
            handle.ActiveMoveCoroutine = null;
            StopCoroutineIfRunning(runner, handle.ActiveAirHeightCoroutine);
            handle.ActiveAirHeightCoroutine = null;
            CancelAnimationFollowup(runner, handle);

            if (handle.Character == null)
            {
                SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);
                RemoveFromRegistryIfNeeded(registry, handle, removeFromRegistry);
                return;
            }

            var motion = SkillCharacterComponentResolver.ResolveMotionController(handle.Character.gameObject);
            motion?.CancelMotion(MotionChannel.Skill, reason: 9203);
            SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);

            float duration = Mathf.Max(0f, fadeOutDurationSeconds);
            if (fadeOutEnabled && duration > 0f)
            {
                StopCoroutineIfRunning(runner, handle.ActiveFadeCoroutine);
                handle.ActiveFadeCoroutine = StartFade(
                    runner,
                    registry,
                    handle,
                    fadeIn: false,
                    durationSeconds: duration,
                    destroyAfterFade: destroyAfterFade,
                    removeFromRegistry: removeFromRegistry);
                return;
            }

            DestroyActor(runner, registry, handle, destroyGameObject: destroyAfterFade, removeFromRegistry: removeFromRegistry);
        }

        /// <summary>
        /// 더미 캐릭터 페이드 인/아웃 코루틴을 시작합니다.
        /// </summary>
        /// <param name="runner">코루틴을 실행할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="handle">페이드를 적용할 더미 핸들입니다.</param>
        /// <param name="fadeIn">페이드 인이면 <see langword="true"/>입니다.</param>
        /// <param name="durationSeconds">페이드 시간(초)입니다.</param>
        /// <param name="destroyAfterFade">페이드 아웃 완료 후 Destroy 여부입니다.</param>
        /// <param name="removeFromRegistry">완료 후 레지스트리 제거 여부입니다.</param>
        /// <returns>시작된 코루틴입니다.</returns>
        public static Coroutine StartFade(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle handle,
            bool fadeIn,
            float durationSeconds,
            bool destroyAfterFade,
            bool removeFromRegistry)
        {
            if (runner == null)
                return null;

            return runner.StartCoroutine(FadeDummyCharacterCoroutine(
                runner,
                registry,
                handle,
                fadeIn,
                durationSeconds,
                destroyAfterFade,
                removeFromRegistry));
        }

        /// <summary>
        /// 더미 캐릭터 애니메이션 유지 시간이 끝난 뒤 후속 애니메이션 전환을 예약합니다.
        /// </summary>
        /// <param name="runner">코루틴을 실행할 MonoBehaviour입니다.</param>
        /// <param name="handle">후속 전환을 적용할 더미 핸들입니다.</param>
        /// <param name="durationSeconds">대기 시간(초)입니다.</param>
        /// <param name="endPolicy">대기 완료 후 적용할 종료 정책입니다.</param>
        /// <param name="endAnimationName">커스텀 종료 애니메이션 이름입니다.</param>
        /// <param name="endAnimationLoop">커스텀 종료 애니메이션 루프 여부입니다.</param>
        /// <param name="endAnimationTimeScale">커스텀 종료 애니메이션 재생 속도 배율입니다.</param>
        /// <returns>시작된 후속 애니메이션 코루틴입니다.</returns>
        public static Coroutine StartAnimationFollowup(
            MonoBehaviour runner,
            SkillDummyActorHandle handle,
            float durationSeconds,
            DummyAnimationEndPolicy endPolicy,
            string endAnimationName,
            bool endAnimationLoop,
            float endAnimationTimeScale)
        {
            if (runner == null || handle == null)
                return null;

            int requestVersion = ++handle.AnimationRequestVersion;
            return runner.StartCoroutine(DummyAnimationFollowupCoroutine(
                handle,
                requestVersion,
                durationSeconds,
                endPolicy,
                endAnimationName,
                endAnimationLoop,
                endAnimationTimeScale));
        }

        /// <summary>
        /// 더미 캐릭터에 예약된 애니메이션 후속 전환을 취소합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단을 실행할 MonoBehaviour입니다.</param>
        /// <param name="handle">취소할 더미 핸들입니다.</param>
        public static void CancelAnimationFollowup(MonoBehaviour runner, SkillDummyActorHandle handle)
        {
            if (handle == null)
                return;

            StopCoroutineIfRunning(runner, handle.ActiveAnimationCoroutine);
            handle.ActiveAnimationCoroutine = null;
            handle.AnimationRequestVersion++;
        }

        /// <summary>
        /// 더미 캐릭터를 즉시 정리하고 필요 시 GameObject와 레지스트리 항목을 제거합니다.
        /// </summary>
        /// <param name="runner">활성 코루틴 중단에 사용할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="handle">정리할 더미 핸들입니다.</param>
        /// <param name="destroyGameObject">Destroy 수행 여부입니다.</param>
        /// <param name="removeFromRegistry">레지스트리 제거 여부입니다.</param>
        public static void DestroyActor(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle handle,
            bool destroyGameObject,
            bool removeFromRegistry)
        {
            if (handle == null)
                return;

            StopActiveCoroutines(runner, handle);
            CancelAnimationFollowup(runner, handle);

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
                        UnityEngine.Object.Destroy(character.gameObject);
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

            RemoveFromRegistryIfNeeded(registry, handle, removeFromRegistry);
            handle.Character = null;
            handle.AirHeight = 0f;
        }

        /// <summary>
        /// 레지스트리에서 파괴되었거나 캐릭터 참조가 사라진 더미 핸들을 제거합니다.
        /// </summary>
        /// <param name="registry">정리할 더미 액터 레지스트리입니다.</param>
        public static void Prune(Dictionary<string, SkillDummyActorHandle> registry)
        {
            if (registry == null || registry.Count == 0)
                return;

            var keysToRemove = new List<string>();
            foreach (var pair in registry)
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
                registry.Remove(keysToRemove[i]);
            }
        }

        /// <summary>
        /// 현재 등록된 더미 캐릭터를 스킬 종료 또는 취소 정책에 맞게 정리합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단에 사용할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="forceAll">모든 더미를 강제 정리할지 여부입니다.</param>
        /// <param name="forCancel">취소 종료 기준(<see langword="true"/>) 또는 정상 종료 기준(<see langword="false"/>)을 선택합니다.</param>
        public static void Cleanup(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            bool forceAll,
            bool forCancel)
        {
            if (registry == null || registry.Count == 0)
                return;

            var keys = new List<string>(registry.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (!registry.TryGetValue(key, out var handle) || handle == null)
                {
                    registry.Remove(key);
                    continue;
                }

                bool shouldCleanup = forceAll || (forCancel ? handle.DespawnOnCancel : handle.DespawnOnSkillEnd);
                if (!shouldCleanup)
                    continue;

                DestroyActor(runner, registry, handle, destroyGameObject: true, removeFromRegistry: true);
            }

            Prune(registry);
        }

        /// <summary>
        /// 더미 핸들에 등록된 이동, 페이드, 공중 높이 코루틴을 중단합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단을 실행할 MonoBehaviour입니다.</param>
        /// <param name="handle">코루틴을 중단할 더미 핸들입니다.</param>
        private static void StopActiveCoroutines(MonoBehaviour runner, SkillDummyActorHandle handle)
        {
            if (handle == null)
                return;

            StopCoroutineIfRunning(runner, handle.ActiveMoveCoroutine);
            handle.ActiveMoveCoroutine = null;
            StopCoroutineIfRunning(runner, handle.ActiveFadeCoroutine);
            handle.ActiveFadeCoroutine = null;
            StopCoroutineIfRunning(runner, handle.ActiveAirHeightCoroutine);
            handle.ActiveAirHeightCoroutine = null;
        }

        /// <summary>
        /// 실행 중인 코루틴 참조가 있으면 중단합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단을 실행할 MonoBehaviour입니다.</param>
        /// <param name="coroutine">중단할 코루틴 참조입니다.</param>
        private static void StopCoroutineIfRunning(MonoBehaviour runner, Coroutine coroutine)
        {
            if (runner != null && coroutine != null)
                runner.StopCoroutine(coroutine);
        }

        /// <summary>
        /// 더미 핸들을 레지스트리에서 제거할 필요가 있으면 actorKey 기준으로 제거합니다.
        /// </summary>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="handle">레지스트리에서 제거할 더미 핸들입니다.</param>
        /// <param name="removeFromRegistry">레지스트리 제거 여부입니다.</param>
        private static void RemoveFromRegistryIfNeeded(
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle handle,
            bool removeFromRegistry)
        {
            if (removeFromRegistry && registry != null && handle != null && !string.IsNullOrEmpty(handle.ActorKey))
                registry.Remove(handle.ActorKey);
        }

        /// <summary>
        /// 더미 캐릭터 페이드 인/아웃을 처리합니다.
        /// </summary>
        /// <param name="runner">후속 Destroy를 실행할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="handle">페이드를 적용할 더미 핸들입니다.</param>
        /// <param name="fadeIn">페이드 인이면 <see langword="true"/>입니다.</param>
        /// <param name="durationSeconds">페이드 시간(초)입니다.</param>
        /// <param name="destroyAfterFade">페이드 아웃 완료 후 Destroy 여부입니다.</param>
        /// <param name="removeFromRegistry">완료 후 레지스트리 제거 여부입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private static IEnumerator FadeDummyCharacterCoroutine(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle handle,
            bool fadeIn,
            float durationSeconds,
            bool destroyAfterFade,
            bool removeFromRegistry)
        {
            if (handle == null || handle.Character == null)
            {
                RemoveFromRegistryIfNeeded(registry, handle, removeFromRegistry);
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

            DestroyActor(runner, registry, handle, destroyGameObject: destroyAfterFade, removeFromRegistry: removeFromRegistry);
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
        private static IEnumerator DummyAnimationFollowupCoroutine(
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
            SkillDummyActorPresentationUtility.ApplyAnimationEndPolicy(
                handle,
                endPolicy,
                endAnimationName,
                endAnimationLoop,
                endAnimationTimeScale);
        }
    }
}
