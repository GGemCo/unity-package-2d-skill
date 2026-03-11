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

            return skill.Range > 0f ? skill.Range : 0f;
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

            if (skill.Range > 0f)
                return skill.Range;

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
                    return IsWithinDistance(origin, ctx.groundPoint, castRange);

                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                    if (ctx.lockedTarget == null)
                        return false;

                    return IsWithinDistance(origin, ctx.lockedTarget.transform.position, castRange);

                default:
                    return true;
            }
        }

        /// <summary>
        /// 전방 기준 기본 배치 좌표를 계산합니다.
        /// </summary>
        public static Vector3 ResolveForwardPlacementPosition(Vector3 origin, Vector3 forward, float placementRange)
        {
            Vector3 resolvedForward = forward.sqrMagnitude < 1e-6f ? Vector3.right : forward.normalized;
            return origin + resolvedForward * Mathf.Max(0.1f, placementRange);
        }

        private static bool IsWithinDistance(Vector3 a, Vector3 b, float range)
        {
            Vector2 delta = new Vector2(b.x - a.x, b.y - a.y);
            return delta.sqrMagnitude <= range * range;
        }
    }
}
