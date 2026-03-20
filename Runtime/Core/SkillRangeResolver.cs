using Config;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 거리 관련 fallback, 사거리 검증, 전방 배치 좌표 계산을 담당하는 공용 해석기입니다.
    /// </summary>
    public static class SkillRangeResolver
    {
        private const float DefaultPlacementRange = 3f;

        /// <summary>
        /// 스킬 사용 가능 거리를 반환합니다.
        /// 신규 CastRange가 없으면 구버전 Range를 fallback으로 사용합니다.
        /// </summary>
        public static float GetCastRange(RuntimeSkillDefinition skill)
        {
            if (skill == null)
                return 0f;

            if (skill.CastRange > 0f)
                return skill.CastRange;

            return 0f;
        }

        /// <summary>
        /// 이벤트 기본 배치 거리를 반환합니다.
        /// 신규 PlacementRange가 없으면 구버전 Range를 fallback으로 사용하고, 둘 다 없으면 기본값을 사용합니다.
        /// </summary>
        public static float GetPlacementRange(RuntimeSkillDefinition skill)
        {
            if (skill == null)
                return DefaultPlacementRange;

            if (skill.PlacementRange > 0f)
                return skill.PlacementRange;

            return DefaultPlacementRange;
        }

        /// <summary>
        /// 타겟팅 모드와 요청 컨텍스트를 바탕으로 캐스트 가능 거리 검증을 수행합니다.
        /// 거리 값이 0 이하이면 제한 없음으로 간주합니다.
        /// </summary>
        public static bool IsWithinCastRange(RuntimeSkillDefinition skill, GameObject caster, SkillTargetContext ctx)
        {
            if (skill == null || caster == null)
                return false;

            float castRange = GetCastRange(skill);
            if (castRange <= 0f)
                return true;

            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            Vector3 origin = caster.transform.position;

            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.Self:
                    return true;

                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    return IsWithinGroundTargetRange(origin, ctx.groundPoint, castRange);

                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                    if (ctx.lockedTarget == null)
                        return false;

                    return IsWithinCastRange(mode, origin, ctx.lockedTarget.transform.position, castRange);

                default:
                    return true;
            }
        }


        /// <summary>
        /// 대상 기반 타겟팅 모드가 수평(X축) 기준 사거리 판정을 사용하는지 반환합니다.
        /// 플랫포머 전투에서는 공중 타겟의 Y 차이 때문에 사거리 판정이 과도하게 실패하지 않도록
        /// 락온 계열 모드를 기본적으로 수평 거리 기준으로 해석합니다.
        /// </summary>
        public static bool UsesHorizontalCastRange(ConfigCommonSkill.SkillTargetingMode mode)
        {
            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 타겟팅 모드에 맞는 기준(수평/2D)으로 사거리 판정을 수행합니다.
        /// </summary>
        public static bool IsWithinCastRange(ConfigCommonSkill.SkillTargetingMode mode, Vector3 origin, Vector3 target, float castRange)
        {
            if (castRange <= 0f)
                return true;

            return UsesHorizontalCastRange(mode)
                ? IsWithinHorizontalDistance(origin, target, castRange)
                : IsWithinDistance2D(origin, target, castRange);
        }

        /// <summary>
        /// 지면 지정 좌표는 기존처럼 2D 거리 기준으로 사거리를 판정합니다.
        /// </summary>
        public static bool IsWithinGroundTargetRange(Vector3 origin, Vector3 groundPoint, float castRange)
        {
            if (castRange <= 0f)
                return true;

            return IsWithinDistance2D(origin, groundPoint, castRange);
        }

        /// <summary>
        /// 두 좌표의 수평(X축) 거리만 비교합니다.
        /// </summary>
        public static bool IsWithinHorizontalDistance(Vector3 a, Vector3 b, float range)
        {
            return Mathf.Abs(b.x - a.x) <= range;
        }

        /// <summary>
        /// 두 좌표의 2D 거리(X/Y)를 비교합니다.
        /// </summary>
        public static bool IsWithinDistance2D(Vector3 a, Vector3 b, float range)
        {
            Vector2 delta = new Vector2(b.x - a.x, b.y - a.y);
            return delta.sqrMagnitude <= range * range;
        }

        /// <summary>
        /// 전방 기준 기본 배치 좌표를 계산합니다.
        /// </summary>
        public static Vector3 ResolveForwardPlacementPosition(Vector3 origin, Vector3 forward, float placementRange)
        {
            Vector3 resolvedForward = forward.sqrMagnitude < 1e-6f ? Vector3.right : forward.normalized;
            return origin + resolvedForward * Mathf.Max(0.1f, placementRange);
        }

    }
}
