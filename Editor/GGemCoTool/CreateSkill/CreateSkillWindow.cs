using System;
using System.Collections.Generic;
using Config;
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
        /// 스킬 차징 단계 테이블입니다.
        /// </summary>
        private TableSkillChargeStage _tableSkillChargeStage;

        /// <summary>
        /// 플레이어 스킬 데이터를 UID 기준으로 조회하기 위한 사전입니다.
        /// </summary>
        private Dictionary<int, StruckTableSkill> _playerDictionary;

        /// <summary>
        /// 몬스터 스킬 데이터를 UID 기준으로 조회하기 위한 사전입니다.
        /// </summary>
        private Dictionary<int, StruckTableSkillMonster> _monsterDictionary;

        /// <summary>
        /// 검색 가능한 드롭다운 UI에 표시할 옵션 목록입니다.
        /// </summary>
        private readonly List<SearchableDropdownUtility.Option<object>> _dropDownOptions = new();

        /// <summary>
        /// 현재 선택된 스킬 테이블 종류입니다.
        /// </summary>
        private ConfigCommon.SkillTableSource _selectedSource = ConfigCommon.SkillTableSource.Player;

        /// <summary>
        /// 현재 드롭다운 또는 편집 UI에서 선택된 스킬 원본 Row입니다.
        /// </summary>
        private object _selectedData;

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

            OnGUICharge();
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
                _tableSkillChargeStage = TableLoaderManagerSkill.LoadTableSkillChargeStage();

                _playerDictionary = BuildPlayerDictionary(_tableSkill);
                _monsterDictionary = BuildMonsterDictionary(_tableSkillMonster);
                RebuildDropdown();
                ReloadCurrentTable();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Repaint();
        }

        /// <summary>
        /// 플레이어 스킬 테이블을 UID 기반 사전으로 변환합니다.
        /// </summary>
        private static Dictionary<int, StruckTableSkill> BuildPlayerDictionary(TableSkill table)
        {
            var result = new Dictionary<int, StruckTableSkill>();
            if (table == null) return result;

            foreach (var pair in table.GetDatas())
            {
                var row = pair.Value;
                if (row == null || row.Uid <= 0) continue;
                result[row.Uid] = row;
            }

            return result;
        }

        /// <summary>
        /// 몬스터 스킬 테이블을 UID 기반 사전으로 변환합니다.
        /// </summary>
        private static Dictionary<int, StruckTableSkillMonster> BuildMonsterDictionary(TableSkillMonster table)
        {
            var result = new Dictionary<int, StruckTableSkillMonster>();
            if (table == null) return result;

            foreach (var pair in table.GetDatas())
            {
                var row = pair.Value;
                if (row == null || row.Uid <= 0) continue;
                result[row.Uid] = row;
            }

            return result;
        }

        /// <summary>
        /// 현재 선택된 테이블 기준으로 드롭다운 옵션 목록을 다시 생성하고 선택 상태를 복원합니다.
        /// </summary>
        private void RebuildDropdown()
        {
            int previousUid = GetSelectedUid();

            RebuildDropdownOptions(
                source: EnumerateCurrentRows(),
                targetOptions: _dropDownOptions,
                isValidRow: row => row != null && GetUid(row) > 0,
                keySelector: row => GetUid(row).ToString(),
                valueSelector: GetDisplayName,
                assignSelected: row => _selectedData = row);

            if (previousUid > 0 && TryGetCurrentRowByUid(previousUid, out var selected))
            {
                _selectedData = selected;
            }
            else if (_dropDownOptions.Count <= 0)
            {
                _selectedData = null;
            }

            CacheRow();
        }

        /// <summary>
        /// 현재 선택된 source에 대응하는 Row 열거를 반환합니다.
        /// </summary>
        private IEnumerable<object> EnumerateCurrentRows()
        {
            if (_selectedSource == ConfigCommon.SkillTableSource.Monster)
            {
                if (_monsterDictionary == null)
                    yield break;

                foreach (var row in _monsterDictionary.Values)
                    yield return row;

                yield break;
            }

            if (_playerDictionary == null)
                yield break;

            foreach (var row in _playerDictionary.Values)
                yield return row;
        }

        /// <summary>
        /// 현재 선택된 source 기준으로 UID에 해당하는 Row를 조회합니다.
        /// </summary>
        private bool TryGetCurrentRowByUid(int uid, out object row)
        {
            row = null;
            if (uid <= 0)
                return false;

            if (_selectedSource == ConfigCommon.SkillTableSource.Monster)
            {
                if (_monsterDictionary != null && _monsterDictionary.TryGetValue(uid, out var monsterRow))
                {
                    row = monsterRow;
                    return true;
                }

                return false;
            }

            if (_playerDictionary != null && _playerDictionary.TryGetValue(uid, out var playerRow))
            {
                row = playerRow;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 현재 선택된 Row의 UID를 반환합니다.
        /// </summary>
        private int GetSelectedUid()
        {
            return GetUid(_selectedData);
        }

        /// <summary>
        /// 현재 선택된 Row의 표시 이름을 반환합니다.
        /// </summary>
        private string GetSelectedDisplayName()
        {
            return GetDisplayName(_selectedData);
        }

        /// <summary>
        /// 현재 편집 중인 Row의 타게팅 모드를 반환합니다.
        /// </summary>
        private ConfigCommonSkill.SkillTargetingMode GetCurrentTargetingModeValue()
        {
            if (_editingRow is StruckTableSkill editingPlayer)
                return editingPlayer.TargetingMode;
            if (_editingRow is StruckTableSkillMonster editingMonster)
                return editingMonster.TargetingMode;
            if (_selectedData is StruckTableSkill selectedPlayer)
                return selectedPlayer.TargetingMode;
            if (_selectedData is StruckTableSkillMonster selectedMonster)
                return selectedMonster.TargetingMode;

            return default;
        }

        /// <summary>
        /// Row에서 UID를 읽습니다.
        /// </summary>
        private static int GetUid(object row)
        {
            return row switch
            {
                StruckTableSkill player => player.Uid,
                StruckTableSkillMonster monster => monster.Uid,
                _ => 0,
            };
        }

        /// <summary>
        /// Row의 표시 문자열을 반환합니다.
        /// </summary>
        private static string GetDisplayName(object row)
        {
            return row switch
            {
                StruckTableSkill player => player.Name,
                StruckTableSkillMonster monster => monster.Name,
                _ => string.Empty,
            };
        }
    }
}
