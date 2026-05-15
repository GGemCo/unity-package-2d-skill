using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// DespawnDummyCharacter 이벤트 정의를 실제 더미 액터 제거 명령으로 변환하고 실행합니다.
    /// </summary>
    internal static class SkillDummyActorDespawnEventUtility
    {
        /// <summary>
        /// 더미 제거 이벤트를 해석해 대상 더미를 찾고, 페이드 아웃과 Destroy 정책에 맞춰 제거를 시작합니다.
        /// </summary>
        /// <param name="runner">페이드 아웃 코루틴과 정리 작업을 실행할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="def">더미 제거 이벤트 정의입니다.</param>
        /// <returns>더미 제거 요청을 시작했으면 <see langword="true"/>입니다.</returns>
        public static bool TryExecuteDespawn(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            DespawnDummyCharacterEventDefinition def)
        {
            if (runner == null || def == null)
                return false;

            if (!SkillDummyActorReferenceUtility.TryGetActorHandle(registry, def.actorKey, def.missingActorPolicy, out var handle))
                return false;

            BeginDespawnFromDefinition(runner, registry, handle, def);
            return true;
        }

        /// <summary>
        /// 이벤트 정의에 설정된 제거 옵션을 생명주기 유틸리티 호출로 전달합니다.
        /// </summary>
        /// <param name="runner">페이드 아웃 코루틴과 정리 작업을 실행할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="handle">제거할 더미 핸들입니다.</param>
        /// <param name="def">더미 제거 이벤트 정의입니다.</param>
        private static void BeginDespawnFromDefinition(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle handle,
            DespawnDummyCharacterEventDefinition def)
        {
            SkillDummyActorLifecycleUtility.BeginDespawn(
                runner,
                registry,
                handle,
                fadeOutEnabled: def.fadeOutEnabled,
                fadeOutDurationSeconds: def.fadeOutDurationSeconds,
                destroyAfterFade: def.destroyAfterFade,
                removeFromRegistry: true);
        }
    }
}
