using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 콤보 스킬 실행 시도가 실패한 이유를 정의합니다.
    /// </summary>
    public enum SkillComboUseFailReason
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
        /// 현재 상태가 가리키는 노드를 찾지 못했습니다.
        /// </summary>
        MissingCurrentNode = 5,

        /// <summary>
        /// 현재 노드에서 다음 메인 노드를 찾지 못했습니다.
        /// </summary>
        MissingNextMainNode = 6,

        /// <summary>
        /// 현재 노드에서 마무리 노드를 찾지 못했습니다.
        /// </summary>
        MissingLastNode = 7,

        /// <summary>
        /// 마무리 노드에서 추가 연계를 시도했습니다.
        /// </summary>
        LastNodeCannotChain = 8,

        /// <summary>
        /// 현재 명령으로 이동할 수 없는 타입의 노드가 연결되어 있습니다.
        /// </summary>
        InvalidTransition = 9,

        /// <summary>
        /// 현재 콤보 상태에서 허용되지 않는 명령입니다.
        /// </summary>
        InvalidCommand = 10,

        /// <summary>
        /// 스킬 실행 드라이버를 찾지 못했습니다.
        /// </summary>
        MissingSkillDriver = 11,

        /// <summary>
        /// 스킬 실행 요청 컨텍스트를 만들지 못했습니다.
        /// </summary>
        MissingSkillRequest = 12,

        /// <summary>
        /// 콤보 노드는 해석되었지만 실제 스킬 실행이 거절되었습니다.
        /// </summary>
        SkillUseRejected = 13,

        /// <summary>
        /// 콤보 입력 가능 시간이 만료되었습니다.
        /// </summary>
        Expired = 14,
    }

    /// <summary>
    /// 콤보 명령 처리와 실제 스킬 실행 시도 결과를 함께 담는 값입니다.
    /// </summary>
    public readonly struct SkillComboUseResult
    {
        /// <summary>
        /// 콤보 명령으로 스킬 실행이 시작되었는지 여부입니다.
        /// </summary>
        public bool IsStarted { get; }

        /// <summary>
        /// 이번 실행으로 콤보가 종료되었는지 여부입니다.
        /// </summary>
        public bool ComboEnded { get; }

        /// <summary>
        /// 실행을 시도한 스킬 UID입니다.
        /// </summary>
        public int SkillUid { get; }

        /// <summary>
        /// 실행을 시도한 콤보 노드 인덱스입니다.
        /// </summary>
        public int NodeIndex { get; }

        /// <summary>
        /// 실행을 시도한 콤보 노드 타입입니다.
        /// </summary>
        public SkillComboNodeType NodeType { get; }

        /// <summary>
        /// 콤보 해석 단계의 실패 이유입니다.
        /// </summary>
        public SkillComboUseFailReason FailReason { get; }

        /// <summary>
        /// 실제 스킬 실행 드라이버가 반환한 실패 이유입니다.
        /// </summary>
        public SkillUseFailReason SkillFailReason { get; }

        /// <summary>
        /// 콤보 사용 결과 값을 생성합니다.
        /// </summary>
        /// <param name="isStarted">스킬 실행 시작 여부입니다.</param>
        /// <param name="comboEnded">이번 실행으로 콤보가 종료되었는지 여부입니다.</param>
        /// <param name="skillUid">실행을 시도한 스킬 UID입니다.</param>
        /// <param name="nodeIndex">실행을 시도한 콤보 노드 인덱스입니다.</param>
        /// <param name="nodeType">실행을 시도한 콤보 노드 타입입니다.</param>
        /// <param name="failReason">콤보 해석 단계의 실패 이유입니다.</param>
        /// <param name="skillFailReason">스킬 실행 드라이버가 반환한 실패 이유입니다.</param>
        private SkillComboUseResult(
            bool isStarted,
            bool comboEnded,
            int skillUid,
            int nodeIndex,
            SkillComboNodeType nodeType,
            SkillComboUseFailReason failReason,
            SkillUseFailReason skillFailReason)
        {
            IsStarted = isStarted;
            ComboEnded = comboEnded;
            SkillUid = skillUid;
            NodeIndex = nodeIndex;
            NodeType = nodeType;
            FailReason = isStarted ? SkillComboUseFailReason.None : failReason;
            SkillFailReason = isStarted ? SkillUseFailReason.None : skillFailReason;
        }

        /// <summary>
        /// 성공 결과를 생성합니다.
        /// </summary>
        /// <param name="node">실행을 시작한 콤보 노드입니다.</param>
        /// <param name="comboEnded">이번 실행으로 콤보가 종료되었는지 여부입니다.</param>
        /// <returns>성공 결과입니다.</returns>
        public static SkillComboUseResult Started(RuntimeSkillComboNode node, bool comboEnded)
        {
            return new SkillComboUseResult(
                true,
                comboEnded,
                node != null ? node.SkillUid : 0,
                node != null ? node.Index : RuntimeSkillComboDefinition.InvalidNodeIndex,
                node != null ? node.Type : SkillComboNodeType.Main,
                SkillComboUseFailReason.None,
                SkillUseFailReason.None);
        }

        /// <summary>
        /// 실패 결과를 생성합니다.
        /// </summary>
        /// <param name="failReason">콤보 해석 단계의 실패 이유입니다.</param>
        /// <param name="skillFailReason">스킬 실행 드라이버가 반환한 실패 이유입니다.</param>
        /// <returns>실패 결과입니다.</returns>
        public static SkillComboUseResult Fail(
            SkillComboUseFailReason failReason,
            SkillUseFailReason skillFailReason = SkillUseFailReason.None)
        {
            return new SkillComboUseResult(
                false,
                false,
                0,
                RuntimeSkillComboDefinition.InvalidNodeIndex,
                SkillComboNodeType.Main,
                failReason,
                skillFailReason);
        }
    }
}
