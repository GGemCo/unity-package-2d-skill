namespace GGemCo2DSkill
{
    /// <summary>
    /// 마무리 스킬 후보의 HUD 프리뷰 상태를 전달하는 이벤트 데이터입니다.
    /// </summary>
    public readonly struct SkillComboLastPreviewEvent
    {
        /// <summary>
        /// 마무리 스킬 프리뷰를 표시해야 하는지 여부입니다.
        /// </summary>
        public bool IsPreviewActive { get; }

        /// <summary>
        /// 프리뷰로 표시할 마무리 스킬 UID입니다.
        /// </summary>
        public int LastSkillUid { get; }

        /// <summary>
        /// 프리뷰로 표시할 마무리 스킬 콤보 노드 인덱스입니다.
        /// </summary>
        public int LastNodeIndex { get; }

        /// <summary>
        /// 마무리 스킬 프리뷰 이벤트 데이터를 생성합니다.
        /// </summary>
        /// <param name="isPreviewActive">프리뷰 표시 여부입니다.</param>
        /// <param name="lastSkillUid">프리뷰 대상 마무리 스킬 UID입니다.</param>
        /// <param name="lastNodeIndex">프리뷰 대상 마무리 노드 인덱스입니다.</param>
        private SkillComboLastPreviewEvent(bool isPreviewActive, int lastSkillUid, int lastNodeIndex)
        {
            IsPreviewActive = isPreviewActive;
            LastSkillUid = isPreviewActive ? lastSkillUid : 0;
            LastNodeIndex = isPreviewActive
                ? lastNodeIndex
                : RuntimeSkillComboDefinition.InvalidNodeIndex;
        }

        /// <summary>
        /// 마무리 스킬 프리뷰 표시 이벤트를 생성합니다.
        /// </summary>
        /// <param name="node">프리뷰 대상 마무리 콤보 노드입니다.</param>
        /// <returns>프리뷰 표시 이벤트 데이터입니다.</returns>
        public static SkillComboLastPreviewEvent Show(RuntimeSkillComboNode node)
        {
            return node != null && node.IsLast && node.SkillUid > 0
                ? new SkillComboLastPreviewEvent(true, node.SkillUid, node.Index)
                : Hide();
        }

        /// <summary>
        /// 마무리 스킬 프리뷰 해제 이벤트를 생성합니다.
        /// </summary>
        /// <returns>프리뷰 해제 이벤트 데이터입니다.</returns>
        public static SkillComboLastPreviewEvent Hide()
        {
            return new SkillComboLastPreviewEvent(
                false,
                0,
                RuntimeSkillComboDefinition.InvalidNodeIndex);
        }
    }
}
