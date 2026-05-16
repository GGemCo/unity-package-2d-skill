namespace GGemCo2DSkill
{
    /// <summary>
    /// 프로젝타일이 조준할 목표점을 어떤 규칙으로 해석할지 정의합니다.
    /// </summary>
    public enum ProjectileTargetPointPolicy
    {
        /// <summary>
        /// 기존 프로젝타일 타게팅 규칙을 그대로 사용합니다.
        /// </summary>
        UseDefaultTargeting = 0,

        /// <summary>
        /// 타겟 중심점에 고정 오프셋을 더한 좌표를 목표점으로 사용합니다.
        /// </summary>
        FixedOffsetFromTargetCenter = 1,

        /// <summary>
        /// 타겟 HitArea의 정규화 좌표(0~1)를 목표점으로 사용합니다.
        /// HitArea를 찾지 못하면 중심점 + 고정 오프셋으로 대체합니다.
        /// </summary>
        FixedNormalizedPointInTargetHitArea = 2,
    }
}
