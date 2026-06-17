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
        private const float LungeDistanceEpsilon = 0.0001f;
        private static readonly RaycastHit2D[] LungeWallProbeHits = new RaycastHit2D[8];

        /// <summary>
        /// 런지 계열 이벤트 페이로드를 판별하고 직선 또는 아크 런지 실행 흐름으로 위임합니다.
        /// </summary>
        /// <param name="runner">더미 Actor 해석 및 캐스터 임시 핸들 정리에 사용할 실행기입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">런지 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산한 기본 지속 시간입니다.</param>
        /// <param name="dummyActors">현재 스킬 실행에서 actorKey 기준으로 등록된 더미 Actor 목록입니다.</param>
        /// <param name="casterActorHandle">Caster 참조를 더미 Actor처럼 다루기 위한 임시 핸들입니다.</param>
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
                HandleArcLunge(runner, skill, ctx, arcDef, eventDurationSeconds, arcAnimationController);
                return;
            }

            if (payloadObj is LungeEventDefinition def)
                HandleLinearLunge(runner, skill, ctx, def, eventDurationSeconds, dummyActors, casterActorHandle);
        }

        /// <summary>
        /// 직선 런지 이벤트 정의를 해석하여 Core 모션 컨트롤러에 직선 이동 요청을 전달합니다.
        /// </summary>
        /// <param name="runner">더미 Actor 해석 및 임시 핸들 정리에 사용할 실행기입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산한 기본 지속 시간입니다.</param>
        /// <param name="dummyActors">현재 스킬 실행에서 actorKey 기준으로 등록된 더미 Actor 목록입니다.</param>
        /// <param name="casterActorHandle">Caster 참조를 더미 Actor처럼 다루기 위한 임시 핸들입니다.</param>
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
            bool useFallbackBodyCollisionPolicy = false;
            if (TryResolveBlockedFallbackMotion(
                    ctx,
                    actorObject,
                    def,
                    resolvedDirection,
                    resolvedDistance,
                    out Vector2 fallbackResolvedDirection,
                    out float fallbackResolvedDistance))
            {
                resolvedDirection = fallbackResolvedDirection;
                resolvedDistance = ApplyScreenClampDistancePolicy(def, actorObject, resolvedDirection, fallbackResolvedDistance);
                useFallbackBodyCollisionPolicy = true;
            }

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
                collisionPolicy: useFallbackBodyCollisionPolicy
                    ? MotionCollisionPolicy.IgnoreTargetCharacter
                    : SkillLungeMotionResolver.ResolveMotionCollisionPolicy(def),
                collisionTarget: useFallbackBodyCollisionPolicy
                    ? ctx.lockedTarget
                    : SkillLungeMotionResolver.ResolveMotionCollisionTarget(ctx, def),
                bodyCollisionPolicy: useFallbackBodyCollisionPolicy
                    ? def.blockedFallbackBodyCollisionPolicy
                    : MotionBodyCollisionPolicy.UseCharacterDefault);

            if (!motion.TryStartMotion(in req))
                return;

            if (def.acquireAirborneDuringEvent)
                BeginLungeAirborneState(runner, actorObject, motion, "SkillLinearLunge");
        }

        /// <summary>
        /// 직선 런지를 실제로 수행할 캐릭터 오브젝트를 Caster 또는 더미 Actor 설정에서 해석합니다.
        /// </summary>
        /// <param name="runner">Caster 참조 임시 핸들 정리에 사용할 실행기입니다.</param>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="dummyActors">현재 스킬 실행에서 생성된 더미 Actor 목록입니다.</param>
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

            // 더미 Actor 선택 시에는 기존 더미 이벤트와 같은 조회 및 경고 정책을 사용합니다.
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
        /// <param name="runner">런지 공중 상태 해제 코루틴을 실행할 실행기입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">아크 런지 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산한 기본 지속 시간입니다.</param>
        /// <param name="arcAnimationController">아크 런지 단계별 애니메이션 컨트롤러입니다.</param>
        private static void HandleArcLunge(
            MonoBehaviour runner,
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

            if (def.acquireAirborneDuringEvent)
                BeginLungeAirborneState(runner, ctx.caster, motion, "SkillArcLunge");

            arcAnimationController?.Begin(ctx.caster, motion, def, riseDuration, apexHoldDuration);
        }

        /// <summary>
        /// 런지 이벤트가 진행되는 동안 캐릭터를 공통 공중 상태로 등록합니다.
        /// 모션 채널이 종료되면 등록한 핸들을 자동으로 해제합니다.
        /// </summary>
        /// <param name="runner">해제 코루틴을 실행할 MonoBehaviour입니다.</param>
        /// <param name="actorObject">런지를 수행하는 캐릭터 오브젝트입니다.</param>
        /// <param name="motion">런지 모션이 재생 중인 모션 컨트롤러입니다.</param>
        /// <param name="reason">디버그 확인용 등록 사유입니다.</param>
        private static void BeginLungeAirborneState(
            MonoBehaviour runner,
            GameObject actorObject,
            ICharacterMotionController motion,
            string reason)
        {
            if (runner == null || actorObject == null || motion == null)
                return;

            CharacterBase character = actorObject.GetComponentInParent<CharacterBase>();
            if (character == null)
                return;

            CharacterAirborneHandle handle = character.AcquireAirborne(CharacterAirborneSource.Lunge, reason);
            if (!handle.IsValid)
                return;

            runner.StartCoroutine(ReleaseLungeAirborneWhenMotionEnds(character, motion, handle));
        }

        /// <summary>
        /// Skill 모션 채널이 종료될 때까지 대기한 뒤 런지 공중 상태를 해제합니다.
        /// </summary>
        /// <param name="character">공중 상태를 해제할 캐릭터입니다.</param>
        /// <param name="motion">상태 종료 기준으로 사용할 모션 컨트롤러입니다.</param>
        /// <param name="handle">해제할 공중 상태 핸들입니다.</param>
        private static System.Collections.IEnumerator ReleaseLungeAirborneWhenMotionEnds(
            CharacterBase character,
            ICharacterMotionController motion,
            CharacterAirborneHandle handle)
        {
            while (character != null && motion != null && motion.IsPlaying(MotionChannel.Skill))
                yield return null;

            if (character != null)
                character.ReleaseAirborne(handle);
        }

        /// <summary>
        /// 캐스터에 적용된 스킬 모션 컨트롤러를 조회합니다.
        /// </summary>
        /// <param name="caster">모션 컨트롤러를 찾을 캐스터 오브젝트입니다.</param>
        /// <returns>캐스터 상위 계층의 모션 컨트롤러입니다.</returns>
        private static ICharacterMotionController ResolveMotionController(GameObject caster)
        {
            return caster != null ? caster.GetComponentInParent<ICharacterMotionController>() : null;
        }

        /// <summary>
        /// 기본 런지 진행 방향이 지형 또는 화면 경계에 거의 막혔을 때 대체 이동 방향과 거리를 계산합니다.
        /// </summary>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="actorObject">런지를 수행하는 실제 캐릭터 오브젝트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="currentDirection">기본 정책으로 계산된 최종 이동 방향입니다.</param>
        /// <param name="currentDistance">화면 보정까지 반영된 기본 이동 거리입니다.</param>
        /// <param name="resolvedDirection">대체 이동 방향입니다.</param>
        /// <param name="resolvedDistance">대체 이동 거리입니다.</param>
        /// <returns>대체 이동을 사용할 수 있으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveBlockedFallbackMotion(
            SkillTargetContext ctx,
            GameObject actorObject,
            LungeEventDefinition def,
            Vector2 currentDirection,
            float currentDistance,
            out Vector2 resolvedDirection,
            out float resolvedDistance)
        {
            resolvedDirection = currentDirection;
            resolvedDistance = currentDistance;

            if (def == null || actorObject == null)
                return false;

            if (def.blockedFallbackMode != SkillLungeBlockedFallbackMode.PassThroughLockedTarget)
                return false;

            if (!IsLungeDirectionBlocked(actorObject, currentDirection, currentDistance, def))
                return false;

            if (!TryResolvePassThroughLockedTargetFallback(
                    ctx,
                    actorObject,
                    def,
                    currentDirection,
                    out resolvedDirection,
                    out resolvedDistance))
            {
                return false;
            }

            // 반대편도 바로 막혀 있으면 무리하게 폴백 이동을 시작하지 않습니다.
            float availableDistance = ResolveWallLimitedDistance(
                actorObject,
                resolvedDirection,
                resolvedDistance,
                def.blockedFallbackProbeSkin);
            if (availableDistance <= Mathf.Max(0f, def.blockedFallbackMinDistance))
                return false;

            resolvedDistance = Mathf.Min(resolvedDistance, availableDistance);
            return resolvedDistance > LungeDistanceEpsilon;
        }

        /// <summary>
        /// 현재 런지 방향으로 실제 이동 가능한 거리가 막힘 기준 이하인지 검사합니다.
        /// </summary>
        /// <param name="actorObject">런지를 수행하는 캐릭터 오브젝트입니다.</param>
        /// <param name="direction">검사할 이동 방향입니다.</param>
        /// <param name="distance">검사할 이동 거리입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <returns>이동 가능 거리가 막힘 기준 이하이면 <see langword="true"/>입니다.</returns>
        private static bool IsLungeDirectionBlocked(
            GameObject actorObject,
            Vector2 direction,
            float distance,
            LungeEventDefinition def)
        {
            float minDistance = Mathf.Max(0f, def.blockedFallbackMinDistance);
            if (distance <= minDistance)
                return true;

            float availableDistance = ResolveWallLimitedDistance(
                actorObject,
                direction,
                distance,
                def.blockedFallbackProbeSkin);
            return availableDistance <= minDistance;
        }

        /// <summary>
        /// 잠금 대상을 통과해 반대편으로 이동하기 위한 방향과 거리를 계산합니다.
        /// </summary>
        /// <param name="ctx">스킬 대상 컨텍스트입니다.</param>
        /// <param name="actorObject">런지를 수행하는 캐릭터 오브젝트입니다.</param>
        /// <param name="def">직선 런지 이벤트 정의입니다.</param>
        /// <param name="blockedDirection">막힌 기본 이동 방향입니다.</param>
        /// <param name="resolvedDirection">잠금 대상 방향의 이동 방향입니다.</param>
        /// <param name="resolvedDistance">대상 통과에 필요한 이동 거리입니다.</param>
        /// <returns>대상 통과 이동을 계산할 수 있으면 <see langword="true"/>입니다.</returns>
        private static bool TryResolvePassThroughLockedTargetFallback(
            SkillTargetContext ctx,
            GameObject actorObject,
            LungeEventDefinition def,
            Vector2 blockedDirection,
            out Vector2 resolvedDirection,
            out float resolvedDistance)
        {
            resolvedDirection = Vector2.zero;
            resolvedDistance = 0f;

            if (ctx.lockedTarget == null)
                return false;

            Vector3 actorPosition = actorObject.transform.position;
            Vector3 targetPosition = ctx.lockedTarget.transform.position;
            Vector2 delta = def.horizontalOnly
                ? new Vector2(targetPosition.x - actorPosition.x, 0f)
                : new Vector2(targetPosition.x - actorPosition.x, targetPosition.y - actorPosition.y);

            float targetDistance = delta.magnitude;
            if (targetDistance <= LungeDistanceEpsilon)
            {
                resolvedDirection = -blockedDirection;
                if (def.horizontalOnly)
                    resolvedDirection = new Vector2(Mathf.Sign(resolvedDirection.x), 0f);
            }
            else
            {
                resolvedDirection = delta / targetDistance;
                if (def.horizontalOnly)
                    resolvedDirection = new Vector2(Mathf.Sign(resolvedDirection.x), 0f);
            }

            if (resolvedDirection.sqrMagnitude <= LungeDistanceEpsilon)
                return false;

            resolvedDistance = targetDistance + Mathf.Max(0f, def.passThroughExtraDistance);
            return resolvedDistance > LungeDistanceEpsilon;
        }

        /// <summary>
        /// TileMapGround 레이어 기준으로 지정 방향의 지형 충돌 전까지 이동 가능한 거리를 계산합니다.
        /// </summary>
        /// <param name="actorObject">검사할 캐릭터 오브젝트입니다.</param>
        /// <param name="direction">검사할 이동 방향입니다.</param>
        /// <param name="distance">검사할 최대 이동 거리입니다.</param>
        /// <param name="skin">충돌체와 떨어져 멈추기 위한 여유 거리입니다.</param>
        /// <returns>지형 충돌 전까지 이동 가능한 거리입니다.</returns>
        private static float ResolveWallLimitedDistance(
            GameObject actorObject,
            Vector2 direction,
            float distance,
            float skin)
        {
            if (actorObject == null || distance <= 0f || direction.sqrMagnitude <= LungeDistanceEpsilon)
                return Mathf.Max(0f, distance);

            Rigidbody2D rb = actorObject.GetComponentInParent<Rigidbody2D>();
            if (rb == null)
                return Mathf.Max(0f, distance);

            int wallMask = ResolveWallLayerMask();
            if (wallMask == 0)
                return Mathf.Max(0f, distance);

            Vector2 normalized = direction.normalized;
            float safeSkin = Mathf.Max(0f, skin);
            ContactFilter2D filter = default;
            filter.useLayerMask = true;
            filter.layerMask = wallMask;
            filter.useTriggers = false;

            int hitCount = rb.Cast(normalized, filter, LungeWallProbeHits, distance + safeSkin);
            if (hitCount <= 0)
                return Mathf.Max(0f, distance);

            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = LungeWallProbeHits[i];
                if (hit.collider == null)
                    continue;

                if (hit.distance < nearestDistance)
                    nearestDistance = hit.distance;
            }

            if (float.IsPositiveInfinity(nearestDistance))
                return Mathf.Max(0f, distance);

            return Mathf.Clamp(nearestDistance - safeSkin, 0f, Mathf.Max(0f, distance));
        }

        /// <summary>
        /// 런지 막힘 판정에 사용할 지형 레이어 마스크를 계산합니다.
        /// </summary>
        /// <returns>TileMapGround 레이어 마스크입니다. 설정을 찾지 못하면 0입니다.</returns>
        private static int ResolveWallLayerMask()
        {
            string wallLayerName = ConfigLayer.GetValue(ConfigLayer.Keys.TileMapGround);
            if (string.IsNullOrWhiteSpace(wallLayerName))
                return 0;

            return LayerMask.GetMask(wallLayerName);
        }

        /// <summary>
        /// 스냅샷 Forward 사용 여부에 따라 런지 해석 실패 시 사용할 기본 방향을 계산합니다.
        /// </summary>
        /// <param name="caster">방향 보정 기준이 되는 캐스터 오브젝트입니다.</param>
        /// <param name="snapshotForward">스킬 시작 시점에 기록된 Forward 벡터입니다.</param>
        /// <param name="useSnapshotForward">스냅샷 Forward를 우선 사용할지 여부입니다.</param>
        /// <returns>런지 대상 해석 실패 시 사용할 기본 2D 방향입니다.</returns>
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
        /// 화면 경계 클램프 정책이 활성화된 경우, 런지 최종 위치가 화면 밖으로 나가지 않도록 이동 거리를 보정합니다.
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
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산한 기본 지속 시간입니다.</param>
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
