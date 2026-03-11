using GGemCo2DSkill;
using UnityEditor;

namespace GGemCo2DSkillEditor
{
    public partial class CreateSkillWindow
    {
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