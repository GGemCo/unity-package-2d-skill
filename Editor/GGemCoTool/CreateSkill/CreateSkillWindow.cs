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
    /// 스킬 테이블 기반으로 스킬 데이터를 선택, 편집, 타임라인 Bake, 실행 테스트까지 수행하는 스킬 제작 에디터 창입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 창은 <c>skill</c> 및 <c>skill_monster</c> 테이블을 SSOT(Single Source of Truth)로 사용하며,
    /// Marker 없이 이벤트 클립 기반으로 스킬 시퀀스를 구성합니다.
    /// </para>
    /// <para>
    /// Bake 결과는 Addressables에서 사용할 수 있는 SkillRuntimeSequence 형태로 저장되는 것을 전제로 합니다.
    /// </para>
    /// <para>
    /// partial 클래스로 분리되어 있으며, 본 파일은 테이블 로드, 드롭다운 구성, 기본 GUI 레이아웃과 같은
    /// 창의 공통 초기화 및 데이터 선택 흐름을 담당합니다.
    /// </para>
    /// </remarks>
    public partial class CreateSkillWindow : DefaultEditorWindowSkill
    {
        /// <summary>
        /// 에디터 창 제목입니다.
        /// </summary>
        private const string Title = "스킬 테스트 툴";

        #region table

        /// <summary>
        /// 플레이어 스킬 원본 테이블입니다.
        /// </summary>
        private TableSkill _tableSkill;

        /// <summary>
        /// 몬스터 스킬 원본 테이블입니다.
        /// </summary>
        private TableSkillMonster _tableSkillMonster;

        /// <summary>
        /// 플레이어 스킬 데이터를 UID 기준으로 조회하기 위한 사전입니다.
        /// </summary>
        private Dictionary<int, SkillEditorRow> _playerDictionary;

        /// <summary>
        /// 몬스터 스킬 데이터를 UID 기준으로 조회하기 위한 사전입니다.
        /// </summary>
        private Dictionary<int, SkillEditorRow> _monsterDictionary;

        /// <summary>
        /// 검색 가능한 드롭다운 UI에 표시할 옵션 목록입니다.
        /// </summary>
        private readonly List<SearchableDropdownUtility.Option<SkillEditorRow>> _dropDownOptions = new();

        /// <summary>
        /// 현재 선택된 스킬 테이블 종류입니다.
        /// </summary>
        private ConfigCommon.SkillTableSource _selectedSource = ConfigCommon.SkillTableSource.Player;

        /// <summary>
        /// 현재 드롭다운 또는 편집 UI에서 선택된 스킬 데이터입니다.
        /// </summary>
        private SkillEditorRow _selectedData;

        #endregion

        #region layout

        /// <summary>
        /// 스크롤 뷰의 현재 스크롤 위치입니다.
        /// </summary>
        private Vector2 _scroll;

        #endregion

        /// <summary>
        /// Unity 메뉴에서 스킬 테스트 창을 엽니다.
        /// </summary>
        [MenuItem(ConfigEditorSkill.NameToolSettingTestSkill, false, (int)ConfigEditorSkill.ToolOrdering.SettingTestSkill)]
        public static void ShowWindow() => GetWindow<CreateSkillWindow>(Title);

        /// <summary>
        /// 에디터 창이 활성화될 때 호출되며, 선택 상태를 초기화하고 모든 스킬 테이블을 다시 로드합니다.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            _selectedData = null;

            ReloadAllTables();
        }

        /// <summary>
        /// 에디터 창의 전체 GUI를 그립니다.
        /// </summary>
        /// <remarks>
        /// 각 섹션은 partial 클래스의 다른 구현부에 분산되어 있으며,
        /// 본 메서드는 전체 레이아웃과 호출 순서를 조정합니다.
        /// </remarks>
        private void OnGUI()
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

        /// <summary>
        /// 현재 선택된 테이블 종류에 따라 사용할 스킬 데이터 사전을 반환합니다.
        /// </summary>
        private Dictionary<int, SkillEditorRow> CurrentDictionary =>
            _selectedSource == ConfigCommon.SkillTableSource.Monster ? _monsterDictionary : _playerDictionary;

        /// <summary>
        /// 플레이어/몬스터 스킬 테이블을 모두 다시 로드하고, 편집용 사전과 드롭다운 목록을 재구성합니다.
        /// </summary>
        /// <remarks>
        /// Edit Mode에서 즉시 사용할 수 있도록 에디터 전용 동기 로더를 사용합니다.
        /// </remarks>
        private void ReloadAllTables()
        {
            try
            {
                // Edit Mode 드롭다운 구성을 위해 에디터 전용 동기 로더를 사용
                _tableSkill = TableLoaderManagerSkill.LoadTableSkill();
                _tableSkillMonster = TableLoaderManagerSkill.LoadTableSkillMonster();

                _playerDictionary = BuildPlayerDictionary(_tableSkill);
                _monsterDictionary = BuildMonsterDictionary(_tableSkillMonster);
                RebuildDropdown();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Repaint();
        }

        /// <summary>
        /// 플레이어 스킬 테이블을 편집용 Row 사전으로 변환합니다.
        /// </summary>
        /// <param name="table">변환할 플레이어 스킬 테이블입니다.</param>
        /// <returns>UID를 키로 사용하는 플레이어 스킬 편집용 사전을 반환합니다.</returns>
        private static Dictionary<int, SkillEditorRow> BuildPlayerDictionary(TableSkill table)
        {
            var result = new Dictionary<int, SkillEditorRow>();
            if (table == null) return result;

            foreach (var pair in table.GetDatas())
            {
                var row = SkillEditorRow.From(pair.Value);
                if (row == null || row.Uid <= 0) continue;
                result[row.Uid] = row;
            }

            return result;
        }

        /// <summary>
        /// 몬스터 스킬 테이블을 편집용 Row 사전으로 변환합니다.
        /// </summary>
        /// <param name="table">변환할 몬스터 스킬 테이블입니다.</param>
        /// <returns>UID를 키로 사용하는 몬스터 스킬 편집용 사전을 반환합니다.</returns>
        private static Dictionary<int, SkillEditorRow> BuildMonsterDictionary(TableSkillMonster table)
        {
            var result = new Dictionary<int, SkillEditorRow>();
            if (table == null) return result;

            foreach (var pair in table.GetDatas())
            {
                var row = SkillEditorRow.From(pair.Value);
                if (row == null || row.Uid <= 0) continue;
                result[row.Uid] = row;
            }

            return result;
        }

        /// <summary>
        /// 현재 선택된 테이블 기준으로 드롭다운 옵션 목록을 다시 생성하고 선택 상태를 복원합니다.
        /// </summary>
        /// <remarks>
        /// 이전에 선택된 UID가 현재 데이터에도 존재하면 해당 항목을 다시 선택하고,
        /// 존재하지 않으면 선택 상태를 초기화합니다.
        /// </remarks>
        private void RebuildDropdown()
        {
            var previousUid = _selectedData != null && _selectedData.Source == _selectedSource ? _selectedData.Uid : 0;

            RebuildDropdownOptions(
                source: CurrentDictionary?.Values,
                targetOptions: _dropDownOptions,
                isValidRow: row => row != null && row.Uid > 0,
                keySelector: row => row.Uid.ToString(),
                valueSelector: row => row.Memo,
                assignSelected: row => _selectedData = row);

            // 동일 UID가 현재 데이터에도 존재하면 기존 선택을 복원
            if (previousUid > 0 && CurrentDictionary != null && CurrentDictionary.TryGetValue(previousUid, out var selected))
            {
                _selectedData = selected;
            }
            else if (_dropDownOptions.Count <= 0)
            {
                _selectedData = null;
            }

            CacheRow();
        }
    }
}