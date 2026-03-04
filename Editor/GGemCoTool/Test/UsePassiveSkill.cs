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
        private Dictionary<int, StruckTableSkillPassive> _skillDict;

        // Dropdown data
        private readonly List<string> _passiveSkillNames = new();
        private readonly List<int> _passiveSkillUids = new();
        private int _selectedSkillIndex;

        // Target
        private CharacterBase _targetCharacter;
        private CharacterPassiveSkillController _targetPassiveController;

        private readonly List<CharacterBase> _sceneCharacters = new();
        private readonly List<string> _sceneCharacterNames = new();
        private int _selectedCharacterIndex;

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

            _selectedSkillIndex = 0;
            _selectedCharacterIndex = 0;
            _equipLevel = 1;

            ReloadAllTables();
            RefreshSceneCharacters();
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

                DrawSkillSection();
                EditorGUILayout.Space(8);

                DrawEquippedSection();
                EditorGUILayout.Space(8);

                DrawPreviewSection();
                EditorGUILayout.Space(8);

                DrawReloadSection();
            }
        }

        private void DrawPlayModeGate()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("실행 조건", EditorStyles.boldLabel);

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Play Mode에서만 동작합니다.", MessageType.Warning);
                    return;
                }

                if (!SceneGame.Instance)
                {
                    EditorGUILayout.HelpBox("SceneGame.Instance를 찾지 못했습니다. 게임 씬이 로드되어 있는지 확인해주세요.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Play Mode에서 동작 중입니다.", MessageType.Info);
                }
            }
        }

        private void DrawTargetSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("대상 캐릭터", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("현재 선택 오브젝트로 지정", GUILayout.Height(22)))
                            TryAssignFromSelection();

                        if (GUILayout.Button("씬 캐릭터 목록 새로고침", GUILayout.Height(22)))
                            RefreshSceneCharacters();
                    }

                    if (_sceneCharacterNames.Count == 0)
                    {
                        EditorGUILayout.HelpBox("씬에서 CharacterBase를 찾지 못했습니다.", MessageType.Info);
                    }
                    else
                    {
                        var newIndex = EditorGUILayout.Popup("씬 캐릭터", _selectedCharacterIndex, _sceneCharacterNames.ToArray());
                        if (newIndex != _selectedCharacterIndex)
                        {
                            _selectedCharacterIndex = newIndex;
                            AssignTarget(_sceneCharacters[_selectedCharacterIndex]);
                        }
                    }

                    _targetCharacter = (CharacterBase)EditorGUILayout.ObjectField("대상(직접 지정)", _targetCharacter, typeof(CharacterBase), true);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _autoAttachComponents = EditorGUILayout.ToggleLeft("필요 컴포넌트 자동 부착", _autoAttachComponents, GUILayout.Width(180));

                        if (GUILayout.Button("대상에 적용 준비", GUILayout.Height(22)))
                            EnsureTargetReady();
                    }

                    DrawTargetSummary();
                }
            }
        }

        private void DrawTargetSummary()
        {
            if (_targetCharacter == null)
            {
                EditorGUILayout.HelpBox("대상을 지정해주세요.", MessageType.Warning);
                return;
            }

            var hasController = _targetCharacter.GetComponent<CharacterPassiveSkillController>() != null;
            var hasAffect = AffectApi.HasRuntime();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("대상 상태", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("GameObject", _targetCharacter.name);
                EditorGUILayout.LabelField("CharacterPassiveSkillController", hasController ? "O" : "X");
                EditorGUILayout.LabelField("Affect Runtime", hasAffect ? "O" : "X (미설치/미로드 시 Affect 옵션은 적용되지 않습니다)");
            }
        }

        private void DrawSkillSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("패시브 스킬 선택", EditorStyles.boldLabel);

                if (_passiveSkillNames.Count == 0)
                {
                    EditorGUILayout.HelpBox("패시브 스킬 목록이 비어 있습니다. 테이블을 재로딩 해주세요.", MessageType.Info);
                    return;
                }

                _selectedSkillIndex = EditorGUILayout.Popup("패시브 스킬", _selectedSkillIndex, _passiveSkillNames.ToArray());
                _equipLevel = Mathf.Max(1, EditorGUILayout.IntField("레벨", _equipLevel));

                using (new EditorGUI.DisabledScope(!Application.isPlaying || _targetCharacter == null))
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

                using (new EditorGUI.DisabledScope(!Application.isPlaying || _targetCharacter == null))
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
                    var skillName = ResolveSkillName(kv.Key);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{kv.Key}  {skillName}", GUILayout.MinWidth(240));
                        EditorGUILayout.LabelField($"Lv {kv.Value}", GUILayout.Width(60));

                        using (new EditorGUI.DisabledScope(!Application.isPlaying))
                        {
                            if (GUILayout.Button("해제", GUILayout.Width(50)))
                            {
                                RemoveEquipped(kv.Key, slotIndex);
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

                if (_passiveSkillUids.Count == 0) return;

                var skillUid = _passiveSkillUids[Mathf.Clamp(_selectedSkillIndex, 0, _passiveSkillUids.Count - 1)];
                var skillRow = _tableSkillPassive?.GetDataByUid(skillUid);
                if (skillRow == null)
                {
                    EditorGUILayout.HelpBox("선택한 스킬 Row를 찾지 못했습니다.", MessageType.Warning);
                    return;
                }

                if (skillRow.SkillKind != ConfigCommonSkill.SkillKind.Passive)
                {
                    EditorGUILayout.HelpBox("선택한 스킬이 Passive가 아닙니다.", MessageType.Warning);
                    return;
                }

                if (skillRow.OptionGroupUid <= 0)
                {
                    EditorGUILayout.HelpBox("OptionGroupUid가 비어 있습니다. (skill_option 테이블과 연결되지 않음)", MessageType.Info);
                    return;
                }

                var options = _tableSkillPassiveOption?.GetOptions(skillRow.OptionGroupUid, Mathf.Max(1, _equipLevel));
                if (options == null || options.Count == 0)
                {
                    EditorGUILayout.HelpBox("해당 레벨의 옵션이 없습니다. (Level=0 옵션만 있거나 데이터 누락)", MessageType.Info);
                    return;
                }

                var flat = new Dictionary<string, int>(16);
                var percent = new Dictionary<string, float>(16);
                var affects = new List<int>();

                for (int i = 0; i < options.Count; i++)
                {
                    var op = options[i];
                    if (op == null || !op.IsValid) continue;

                    switch (op.Kind)
                    {
                        case SkillOptionKind.Stat:
                            AccumulateStat(flat, percent, op);
                            break;

                        case SkillOptionKind.Affect:
                            if (int.TryParse(op.TargetId, out var aid) && aid > 0)
                                affects.Add(aid);
                            break;
                    }
                }

                _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.MinHeight(120));

                EditorGUILayout.LabelField("Stat", EditorStyles.boldLabel);
                if (flat.Count == 0 && percent.Count == 0)
                {
                    EditorGUILayout.LabelField("- (없음)");
                }
                else
                {
                    foreach (var kv in flat)
                        EditorGUILayout.LabelField($"- {kv.Key}: {kv.Value:+#;-#;0}");

                    foreach (var kv in percent)
                        EditorGUILayout.LabelField($"- {kv.Key}: {kv.Value:+0.##;-0.##;0}%");
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Affect", EditorStyles.boldLabel);
                if (affects.Count == 0)
                {
                    EditorGUILayout.LabelField("- (없음)");
                }
                else
                {
                    for (int i = 0; i < affects.Count; i++)
                        EditorGUILayout.LabelField($"- {affects[i]}");
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawReloadSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("테이블", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("테이블 재로딩", GUILayout.Height(22)))
                        ReloadAllTables();

                    if (!string.IsNullOrEmpty(_lastReloadMessage))
                        EditorGUILayout.LabelField(_lastReloadMessage);
                }
            }
        }

        private void ReloadAllTables()
        {
            try
            {
                _tableSkillPassive = TableLoaderManagerSkill.LoadTableSkillPassive(forceReload: true);
                _tableSkillPassiveOption = TableLoaderManagerSkill.LoadTableSkillPassiveOption(forceReload: true);

                _skillDict = _tableSkillPassive?.GetDatas();
                RebuildPassiveSkillDropdown();

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

        private void RebuildPassiveSkillDropdown()
        {
            _passiveSkillNames.Clear();
            _passiveSkillUids.Clear();

            if (_skillDict == null) return;

            foreach (var kvp in _skillDict)
            {
                var row = kvp.Value;
                if (row == null) continue;
                if (row.SkillKind != ConfigCommonSkill.SkillKind.Passive) continue;

                _passiveSkillUids.Add(row.Uid);
                _passiveSkillNames.Add($"{row.Uid}  {row.Name}");
            }

            if (_selectedSkillIndex >= _passiveSkillNames.Count)
                _selectedSkillIndex = Mathf.Max(0, _passiveSkillNames.Count - 1);
        }

        private void RefreshSceneCharacters()
        {
            _sceneCharacters.Clear();
            _sceneCharacterNames.Clear();

            if (!Application.isPlaying)
            {
                Repaint();
                return;
            }

            #if UNITY_2023_1_OR_NEWER
            var chars = UnityEngine.Object.FindObjectsByType<CharacterBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var chars = FindObjectsOfType<CharacterBase>(includeInactive: false);
#endif
            foreach (var c in chars)
            {
                _sceneCharacters.Add(c);
                _sceneCharacterNames.Add(c ? c.name : "(null)");
            }

            if (_sceneCharacters.Count > 0)
            {
                _selectedCharacterIndex = Mathf.Clamp(_selectedCharacterIndex, 0, _sceneCharacters.Count - 1);
                if (_targetCharacter == null)
                    AssignTarget(_sceneCharacters[_selectedCharacterIndex]);
            }

            Repaint();
        }

        private void TryAssignFromSelection()
        {
            var go = Selection.activeGameObject;
            if (!go) return;

            var c = go.GetComponentInParent<CharacterBase>();
            if (!c) c = go.GetComponentInChildren<CharacterBase>();
            AssignTarget(c);
        }

        private void AssignTarget(CharacterBase c)
        {
            _targetCharacter = c;
            _targetPassiveController = _targetCharacter ? _targetCharacter.GetComponent<CharacterPassiveSkillController>() : null;
            Repaint();
        }

        private void EnsureTargetReady()
        {
            if (_targetCharacter == null) return;

            if (_autoAttachComponents)
            {
                // 패시브 컨트롤러
                _targetPassiveController = _targetCharacter.GetComponent<CharacterPassiveSkillController>();
                if (_targetPassiveController == null)
                    _targetPassiveController = _targetCharacter.gameObject.AddComponent<CharacterPassiveSkillController>();

                // Affect 시스템(설치되어 있으면 자동 부착)
                AffectApi.Ensure(_targetCharacter.gameObject);
            }
            else
            {
                _targetPassiveController = _targetCharacter.GetComponent<CharacterPassiveSkillController>();
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

            if (_passiveSkillUids.Count == 0) return;
            var uid = _passiveSkillUids[Mathf.Clamp(_selectedSkillIndex, 0, _passiveSkillUids.Count - 1)];

            // 테스트 툴에서도 "장착 후 임시 HP Current 채움" 정책을 적용합니다.
            var before = PassiveTempHpFillUtility.Capture(_targetCharacter);

            var dict = new Dictionary<int, int>(_targetPassiveController.EquippedPassives.Count + 1);
            foreach (var kv in _targetPassiveController.EquippedPassives)
                dict[kv.Key] = kv.Value;

            dict[uid] = Mathf.Max(1, _equipLevel);

            _targetPassiveController.ApplyEquippedPassives(dict);

            PassiveTempHpFillUtility.FillCurrentIfTempMaxIncreased(_targetCharacter, before);
            
            // UI 장착 시에만 "임시 HP Current도 채움" 정책을 적용합니다.
            // (기본 런타임 정책은 임시 최대 HP 변경 시 Current를 자동 충전하지 않습니다.)
            var player = _targetCharacter.GetComponent<Player>();
            if (player)
            {
                var skillData = SkillPackageManager.Instance?.SaveDataManagerSkill?.Skill;
                if (skillData != null)
                {
                    skillData.SetPassiveEquip(slotIndex:dict.Count-1, skillUid:uid, skillCount:1, skillLevel:1, skillLearn:true);    
                }
            }
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
            
            // UI 장착 시에만 "임시 HP Current도 채움" 정책을 적용합니다.
            // (기본 런타임 정책은 임시 최대 HP 변경 시 Current를 자동 충전하지 않습니다.)
            var player = _targetCharacter.GetComponent<Player>();
            if (player)
            {
                var skillData = SkillPackageManager.Instance?.SaveDataManagerSkill?.Skill;
                if (skillData != null)
                {
                    skillData.RemovePassiveEquip(slotIndex:slotIndex);
                }
            }
        }

        private void ClearAll()
        {
            EnsureTargetReady();
            if (_targetPassiveController == null) return;
            _targetPassiveController.Clear();
            
            var player = _targetCharacter.GetComponent<Player>();
            if (player)
            {
                var skillData = SkillPackageManager.Instance?.SaveDataManagerSkill?.Skill;
                if (skillData != null)
                {
                    skillData.RemovePassiveEquipAll();
                }
            }
        }

        private string ResolveSkillName(int skillUid)
        {
            var row = _tableSkillPassive?.GetDataByUid(skillUid);
            return row?.Name ?? "(Unknown)";
        }

        private static void AccumulateStat(Dictionary<string, int> flat, Dictionary<string, float> percent, StruckTableSkillPassiveOption op)
        {
            if (string.IsNullOrEmpty(op.TargetId)) return;

            // CharacterPassiveSkillController 정책과 동일
            switch (op.Op)
            {
                case ConfigCommon.SuffixType.Plus:
                    flat[op.TargetId] = flat.GetValueOrDefault(op.TargetId, 0) + (int)op.Value;
                    break;

                case ConfigCommon.SuffixType.Minus:
                    flat[op.TargetId] = flat.GetValueOrDefault(op.TargetId, 0) - (int)op.Value;
                    break;

                case ConfigCommon.SuffixType.Increase:
                    percent[op.TargetId] = percent.GetValueOrDefault(op.TargetId, 0f) + op.Value;
                    break;

                case ConfigCommon.SuffixType.Decrease:
                    percent[op.TargetId] = percent.GetValueOrDefault(op.TargetId, 0f) - op.Value;
                    break;

                case ConfigCommon.SuffixType.None:
                    flat[op.TargetId] = flat.GetValueOrDefault(op.TargetId, 0) + (int)op.Value;
                    break;
            }
        }
    }
}
