using GGemCo2DSkill;
using UnityEditor;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// CreateSkillWindow의 캐스터 정보 패널을 구성하는 partial 구현입니다.
    /// 스킬 실행 대상 캐릭터를 선택하고, 필요한 컴포넌트의 부착 상태를 표시합니다.
    /// </summary>
    public partial class CreateSkillWindow
    {
        /// <summary>
        /// 캐스터 선택 및 컴포넌트 상태 표시 UI를 그립니다.
        /// 선택된 캐릭터가 있으면 스킬 실행에 필요한 구성 요소의 존재 여부를 함께 표시합니다.
        /// </summary>
        private void OnGUICaster()
        {
            DrawCharacterSelectionSection(Title);

            if (selectedCharacter == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("컴포넌트 상태", EditorStyles.boldLabel);

                var component1 = selectedCharacter.GetComponent<SkillExecutor>() ?? selectedCharacter.GetComponentInChildren<SkillExecutor>();
                // bool hasTarget = selectedCharacter.GetComponent<IAffectTarget>() != null || selectedCharacter.GetComponentInChildren<IAffectTarget>() != null;

                EditorGUILayout.LabelField($"{nameof(SkillExecutor)}", component1 != null ? "OK" : "없음");
                // EditorGUILayout.LabelField("IAffectTarget", hasTarget ? "OK" : "없음");
            }
        }
    }
}