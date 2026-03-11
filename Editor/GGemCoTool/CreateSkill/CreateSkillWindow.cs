using System;
using System.IO;
using System.Linq;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using TableLoaderManager = GGemCo2DCore.TableLoaderManager;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 제작 툴(V2)
    /// - SSOT: skill / skill_monster 테이블
    /// - Marker 미사용, 이벤트 클립 기반
    /// - Bake 결과는 SkillRuntimeSequence(Addressables)로 저장
    /// </summary>
    public sealed class CreateSkillWindow : EditorWindow
    {
        private static readonly System.Reflection.FieldInfo ApplyStatusApplyToField =
            typeof(ApplyStatusEventDefinition).GetField("applyTo");

        private static void TrySetApplyStatusApplyTo(ApplyStatusEventDefinition def, int applyToRaw)
        {
            if (ApplyStatusApplyToField == null) return;

            var fieldType = ApplyStatusApplyToField.FieldType;
            if (!fieldType.IsEnum) return;

            try
            {
                var value = System.Enum.ToObject(fieldType, applyToRaw);
                ApplyStatusApplyToField.SetValue(def, value);
            }
            catch
            {
                // ignore
            }
        }


        private const string Title = "Skill Authoring V2";

        private readonly SkillAuthoringState _state = new();
        private readonly SkillSelectionPanel _selectionPanel = new();
        private readonly SkillEditorPanel _editorPanel = new();
        private readonly SkillPlayModeTesterPanel _playModeTesterPanel = new();

        [MenuItem(ConfigEditorSkill.NameToolSettingTestSkill, false, (int)ConfigEditorSkill.ToolOrdering.SettingTestSkill)]
        public static void Open()
        {
            var w = GetWindow<CreateSkillWindow>();
            w.titleContent = new GUIContent(Title);
            w.minSize = new Vector2(700, 260);
        }

        // Skill selection (SearchableDropdownUtility)
        private Button _btnSelectSkill;
        private Label _labelSelectedSkill;
        private EnumField _tableKindField;

        // Editing UI
        private ScrollView _rightScroll;
        private VisualElement _editRoot;

        private IntegerField _fUid;
        private TextField _fName;
        private TextField _fMemo;
        private Toggle _fDefaultLearn;
        private IntegerField _fNeedPlayerLevel;
        private TextField _fIconFileName;
        private FloatField _fCastTime;
        private FloatField _fCoolTime;
        private EnumField _fTargetingMode;
        private FloatField _fRange;
        private IntegerField _fMaxTargets;
        private TextField _fDefaultAreaId;
        private TextField _fCastStartClip;
        private TextField _fCastLoopClip;
        private TextField _fCastEndClip;
        private TextField _fUseClip;

        private Button _btnRevert;
        private Button _btnApplyTest;
        private Button _btnSaveTable;

        // PlayMode Test
        private DropdownField _monsterDropdown;
        private Button _btnSpawnMonster;
        private Toggle _toggleAutoResetMonster;
        private Button _btnCaptureMonsterOrigin;
        private Button _btnResetMonsterOrigin;

        // PlayMode Caster (Scene)
        private Button _btnRefreshSceneCasters;
        private DropdownField _sceneCasterDropdown;
        private Button _btnSelectSceneCaster;
        private Button _btnSelectCasterFromSelection;

        // PlayMode Target (Scene)
        private Button _btnRefreshSceneTargets;
        private DropdownField _sceneTargetDropdown;
        private Button _btnSelectSceneTarget;
        private Button _btnSelectTargetFromSelection;
        private Button _btnClearManualTarget;

        private readonly System.Collections.Generic.List<CharacterBase> _sceneCasters = new();
        private readonly System.Collections.Generic.List<string> _sceneCasterNames = new();
        private int _selectedSceneCasterIndex;

        private readonly System.Collections.Generic.List<CharacterBase> _sceneTargets = new();
        private readonly System.Collections.Generic.List<string> _sceneTargetNames = new();
        private int _selectedSceneTargetIndex;

        private readonly System.Collections.Generic.List<string> _monsterNames = new();
        private readonly System.Collections.Generic.List<int> _monsterUids = new();
        private int _selectedMonsterIndex;

        private Button _btnUseSkill;
        private HelpBox _playModeHelp;

        private ObjectField _timelineField;

        private Button _bakeButton;

        private Toggle _forceReload;

        private SkillAuthoringTableKind _selectedTableKind = SkillAuthoringTableKind.Player;
        private System.Collections.Generic.List<SkillAuthoringModel> _skillListSource;

        private SkillAuthoringModel _selectedSkill;
        private SkillAuthoringModel _cachedSkillOriginal;
        private SkillAuthoringModel _editingSkill;
        private bool _editingDirty;

        private bool _lastPlayModeState;

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            rootVisualElement.Add(_selectionPanel.BuildToolbar(
                _state,
                onForceReloadChanged: value =>
                {
                    if (_forceReload == null)
                        _forceReload = new Toggle();
                    _forceReload.value = value;
                },
                onTableKindChanged: kind =>
                {
                    _selectedTableKind = kind;
                    _state.TableKind = kind;
                    _selectedSkill = null;
                    _state.ClearSelection();
                    LoadSkills();
                    RefreshSelected();
                },
                onReload: LoadSkills));

            var selectBar = _selectionPanel.BuildSkillSelectionBar(OpenSkillSearchDropdown);
            _labelSelectedSkill = selectBar.Q<Label>("selected-skill-label");
            _btnSelectSkill = selectBar.Q<Button>();
            rootVisualElement.Add(selectBar);

            var monsterSection = BuildPlayModeMonsterUI();
            var casterSection = BuildPlayModeSceneCasterUI();
            var targetSection = BuildPlayModeSceneTargetUI();

            _rightScroll = _editorPanel.Build(
                onBuildFields: _ => BuildEditFields(),
                onRevert: RevertEdits,
                onApplyTest: ApplyTestEdits,
                onSaveTable: SaveEditsToSkillTxt,
                onBake: BakeRuntimeSequence,
                out _editRoot,
                out _timelineField,
                out _btnRevert,
                out _btnApplyTest,
                out _btnSaveTable,
                out _bakeButton);

            _playModeHelp = _playModeTesterPanel.Build(
                _editRoot,
                onUseSkill: UseSkillInPlayMode,
                out _btnUseSkill,
                monsterSection,
                casterSection,
                targetSection);

            rootVisualElement.Add(_rightScroll);

            LoadSkills();
            RefreshSelected();

            _lastPlayModeState = Application.isPlaying;
        }

        private VisualElement BuildPlayModeMonsterUI()
        {
            var box = new VisualElement
            {
                style =
                {
                    marginTop = 8,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 6,
                    paddingBottom = 6,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                }
            };

            box.Add(new Label("몬스터 선택/소환(PlayMode)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            _monsterDropdown = new DropdownField("Caster Monster", _monsterNames, 0);
            _monsterDropdown.RegisterValueChangedCallback(_ =>
            {
                _selectedMonsterIndex = Mathf.Clamp(_monsterDropdown.index, 0, Mathf.Max(0, _monsterUids.Count - 1));
            });
            box.Add(_monsterDropdown);

            _btnSpawnMonster = new Button(SpawnSelectedMonsterInPlayMode) { text = "선택 몬스터 소환" };
            box.Add(_btnSpawnMonster);
            _toggleAutoResetMonster = new Toggle("Auto Reset (몬스터 위치 원복)") { value = true };
            _toggleAutoResetMonster.RegisterValueChangedCallback(evt =>
            {
                if (!Application.isPlaying) return;
                var hub = SkillTestRuntimeHub.Instance != null
                    ? SkillTestRuntimeHub.Instance
                    : UnityEngine.Object.FindFirstObjectByType<SkillTestRuntimeHub>();
                if (hub != null) hub.AutoResetSelectedMonsterAfterSkill = evt.newValue;
            });
            box.Add(_toggleAutoResetMonster);

            _btnCaptureMonsterOrigin = new Button(CaptureSelectedMonsterOriginInPlayMode) { text = "현재 위치를 원본으로 저장" };
            box.Add(_btnCaptureMonsterOrigin);

            _btnResetMonsterOrigin = new Button(ResetSelectedMonsterOriginInPlayMode) { text = "원본 위치로 되돌리기" };
            box.Add(_btnResetMonsterOrigin);


            RefreshMonsterDropdown();
            return box;
        }

        private VisualElement BuildPlayModeSceneCasterUI()
        {
            var box = new VisualElement
            {
                style =
                {
                    marginTop = 8,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 6,
                    paddingBottom = 6,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                }
            };

            box.Add(new Label("씬 Caster 선택(PlayMode)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            _btnSelectCasterFromSelection = new Button(SelectCasterFromCurrentSelection)
            {
                text = "현재 선택 오브젝트로 지정",
                style = { marginRight = 6 }
            };
            row.Add(_btnSelectCasterFromSelection);

            _btnRefreshSceneCasters = new Button(RefreshSceneCasterDropdown)
            {
                text = "씬 캐릭터 목록 새로고침",
            };
            row.Add(_btnRefreshSceneCasters);
            box.Add(row);

            _sceneCasterDropdown = new DropdownField("Scene Caster", _sceneCasterNames, 0);
            _sceneCasterDropdown.RegisterValueChangedCallback(_ =>
            {
                _selectedSceneCasterIndex = Mathf.Clamp(_sceneCasterDropdown.index, 0, Mathf.Max(0, _sceneCasters.Count - 1));
            });
            box.Add(_sceneCasterDropdown);

            _btnSelectSceneCaster = new Button(SelectSceneCasterInPlayMode) { text = "선택 캐릭터를 Caster로 지정" };
            box.Add(_btnSelectSceneCaster);

            box.Add(new HelpBox(
                "- Play Mode에서만 동작합니다.\n" +
                "- 선택한 캐릭터(플레이어/몬스터 모두 가능)를 스킬 실행 캐스터로 지정합니다.",
                HelpBoxMessageType.Info));

            RefreshSceneCasterDropdown();
            return box;
        }

        private VisualElement BuildPlayModeSceneTargetUI()
        {
            var box = new VisualElement
            {
                style =
                {
                    marginTop = 8,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 6,
                    paddingBottom = 6,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                }
            };

            box.Add(new Label("씬 Target 선택(PlayMode)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            _btnSelectTargetFromSelection = new Button(SelectTargetFromCurrentSelection)
            {
                text = "현재 선택 오브젝트로 지정",
                style = { marginRight = 6 }
            };
            row.Add(_btnSelectTargetFromSelection);

            _btnRefreshSceneTargets = new Button(RefreshSceneTargetDropdown)
            {
                text = "씬 캐릭터 목록 새로고침",
                style = { marginRight = 6 }
            };
            row.Add(_btnRefreshSceneTargets);

            _btnClearManualTarget = new Button(ClearManualTargetInPlayMode)
            {
                text = "수동 Target 해제(기본 정책)",
            };
            row.Add(_btnClearManualTarget);
            box.Add(row);

            _sceneTargetDropdown = new DropdownField("Scene Target", _sceneTargetNames, 0);
            _sceneTargetDropdown.RegisterValueChangedCallback(_ =>
            {
                _selectedSceneTargetIndex = Mathf.Clamp(_sceneTargetDropdown.index, 0, Mathf.Max(0, _sceneTargets.Count - 1));
            });
            box.Add(_sceneTargetDropdown);

            _btnSelectSceneTarget = new Button(SelectSceneTargetInPlayMode) { text = "선택 캐릭터를 Target으로 지정" };
            box.Add(_btnSelectSceneTarget);

            box.Add(new HelpBox(
                "- Play Mode에서만 동작합니다.\n" +
                "- Target은 스킬 실행 컨텍스트(lockedTarget/groundPoint/forward)에 사용됩니다.\n" +
                "- '수동 Target 해제'를 누르면 기존 기본 정책(캐스터가 Player면 몬스터 우선, 몬스터면 Player 고정)으로 돌아갑니다.",
                HelpBoxMessageType.Info));

            RefreshSceneTargetDropdown();
            return box;
        }

        private void LoadSkills()
        {
            _skillListSource = SkillAuthoringRepository.LoadSkills(_selectedTableKind, _forceReload != null && _forceReload.value);
            _state.TableKind = _selectedTableKind;
            _state.SkillListSource = _skillListSource;

            if (_selectedSkill != null)
            {
                int keepUid = _selectedSkill.Uid;
                _selectedSkill = _skillListSource.FirstOrDefault(s => s != null && s.Uid == keepUid);
            }

            UpdateSelectedSkillLabel();
            RefreshMonsterDropdown();
        }

        private void OpenSkillSearchDropdown()
        {
            // 데이터가 아직 없으면 먼저 로드
            if (_skillListSource == null || _skillListSource.Count == 0)
                LoadSkills();

            if (_skillListSource == null || _skillListSource.Count == 0)
            {
                ShowNotification(new GUIContent($"{_selectedTableKind} 스킬 테이블이 비어있습니다."));
                return;
            }

            var options = new System.Collections.Generic.List<SearchableDropdownUtility.Option<SkillAuthoringModel>>(_skillListSource.Count);
            for (int i = 0; i < _skillListSource.Count; i++)
            {
                var s = _skillListSource[i];
                if (s == null) continue;
                // Key: UID, Value: 메모/이름
                string key = s.Uid.ToString();
                string value = string.IsNullOrEmpty(s.Memo) ? s.Name : s.Memo;
                options.Add(new SearchableDropdownUtility.Option<SkillAuthoringModel>(key, value, s));
            }

            int selectedIndex = -1;
            if (_selectedSkill != null)
                selectedIndex = options.FindIndex(o => o.Data != null && o.Data.Uid == _selectedSkill.Uid);

            var rect = SearchableDropdownUtility.GetScreenRect(this, _btnSelectSkill);
            SearchableDropdownUtility.ShowUiToolkit(
                owner: this,
                activatorRectScreen: rect,
                options: options,
                selectedIndex: selectedIndex,
                onSelected: (idx, opt) =>
                {
                    _selectedSkill = opt.Data;
                    RefreshSelected();
                },
                defaultSearchMode: SearchableDropdownUtility.SearchMode.Both);
        }

        private void UpdateSelectedSkillLabel()
        {
            if (_labelSelectedSkill == null)
                return;

            _labelSelectedSkill.text = _selectedSkill == null
                ? "선택된 스킬: (없음)"
                : $"선택된 스킬: {_selectedSkill.Uid} - {(string.IsNullOrEmpty(_selectedSkill.Memo) ? _selectedSkill.Name : _selectedSkill.Memo)}";
        }

        private void RefreshSelected()
        {
            UpdateSelectedSkillLabel();

            if (_selectedSkill == null)
            {
                _cachedSkillOriginal = null;
                _editingSkill = null;
                _editingDirty = false;
                SetEditUIEnabled(false);
                SetEditUIValues(null, withoutNotify: true);
                UpdateEditButtonsState();
                return;
            }

            _cachedSkillOriginal = CloneSkillRow(_selectedSkill);
            _editingSkill = CloneSkillRow(_selectedSkill);
            _editingDirty = false;
            _state.SetSelection(_selectedSkill);

            SetEditUIEnabled(true);
            SetEditUIValues(_editingSkill, withoutNotify: true);
            UpdateEditButtonsState();
        }

        private void BuildEditFields()
        {
            _fUid = new IntegerField("Uid");
            _fUid.SetEnabled(false);
            _editRoot.Add(_fUid);

            _fName = new TextField("Name");
            _fName.tooltip = "skill.txt의 Name 컬럼 값입니다. (런타임에서 로컬라이즈로 덮어쓸 수 있습니다.)";
            _editRoot.Add(_fName);

            _fMemo = new TextField("Memo");
            _editRoot.Add(_fMemo);

            _fDefaultLearn = new Toggle("DefaultLearn");
            _editRoot.Add(_fDefaultLearn);

            _fNeedPlayerLevel = new IntegerField("NeedPlayerLevel");
            _editRoot.Add(_fNeedPlayerLevel);

            _fIconFileName = new TextField("IconFileName");
            _editRoot.Add(_fIconFileName);

            _fCastTime = new FloatField("CastTime");
            _editRoot.Add(_fCastTime);

            _fCoolTime = new FloatField("CoolTime");
            _editRoot.Add(_fCoolTime);

            _fTargetingMode = new EnumField("TargetingMode", ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit);
            _editRoot.Add(_fTargetingMode);

            _fRange = new FloatField("Range");
            _editRoot.Add(_fRange);

            _fMaxTargets = new IntegerField("MaxTargets");
            _editRoot.Add(_fMaxTargets);

            _fDefaultAreaId = new TextField("DefaultAreaId");
            _fDefaultAreaId.SetEnabled(false);
            _fDefaultAreaId.style.display = DisplayStyle.None;
            _editRoot.Add(_fDefaultAreaId);

            _fCastStartClip = new TextField("CastStartClip");
            _editRoot.Add(_fCastStartClip);

            _fCastLoopClip = new TextField("CastLoopClip");
            _editRoot.Add(_fCastLoopClip);

            _fCastEndClip = new TextField("CastEndClip");
            _editRoot.Add(_fCastEndClip);

            _fUseClip = new TextField("UseClip");
            _editRoot.Add(_fUseClip);

            RegisterDirtyTracking(_fName, (v) => _editingSkill.Name = v);
            
            RegisterDirtyTracking(_fMemo, (v) => _editingSkill.Memo = v);
            _fDefaultLearn.RegisterValueChangedCallback(evt =>
            {
                if (_editingSkill == null) return;
                _editingSkill.DefaultLearn = evt.newValue;
                MarkDirty();
            });
            RegisterDirtyTracking(_fNeedPlayerLevel, (v) => _editingSkill.NeedPlayerLevel = v);
            RegisterDirtyTracking(_fIconFileName, (v) => _editingSkill.IconFileName = v);
            RegisterDirtyTracking(_fCastTime, (v) => _editingSkill.CastTime = v);
            RegisterDirtyTracking(_fCoolTime, (v) => _editingSkill.CoolTime = v);
            _fTargetingMode.RegisterValueChangedCallback(evt =>
            {
                if (_editingSkill == null) return;
                _editingSkill.TargetingMode = (ConfigCommonSkill.SkillTargetingMode)evt.newValue;
                MarkDirty();
            });
            RegisterDirtyTracking(_fRange, (v) => _editingSkill.Range = v);
            RegisterDirtyTracking(_fMaxTargets, (v) => _editingSkill.MaxTargets = v);
            RegisterDirtyTracking(_fCastStartClip, (v) => _editingSkill.CastStartClip = v);
            RegisterDirtyTracking(_fCastLoopClip, (v) => _editingSkill.CastLoopClip = v);
            RegisterDirtyTracking(_fCastEndClip, (v) => _editingSkill.CastEndClip = v);
            RegisterDirtyTracking(_fUseClip, (v) => _editingSkill.UseClip = v);
        }

        private void RegisterDirtyTracking(TextField field, Action<string> assign)
        {
            field.RegisterValueChangedCallback(evt =>
            {
                if (_editingSkill == null) return;
                assign?.Invoke(evt.newValue);
                MarkDirty();
            });
        }

        private void RegisterDirtyTracking(FloatField field, Action<float> assign)
        {
            field.RegisterValueChangedCallback(evt =>
            {
                if (_editingSkill == null) return;
                assign?.Invoke(evt.newValue);
                MarkDirty();
            });
        }

        private void RegisterDirtyTracking(IntegerField field, Action<int> assign)
        {
            field.RegisterValueChangedCallback(evt =>
            {
                if (_editingSkill == null) return;
                assign?.Invoke(evt.newValue);
                MarkDirty();
            });
        }

        private void MarkDirty()
        {
            _editingDirty = true;
            _state.IsDirty = true;
            _state.Editing = _editingSkill;
            UpdateEditButtonsState();
        }

        private void SetEditUIEnabled(bool enabled)
        {
            _timelineField.SetEnabled(enabled);
            _bakeButton.SetEnabled(enabled);

            bool showPlayerFields = enabled && _selectedTableKind == SkillAuthoringTableKind.Player;
            _fName.style.display = showPlayerFields ? DisplayStyle.Flex : DisplayStyle.None;
            _fName.SetEnabled(showPlayerFields);
            _fMemo.SetEnabled(enabled);
            _fDefaultLearn.style.display = showPlayerFields ? DisplayStyle.Flex : DisplayStyle.None;
            _fNeedPlayerLevel.style.display = showPlayerFields ? DisplayStyle.Flex : DisplayStyle.None;
            _fIconFileName.style.display = showPlayerFields ? DisplayStyle.Flex : DisplayStyle.None;
            _fDefaultLearn.SetEnabled(showPlayerFields);
            _fNeedPlayerLevel.SetEnabled(showPlayerFields);
            _fIconFileName.SetEnabled(showPlayerFields);
            _fCastTime.SetEnabled(enabled);
            _fCoolTime.SetEnabled(enabled);
            _fTargetingMode.SetEnabled(enabled);
            _fRange.SetEnabled(enabled);
            _fMaxTargets.SetEnabled(enabled);
            _fDefaultAreaId.SetEnabled(enabled);
            _fCastStartClip.SetEnabled(enabled);
            _fCastLoopClip.SetEnabled(enabled);
            _fCastEndClip.SetEnabled(enabled);
            _fUseClip.SetEnabled(enabled);

            UpdateEditButtonsState();
        }

        private void UpdateEditButtonsState()
        {
            bool hasSelection = _selectedSkill != null;
            bool canRevert = hasSelection && _editingDirty;

            _btnRevert?.SetEnabled(canRevert);
            _btnApplyTest?.SetEnabled(hasSelection && _editingDirty);
            _btnSaveTable?.SetEnabled(hasSelection && _editingDirty);

            UpdatePlayModeButtonsState();
        }

        private void UpdatePlayModeButtonsState()
        {
            SkillPlayModeTesterService.UpdatePlayModeUiState(
                isPlaying: Application.isPlaying,
                hasSelection: _selectedSkill != null,
                playModeHelp: _playModeHelp,
                useSkillButton: _btnUseSkill,
                spawnMonsterButton: _btnSpawnMonster);

            // PlayMode 진입/종료 시 몬스터 드롭다운 갱신
            if (_lastPlayModeState != Application.isPlaying)
            {
                _lastPlayModeState = Application.isPlaying;
                RefreshMonsterDropdown();
            }
        }

        private void UseSkillInPlayMode()
        {
            if (!SkillPlayModeTesterService.ValidatePlayModeAndSelection(Title, _selectedSkill))
            {
                return;
            }

            // 입력값이 바뀐 상태라면, 테스트 적용(테이블 오버라이드)까지 포함해서 즉시 반영한다.
            if (_editingDirty)
            {
                ApplyEditingToSelectedRow();
                SkillAuthoringRepository.UpdateInGameSkillTableInfo(_editingSkill);
            }

            // Timeline이 지정되어 있으면, 메모리에서 Bake한 시퀀스를 Repository에 주입하여
            // SkillExecutor가 Addressables 로딩 없이 실행하도록 한다.
            TryRegisterEditorSequenceOverrideFromTimeline(_selectedSkill.Uid);

            if (!TryGetPlayModeCasterAndTarget(out var caster, out var target, out string error))
            {
                EditorUtility.DisplayDialog(Title, error, "OK");
                return;
            }

            // Skill 시스템이 붙어있는 캐스터에서 실행
            // Unity의 GetComponent<T>()는 interface를 직접 받을 수 없으므로, Component 스캔으로 찾는다.
            GGemCo2DCore.IMonsterSkillDriver driver = null;
            var comps = caster.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] is GGemCo2DCore.IMonsterSkillDriver d)
                {
                    driver = d;
                    break;
                }
            }

            if (driver == null)
            {
                // 최후: SkillExecutor 직접 실행
                var executor = caster.GetComponent<SkillExecutor>();
                if (executor == null)
                {
                    EditorUtility.DisplayDialog(Title, "캐스터에 SkillExecutor(또는 MonsterSkillDriverAdapter)가 없습니다.", "OK");
                    return;
                }

                var ctx = new SkillTargetContext(
                    caster: caster,
                    lockedTarget: target.LockedTarget != null ? target.LockedTarget.gameObject : null,
                    groundPoint: target.GroundPoint,
                    forward: new Vector3(target.Forward.x, target.Forward.y, 0f));

                bool started = executor.TryUse(_selectedSkill.Uid, ctx, preferMonsterTable: _selectedTableKind == SkillAuthoringTableKind.Monster);
                if (!started)
                    ShowNotification(new GUIContent("스킬 실행 실패(진행 중이거나 테이블/시퀀스 누락)"));
                else
                    ShowNotification(new GUIContent("스킬 실행"));
                return;
            }

            var result = driver.TryUseSkill(_selectedSkill.Uid, target);
            ShowNotification(new GUIContent(result == GGemCo2DCore.SkillUseResult.Started ? "스킬 실행" : "스킬 실행 실패"));
        }

        private bool TryGetPlayModeCasterAndTarget(out GameObject caster, out GGemCo2DCore.MonsterSkillTarget target, out string error)
        {
            caster = null;
            target = default;
            error = null;

            var hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : UnityEngine.Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub == null)
            {
                error = "SkillTestRuntimeHub를 찾지 못했습니다. (Play Mode 진입 시 자동 생성되어야 합니다.)";
                return false;
            }

            caster = hub.SelectedMonster;
            if (caster == null)
            {
                error = "스킬을 사용할 캐스터가 선택되지 않았습니다. '선택 몬스터 소환' 또는 '씬 Caster 선택'을 사용하세요.";
                return false;
            }


            // TargetingMode가 Self이면 타겟은 캐스터 자신으로 고정한다.
            // (이 경우 수동 Target 지정/기본 정책 타겟 결정은 무시한다.)
            var targetingMode = _editingSkill != null
                ? _editingSkill.TargetingMode
                : (_selectedSkill != null ? _selectedSkill.TargetingMode : default);

            if (targetingMode == ConfigCommonSkill.SkillTargetingMode.Self)
            {
                var self = caster.transform;
                var p = self.position;
                var forwardSelf = (Vector2)caster.transform.right;
                if (forwardSelf.sqrMagnitude < 1e-6f) forwardSelf = Vector2.right;

                hub.SetGroundPoint(p);
                hub.SetLockedTarget(self);
                hub.SetForward(forwardSelf);

                target = new GGemCo2DCore.MonsterSkillTarget(self, p, forwardSelf);
                return true;
            }

            // 타겟 결정 우선순위
            // 1) 툴에서 수동 지정한 Target
            // 2) 기본 정책
            //    - 캐스터가 Player이면: 타겟은 '스폰/선택된 다른 캐릭터(몬스터)' 우선
            //    - 그 외(몬스터 등)이면: 타겟은 Player 고정
            Transform lockedTarget = null;

            if (hub.UseManualLockedTarget && hub.LockedTarget != null)
            {
                // 캐스터/타겟 동일이면(자기 자신) 의도치 않은 케이스가 많아 경고 후 기본 정책으로 폴백합니다.
                if (hub.LockedTarget.gameObject != caster)
                {
                    lockedTarget = hub.LockedTarget;
                }
            }

            if (SceneGame.Instance != null && SceneGame.Instance.player != null && caster == SceneGame.Instance.player.gameObject)
            {
                if (lockedTarget == null)
                {
                    // Player 캐스터: Spawned 중 자신이 아닌 첫 대상을 타겟으로 사용
                for (int i = 0; i < hub.Spawned.Count; i++)
                {
                    var go = hub.Spawned[i];
                    if (go == null || go == caster) continue;
                    lockedTarget = go.transform;
                    break;
                }
                }

                if (lockedTarget == null)
                {
                    error = "Player를 캐스터로 선택했지만, 타겟을 찾지 못했습니다.\n" +
                            "- 1) '씬 Target 선택'에서 타겟을 수동 지정하거나\n" +
                            "- 2) 먼저 몬스터를 소환해서 기본 정책 타겟을 만들고\n" +
                            "다시 시도하세요.";
                    return false;
                }
            }
            else
            {
                if (lockedTarget == null)
                {
                    // 몬스터 캐스터: Player 타겟
                    if (!hub.TryBindPlayerAsTarget() || hub.LockedTarget == null)
                    {
                        error = "Player를 찾지 못했습니다. SceneGame.player 또는 Tag=Player 오브젝트가 필요합니다.";
                        return false;
                    }

                    lockedTarget = hub.LockedTarget;
                }
            }

            var groundPoint = lockedTarget.position;
            var d = lockedTarget.position - caster.transform.position;
            var forward = new Vector2(d.x, d.y);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector2.right;

            hub.SetGroundPoint(groundPoint);
            // 수동 타겟이 설정된 상태라면, hub 내부 상태도 유지
            if (hub.UseManualLockedTarget)
                hub.SetManualLockedTarget(lockedTarget);
            else
                hub.SetLockedTarget(lockedTarget);
            hub.SetForward(forward);

            target = new GGemCo2DCore.MonsterSkillTarget(lockedTarget, groundPoint, forward);
            return true;
        }

        private void RefreshSceneCasterDropdown()
        {
            _sceneCasters.Clear();
            _sceneCasterNames.Clear();

            if (!Application.isPlaying)
            {
                // UI Toolkit DropdownField는 choices 변경 시 인덱스가 어긋날 수 있으므로 항상 Notify 없이 갱신
                if (_sceneCasterDropdown != null)
                    _sceneCasterDropdown.choices = _sceneCasterNames;
                return;
            }

#if UNITY_2023_1_OR_NEWER
            var chars = UnityEngine.Object.FindObjectsByType<CharacterBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var chars = FindObjectsOfType<CharacterBase>(includeInactive: false);
#endif
            for (int i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c == null) continue;
                _sceneCasters.Add(c);
                _sceneCasterNames.Add(c.name);
            }

            if (_sceneCasterDropdown != null)
            {
                _sceneCasterDropdown.choices = _sceneCasterNames;
                _selectedSceneCasterIndex = Mathf.Clamp(_selectedSceneCasterIndex, 0, Mathf.Max(0, _sceneCasters.Count - 1));
                _sceneCasterDropdown.SetValueWithoutNotify(_sceneCasterNames.Count > 0 ? _sceneCasterNames[_selectedSceneCasterIndex] : string.Empty);
            }
        }

        private void SelectCasterFromCurrentSelection()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return;
            }

            var go = Selection.activeGameObject;
            if (go == null)
            {
                ShowNotification(new GUIContent("선택된 오브젝트가 없습니다."));
                return;
            }

            var c = go.GetComponentInParent<CharacterBase>();
            if (c == null) c = go.GetComponentInChildren<CharacterBase>();
            if (c == null)
            {
                ShowNotification(new GUIContent("선택된 오브젝트에서 CharacterBase를 찾지 못했습니다."));
                return;
            }

            SelectCasterInPlayMode(c.gameObject);
        }

        private void RefreshSceneTargetDropdown()
        {
            _sceneTargets.Clear();
            _sceneTargetNames.Clear();

            if (!Application.isPlaying)
            {
                if (_sceneTargetDropdown != null)
                    _sceneTargetDropdown.choices = _sceneTargetNames;
                return;
            }

#if UNITY_2023_1_OR_NEWER
            var chars = UnityEngine.Object.FindObjectsByType<CharacterBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var chars = FindObjectsOfType<CharacterBase>(includeInactive: false);
#endif
            for (int i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c == null) continue;
                _sceneTargets.Add(c);
                _sceneTargetNames.Add(c.name);
            }

            if (_sceneTargetDropdown != null)
            {
                _sceneTargetDropdown.choices = _sceneTargetNames;
                _selectedSceneTargetIndex = Mathf.Clamp(_selectedSceneTargetIndex, 0, Mathf.Max(0, _sceneTargets.Count - 1));
                _sceneTargetDropdown.SetValueWithoutNotify(_sceneTargetNames.Count > 0 ? _sceneTargetNames[_selectedSceneTargetIndex] : string.Empty);
            }
        }

        private void SelectTargetFromCurrentSelection()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return;
            }

            var go = Selection.activeGameObject;
            if (go == null)
            {
                ShowNotification(new GUIContent("선택된 오브젝트가 없습니다."));
                return;
            }

            var c = go.GetComponentInParent<CharacterBase>();
            if (c == null) c = go.GetComponentInChildren<CharacterBase>();
            if (c == null)
            {
                ShowNotification(new GUIContent("선택된 오브젝트에서 CharacterBase를 찾지 못했습니다."));
                return;
            }

            var hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : UnityEngine.Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub == null)
            {
                EditorUtility.DisplayDialog(Title, "SkillTestRuntimeHub를 찾지 못했습니다.", "OK");
                return;
            }

            hub.SetManualLockedTarget(c.transform);
            ShowNotification(new GUIContent($"Target 지정: {c.name}"));

            // UI 드롭다운도 동기화
            RefreshSceneTargetDropdown();
            _selectedSceneTargetIndex = _sceneTargets.IndexOf(c);
            if (_selectedSceneTargetIndex < 0) _selectedSceneTargetIndex = 0;
            if (_sceneTargetDropdown != null && _sceneTargetNames.Count > 0)
                _sceneTargetDropdown.SetValueWithoutNotify(_sceneTargetNames[_selectedSceneTargetIndex]);
        }

        private void SelectSceneTargetInPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return;
            }

            if (_sceneTargets.Count <= 0)
            {
                ShowNotification(new GUIContent("씬에 캐릭터(CharacterBase)가 없습니다."));
                return;
            }

            _selectedSceneTargetIndex = Mathf.Clamp(_selectedSceneTargetIndex, 0, _sceneTargets.Count - 1);
            var c = _sceneTargets[_selectedSceneTargetIndex];
            if (c == null)
            {
                ShowNotification(new GUIContent("선택 Target이 null 입니다."));
                return;
            }

            var hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : UnityEngine.Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub == null)
            {
                EditorUtility.DisplayDialog(Title, "SkillTestRuntimeHub를 찾지 못했습니다.", "OK");
                return;
            }

            hub.SetManualLockedTarget(c.transform);
            ShowNotification(new GUIContent($"Target 지정: {c.name}"));
        }

        private void ClearManualTargetInPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return;
            }

            var hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : UnityEngine.Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub == null)
            {
                EditorUtility.DisplayDialog(Title, "SkillTestRuntimeHub를 찾지 못했습니다.", "OK");
                return;
            }

            hub.ClearManualLockedTarget();
            ShowNotification(new GUIContent("수동 Target 해제"));
        }

        private void SelectSceneCasterInPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return;
            }

            if (_sceneCasters.Count <= 0)
            {
                EditorUtility.DisplayDialog(Title, "씬에서 CharacterBase를 찾지 못했습니다. '씬 캐릭터 목록 새로고침'을 먼저 실행하세요.", "OK");
                return;
            }

            _selectedSceneCasterIndex = Mathf.Clamp(_sceneCasterDropdown.index, 0, _sceneCasters.Count - 1);
            var c = _sceneCasters[_selectedSceneCasterIndex];
            if (c == null)
            {
                ShowNotification(new GUIContent("유효하지 않은 캐릭터입니다."));
                return;
            }

            SelectCasterInPlayMode(c.gameObject);
        }

        private void SelectCasterInPlayMode(GameObject caster)
        {
            var hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : UnityEngine.Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub == null)
            {
                EditorUtility.DisplayDialog(Title, "SkillTestRuntimeHub를 찾지 못했습니다. (Play Mode 진입 시 자동 생성되어야 합니다.)", "OK");
                return;
            }

            hub.SelectCaster(caster, captureSnapshot: false);
            hub.AutoResetSelectedMonsterAfterSkill = _toggleAutoResetMonster != null && _toggleAutoResetMonster.value;
            ShowNotification(new GUIContent($"Caster 지정: {caster.name}"));
        }

        private async void SpawnSelectedMonsterInPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return;
            }

            var hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : UnityEngine.Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub == null)
            {
                EditorUtility.DisplayDialog(Title, "SkillTestRuntimeHub를 찾지 못했습니다. (Play Mode 진입 시 자동 생성되어야 합니다.)", "OK");
                return;
            }

            if (_monsterUids.Count <= 0)
            {
                EditorUtility.DisplayDialog(Title, "monster 테이블을 찾지 못했습니다. (TableLoaderManager가 초기화되어야 합니다.)", "OK");
                return;
            }

            _selectedMonsterIndex = Mathf.Clamp(_monsterDropdown.index, 0, _monsterUids.Count - 1);
            int uid = _monsterUids[_selectedMonsterIndex];
            if (uid <= 0)
            {
                EditorUtility.DisplayDialog(Title, "유효하지 않은 MonsterUid 입니다.", "OK");
                return;
            }

            StruckTableMonster struckTableMonster = TableLoaderManager.Instance.GetMonsterData(uid);
            StruckTableAnimation struckTableAnimation = TableLoaderManager.Instance.GetAnimationData(struckTableMonster.AnimationUid);

            try
            {
                await hub.SpawnMonster(uid, struckTableMonster, struckTableAnimation);
                hub.AutoResetSelectedMonsterAfterSkill = _toggleAutoResetMonster != null && _toggleAutoResetMonster.value;
                ShowNotification(new GUIContent($"몬스터 소환: {uid}"));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog(Title, "몬스터 소환 중 예외가 발생했습니다. Console을 확인하세요.", "OK");
            }
        }

        
        private void CaptureSelectedMonsterOriginInPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return;
            }

            var hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : UnityEngine.Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub == null)
            {
                EditorUtility.DisplayDialog(Title, "SkillTestRuntimeHub를 찾지 못했습니다.", "OK");
                return;
            }

            if (hub.SelectedMonster == null)
            {
                EditorUtility.DisplayDialog(Title, "선택된 몬스터가 없습니다. 먼저 '선택 몬스터 소환'을 실행하세요.", "OK");
                return;
            }

            hub.CaptureSnapshot(hub.SelectedMonster);
            ShowNotification(new GUIContent("원본 위치 저장"));
        }

        private void ResetSelectedMonsterOriginInPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return;
            }

            var hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : UnityEngine.Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub == null)
            {
                EditorUtility.DisplayDialog(Title, "SkillTestRuntimeHub를 찾지 못했습니다.", "OK");
                return;
            }

            if (!hub.ResetSelectedMonsterToSnapshot())
            {
                EditorUtility.DisplayDialog(Title, "원본 스냅샷이 없거나, 몬스터가 선택되지 않았습니다.", "OK");
                return;
            }

            ShowNotification(new GUIContent("원본 위치로 복원"));
        }
        private void RefreshMonsterDropdown()
        {
            _monsterNames.Clear();
            _monsterUids.Clear();

            try
            {
                if (GGemCo2DCore.TableLoaderManager.Instance != null && GGemCo2DCore.TableLoaderManager.Instance.TableMonster != null)
                {
                    foreach (var kv in GGemCo2DCore.TableLoaderManager.Instance.TableMonster.GetDatas())
                    {
                        var row = kv.Value;
                        if (row == null) continue;
                        _monsterUids.Add(row.Uid);
                        _monsterNames.Add($"{row.Uid} - {row.Name}");
                    }
                }
            }
            catch
            {
                // ignore
            }

            if (_monsterNames.Count == 0)
            {
                _monsterNames.Add("(monster table not ready)");
                _monsterUids.Add(0);
                _selectedMonsterIndex = 0;
            }
            else
            {
                // UID 정렬
                var zipped = _monsterUids.Zip(_monsterNames, (uid, name) => new { uid, name })
                    .OrderBy(x => x.uid)
                    .ToList();
                _monsterUids.Clear();
                _monsterNames.Clear();
                foreach (var z in zipped)
                {
                    _monsterUids.Add(z.uid);
                    _monsterNames.Add(z.name);
                }
                _selectedMonsterIndex = Mathf.Clamp(_selectedMonsterIndex, 0, _monsterUids.Count - 1);
            }

            if (_monsterDropdown != null)
            {
                _monsterDropdown.choices = _monsterNames;
                _monsterDropdown.index = _selectedMonsterIndex;
            }
        }

        private void TryRegisterEditorSequenceOverrideFromTimeline(int skillUid)
        {
            var timeline = _timelineField != null ? _timelineField.value as TimelineAsset : null;
            if (timeline == null) return;

            try
            {
                var (events, payloads, dur) = ExtractTimelineEvents(timeline);
                var seq = ScriptableObject.CreateInstance<SkillRuntimeSequence>();
                seq.EditorSetData(skillUid, dur, events, payloads);

                string key = ConfigAddressableKeySkill.GetRuntimeSequenceKey(skillUid);
                AddressableLoaderSkillRuntimeSequence.RegisterEditorOverride(key, seq);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static (SkillRuntimeEvent[] events, UnityEngine.Object[] payloads, float duration) ExtractTimelineEvents(TimelineAsset timeline)
        {
            // PlayMode 테스트(에디터 오버라이드)에서도 정식 Baker와 동일한 로직(EndTime/트랙 재귀/페이로드)을 사용한다.
            return SkillTimelineBaker.BakeToMemory(timeline);
        }

        private static UnityEngine.Object CreateRuntimePayloadFromLegacyClip(SkillEventClipBase evClip)
        {
            switch (evClip.EventType)
            {
                case ConfigCommonSkill.SkillEventType.Damage:
                {
                    if (evClip is not SkillDamageClip c) return null;

                    var def = ScriptableObject.CreateInstance<DamageEventDefinition>();
                    def.multiplier = Mathf.Max(0f, c.Multiplier);
                    def.damageModelId = c.DamageTypeUid > 0 ? $"DamageType_{c.DamageTypeUid}" : "Default";

                    return def;
                }
                case ConfigCommonSkill.SkillEventType.SpawnEffect:
                {
                    if (evClip is not SkillSpawnEffectClip c) return null;

                    var def = ScriptableObject.CreateInstance<EffectEventDefinition>();
                    def.effectUid = c.EffectUid;
                    def.localOffset = new Vector3(c.Offset.x, c.Offset.y, 0f);

                    if (c.Anchor == (int)SkillSpawnEffectClip.AnchorType.Target)
                    {
                        def.attachToTarget = true;
                        def.targetingOverride.enabled = true;
                        def.targetingOverride.mode = ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit;
                    }
                    else if (c.Anchor == (int)SkillSpawnEffectClip.AnchorType.Ground)
                    {
                        def.attachToTarget = false;
                        def.targetingOverride.enabled = true;
                        def.targetingOverride.mode = ConfigCommonSkill.SkillTargetingMode.GroundTarget;
                    }
                    else
                    {
                        def.attachToTarget = false;
                        def.targetingOverride.enabled = false;
                    }

                    return def;
                }
                case ConfigCommonSkill.SkillEventType.ApplyAffect:
                {
                    if (evClip is not SkillApplyAffectClip c) return null;

                    var def = ScriptableObject.CreateInstance<ApplyStatusEventDefinition>();
                    def.statusId = new StatusEffectId { id = c.AffectUid.ToString() };
                    def.stacks = 1;
                    def.durationOverrideSeconds = c.AffectDuration;
                    def.chance01 = 1f;
                    TrySetApplyStatusApplyTo(def, (int)c.ApplyTo);
                    return def;
                }
                case ConfigCommonSkill.SkillEventType.Lunge:
                {
                    if (evClip is not SkillLungeClip c) return null;

                    var def = ScriptableObject.CreateInstance<LungeEventDefinition>();
                    def.distance = Mathf.Max(0f, c.Distance);
                    def.durationOverrideSeconds = c.DurationOverrideSeconds;
                    def.easing = c.Easing;

                    def.invertForward = c.InvertForward;

                    def.useArcMotion = c.UseArcMotion;
                    def.arcHeight = Mathf.Max(0f, c.ArcHeight);

                    def.stopAtEnd = c.StopAtEnd;
                    def.useMovePosition = c.UseMovePosition;
                    def.useSnapshotForward = c.UseSnapshotForward;

                    def.allowReplace = c.AllowReplace;
                    return def;
                }
                case ConfigCommonSkill.SkillEventType.Projectile:
                {
                    if (evClip is not SkillProjectileClip c) return null;

                    var def = ScriptableObject.CreateInstance<ProjectileEventDefinition>();
                    def.projectileUid = c.ProjectileUid;
                    def.damageType = c.DamageType;
                    def.damage = c.Damage;
                    def.speedMultiplier = c.SpeedMultiplier;
                    def.scaleMultiplier = c.ScaleMultiplier;
                    def.visualType = c.VisualType;
                    def.visualSprite = c.VisualSprite;
                    def.visualAnimatorController = c.VisualAnimatorController;
                    def.visualEffectUidOverride = c.VisualEffectUidOverride;
                    def.targetingOverride = c.TargetingOverride;
                    return def;
                }

                default:
                    return null;
            }
        }

        private void SetEditUIValues(SkillAuthoringModel s, bool withoutNotify)
        {
            if (withoutNotify)
            {
                _fUid.SetValueWithoutNotify(s?.Uid ?? 0);
                _fName.SetValueWithoutNotify(s?.Name ?? string.Empty);
                _fMemo.SetValueWithoutNotify(s?.Memo ?? string.Empty);
                _fDefaultLearn.SetValueWithoutNotify(s?.DefaultLearn ?? false);
                _fNeedPlayerLevel.SetValueWithoutNotify(s?.NeedPlayerLevel ?? 0);
                _fIconFileName.SetValueWithoutNotify(s?.IconFileName ?? string.Empty);
                _fCastTime.SetValueWithoutNotify(s?.CastTime ?? 0f);
                _fCoolTime.SetValueWithoutNotify(s?.CoolTime ?? 0f);
                _fTargetingMode.SetValueWithoutNotify(s != null ? (Enum)s.TargetingMode : ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit);
                _fRange.SetValueWithoutNotify(s?.Range ?? 0f);
                _fMaxTargets.SetValueWithoutNotify(s?.MaxTargets ?? 0);
                _fCastStartClip.SetValueWithoutNotify(s?.CastStartClip ?? string.Empty);
                _fCastLoopClip.SetValueWithoutNotify(s?.CastLoopClip ?? string.Empty);
                _fCastEndClip.SetValueWithoutNotify(s?.CastEndClip ?? string.Empty);
                _fUseClip.SetValueWithoutNotify(s?.UseClip ?? string.Empty);
                return;
            }

            _fUid.value = s?.Uid ?? 0;
            _fName.value = s?.Name ?? string.Empty;
            _fMemo.value = s?.Memo ?? string.Empty;
            _fDefaultLearn.value = s?.DefaultLearn ?? false;
            _fNeedPlayerLevel.value = s?.NeedPlayerLevel ?? 0;
            _fIconFileName.value = s?.IconFileName ?? string.Empty;
            _fCastTime.value = s?.CastTime ?? 0f;
            _fCoolTime.value = s?.CoolTime ?? 0f;
            _fTargetingMode.value = s != null ? (Enum)s.TargetingMode : ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit;
            _fRange.value = s?.Range ?? 0f;
            _fMaxTargets.value = s?.MaxTargets ?? 0;
            _fCastStartClip.value = s?.CastStartClip ?? string.Empty;
            _fCastLoopClip.value = s?.CastLoopClip ?? string.Empty;
            _fCastEndClip.value = s?.CastEndClip ?? string.Empty;
            _fUseClip.value = s?.UseClip ?? string.Empty;
        }

        private void RevertEdits()
        {
            if (_selectedSkill == null || _cachedSkillOriginal == null) return;
            _editingSkill = CloneSkillRow(_cachedSkillOriginal);
            _editingDirty = false;
            _state.Editing = _editingSkill;
            _state.IsDirty = false;
            SetEditUIValues(_editingSkill, withoutNotify: true);
            UpdateEditButtonsState();
            ShowNotification(new GUIContent("되돌리기 완료"));
        }

        private void ApplyTestEdits()
        {
            if (!ApplyEditingToSelectedRow())
                return;

            // 플레이 모드에서는 런타임 TableLoaderManagerSkill에도 적용
            SkillAuthoringRepository.UpdateInGameSkillTableInfo(_editingSkill);

            _editingDirty = false;
            _state.IsDirty = false;
            _state.Editing = _editingSkill;
            UpdateEditButtonsState();
            ShowNotification(new GUIContent("테스트 적용 완료"));
        }

        private void SaveEditsToSkillTxt()
        {
            if (!ApplyEditingToSelectedRow())
                return;

            if (!SkillAuthoringRepository.Save(_selectedTableKind, _skillListSource, out var err))
            {
                EditorUtility.DisplayDialog(Title, err, "OK");
                return;
            }

            int keepUid = _selectedSkill.Uid;
            LoadSkills();

            _selectedSkill = _skillListSource != null
                ? _skillListSource.FirstOrDefault(s => s != null && s.Uid == keepUid)
                : null;
            RefreshSelected();

            SkillAuthoringRepository.UpdateInGameSkillTableInfo(_editingSkill);

            _editingDirty = false;
            UpdateEditButtonsState();
            ShowNotification(new GUIContent($"{_selectedTableKind} 저장 완료"));
        }

        private bool ApplyEditingToSelectedRow()
        {
            if (_selectedSkill == null || _editingSkill == null)
                return false;

            // Uid는 키이므로 편집하지 않습니다.
            _selectedSkill.Name = _editingSkill.Name;
            _selectedSkill.Memo = _editingSkill.Memo;
            _selectedSkill.DefaultLearn = _editingSkill.DefaultLearn;
            _selectedSkill.NeedPlayerLevel = _editingSkill.NeedPlayerLevel;
            _selectedSkill.IconFileName = _editingSkill.IconFileName;
            _selectedSkill.SoFileName = _editingSkill.SoFileName;
            _selectedSkill.CastTime = _editingSkill.CastTime;
            _selectedSkill.CoolTime = _editingSkill.CoolTime;
            _selectedSkill.TargetingMode = _editingSkill.TargetingMode;
            _selectedSkill.Range = _editingSkill.Range;
            _selectedSkill.MaxTargets = _editingSkill.MaxTargets;
            _selectedSkill.CastStartClip = _editingSkill.CastStartClip;
            _selectedSkill.CastLoopClip = _editingSkill.CastLoopClip;
            _selectedSkill.CastEndClip = _editingSkill.CastEndClip;
            _selectedSkill.UseClip = _editingSkill.UseClip;

            return true;
        }

        private static SkillAuthoringModel CloneSkillRow(SkillAuthoringModel row)
        {
            return row?.Clone();
        }

        private void BakeRuntimeSequence()
        {
            if (SkillBakeService.BakeRuntimeSequence(_selectedSkill, _timelineField.value as TimelineAsset, out var message))
            {
                Debug.Log(message);
                return;
            }

            if (!string.IsNullOrEmpty(message))
            {
                Debug.LogWarning(message);
            }
        }
    }
}
