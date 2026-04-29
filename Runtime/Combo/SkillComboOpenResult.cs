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
        /// 외부에서 전달한 성공 스킬 UID와 콤보 진입 조건이 맞지 않습니다.
        /// </summary>
        ConfirmedSkillMismatch = 5,

        /// <summary>
        /// 외부 진입 조건이 지정되지 않았습니다.
        /// </summary>
        InvalidEntryTrigger = 6,

        /// <summary>
        /// 진입 위치의 마무리 노드가 마무리 타입이 아닙니다.
        /// </summary>
        InvalidEntryLastNode = 7,
    }

    /// <summary>
    /// 외부 성공 이벤트로 콤보 진입 위치를 연 결과입니다.
    /// </summary>
    public readonly struct SkillComboOpenResult
    {
        /// <summary>
        /// 콤보 트리가 열렸는지 여부입니다.
        /// </summary>
        public bool IsOpened { get; }

        /// <summary>
        /// 진입 위치에서 Main 명령으로 실행할 첫 번째 메인 노드의 스킬 UID입니다.
        /// </summary>
        public int SkillUid { get; }

        /// <summary>
        /// 진입 위치에서 Main 명령으로 실행할 첫 번째 메인 노드 인덱스입니다.
        /// </summary>
        public int NodeIndex { get; }

        /// <summary>
        /// 진입 위치에서 Last 명령으로 실행할 첫 번째 마무리 노드의 스킬 UID입니다.
        /// </summary>
        public int LastSkillUid { get; }

        /// <summary>
        /// 진입 위치에서 Last 명령으로 실행할 첫 번째 마무리 노드 인덱스입니다.
        /// </summary>
        public int LastNodeIndex { get; }

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
        /// <param name="skillUid">진입 위치에서 Main 명령으로 실행할 첫 번째 메인 노드의 스킬 UID입니다.</param>
        /// <param name="nodeIndex">진입 위치에서 Main 명령으로 실행할 첫 번째 메인 노드 인덱스입니다.</param>
        /// <param name="lastSkillUid">진입 위치에서 Last 명령으로 실행할 첫 번째 마무리 노드의 스킬 UID입니다.</param>
        /// <param name="lastNodeIndex">진입 위치에서 Last 명령으로 실행할 첫 번째 마무리 노드 인덱스입니다.</param>
        /// <param name="entryTrigger">콤보를 연 외부 진입 조건입니다.</param>
        /// <param name="failReason">콤보 열기 실패 이유입니다.</param>
        private SkillComboOpenResult(
            bool isOpened,
            int skillUid,
            int nodeIndex,
            int lastSkillUid,
            int lastNodeIndex,
            SkillComboEntryTrigger entryTrigger,
            SkillComboOpenFailReason failReason)
        {
            IsOpened = isOpened;
            SkillUid = skillUid;
            NodeIndex = nodeIndex;
            LastSkillUid = lastSkillUid;
            LastNodeIndex = lastNodeIndex;
            EntryTrigger = entryTrigger;
            FailReason = isOpened ? SkillComboOpenFailReason.None : failReason;
        }

        /// <summary>
        /// 성공 결과를 생성합니다.
        /// </summary>
        /// <param name="node">진입 위치에서 Main 명령으로 실행할 첫 번째 메인 노드입니다.</param>
        /// <param name="entryTrigger">콤보를 연 외부 진입 조건입니다.</param>
        /// <returns>성공 결과입니다.</returns>
        public static SkillComboOpenResult Opened(
            RuntimeSkillComboNode node,
            SkillComboEntryTrigger entryTrigger)
        {
            return Opened(node, null, entryTrigger);
        }

        /// <summary>
        /// 성공 결과를 생성합니다.
        /// </summary>
        /// <param name="entryMainNode">진입 위치에서 Main 명령으로 실행할 첫 번째 메인 노드입니다.</param>
        /// <param name="entryLastNode">진입 위치에서 Last 명령으로 실행할 첫 번째 마무리 노드입니다.</param>
        /// <param name="entryTrigger">콤보를 연 외부 진입 조건입니다.</param>
        /// <returns>성공 결과입니다.</returns>
        public static SkillComboOpenResult Opened(
            RuntimeSkillComboNode entryMainNode,
            RuntimeSkillComboNode entryLastNode,
            SkillComboEntryTrigger entryTrigger)
        {
            return new SkillComboOpenResult(
                true,
                entryMainNode != null ? entryMainNode.SkillUid : 0,
                entryMainNode != null ? entryMainNode.Index : RuntimeSkillComboDefinition.InvalidNodeIndex,
                entryLastNode != null ? entryLastNode.SkillUid : 0,
                entryLastNode != null ? entryLastNode.Index : RuntimeSkillComboDefinition.InvalidNodeIndex,
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
                0,
                RuntimeSkillComboDefinition.InvalidNodeIndex,
                entryTrigger,
                failReason);
        }
    }
}
