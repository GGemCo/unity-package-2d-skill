namespace GGemCo2DSkill
{
    /// <summary>
    /// 외부 성공 이벤트로 콤보 트리를 여는 과정에서 발생할 수 있는 실패 이유를 정의합니다.
    /// </summary>
    public enum SkillComboOpenFailReason
    {
        /// <summary>
        /// 실패하지 않았습니다.
        /// </summary>
        None = 0,

        /// <summary>
        /// 사용할 콤보 정의를 찾지 못했습니다.
        /// </summary>
        MissingDefinition = 1,

        /// <summary>
        /// 콤보 정의에 노드가 없습니다.
        /// </summary>
        EmptyDefinition = 2,

        /// <summary>
        /// 시작 노드를 찾지 못했습니다.
        /// </summary>
        MissingStartNode = 3,

        /// <summary>
        /// 시작 노드가 메인 타입이 아닙니다.
        /// </summary>
        InvalidStartNode = 4,

        /// <summary>
        /// 외부에서 전달한 성공 스킬 UID와 시작 노드의 스킬 UID가 다릅니다.
        /// </summary>
        ConfirmedSkillMismatch = 5,

        /// <summary>
        /// 외부 진입 조건이 지정되지 않았습니다.
        /// </summary>
        InvalidEntryTrigger = 6,
    }

    /// <summary>
    /// 외부 성공 이벤트로 콤보 트리 시작 노드를 연 결과입니다.
    /// </summary>
    public readonly struct SkillComboOpenResult
    {
        /// <summary>
        /// 콤보 트리가 열렸는지 여부입니다.
        /// </summary>
        public bool IsOpened { get; }

        /// <summary>
        /// 열린 시작 노드의 스킬 UID입니다.
        /// </summary>
        public int SkillUid { get; }

        /// <summary>
        /// 열린 시작 노드 인덱스입니다.
        /// </summary>
        public int NodeIndex { get; }

        /// <summary>
        /// 콤보를 연 외부 진입 조건입니다.
        /// </summary>
        public SkillComboEntryTrigger EntryTrigger { get; }

        /// <summary>
        /// 콤보 열기 실패 이유입니다.
        /// </summary>
        public SkillComboOpenFailReason FailReason { get; }

        /// <summary>
        /// 콤보 열기 결과 값을 생성합니다.
        /// </summary>
        /// <param name="isOpened">콤보가 열렸는지 여부입니다.</param>
        /// <param name="skillUid">열린 시작 노드의 스킬 UID입니다.</param>
        /// <param name="nodeIndex">열린 시작 노드 인덱스입니다.</param>
        /// <param name="entryTrigger">콤보를 연 외부 진입 조건입니다.</param>
        /// <param name="failReason">콤보 열기 실패 이유입니다.</param>
        private SkillComboOpenResult(
            bool isOpened,
            int skillUid,
            int nodeIndex,
            SkillComboEntryTrigger entryTrigger,
            SkillComboOpenFailReason failReason)
        {
            IsOpened = isOpened;
            SkillUid = skillUid;
            NodeIndex = nodeIndex;
            EntryTrigger = entryTrigger;
            FailReason = isOpened ? SkillComboOpenFailReason.None : failReason;
        }

        /// <summary>
        /// 성공 결과를 생성합니다.
        /// </summary>
        /// <param name="node">외부 성공 이벤트로 열린 시작 노드입니다.</param>
        /// <param name="entryTrigger">콤보를 연 외부 진입 조건입니다.</param>
        /// <returns>성공 결과입니다.</returns>
        public static SkillComboOpenResult Opened(
            RuntimeSkillComboNode node,
            SkillComboEntryTrigger entryTrigger)
        {
            return new SkillComboOpenResult(
                true,
                node != null ? node.SkillUid : 0,
                node != null ? node.Index : RuntimeSkillComboDefinition.InvalidNodeIndex,
                entryTrigger,
                SkillComboOpenFailReason.None);
        }

        /// <summary>
        /// 실패 결과를 생성합니다.
        /// </summary>
        /// <param name="failReason">콤보 열기 실패 이유입니다.</param>
        /// <param name="entryTrigger">시도한 외부 진입 조건입니다.</param>
        /// <returns>실패 결과입니다.</returns>
        public static SkillComboOpenResult Fail(
            SkillComboOpenFailReason failReason,
            SkillComboEntryTrigger entryTrigger = SkillComboEntryTrigger.None)
        {
            return new SkillComboOpenResult(
                false,
                0,
                RuntimeSkillComboDefinition.InvalidNodeIndex,
                entryTrigger,
                failReason);
        }
    }
}
