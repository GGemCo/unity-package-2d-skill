namespace GGemCo2DSkill
{
    /// <summary>
    /// 레이저 시작점 오버라이드 값을 어떤 기준점에서 해석할지 정의합니다.
    /// </summary>
    public enum LaserStartAnchor
    {
        /// <summary>
        /// 시전자 위치를 기준으로 시작점 오버라이드 값을 해석합니다.
        /// </summary>
        Caster = 0,

        /// <summary>
        /// 고정 타겟 위치를 기준으로 시작점 오버라이드 값을 해석합니다.
        /// </summary>
        Target = 1,

        /// <summary>
        /// 지면 기준점(groundPoint)을 기준으로 시작점 오버라이드 값을 해석합니다.
        /// </summary>
        Ground = 2,

        /// <summary>
        /// 같은 스킬 실행 중 저장된 이름 있는 위치 앵커를 기준으로 시작점 오버라이드 값을 해석합니다.
        /// </summary>
        NamedPositionAnchor = 3,
    }

    /// <summary>
    /// 레이저가 조준할 목표점을 어떤 규칙으로 해석할지 정의합니다.
    /// </summary>
    public enum LaserTargetPointPolicy
    {
        /// <summary>
        /// 기존 타게팅 규칙을 그대로 사용합니다.
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
