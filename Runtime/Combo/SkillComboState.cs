namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어가 현재 어느 콤보 노드에 머물러 있는지 추적하는 런타임 상태입니다.
    /// </summary>
    public sealed class SkillComboState
    {
        /// <summary>
        /// 현재 이어갈 수 있는 콤보가 열려 있는지 반환합니다.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 현재 메인 콤보 노드 인덱스입니다. 진입 대기 상태에서는 InvalidNodeIndex입니다.
        /// </summary>
        public int CurrentNodeIndex { get; private set; } = RuntimeSkillComboDefinition.InvalidNodeIndex;

        /// <summary>
        /// 현재 메인 콤보 노드에서 실행한 스킬 UID입니다.
        /// </summary>
        public int CurrentSkillUid { get; private set; }

        /// <summary>
        /// 현재 콤보가 외부 성공 이벤트로 열린 상태인지 반환합니다.
        /// </summary>
        public bool IsExternalEntry { get; private set; }

        /// <summary>
        /// 외부 트리거 위치에서 Main 또는 Last 입력을 기다리는 상태인지 반환합니다.
        /// </summary>
        public bool IsEntryGateActive { get; private set; }

        /// <summary>
        /// 현재 콤보를 연 외부 진입 조건입니다.
        /// </summary>
        public SkillComboEntryTrigger EntryTrigger { get; private set; } = SkillComboEntryTrigger.None;

        /// <summary>
        /// 외부 진입 조건을 성립시킨 스킬 UID입니다. 스킬 없이 열린 진입이면 0입니다.
        /// </summary>
        public int EntrySkillUid { get; private set; }

        /// <summary>
        /// 현재 콤보 상태를 지정한 메인 노드로 이동합니다.
        /// </summary>
        /// <param name="node">현재 상태로 저장할 메인 콤보 노드입니다.</param>
        public void Activate(RuntimeSkillComboNode node)
        {
            ActivateInternal(node, false, false, SkillComboEntryTrigger.ManualSkillUse);
        }

        /// <summary>
        /// 외부 성공 이벤트의 트리거 위치에서 다음 콤보 명령을 기다리도록 상태를 엽니다.
        /// </summary>
        /// <param name="entryTrigger">콤보를 연 외부 진입 조건입니다.</param>
        /// <param name="entrySkillUid">외부 진입 조건을 성립시킨 스킬 UID입니다.</param>
        public void OpenEntryGate(SkillComboEntryTrigger entryTrigger, int entrySkillUid = 0)
        {
            if (entryTrigger == SkillComboEntryTrigger.None ||
                entryTrigger == SkillComboEntryTrigger.ManualSkillUse)
            {
                Reset();
                return;
            }

            IsActive = true;
            CurrentNodeIndex = RuntimeSkillComboDefinition.InvalidNodeIndex;
            CurrentSkillUid = 0;
            IsExternalEntry = true;
            IsEntryGateActive = true;
            EntryTrigger = entryTrigger;
            EntrySkillUid = entrySkillUid;
        }

        /// <summary>
        /// 외부 성공 이벤트가 이미 지정한 메인 노드를 성립시킨 것으로 보고 콤보 상태를 갱신합니다.
        /// EntryGate 방식 이전 호출과의 호환을 위한 API입니다.
        /// </summary>
        /// <param name="node">외부 성공 이벤트가 성립시킨 시작 메인 노드입니다.</param>
        /// <param name="entryTrigger">콤보를 연 외부 진입 조건입니다.</param>
        public void ActivateExternalEntry(RuntimeSkillComboNode node, SkillComboEntryTrigger entryTrigger)
        {
            ActivateInternal(node, true, false, entryTrigger);
        }

        /// <summary>
        /// 콤보 상태를 지정한 메인 노드로 갱신합니다.
        /// </summary>
        /// <param name="node">현재 상태로 저장할 메인 콤보 노드입니다.</param>
        /// <param name="isExternalEntry">외부 성공 이벤트로 열린 콤보인지 여부입니다.</param>
        /// <param name="isEntryGateActive">트리거 위치에서 입력을 기다리는 상태인지 여부입니다.</param>
        /// <param name="entryTrigger">콤보를 연 진입 조건입니다.</param>
        private void ActivateInternal(
            RuntimeSkillComboNode node,
            bool isExternalEntry,
            bool isEntryGateActive,
            SkillComboEntryTrigger entryTrigger)
        {
            if (node == null || node.Type != SkillComboNodeType.Main || node.SkillUid <= 0)
            {
                Reset();
                return;
            }

            IsActive = true;
            CurrentNodeIndex = node.Index;
            CurrentSkillUid = node.SkillUid;
            IsExternalEntry = isExternalEntry;
            IsEntryGateActive = isEntryGateActive;
            EntryTrigger = entryTrigger;
            EntrySkillUid = 0;
        }

        /// <summary>
        /// 콤보 진행 상태를 초기 상태로 되돌립니다.
        /// </summary>
        public void Reset()
        {
            IsActive = false;
            CurrentNodeIndex = RuntimeSkillComboDefinition.InvalidNodeIndex;
            CurrentSkillUid = 0;
            IsExternalEntry = false;
            IsEntryGateActive = false;
            EntryTrigger = SkillComboEntryTrigger.None;
            EntrySkillUid = 0;
        }
    }
}
