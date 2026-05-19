using GGemCo2DCore;
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
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">런지 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        /// <param name="arcAnimationController">아크 런지 모션 시작 후 단계별 애니메이션을 관리할 컨트롤러입니다.</param>
        public static void Handle(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds,
            SkillArcLungeAnimationController arcAnimationController)
        {
            if (payloadObj is ArcLungeEventDefinition arcDef)
            {
                HandleArcLunge(skill, ctx, arcDef, eventDurationSeconds, arcAnimationController);
                return;
            }

            if (payloadObj is LungeEventDefinition def)
                HandleLinearLunge(skill, ctx, def, eventDurationSeconds);
        }

        /// <summary>
        /// 직선 런지 이벤트 정의를 해석하여 코어 모션 컨트롤러에 직선 이동 요청을 전달합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        private static void HandleLinearLunge(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            LungeEventDefinition def,
            float eventDurationSeconds)
        {
            if (def == null || ctx.caster == null)
                return;

            var motion = ResolveMotionController(ctx.caster);
            if (motion == null)
                return;

            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : eventDurationSeconds;
            if (duration <= 0f)
                return;

            Vector2 fallbackDirection = ResolveFallbackDirection(ctx.caster, ctx.forward, def.useSnapshotForward);
            if (!SkillLungeMotionResolver.TryResolveLungeMotion(skill, ctx, def, fallbackDirection, out var resolvedDirection, out float resolvedDistance))
                return;

            resolvedDirection = ApplyDirectionPolicy(ctx.caster, resolvedDirection, def.invertForward, def.horizontalOnly);
            resolvedDistance = ApplyScreenClampDistancePolicy(def, ctx.caster, resolvedDirection, resolvedDistance);
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

            if (!TryGetCameraWorldRect(caster.transform.position.z, Mathf.Max(0f, def.screenEdgePadding), out Rect screenRect))
                return distance;

            if (!TryClampDistanceToScreenRect(caster.transform.position, direction, distance, screenRect, out float clampedDistance))
                return distance;

            return clampedDistance;
        }

        /// <summary>
        /// 현재 메인 카메라 기준으로 월드 공간의 화면 경계 사각형을 계산합니다.
        /// </summary>
        /// <param name="worldZ">보정할 오브젝트의 월드 Z 좌표입니다.</param>
        /// <param name="padding">화면 경계 안쪽으로 유지할 여유 거리입니다.</param>
        /// <param name="rect">계산된 화면 경계 Rect입니다.</param>
        /// <returns>경계 계산에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryGetCameraWorldRect(float worldZ, float padding, out Rect rect)
        {
            rect = default;

            Camera camera = SceneGame.Instance != null && SceneGame.Instance.mainCamera != null
                ? SceneGame.Instance.mainCamera
                : Camera.main;
            if (camera == null)
                return false;

            float z = Mathf.Abs(camera.transform.position.z - worldZ);
            Vector3 min = camera.ViewportToWorldPoint(new Vector3(0f, 0f, z));
            Vector3 max = camera.ViewportToWorldPoint(new Vector3(1f, 1f, z));

            float minX = Mathf.Min(min.x, max.x) + padding;
            float maxX = Mathf.Max(min.x, max.x) - padding;
            float minY = Mathf.Min(min.y, max.y) + padding;
            float maxY = Mathf.Max(min.y, max.y) - padding;
            if (maxX < minX || maxY < minY)
                return false;

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        /// <summary>
        /// 시작 위치가 화면 안쪽일 때, 지정한 방향으로 이동 가능한 최대 거리를 화면 경계 기준으로 계산합니다.
        /// </summary>
        /// <param name="startPosition">캐스터 시작 월드 위치입니다.</param>
        /// <param name="direction">정규화 전 이동 방향입니다.</param>
        /// <param name="distance">원래 이동 거리입니다.</param>
        /// <param name="screenRect">월드 공간 화면 경계입니다.</param>
        /// <param name="clampedDistance">화면 경계가 반영된 이동 거리입니다.</param>
        /// <returns>거리 축소가 필요하면 <see langword="true"/>입니다.</returns>
        private static bool TryClampDistanceToScreenRect(
            Vector2 startPosition,
            Vector2 direction,
            float distance,
            Rect screenRect,
            out float clampedDistance)
        {
            clampedDistance = distance;
            if (distance <= 0f || direction.sqrMagnitude <= 1e-6f)
                return false;

            if (!screenRect.Contains(startPosition))
                return false;

            Vector2 normalizedDirection = direction.normalized;
            float maxAllowedDistance = distance;
            const float epsilon = 1e-6f;

            if (normalizedDirection.x > epsilon)
                maxAllowedDistance = Mathf.Min(maxAllowedDistance, (screenRect.xMax - startPosition.x) / normalizedDirection.x);
            else if (normalizedDirection.x < -epsilon)
                maxAllowedDistance = Mathf.Min(maxAllowedDistance, (screenRect.xMin - startPosition.x) / normalizedDirection.x);

            if (normalizedDirection.y > epsilon)
                maxAllowedDistance = Mathf.Min(maxAllowedDistance, (screenRect.yMax - startPosition.y) / normalizedDirection.y);
            else if (normalizedDirection.y < -epsilon)
                maxAllowedDistance = Mathf.Min(maxAllowedDistance, (screenRect.yMin - startPosition.y) / normalizedDirection.y);

            maxAllowedDistance = Mathf.Max(0f, maxAllowedDistance);
            if (maxAllowedDistance >= distance)
                return false;

            clampedDistance = maxAllowedDistance;
            return true;
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
