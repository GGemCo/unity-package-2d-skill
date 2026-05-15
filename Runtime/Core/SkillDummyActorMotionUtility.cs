using System.Collections;
using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 더미 액터의 지면 이동, 이동 중 바라보기, 공중 높이 전환과 공중 유지 보정을 담당합니다.
    /// </summary>
    internal static class SkillDummyActorMotionUtility
    {
        /// <summary>
        /// 더미 캐릭터의 지면 좌표 이동을 시작합니다.
        /// </summary>
        /// <param name="runner">코루틴을 실행하거나 중단할 MonoBehaviour입니다.</param>
        /// <param name="handle">이동 대상 더미 핸들입니다.</param>
        /// <param name="targetPosition">이동 목표 월드 좌표입니다.</param>
        /// <param name="def">이동 이벤트 정의입니다.</param>
        /// <param name="lookTargetTransform">이동 중 실시간으로 추적할 타겟 Transform입니다.</param>
        /// <param name="fallbackLookTargetPosition">실시간 타겟이 없을 때 사용할 고정 바라보기 좌표입니다.</param>
        public static void StartMove(
            MonoBehaviour runner,
            SkillDummyActorHandle handle,
            Vector3 targetPosition,
            MoveDummyCharacterEventDefinition def,
            Transform lookTargetTransform,
            Vector3 fallbackLookTargetPosition)
        {
            if (runner == null || handle == null || handle.Character == null || def == null)
                return;

            SkillDummyActorRuntimeUtility.SyncGroundFromTransform(handle);
            var character = handle.Character;
            Vector3 targetGroundPosition = new Vector3(targetPosition.x, targetPosition.y, handle.GroundPosition.z);

            if (handle.ActiveMoveCoroutine != null)
            {
                if (!def.allowReplace)
                    return;

                runner.StopCoroutine(handle.ActiveMoveCoroutine);
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
                ApplyImmediateMove(handle, targetGroundPosition, def.lookAtTargetDuringMove, lookTargetTransform, fallbackLookTargetPosition);
                return;
            }

            handle.ActiveMoveCoroutine = runner.StartCoroutine(CoMoveByTransform(
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
        /// 레지스트리에 남아 있는 공중 더미들의 중력, 속도, 좌표를 프레임마다 보정합니다.
        /// </summary>
        /// <param name="registry">점검할 더미 액터 레지스트리입니다.</param>
        public static void MaintainAirborneState(Dictionary<string, SkillDummyActorHandle> registry)
        {
            if (registry == null || registry.Count == 0)
                return;

            foreach (var pair in registry)
            {
                SkillDummyActorRuntimeUtility.MaintainAirborneState(pair.Value);
            }
        }

        /// <summary>
        /// 더미 캐릭터의 공중 높이 전환을 시작합니다.
        /// </summary>
        /// <param name="runner">코루틴을 실행하거나 중단할 MonoBehaviour입니다.</param>
        /// <param name="handle">대상 더미 핸들입니다.</param>
        /// <param name="targetAirHeight">목표 공중 높이(+Y)입니다.</param>
        /// <param name="durationSeconds">보간 시간(초)입니다.</param>
        /// <param name="easing">보간 easing입니다.</param>
        /// <param name="allowReplace">기존 공중 보간 덮어쓰기 허용 여부입니다.</param>
        /// <param name="keepAirborneGravity">완료 후에도 공중 중력 오버라이드를 유지할지 여부입니다.</param>
        public static void StartAirHeightTransition(
            MonoBehaviour runner,
            SkillDummyActorHandle handle,
            float targetAirHeight,
            float durationSeconds,
            Easing.EaseType easing,
            bool allowReplace,
            bool keepAirborneGravity)
        {
            if (runner == null || handle == null || handle.Character == null)
                return;

            if (handle.ActiveAirHeightCoroutine != null)
            {
                if (!allowReplace)
                    return;

                runner.StopCoroutine(handle.ActiveAirHeightCoroutine);
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
                ApplyImmediateAirHeight(handle, endHeight, keepAirborneGravity);
                return;
            }

            handle.ActiveAirHeightCoroutine = runner.StartCoroutine(CoAirHeightTransition(
                handle,
                startHeight,
                endHeight,
                duration,
                easing,
                keepAirborneGravity));
        }

        /// <summary>
        /// 즉시 이동이 필요한 경우 지면 좌표와 바라보기 상태를 한 번에 적용합니다.
        /// </summary>
        /// <param name="handle">이동 대상 더미 핸들입니다.</param>
        /// <param name="targetGroundPosition">적용할 지면 좌표입니다.</param>
        /// <param name="lookAtTargetDuringMove">타겟 바라보기 갱신 여부입니다.</param>
        /// <param name="lookTargetTransform">실시간으로 추적할 타겟 Transform입니다.</param>
        /// <param name="fallbackLookTargetPosition">실시간 타겟이 없을 때 사용할 고정 바라보기 좌표입니다.</param>
        private static void ApplyImmediateMove(
            SkillDummyActorHandle handle,
            Vector3 targetGroundPosition,
            bool lookAtTargetDuringMove,
            Transform lookTargetTransform,
            Vector3 fallbackLookTargetPosition)
        {
            handle.GroundPosition = targetGroundPosition;
            SkillDummyActorRuntimeUtility.ApplyWorldPosition(handle);
            if (lookAtTargetDuringMove)
                SkillDummyActorRuntimeUtility.UpdateFacingDuringMove(handle, lookTargetTransform, fallbackLookTargetPosition);
        }

        /// <summary>
        /// 즉시 공중 높이 변경이 필요한 경우 좌표와 물리 속도를 한 번에 보정합니다.
        /// </summary>
        /// <param name="handle">대상 더미 핸들입니다.</param>
        /// <param name="targetAirHeight">적용할 공중 높이입니다.</param>
        /// <param name="keepAirborneGravity">완료 후에도 공중 중력 오버라이드를 유지할지 여부입니다.</param>
        private static void ApplyImmediateAirHeight(
            SkillDummyActorHandle handle,
            float targetAirHeight,
            bool keepAirborneGravity)
        {
            handle.AirHeight = targetAirHeight;
            SkillDummyActorRuntimeUtility.ApplyWorldPosition(handle);
            SkillDummyActorRuntimeUtility.ZeroRigidbodyVelocity(handle);

            if (!keepAirborneGravity && targetAirHeight <= 0f)
                SkillDummyActorRuntimeUtility.ReleaseGravityOverride(handle);
        }

        /// <summary>
        /// 더미 캐릭터의 지면 좌표를 프레임 단위로 보간하고, 필요 시 바라보기 방향을 갱신합니다.
        /// </summary>
        /// <param name="handle">이동 대상 더미 핸들입니다.</param>
        /// <param name="from">시작 지면 좌표입니다.</param>
        /// <param name="to">도착 지면 좌표입니다.</param>
        /// <param name="durationSeconds">이동 시간(초)입니다.</param>
        /// <param name="easeType">보간 easing입니다.</param>
        /// <param name="lookAtTargetDuringMove">이동 중 타겟 바라보기 갱신 여부입니다.</param>
        /// <param name="lookTargetTransform">실시간으로 추적할 타겟 Transform입니다.</param>
        /// <param name="fallbackLookTargetPosition">실시간 타겟이 없을 때 사용할 고정 바라보기 좌표입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private static IEnumerator CoMoveByTransform(
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
                ApplyImmediateMove(handle, to, lookAtTargetDuringMove, lookTargetTransform, fallbackLookTargetPosition);

            handle.ActiveMoveCoroutine = null;
        }

        /// <summary>
        /// 더미 캐릭터의 공중 높이를 프레임 단위로 보간합니다.
        /// </summary>
        /// <param name="handle">대상 더미 핸들입니다.</param>
        /// <param name="startAirHeight">시작 공중 높이입니다.</param>
        /// <param name="targetAirHeight">목표 공중 높이입니다.</param>
        /// <param name="durationSeconds">보간 시간(초)입니다.</param>
        /// <param name="easing">보간 easing입니다.</param>
        /// <param name="keepAirborneGravity">완료 후 중력 오버라이드 유지 여부입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private static IEnumerator CoAirHeightTransition(
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
    }
}
