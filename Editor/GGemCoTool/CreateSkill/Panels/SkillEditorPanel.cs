using System;
using UnityEditor.UIElements;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 편집 필드와 저장/테스트/Bake 버튼 영역을 조립합니다.
    /// 실제 편집 필드 생성은 CreateSkillWindow에서 수행하고,
    /// 패널은 레이아웃 구성 책임만 가집니다.
    /// </summary>
    public sealed class SkillEditorPanel
    {
        public ScrollView Build(
            Action<VisualElement> onBuildFields,
            Action onRevert,
            Action onApplyTest,
            Action onSaveTable,
            Action onBake,
            out VisualElement editRoot,
            out ObjectField timelineField,
            out Button revertButton,
            out Button applyTestButton,
            out Button saveTableButton,
            out Button bakeButton)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { flexGrow = 1 }
            };

            editRoot = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1,
                }
            };
            scroll.Add(editRoot);

            timelineField = new ObjectField("TimelineAsset") { objectType = typeof(TimelineAsset) };
            editRoot.Add(timelineField);

            onBuildFields?.Invoke(editRoot);

            var editButtons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            revertButton = new Button(() => onRevert?.Invoke()) { text = "되돌리기", style = { marginRight = 6 } };
            applyTestButton = new Button(() => onApplyTest?.Invoke()) { text = "테스트 적용하기", style = { marginRight = 6 } };
            saveTableButton = new Button(() => onSaveTable?.Invoke()) { text = "저장하기(선택 테이블)" };
            editButtons.Add(revertButton);
            editButtons.Add(applyTestButton);
            editButtons.Add(saveTableButton);
            editRoot.Add(editButtons);

            bakeButton = new Button(() => onBake?.Invoke()) { text = "Bake RuntimeSequence + Register Addressables" };
            editRoot.Add(bakeButton);

            var help = new HelpBox(
                "필수: skill 테이블의 TimelineKey / RuntimeSequenceKey 컬럼을 채워주세요.\n" +
                "- TimelineKey: 제작용 TimelineAsset Addressables Key\n" +
                "- RuntimeSequenceKey: 런타임용 SkillRuntimeSequence Addressables Key\n" +
                "Bake는 Timeline의 이벤트 클립(SkillEventTrack)만 수집합니다.",
                HelpBoxMessageType.Info);
            editRoot.Add(help);

            return scroll;
        }
    }
}
