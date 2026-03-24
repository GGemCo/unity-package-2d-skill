using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

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
        
        protected override void ApplyPostRefreshCharacterSelectionPolicy()
        {
            if (_selectedSource != ConfigCommon.SkillTableSource.Player)
                return;

            for (int i = 0; i < sceneCharacters.Count; i++)
            {
                var character = sceneCharacters[i];
                if (character == null || !character.IsPlayer())
                    continue;

                if (selectedCharacter == character)
                {
                    selectedCharacterIndex = i;
                    return;
                }

                selectedCharacter = character;
                selectedCharacterIndex = i;
                OnSelectedCharacterChanged(selectedCharacter);
                return;
            }
        }

        protected override void OnSelectedCharacterChanged(CharacterBase character)
        {
            Repaint();
            if (character == null)
                return;

            if (!TryGetSkillTestRuntimeHub(out var hub, out var error))
            {
                Debug.LogError(error);
                return;
            }

            hub.SelectCaster(character.gameObject, true);
        }
    }
}