using Config;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// CreateSkillWindow의 스킬 선택 패널 UI를 구성하는 partial 구현입니다.
    /// 선택된 스킬 테이블의 상태를 표시하고, 테이블 전환 및 스킬 선택 기능을 제공합니다.
    /// </summary>
    public partial class CreateSkillWindow
    {
        /// <summary>
        /// 현재 선택된 스킬 테이블이 정상적으로 로드되었는지 여부를 반환합니다.
        /// 몬스터 테이블이 선택된 경우 몬스터 스킬 테이블의 로드 상태를, 그 외에는 일반 스킬 테이블의 로드 상태를 확인합니다.
        /// </summary>
        private bool HasCurrentTableLoaded =>
            _selectedSource == ConfigCommonSkill.SkillTableSource.Monster
                ? _tableSkillMonster != null
                : _tableSkill != null;

        /// <summary>
        /// 스킬 선택 패널의 GUI를 그립니다.
        /// 현재 테이블 상태를 표시하고, 스킬 테이블 소스 변경, 드롭다운을 통한 스킬 선택, 테이블 리로드 기능을 처리합니다.
        /// </summary>
        private void OnGUISelection()
        {
            EditorGUILayout.LabelField("스킬 선택", EditorStyles.boldLabel);

            if (!HasCurrentTableLoaded)
            {
                EditorGUILayout.HelpBox(
                    "테이블을 불러오지 못했습니다. Addressables 설정/테이블 등록 상태를 확인하세요.",
                    MessageType.Warning);
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