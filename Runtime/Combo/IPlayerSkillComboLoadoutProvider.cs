namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어가 현재 장착한 스킬 콤보 정의를 제공하는 인터페이스입니다.
    /// </summary>
    public interface IPlayerSkillComboLoadoutProvider
    {
        /// <summary>
        /// 현재 플레이어 장착 상태를 기준으로 런타임 콤보 정의를 구성합니다.
        /// </summary>
        /// <param name="definition">구성된 콤보 정의입니다.</param>
        /// <returns>사용 가능한 콤보 정의가 있으면 true입니다.</returns>
        bool TryBuildComboDefinition(out RuntimeSkillComboDefinition definition);
    }
}
