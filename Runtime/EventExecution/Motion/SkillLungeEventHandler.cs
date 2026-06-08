using GGemCo2DCore;
using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 직선 런지와 아크 런지 이벤트의 방향 해석, 모션 요청 생성, 후속 애니메이션 시작을 담당합니다.
    /// </summary>
    internal static class SkillLungeEventHandler
    {
        /// <summary>
        /// 런지 계열 이벤트 페이로드를 판별하고 직선 또는 아크 런지 실행 흐름으로 위임합니다.
        /// </summary>
        /// <param name="runner">더미 Actor 해석 시 캐스터 임시 핸들의 코루틴 정리에 사용할 실행기입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">런지 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        /// <param name="dummyActors">현재 스킬 실행에서 actorKey 기준으로 등록된 더미 Actor 레지스트리입니다.</param>
        /// <param name="casterActorHandle">Caster 참조를 더미 Actor처럼 다룰 때 사용하는 임시 핸들입니다.</param>
        /// <param name="arcAnimationController">아크 런지 모션 시작 후 단계별 애니메이션을 관리할 컨트롤러입니다.</param>
        public static void Handle(
            MonoBehaviour runner,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds,
            Dictionary<string, SkillDummyActorHandle> dummyActors,
            SkillDummyActorHandle casterActorHandle,
            SkillArcLungeAnimationController arcAnimationController)
        {
            if (payloadObj is ArcLungeEventDefinition arcDef)
            {
                HandleArcLunge(skill, ctx, arcDef, eventDurationSeconds, arcAnimationController);
                return;
            }

            if (payloadObj is LungeEventDefinition def)
                HandleLinearLunge(runner, skill, ctx, def, eventDurationSeconds, dummyActors, casterActorHandle);
        }

        /// <summary>
        /// 직선 런지 이벤트 정의를 해석하여 코어 모션 컨트롤러에 직선 이동 요청을 전달합니다.
        /// </summary>
        /// <param name="runner">더미 Actor 해석 시 캐스터 임시 핸들의 코루틴 정리에 사용할 실행기입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        /// <param name="dummyActors">현재 스킬 실행에서 actorKey 기준으로 등록된 더미 Actor 레지스트리입니다.</param>
        /// <param name="casterActorHandle">Caster 참조를 더미 Actor처럼 다룰 때 사용하는 임시 핸들입니다.</param>
        private static void HandleLinearLunge(
            MonoBehaviour runner,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            LungeEventDefinition def,
            float eventDurationSeconds,
            Dictionary<string, SkillDummyActorHandle> dummyActors,
            SkillDummyActorHandle casterActorHandle)
        {
            if (def == null)
                return;

            if (!TryResolveLinearLungeActor(runner, ctx, def, dummyActors, casterActorHandle, out GameObject actorObject))
                return;

            var motion = ResolveMotionController(actorObject);
            if (motion == null)
                return;

            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : eventDurationSeconds;
            if (duration <= 0f)
                return;

            Vector2 fallbackDirection = ResolveFallbackDirection(actorObject, ctx.forward, def.useSnapshotForward);
            if (!SkillLungeMotionResolver.TryResolveLungeMotion(skill, ctx, actorObject, def, fallbackDirection, out var resolvedDirection, out float resolvedDistance))
                return;

            resolvedDirection = ApplyDirectionPolicy(actorObject, resolvedDirection, def.invertForward, def.horizontalOnly);
            resolvedDistance = ApplyScreenClampDistancePolicy(def, actorObject, resolvedDirection, resolvedDistance);
            if (resolvedDistance <= 0f)
                return;

            var req = new MotionRequest(
                MotionChannel.Skill,
                MotionKind.Linear,
                resolvedDirection,
                duration,
                resolvedDistance,
                def.easing,
                holdSecondsAfter: 0f,
                stopAtEnd: def.stopAtEnd,
                useMovePosition: def.useMovePosition,
                allowReplace: def.allowReplace,
                collisionPolicy: SkillLungeMotionResolver.ResolveMotionCollisionPolicy(def),
                collisionTarget: SkillLungeMotionResolver.ResolveMotionCollisionTarget(ctx, def));

            motion.TryStartMotion(in req);
        }

        /// <summary>
        /// 직선 런지를 실제로 수행할 캐릭터 오브젝트를 Caster 또는 더미 Actor 설정에서 해석합니다.
        /// </summary>
        /// <param name="runner">Caster 참조 임시 핸들을 정리할 실행기입니다.</param>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="dummyActors">현재 스킬 실행에서 생성된 더미 Actor 레지스트리입니다.</param>
        /// <param name="casterActorHandle">Caster를 Actor 핸들처럼 다루기 위한 임시 핸들입니다.</param>
        /// <param name="actorObject">해석된 런지 수행 오브젝트입니다.</param>
        /// <returns>런지를 수행할 캐릭터를 찾았으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveLinearLungeActor(
            MonoBehaviour runner,
            SkillTargetContext ctx,
            LungeEventDefinition def,
            Dictionary<string, SkillDummyActorHandle> dummyActors,
            SkillDummyActorHandle casterActorHandle,
            out GameObject actorObject)
        {
            actorObject = null;

            if (def.actorReferenceType == DummyActorReferenceType.Caster)
            {
                actorObject = ctx.caster;
                if (actorObject == null && def.missingActorPolicy == DummyMissingActorPolicy.Warn)
                    Debug.LogWarning("[SkillExecutor] Lunge actor is Caster, but caster is null.");

                return actorObject != null;
            }

            // 더미 Actor 선택 시에는 기존 더미 이벤트와 같은 조회/경고 정책을 사용합니다.
            if (!SkillDummyActorReferenceUtility.TryResolveActorHandle(
                    runner,
                    dummyActors,
                    casterActorHandle,
                    ctx,
                    def.actorReferenceType,
                    def.actorKey,
                    def.missingActorPolicy,
                    out SkillDummyActorHandle handle))
            {
                return false;
            }

            actorObject = handle.Character != null ? handle.Character.gameObject : null;
            return actorObject != null;
        }

        /// <summary>
        /// 아크 런지 이벤트 정의를 해석하여 포물선 이동 요청을 전달하고 애니메이션 컨트롤러를 시작합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">아크 런지 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        /// <param name="arcAnimationController">아크 런지 단계별 애니메이션 컨트롤러입니다.</param>
        private static void HandleArcLunge(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            ArcLungeEventDefinition def,
            float eventDurationSeconds,
            SkillArcLungeAnimationController arcAnimationController)
        {
            if (def == null || ctx.caster == null)
                return;

            var motion = ResolveMotionController(ctx.caster);
            if (motion == null)
                return;

            float riseDuration = Mathf.Max(0f, def.riseDurationSeconds);
            float apexHoldDuration = Mathf.Max(0f, def.apexHoldDurationSeconds);
            float fallDuration = Mathf.Max(0f, def.fallDurationSeconds);
            float totalDuration = ResolveArcTotalDuration(
                def,
                riseDuration,
                apexHoldDuration,
                fallDuration,
                eventDurationSeconds);
            if (totalDuration <= 0f || def.arcHeight <= 0f)
                return;

            Vector2 fallbackDirection = ResolveFallbackDirection(ctx.caster, ctx.forward, def.useSnapshotForward);
            if (!SkillLungeMotionResolver.TryResolveArcLungeMotion(skill, ctx, def, fallbackDirection, out var resolvedDirection, out float resolvedDistance))
                return;

            resolvedDirection = ApplyDirectionPolicy(ctx.caster, resolvedDirection, def.invertForward, def.horizontalOnly);
            if (resolvedDistance <= 0f)
                return;

            SkillLungeMotionResolver.NormalizeArcDurations(
                riseDuration,
                apexHoldDuration,
                fallDuration,
                totalDuration,
                out float normalizedRise,
                out float normalizedApex,
                out float normalizedFall);

            var req = new MotionRequest(
                MotionChannel.Skill,
                MotionKind.Arc,
                resolvedDirection,
                totalDuration,
                resolvedDistance,
                def.easing,
                arcHeight: def.arcHeight,
                arcMode: def.arcMode,
                arcRiseEaseType: def.arcRiseEase,
                arcFallEaseType: def.arcFallEase,
                arcApexHoldNormalized: normalizedApex,
                arcRiseRatioNormalized: normalizedRise,
                arcFallRatioNormalized: normalizedFall,
                holdSecondsAfter: 0f,
                stopAtEnd: def.stopAtEnd,
                useMovePosition: def.useMovePosition,
                allowReplace: def.allowReplace,
                collisionPolicy: SkillLungeMotionResolver.ResolveMotionCollisionPolicy(def.collisionPolicy),
                collisionTarget: SkillLungeMotionResolver.ResolveMotionCollisionTarget(ctx, def.collisionPolicy));

            if (!motion.TryStartMotion(in req))
                return;

            arcAnimationController?.Begin(ctx.caster, motion, def, riseDuration, apexHoldDuration);
        }

        /// <summary>
        /// 캐스터에서 사용할 스킬 모션 컨트롤러를 조회합니다.
        /// </summary>
        /// <param name="caster">모션 컨트롤러를 찾을 캐스터 오브젝트입니다.</param>
        /// <returns>캐스터 상위 계층의 모션 컨트롤러입니다.</returns>
        private static ICharacterMotionController ResolveMotionController(GameObject caster)
        {
            return caster != null ? caster.GetComponentInParent<ICharacterMotionController>() : null;
        }

        /// <summary>
        /// 스냅샷 전방 사용 여부에 따라 런지 해석에 사용할 기본 방향을 계산합니다.
        /// </summary>
        /// <param name="caster">방향 보정 기준이 되는 캐스터 오브젝트입니다.</param>
        /// <param name="snapshotForward">스킬 시작 시점에 기록된 전방 벡터입니다.</param>
        /// <param name="useSnapshotForward">스냅샷 전방을 우선 사용할지 여부입니다.</param>
        /// <returns>런지 타겟 해석 실패 시 사용할 기본 2D 방향입니다.</returns>
        private static Vector2 ResolveFallbackDirection(GameObject caster, Vector3 snapshotForward, bool useSnapshotForward)
        {
            return useSnapshotForward
                ? (Vector2)SkillDirectionResolver.ResolveForward2D(caster, snapshotForward)
                : SkillDirectionResolver.ResolveCurrentFacing2D(caster);
        }

        /// <summary>
        /// 런지 이벤트의 반전 및 수평 이동 정책을 적용해 최종 이동 방향을 보정합니다.
        /// </summary>
        /// <param name="caster">좌우 방향 대체값을 계산할 캐스터 오브젝트입니다.</param>
        /// <param name="direction">해석된 원본 이동 방향입니다.</param>
        /// <param name="invertForward">이동 방향을 반전할지 여부입니다.</param>
        /// <param name="horizontalOnly">수평 방향만 사용할지 여부입니다.</param>
        /// <returns>이벤트 정책이 적용된 최종 이동 방향입니다.</returns>
        private static Vector2 ApplyDirectionPolicy(
            GameObject caster,
            Vector2 direction,
            bool invertForward,
            bool horizontalOnly)
        {
            if (invertForward)
                direction = -direction;

            if (Mathf.Abs(direction.x) < 1e-4f && horizontalOnly)
            {
                float sign = caster != null ? Mathf.Sign(caster.transform.localScale.x) : 1f;
                if (Mathf.Approximately(sign, 0f))
                    sign = 1f;

                direction = new Vector2(sign, 0f);
            }

            if (horizontalOnly)
                return new Vector2(Mathf.Sign(direction.x), 0f);

            if (direction.sqrMagnitude > 1e-6f)
                direction.Normalize();

            return direction;
        }

        /// <summary>
        /// 화면 경계 클램프 정책이 활성화된 경우, 런지 최종 도착 위치가 화면을 벗어나지 않도록 이동 거리를 보정합니다.
        /// </summary>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="caster">현재 런지를 수행하는 캐스터 오브젝트입니다.</param>
        /// <param name="direction">정책 적용이 끝난 최종 이동 방향입니다.</param>
        /// <param name="distance">정책 적용 전 이동 거리입니다.</param>
        /// <returns>화면 경계 정책이 반영된 이동 거리입니다.</returns>
        private static float ApplyScreenClampDistancePolicy(
            LungeEventDefinition def,
            GameObject caster,
            Vector2 direction,
            float distance)
        {
            if (def == null || caster == null || distance <= 0f)
                return distance;

            if (def.screenClampPolicy != SkillLungeScreenClampPolicy.ClampToViewportEdge)
                return distance;

            if (!SkillScreenClampUtility.TryClampDistanceToCameraRect(
                    caster.transform.position,
                    direction,
                    distance,
                    Mathf.Max(0f, def.screenEdgePadding),
                    out float clampedDistance))
                return distance;

            return clampedDistance;
        }

        /// <summary>
        /// 아크 런지 이벤트의 전체 이동 시간을 명시값, 단계 합산값, 이벤트 지속 시간 순으로 계산합니다.
        /// </summary>
        /// <param name="def">아크 런지 이벤트 정의입니다.</param>
        /// <param name="riseDuration">상승 단계 지속 시간(초)입니다.</param>
        /// <param name="apexHoldDuration">정점 대기 단계 지속 시간(초)입니다.</param>
        /// <param name="fallDuration">하강 단계 지속 시간(초)입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        /// <returns>아크 런지 전체 이동 시간(초)입니다.</returns>
        private static float ResolveArcTotalDuration(
            ArcLungeEventDefinition def,
            float riseDuration,
            float apexHoldDuration,
            float fallDuration,
            float eventDurationSeconds)
        {
            float totalDuration = def.durationOverrideSeconds > 0f
                ? def.durationOverrideSeconds
                : riseDuration + apexHoldDuration + fallDuration;

            return totalDuration > 0f ? totalDuration : eventDurationSeconds;
        }
    }
}
