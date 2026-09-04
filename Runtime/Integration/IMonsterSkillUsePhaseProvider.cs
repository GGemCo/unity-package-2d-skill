namespace GGemCo2DSkill
{
    /// <summary>
    /// 몬스터 AI가 차징 스킬의 실제 캐스팅/사용 단계 진입 여부를 조회할 수 있도록 제공하는 읽기 전용 포트입니다.
    /// </summary>
    public interface IMonsterSkillUsePhaseProvider
    {
        /// <summary>
        /// 지정한 스킬이 현재 실행에서 차징/차징 완료 대기를 끝내고 실제 사용 단계에 진입했는지 반환합니다.
        /// </summary>
        /// <param name="skillUid">확인할 스킬 UID입니다.</param>
        /// <returns>실제 사용 단계가 시작되었으면 <see langword="true"/>입니다.</returns>
        bool HasEnteredUsePhase(int skillUid);
    }
}
