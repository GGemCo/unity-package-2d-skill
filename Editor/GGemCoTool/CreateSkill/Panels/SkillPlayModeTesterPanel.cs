using System;
using UnityEngine.UIElements;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// PlayMode 테스트 영역을 조립합니다.
    /// 몬스터/캐스터/타겟 선택 UI는 외부에서 생성한 VisualElement를 주입받습니다.
    /// </summary>
    public sealed class SkillPlayModeTesterPanel
    {
        public HelpBox Build(
            VisualElement parent,
            Action onUseSkill,
            out Button useSkillButton,
            params VisualElement[] sections)
        {
            var help = new HelpBox(
                "Play Mode에서만 동작합니다. '스킬 사용하기'는 현재 Input Field 값 + (선택 시) Timeline 이벤트를 사용해 실행합니다.\n" +
                "- TimelineAsset이 지정되어 있으면, 런타임 시퀀스를 메모리에서 Bake하여 Addressables 로딩을 우회합니다.",
                HelpBoxMessageType.Info);
            parent.Add(help);

            if (sections != null)
            {
                for (int i = 0; i < sections.Length; i++)
                {
                    if (sections[i] != null)
                        parent.Add(sections[i]);
                }
            }

            useSkillButton = new Button(() => onUseSkill?.Invoke()) { text = "스킬 사용하기(PlayMode)" };
            parent.Add(useSkillButton);
            return help;
        }
    }
}
