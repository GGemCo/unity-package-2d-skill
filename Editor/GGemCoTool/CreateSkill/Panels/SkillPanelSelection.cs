using Config;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    public partial class CreateSkillWindow
    {
        /// <summary>
        /// 현재 선택된 테이블이 정상적으로 로드되었는지 여부를 반환합니다.
        /// </summary>
        private bool HasCurrentTableLoaded =>
            _selectedSource == ConfigCommonSkill.SkillTableSource.Monster ? _tableSkillMonster != null : _tableSkill != null;
        
        private void OnGUISelection()
        {
            EditorGUILayout.LabelField("스킬 선택", EditorStyles.boldLabel);
            if (!HasCurrentTableLoaded)
            {
                EditorGUILayout.HelpBox("테이블을 불러오지 못했습니다. Addressables 설정/테이블 등록 상태를 확인하세요.", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("테이블");
                var nextSource = (ConfigCommonSkill.SkillTableSource)EditorGUILayout.EnumPopup(_selectedSource);
                if (nextSource != _selectedSource)
                {
                    _selectedSource = nextSource;
                    _selectedData = null;
                    RebuildDropdown();
                    Repaint();
                }
            }

            if (_dropDownOptions.Count <= 0)
                _selectedData = null;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("스킬");

                string prefix = _selectedSource == ConfigCommonSkill.SkillTableSource.Monster ? "[M]" : "[P]";
                string currentText = _selectedData != null
                    ? $"{prefix} {_selectedData.Uid} | {_selectedData.Memo}"
                    : "선택...";
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
