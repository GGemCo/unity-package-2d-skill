namespace GGemCo2DSkill
{
    /// <summary>
    /// 콤보가 마무리 스킬이 아닌 사유로 종료될 때 UI와 외부 시스템에 전달할 취소 사유를 정의합니다.
    /// </summary>
    public enum SkillComboCancelReason
    {
        /// <summary>
        /// 취소 사유가 지정되지 않았습니다.
        /// </summary>
        None = 0,

        /// <summary>
        /// 외부 코드에서 명시적으로 콤보를 취소했습니다.
        /// </summary>
        Manual = 1,

        /// <summary>
        /// 콤보 입력 대기 시간이 만료되어 콤보가 취소되었습니다.
        /// </summary>
        Expired = 2,

        /// <summary>
        /// 실행 중이던 콤보 스킬이 성공 상태로 끝나지 않아 콤보가 취소되었습니다.
        /// </summary>
        SkillExecutionFailed = 3,

        /// <summary>
        /// 다음 콤보 입력 대기 시간이 비활성화되어 콤보가 이어지지 않고 취소되었습니다.
        /// </summary>
        ChainInputWindowDisabled = 4,
    }

    /// <summary>
    /// 콤보 취소 시점의 UI 갱신에 필요한 최소 상태를 담는 값입니다.
    /// </summary>
    public readonly struct SkillComboCancelEvent
    {
        /// <summary>
        /// 콤보가 취소된 사유입니다.
        /// </summary>
        public SkillComboCancelReason Reason { get; }

        /// <summary>
        /// 취소 직전 진행 중이던 스킬 UID입니다.
        /// </summary>
        public int SkillUid { get; }

        /// <summary>
        /// 취소 직전 진행 중이던 콤보 노드 인덱스입니다.
        /// </summary>
        public int NodeIndex { get; }

        /// <summary>
        /// 콤보를 열었던 외부 진입 조건입니다.
        /// </summary>
        public SkillComboEntryTrigger EntryTrigger { get; }

        /// <summary>
        /// 콤보 취소 이벤트 값을 생성합니다.
        /// </summary>
        /// <param name="reason">콤보 취소 사유입니다.</param>
        /// <param name="skillUid">취소 직전 진행 중이던 스킬 UID입니다.</param>
        /// <param name="nodeIndex">취소 직전 진행 중이던 콤보 노드 인덱스입니다.</param>
        /// <param name="entryTrigger">콤보를 열었던 외부 진입 조건입니다.</param>
        public SkillComboCancelEvent(
            SkillComboCancelReason reason,
            int skillUid,
            int nodeIndex,
            SkillComboEntryTrigger entryTrigger)
        {
            Reason = reason;
            SkillUid = skillUid;
            NodeIndex = nodeIndex;
            EntryTrigger = entryTrigger;
        }
    }
}
