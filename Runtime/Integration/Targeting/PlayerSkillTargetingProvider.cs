using Object = UnityEngine.Object;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어 퀵슬롯/입력 레이어가 스킬 테이블의 TargetingMode, CastRange, PlacementRange 의미를 해석해
    /// 적절한 <see cref="SkillDriverRequest"/>를 만들 수 있도록 돕는 기본 구현입니다.
    /// 명시적 락온/조준 포트가 있으면 이를 우선 사용하고, 없으면 전방 cone 기반 soft target과 거리 fallback을 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSkillTargetingProvider : MonoBehaviour, IPlayerSkillTargetingProvider
    {
        [Header("Soft Target")]
        [SerializeField, Tooltip("명시적 락온이 없을 때 자동 타겟 탐색에 사용하는 전방 cone 각도(도)입니다.")]
        private float softTargetConeAngle = 85f;

        [SerializeField, Tooltip("CastRange가 0 이하인 스킬이 자동 타겟을 찾을 때 사용할 기본 탐색 반경입니다.")]
        private float defaultSoftTargetSearchRange = 6f;

        [SerializeField, Tooltip("타겟 후보 점수에서 거리보다 전방 정렬도를 얼마나 우선할지 결정합니다.")]
        private float directionWeight = 2f;

        public bool TryBuildSkillRequest(GameObject caster, int skillUid, ConfigCommon.SkillTableSource source,
            out SkillDriverRequest request)
        {
            request = default;

            if (caster == null || skillUid <= 0)
                return false;

            if (!SkillDefinitionResolver.TryResolve(skillUid, source, out var skill) || skill == null)
                return false;

            Vector2 forward = ResolveBaseForward(caster);
            var casterTransform = caster.transform;
            Vector3 casterPosition = casterTransform.position;
            float castRange = SkillRangeResolver.GetCastRange(skill);
            float placementRange = SkillRangeResolver.GetPlacementRange(skill);
            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);

            TryResolveLockedTarget(caster, out Transform lockedTarget);
            TryResolveAimDirection(caster, out Vector2 aimDirection);
            TryResolveAimGroundPoint(caster, out Vector3 aimGroundPoint);

            if (aimDirection.sqrMagnitude > 1e-6f)
                forward = aimDirection.normalized;
            else if (lockedTarget != null)
                forward = ResolveDirectionToTarget(casterPosition, lockedTarget.position, forward);

            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.Self:
                    request = new SkillDriverRequest(null, casterPosition, forward, source);
                    return true;

                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                {
                    Vector3 groundPoint;
                    if (IsGroundPointValid(casterPosition, aimGroundPoint, castRange))
                    {
                        groundPoint = aimGroundPoint;
                    }
                    else if (lockedTarget != null && IsGroundPointValid(casterPosition, lockedTarget.position, castRange))
                    {
                        groundPoint = lockedTarget.position;
                        forward = ResolveDirectionToTarget(casterPosition, groundPoint, forward);
                    }
                    else if (TryFindSoftTarget(caster, forward, castRange, out var softTarget))
                    {
                        lockedTarget = softTarget.transform;
                        groundPoint = softTarget.transform.position;
                        forward = ResolveDirectionToTarget(casterPosition, groundPoint, forward);
                    }
                    else
                    {
                        float fallbackDistance = ResolveGroundFallbackDistance(castRange, placementRange);
                        groundPoint = SkillRangeResolver.ResolveForwardPlacementPosition(casterPosition, forward, fallbackDistance);
                    }

                    request = new SkillDriverRequest(lockedTarget, groundPoint, forward, source);
                    return true;
                }

                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                {
                    if (lockedTarget == null || !IsTargetWithinCastRange(casterPosition, lockedTarget.position, castRange))
                    {
                        if (!TryFindSoftTarget(caster, forward, castRange, out var softTarget))
                            return false;

                        lockedTarget = softTarget.transform;
                    }

                    Vector3 targetPoint = lockedTarget.position;
                    forward = ResolveDirectionToTarget(casterPosition, targetPoint, forward);
                    request = new SkillDriverRequest(lockedTarget, targetPoint, forward, source);
                    return true;
                }

                case ConfigCommonSkill.SkillTargetingMode.ForwardDirectional:
                case ConfigCommonSkill.SkillTargetingMode.Projectile:
                default:
                {
                    if (lockedTarget == null && TryFindSoftTarget(caster, forward, castRange, out var softTarget))
                        lockedTarget = softTarget.transform;

                    Vector3 groundPoint = lockedTarget != null
                        ? lockedTarget.position
                        : SkillRangeResolver.ResolveForwardPlacementPosition(casterPosition, forward, placementRange);

                    if (lockedTarget != null)
                        forward = ResolveDirectionToTarget(casterPosition, lockedTarget.position, forward);

                    request = new SkillDriverRequest(lockedTarget, groundPoint, forward, source);
                    return true;
                }
            }
        }

        private bool TryResolveLockedTarget(GameObject caster, out Transform lockedTarget)
        {
            lockedTarget = null;
            var components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null || ReferenceEquals(components[i], this))
                    continue;

                if (!(components[i] is IPlayerLockedTargetProvider provider))
                    continue;

                if (!provider.TryGetLockedTarget(caster, out var candidate) || !IsValidTarget(caster, candidate))
                    continue;

                lockedTarget = candidate;
                return true;
            }

            return false;
        }

        private bool TryResolveAimDirection(GameObject caster, out Vector2 direction)
        {
            direction = default;
            var components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null || ReferenceEquals(components[i], this))
                    continue;

                if (!(components[i] is IPlayerAimProvider provider))
                    continue;

                if (!provider.TryGetAimDirection(caster, out var candidate) || candidate.sqrMagnitude < 1e-6f)
                    continue;

                direction = candidate.normalized;
                return true;
            }

            return false;
        }

        private bool TryResolveAimGroundPoint(GameObject caster, out Vector3 groundPoint)
        {
            groundPoint = default;
            var components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null || ReferenceEquals(components[i], this))
                    continue;

                if (!(components[i] is IPlayerAimProvider provider))
                    continue;

                if (!provider.TryGetAimGroundPoint(caster, out var candidate))
                    continue;

                groundPoint = candidate;
                return true;
            }

            return false;
        }

        private bool TryFindSoftTarget(GameObject caster, Vector2 forward, float castRange, out CharacterBase target)
        {
            target = null;
            if (caster == null)
                return false;

            var casterCharacter = caster.GetComponent<CharacterBase>();
            float range = castRange > 0f ? castRange : defaultSoftTargetSearchRange;
            float rangeSqr = range * range;
            float halfAngle = Mathf.Max(0f, softTargetConeAngle) * 0.5f;
            float cosThreshold = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            Vector2 origin = caster.transform.position;
            Vector2 resolvedForward = forward.sqrMagnitude < 1e-6f ? Vector2.right : forward.normalized;

            CharacterBase best = null;
            float bestScore = float.NegativeInfinity;
            var characters = Object.FindObjectsOfType<CharacterBase>();
            for (int i = 0; i < characters.Length; i++)
            {
                var candidate = characters[i];
                if (!IsValidCandidate(casterCharacter, candidate))
                    continue;

                Vector2 toTarget = (Vector2)(candidate.transform.position - caster.transform.position);
                float distanceSqr = toTarget.sqrMagnitude;
                if (distanceSqr > rangeSqr || distanceSqr < 1e-6f)
                    continue;

                Vector2 dirToTarget = toTarget.normalized;
                float alignment = Vector2.Dot(resolvedForward, dirToTarget);
                if (alignment < cosThreshold)
                    continue;

                float score = alignment * directionWeight - Mathf.Sqrt(distanceSqr);
                if (score <= bestScore)
                    continue;

                best = candidate;
                bestScore = score;
            }

            target = best;
            return target != null;
        }

        private static bool IsValidCandidate(CharacterBase caster, CharacterBase candidate)
        {
            if (candidate == null || caster == null)
                return false;

            if (candidate == caster || candidate.gameObject == caster.gameObject)
                return false;

            if (!candidate.gameObject.activeInHierarchy || candidate.IsStatusDead())
                return false;

            if (caster.IsPlayer())
                return candidate.IsMonster();

            if (caster.IsMonster())
                return candidate.IsPlayer();

            return false;
        }

        private static bool IsValidTarget(GameObject caster, Transform target)
        {
            if (caster == null || target == null || target == caster.transform)
                return false;

            var targetCharacter = target.GetComponent<CharacterBase>();
            var casterCharacter = caster.GetComponent<CharacterBase>();
            return IsValidCandidate(casterCharacter, targetCharacter);
        }

        private static Vector2 ResolveBaseForward(GameObject caster)
        {
            if (caster == null)
                return Vector2.right;

            var characterBase = caster.GetComponent<CharacterBase>();
            if (characterBase != null)
            {
                var facing = CharacterConstants.FacingToVector2(characterBase.CurrentFacing);
                if (facing.sqrMagnitude > 1e-6f)
                    return facing.normalized;
            }

            Vector3 right = caster.transform.right;
            Vector2 fallback = new Vector2(right.x, right.y);
            return fallback.sqrMagnitude < 1e-6f ? Vector2.right : fallback.normalized;
        }

        private static Vector2 ResolveDirectionToTarget(Vector3 origin, Vector3 target, Vector2 fallback)
        {
            Vector2 direction = new Vector2(target.x - origin.x, target.y - origin.y);
            return direction.sqrMagnitude < 1e-6f ? fallback : direction.normalized;
        }

        private static bool IsGroundPointValid(Vector3 origin, Vector3 groundPoint, float castRange)
        {
            if (castRange <= 0f)
                return true;

            Vector2 delta = new Vector2(groundPoint.x - origin.x, groundPoint.y - origin.y);
            return delta.sqrMagnitude <= castRange * castRange;
        }

        private static bool IsTargetWithinCastRange(Vector3 origin, Vector3 target, float castRange)
        {
            if (castRange <= 0f)
                return true;

            Vector2 delta = new Vector2(target.x - origin.x, target.y - origin.y);
            return delta.sqrMagnitude <= castRange * castRange;
        }

        private static float ResolveGroundFallbackDistance(float castRange, float placementRange)
        {
            if (castRange > 0f && placementRange > 0f)
                return Mathf.Min(castRange, placementRange);

            if (castRange > 0f)
                return castRange;

            return placementRange;
        }
    }
}
