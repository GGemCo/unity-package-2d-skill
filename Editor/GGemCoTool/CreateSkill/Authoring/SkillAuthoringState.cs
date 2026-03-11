using System.Collections.Generic;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// CreateSkillWindow에서 공통으로 사용하는 편집 상태입니다.
    /// UI 패널, PlayMode 테스트, Bake 서비스가 동일 상태를 공유할 수 있게 합니다.
    /// </summary>
    public sealed class SkillAuthoringState
    {
        public SkillAuthoringTableKind TableKind = SkillAuthoringTableKind.Player;
        public List<SkillAuthoringModel> SkillListSource = new();
        public SkillAuthoringModel Selected;
        public SkillAuthoringModel CachedOriginal;
        public SkillAuthoringModel Editing;
        public bool IsDirty;

        public void ClearSelection()
        {
            Selected = null;
            CachedOriginal = null;
            Editing = null;
            IsDirty = false;
        }

        public void SetSelection(SkillAuthoringModel selected)
        {
            Selected = selected;
            CachedOriginal = selected?.Clone();
            Editing = selected?.Clone();
            IsDirty = false;
        }
    }
}
