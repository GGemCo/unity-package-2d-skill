using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 더미 액터 레지스트리와 캐스터 임시 핸들에서 이벤트 대상 참조를 해석합니다.
    /// </summary>
    internal static class SkillDummyActorReferenceUtility
    {
        /// <summary>
        /// 캐스터를 더미 액터 참조처럼 다룰 때 사용하는 내부 식별 키입니다.
        /// </summary>
        public const string CasterActorKey = "__caster__";

        /// <summary>
        /// 더미 이벤트가 지정한 대상(Actor/Caster)을 실제 런타임 핸들로 해석합니다.
        /// </summary>
        /// <param name="runner">캐스터 임시 핸들의 코루틴 정리에 사용할 MonoBehaviour입니다.</param>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="casterHandle">Caster 참조에 재사용할 임시 핸들입니다.</param>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="actorReferenceType">대상 참조 방식입니다.</param>
        /// <param name="actorKey">Actor 참조일 때 사용할 식별 키입니다.</param>
        /// <param name="missingPolicy">대상 미존재 시 처리 정책입니다.</param>
        /// <param name="handle">해석된 런타임 핸들입니다.</param>
        /// <returns>대상 해석에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryResolveActorHandle(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> registry,
            SkillDummyActorHandle casterHandle,
            SkillTargetContext ctx,
            DummyActorReferenceType actorReferenceType,
            string actorKey,
            DummyMissingActorPolicy missingPolicy,
            out SkillDummyActorHandle handle)
        {
            switch (actorReferenceType)
            {
                case DummyActorReferenceType.Caster:
                    return TryGetCasterHandle(runner, casterHandle, ctx, missingPolicy, out handle);
                case DummyActorReferenceType.Actor:
                default:
                    return TryGetActorHandle(registry, actorKey, missingPolicy, out handle);
            }
        }

        /// <summary>
        /// actorKey에 해당하는 더미 액터 핸들을 레지스트리에서 조회합니다.
        /// </summary>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="actorKey">조회할 더미 액터 키입니다.</param>
        /// <param name="missingPolicy">미존재 시 로깅 정책입니다.</param>
        /// <param name="handle">조회된 더미 액터 핸들입니다.</param>
        /// <returns>조회에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryGetActorHandle(
            Dictionary<string, SkillDummyActorHandle> registry,
            string actorKey,
            DummyMissingActorPolicy missingPolicy,
            out SkillDummyActorHandle handle)
        {
            handle = null;

            string normalizedKey = SkillDummyEventUtility.NormalizeActorKey(actorKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                if (missingPolicy == DummyMissingActorPolicy.Warn)
                    Debug.LogWarning("[SkillExecutor] Dummy actorKey is empty.");
                return false;
            }

            if (registry == null)
            {
                WarnMissingActor(normalizedKey, missingPolicy);
                return false;
            }

            SkillDummyActorLifecycleUtility.Prune(registry);

            if (registry.TryGetValue(normalizedKey, out handle) && handle != null && handle.Character != null)
                return true;

            registry.Remove(normalizedKey);
            handle = null;
            WarnMissingActor(normalizedKey, missingPolicy);
            return false;
        }

        /// <summary>
        /// 현재 컨텍스트의 캐스터를 더미 액터 핸들 형태로 변환해 반환합니다.
        /// </summary>
        /// <param name="runner">캐스터가 변경되었을 때 기존 임시 상태 정리에 사용할 MonoBehaviour입니다.</param>
        /// <param name="casterHandle">Caster 참조에 재사용할 임시 핸들입니다.</param>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="missingPolicy">캐스터 해석 실패 시 처리 정책입니다.</param>
        /// <param name="handle">캐스터에 바인딩된 임시 핸들입니다.</param>
        /// <returns>캐스터를 핸들로 해석하면 <see langword="true"/>입니다.</returns>
        public static bool TryGetCasterHandle(
            MonoBehaviour runner,
            SkillDummyActorHandle casterHandle,
            SkillTargetContext ctx,
            DummyMissingActorPolicy missingPolicy,
            out SkillDummyActorHandle handle)
        {
            handle = null;

            if (casterHandle == null)
                return false;

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

            if (!ReferenceEquals(casterHandle.Character, character))
            {
                ResetCasterTransientState(runner, casterHandle, clearCharacter: true);
                casterHandle.Character = character;
            }

            casterHandle.ActorKey = CasterActorKey;
            casterHandle.AirHeight = 0f;
            SkillDummyActorRuntimeUtility.SyncGroundFromTransform(casterHandle);
            handle = casterHandle;
            return true;
        }

        /// <summary>
        /// 캐릭터 잔상 이벤트가 지정한 대상을 실제 GameObject로 해석합니다.
        /// </summary>
        /// <param name="registry">더미 액터 레지스트리입니다.</param>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="def">캐릭터 잔상 이벤트 정의입니다.</param>
        /// <param name="targetObject">해석된 잔상 대상 GameObject입니다.</param>
        /// <returns>대상 해석에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryResolveAfterimageTarget(
            Dictionary<string, SkillDummyActorHandle> registry,
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
                    if (!TryGetActorHandle(registry, def.actorKey, def.missingActorPolicy, out var handle))
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
        /// Caster 참조 임시 핸들에 남아 있는 이동, 페이드, 공중 높이, 애니메이션 후속 작업을 정리합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단을 실행할 MonoBehaviour입니다.</param>
        /// <param name="casterHandle">정리할 Caster 임시 핸들입니다.</param>
        /// <param name="clearCharacter">캐릭터 참조까지 제거할지 여부입니다.</param>
        public static void ResetCasterTransientState(
            MonoBehaviour runner,
            SkillDummyActorHandle casterHandle,
            bool clearCharacter)
        {
            SkillDummyActorLifecycleUtility.ResetCasterHandleTransientState(runner, casterHandle, clearCharacter);
        }

        /// <summary>
        /// 정책이 경고일 때 더미 액터 미발견 로그를 출력합니다.
        /// </summary>
        /// <param name="actorKey">찾지 못한 더미 액터 키입니다.</param>
        /// <param name="missingPolicy">미존재 시 로깅 정책입니다.</param>
        private static void WarnMissingActor(string actorKey, DummyMissingActorPolicy missingPolicy)
        {
            if (missingPolicy == DummyMissingActorPolicy.Warn)
                Debug.LogWarning($"[SkillExecutor] Dummy actor not found. key={actorKey}");
        }
    }
}
