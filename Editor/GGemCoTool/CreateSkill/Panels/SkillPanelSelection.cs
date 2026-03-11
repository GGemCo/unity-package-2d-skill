using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    public partial class CreateSkillWindow
    {
        private void OnGUISelection()
        {
            EditorGUILayout.LabelField("스킬 선택", EditorStyles.boldLabel);
            if (_tableSkill == null)
            {
                EditorGUILayout.HelpBox("테이블을 불러오지 못했습니다. Addressables 설정/테이블 등록 상태를 확인하세요.", MessageType.Warning);
            }

            if (_dropDownOptions.Count <= 0)
                _selectedData = null;

            // Searchable dropdown (UseCrowdControl/UseProjectile 스타일)
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("스킬");

                string currentText = _selectedData != null ? $"{_selectedData.Uid} | {_selectedData.Memo}" : "선택...";
                int selectIndex = _selectedData?.Uid ?? 0;

                SearchableDropdownUtility.DrawButtonAndShow(
                    buttonText: currentText,
                    options: _dropDownOptions,
                    selectedIndex: selectIndex,
                    onSelected: (idx, opt) =>
                    {
                        _selectedData = opt.Data;
                        CacheRow();
                        Repaint();
                    },
                    defaultSearchMode: SearchableDropdownUtility.SearchMode.Both);
                
                if (GUILayout.Button("리로드", GUILayout.Width(60)))
                {
                    ReloadAllTables();
                }
            }
        }
    }
}
