using System;
using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 씬의 특정 캐릭터에게 패시브 스킬을 장착/해제하여 즉시 효과를 확인하는 커스텀 툴.
    /// </summary>
    /// <remarks>
    /// - Play Mode에서만 동작합니다.
    /// - TableSkill / TableSkillOption 기반으로 패시브 옵션을 미리보기 합니다.
    /// - 대상에 <see cref="CharacterPassiveSkillController"/>가 없으면 자동 부착(옵션)할 수 있습니다.
    /// - Affect 옵션을 사용하는 패시브는 <see cref="AffectApi"/>를 통해 런타임 브리지로 적용됩니다.
    /// </remarks>
    public sealed class UsePassiveSkill : DefaultEditorWindow
    {
        private const string Title = "패시브 스킬 사용하기";

        // Tables
        private TableSkillPassive _tableSkillPassive;
        private TableSkillPassiveOption _tableSkillPassiveOption;

        // Dropdown data
        private Dictionary<int, StruckTableSkillPassive> _dictionary;
        private readonly List<SearchableDropdownUtility.Option<StruckTableSkillPassive>> _dropDownOptions = new();
        private StruckTableSkillPassive _selectedData;

        private CharacterPassiveSkillController _targetPassiveController;

        // Equip params
        private int _equipLevel = 1;

        // Options
        private bool _autoAttachComponents = true;

        // UI
        private Vector2 _equippedScroll;
        private Vector2 _previewScroll;
        private string _lastReloadMessage = string.Empty;
        private Vector2 _scroll;

        [MenuItem(ConfigEditorSkill.NameToolUsePassiveSkill, false, (int)ConfigEditorSkill.ToolOrdering.UsePassiveSkill)]
        public static void ShowWindow()
        {
            GetWindow<UsePassiveSkill>(Title);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            packageType = ConfigPackageInfo.PackageType.Skill;

            _selectedData = null;
            _equipLevel = 1;

            ReloadAllTables();
        }

        protected override void OnSelectedCharacterChanged(CharacterBase character)
        {
            Repaint();
        }
        private void OnGUI()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                EditorGUILayout.Space(6);

                DrawPlayModeGate();
                EditorGUILayout.Space(6);

                DrawTargetSection();
                EditorGUILayout.Space(8);

                DrawTableSection();
                EditorGUILayout.Space(8);

                DrawEquippedSection();
                EditorGUILayout.Space(8);

                DrawPreviewSection();
                EditorGUILayout.Space(8);

                DrawReloadSection();
                EditorGUILayout.Space(20);
            }
        }

        #region GUI
        private void DrawTargetSection()
        {
            DrawCharacterSelectionSection(Title);

            if (selectedCharacter == null)
                return;
        }

        private void DrawTableSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("패시브 스킬");

                    if (_dropDownOptions.Count == 0)
                    {
                        EditorGUILayout.HelpBox("Affect 테이블이 비어있습니다. 테이블 로딩/Addressables 설정을 확인해주세요.", MessageType.Warning);
                        return;
                    }

                    string currentText = _selectedData != null ? $"{_selectedData.Uid} | {_selectedData.Name}" : "선택...";
                    int selectIndex = _selectedData?.Uid ?? 0;

                    SearchableDropdownUtility.DrawButtonAndShow(
                        buttonText: currentText,
                        options: _dropDownOptions,
                        selectedIndex: selectIndex,
                        onSelected: (idx, opt) =>
                        {
                            _selectedData = opt.Data;
                            Repaint();
                        },
                        defaultSearchMode: SearchableDropdownUtility.SearchMode.Both);
                }
                
                _equipLevel = Mathf.Max(1, EditorGUILayout.IntField("레벨", _equipLevel));

                using (new EditorGUI.DisabledScope(!Application.isPlaying || selectedCharacter == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("장착(추가/갱신)", GUILayout.Height(24)))
                        EquipSelected();

                    // if (GUILayout.Button("해제", GUILayout.Height(24)))
                    //     UnequipSelected();
                }
            }
        }

        private void DrawEquippedSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("현재 장착된 패시브", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(!Application.isPlaying || selectedCharacter == null))
                {
                    if (GUILayout.Button("전체 해제", GUILayout.Height(22)))
                        ClearAll();
                }

                if (_targetPassiveController == null)
                {
                    EditorGUILayout.HelpBox("대상에 CharacterPassiveSkillController가 없습니다. '대상에 적용 준비'를 실행하거나 자동 부착을 켜주세요.", MessageType.Info);
                    return;
                }

                var equipped = _targetPassiveController.EquippedPassives;
                if (equipped == null || equipped.Count == 0)
                {
                    EditorGUILayout.HelpBox("장착된 패시브가 없습니다.", MessageType.Info);
                    return;
                }

                _equippedScroll = EditorGUILayout.BeginScrollView(_equippedScroll, GUILayout.MinHeight(90));
                int slotIndex = 0;
                foreach (var kv in equipped)
                {
                    var uid = kv.Key;
                    var row = _tableSkillPassive?.GetDataByUid(uid);
                    var skillName =  row?.Name ?? "(Unknown)";
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{uid}  {skillName}", GUILayout.MinWidth(240));
                        EditorGUILayout.LabelField($"Lv {kv.Value}", GUILayout.Width(60));

                        using (new EditorGUI.DisabledScope(!Application.isPlaying))
                        {
                            if (GUILayout.Button("해제", GUILayout.Width(50)))
                            {
                                RemoveEquipped(uid, slotIndex);
                                break; // collection changed
                            }
                        }
                    }

                    ++slotIndex;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPreviewSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("옵션 미리보기", EditorStyles.boldLabel);

                if (_dictionary.Count == 0) return;

                if (_selectedData == null)
                {
                    EditorGUILayout.HelpBox("선택한 스킬 Row를 찾지 못했습니다.", MessageType.Warning);
                    return;
                }

                if (_selectedData.SkillKind != ConfigCommonSkill.SkillKind.Passive)
                {
                    EditorGUILayout.HelpBox("선택한 스킬이 Passive가 아닙니다.", MessageType.Warning);
                    return;
                }

                var options = _tableSkillPassiveOption?.GetOptions(_selectedData.Uid, Mathf.Max(1, _equipLevel));
                if (options == null || options.Count == 0)
                {
                    EditorGUILayout.HelpBox("해당 패시브/레벨에 연결된 옵션이 없습니다. (Level=0 공통 옵션 포함)", MessageType.Info);
                    return;
                }

                var result = new StatPreviewResult();
                
                for (int i = 0; i < options.Count; i++)
                {
                    var op = options[i];
                    if (op == null || !op.IsValid) continue;

                    switch (op.Kind)
                    {
                        case SkillOptionKind.Stat:
                            StatModifierHelper.AccumulateStat(result.Flat, result.Percent, 
                                op.TargetId, op.Op, op.Value);
                            break;

                        case SkillOptionKind.Affect:
                            StatModifierHelper.AccumulateAffect(result.AffectUids, op.TargetId);
                            break;
                    }
                }

                _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.MinHeight(120));

                EditorGUILayout.LabelField("Stat", EditorStyles.boldLabel);
                if (result.Flat.Count == 0 && result.Percent.Count == 0)
                {
                    EditorGUILayout.LabelField("- (없음)");
                }
                else
                {
                    foreach (var kv in result.Flat)
                        EditorGUILayout.LabelField($"- {kv.Key}: {kv.Value:+#;-#;0}");

                    foreach (var kv in result.Percent)
                        EditorGUILayout.LabelField($"- {kv.Key}: {kv.Value:+0.##;-0.##;0}%");
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Affect", EditorStyles.boldLabel);
                if (result.AffectUids.Count == 0)
                {
                    EditorGUILayout.LabelField("- (없음)");
                }
                else
                {
                    for (int i = 0; i < result.AffectUids.Count; i++)
                        EditorGUILayout.LabelField($"- {result.AffectUids[i]}");
                }
                
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawReloadSection()
        {
            DrawTableReloadSection(
                _lastReloadMessage,
                "skill_passive / skill_passive_option 재로딩",
                ReloadAllTables);
        }
        #endregion

        private void ReloadAllTables()
        {
            try
            {
                _tableSkillPassive = TableLoaderManagerSkill.LoadTableSkillPassive(forceReload: true);
                _tableSkillPassiveOption = TableLoaderManagerSkill.LoadTableSkillPassiveOption(forceReload: true);

                _dictionary = _tableSkillPassive?.GetDatas();
                RebuildDropdown();

                // Core 테이블(옵션 미리보기 검증에 사용 가능)
                TableLoaderManagerSkill.LoadCoreTable<TableStat>("stat", forceReload: true);
                TableLoaderManagerSkill.LoadCoreTable<TableState>("state", forceReload: true);
                TableLoaderManagerSkill.LoadCoreTable<TableDamageType>("damage_type", forceReload: true);

                _lastReloadMessage = $"테이블 재로딩 완료: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _lastReloadMessage = $"테이블 재로딩 실패: {e.GetType().Name} - {e.Message}";
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
                valueSelector: row => row.Name,
                assignSelected: row => _selectedData = row);
        }

        private void EnsureTargetReady()
        {
            if (selectedCharacter == null) return;

            if (_autoAttachComponents)
            {
                // 패시브 컨트롤러
                _targetPassiveController = selectedCharacter.GetComponent<CharacterPassiveSkillController>();
                if (_targetPassiveController == null)
                    _targetPassiveController = selectedCharacter.gameObject.AddComponent<CharacterPassiveSkillController>();

                // Affect 시스템(설치되어 있으면 자동 부착)
                AffectApi.Ensure(selectedCharacter.gameObject);
            }
            else
            {
                _targetPassiveController = selectedCharacter.GetComponent<CharacterPassiveSkillController>();
            }

            Repaint();
        }

        private void EquipSelected()
        {
            EnsureTargetReady();
            if (_targetPassiveController == null)
            {
                Debug.LogWarning("CharacterPassiveSkillController가 없습니다.");
                return;
            }

            if (_dictionary.Count == 0 || _selectedData == null || _selectedData.Uid <= 0) return;
            var uid = _selectedData.Uid;

            var dict = new Dictionary<int, int>(_targetPassiveController.EquippedPassives.Count + 1);
            foreach (var kv in _targetPassiveController.EquippedPassives)
                dict[kv.Key] = kv.Value;

            dict[uid] = Mathf.Max(1, _equipLevel);

            _targetPassiveController.ApplyEquippedPassives(dict);
        }

        // private void UnequipSelected()
        // {
        //     if (_targetPassiveController == null) return;
        //     if (_passiveSkillUids.Count == 0) return;
        //
        //     var uid = _passiveSkillUids[Mathf.Clamp(_selectedSkillIndex, 0, _passiveSkillUids.Count - 1)];
        //     RemoveEquipped(uid);
        // }

        private void RemoveEquipped(int uid, int slotIndex = 0)
        {
            if (_targetPassiveController == null) return;

            var dict = new Dictionary<int, int>(_targetPassiveController.EquippedPassives.Count);
            foreach (var kv in _targetPassiveController.EquippedPassives)
            {
                if (kv.Key == uid) continue;
                dict[kv.Key] = kv.Value;
            }

            _targetPassiveController.ApplyEquippedPassives(dict);
        }

        private void ClearAll()
        {
            EnsureTargetReady();
            if (_targetPassiveController == null) return;
            _targetPassiveController.Clear();
        }
    }
}
