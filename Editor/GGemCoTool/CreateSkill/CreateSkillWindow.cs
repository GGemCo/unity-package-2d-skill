using System;
using System.Collections.Generic;
using GGemCo2DAffectEditor;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 제작 툴(V2)
    /// - SSOT: skill / skill_monster 테이블
    /// - Marker 미사용, 이벤트 클립 기반
    /// - Bake 결과는 SkillRuntimeSequence(Addressables)로 저장
    /// </summary>
    public partial class CreateSkillWindow : DefaultEditorWindowSkill
    {
        private const string Title = "스킬 테스트 툴";

        #region table
        private TableSkill _tableSkill;
        private Dictionary<int, StruckTableSkill> _dictionary;
        private readonly List<SearchableDropdownUtility.Option<StruckTableSkill>> _dropDownOptions = new();
        private StruckTableSkill _selectedData;
        #endregion
        
        #region layout
        private Vector2 _scroll;
        #endregion
        
        private CharacterBase _dummyTargetCharacter;

        [MenuItem(ConfigEditorSkill.NameToolSettingTestSkill, false, (int)ConfigEditorSkill.ToolOrdering.SettingTestSkill)]
        public static void ShowWindow() => GetWindow<CreateSkillWindow>(Title);

        protected override void OnEnable()
        {
            base.OnEnable();

            _selectedData = null;

            ReloadAllTables();
        }

        public void OnGUI()
        {
            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;
            EditorGUILayout.Space(6);

            DrawPlayModeGate();
            EditorGUILayout.Space(6);

            OnGUISelection();
            EditorGUILayout.Space(6);

            OnGUIRowEditor();
            EditorGUILayout.Space(6);

            OnGUITimelineBake();
            EditorGUILayout.Space(6);

            OnGUICaster();
            EditorGUILayout.Space(6);

            OnGUITarget();
            EditorGUILayout.Space(6);

            OnGUIExecutor();
            EditorGUILayout.Space(6);

            EditorGUILayout.Space(20);
        }

        private void ReloadAllTables()
        {
            try
            {
                // Edit Mode 드롭다운을 위해 에디터 로더 사용(동기)
                _tableSkill = TableLoaderManagerSkill.LoadTableSkill();

                _dictionary = _tableSkill.GetDatas();
                RebuildDropdown();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Repaint();
        }
        private void RebuildDropdown()
        {
            RebuildDropdownOptions(
                source: _dictionary?.Values,
                targetOptions: _dropDownOptions,
                isValidRow: row => row.Uid > 0,
                keySelector: row => row.Uid.ToString(),
                valueSelector: row => row.Memo,
                assignSelected: row => _selectedData = row
            );
        }
    }
}
