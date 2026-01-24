using System;
using System.IO;
using System.Linq;
using Config;
using GGemCo2DSkill;
using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 제작 툴(V2)
    /// - SSOT: skill 테이블
    /// - Marker 미사용, 이벤트 클립 기반
    /// - Bake 결과는 SkillRuntimeSequence(Addressables)로 저장
    /// </summary>
    public sealed class CreateSkillWindow : EditorWindow
    {
        private const string Title = "Skill Authoring V2";

        // Skill.txt canonical column order (TableSkill 기준)
        private static readonly string[] SkillTableHeaders =
        {
            "Uid","Name","Memo","IconFileName","CastTime","CoolTime","TargetingMode","Range","MaxTargets",
            "CastStartClip","CastLoopClip","CastEndClip","UseClip"
        };

        [MenuItem(ConfigEditorSkill.NameToolSettingTestSkill, false, (int)ConfigEditorSkill.ToolOrdering.SettingTestSkill)]
        public static void Open()
        {
            var w = GetWindow<CreateSkillWindow>();
            w.titleContent = new GUIContent(Title);
            w.minSize = new Vector2(900, 260);
        }

        private ListView _skillList;

        // Editing UI
        private ScrollView _rightScroll;
        private VisualElement _editRoot;

        private IntegerField _fUid;
        private TextField _fName;
        private TextField _fMemo;
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
        private readonly System.Collections.Generic.List<string> _monsterNames = new();
        private readonly System.Collections.Generic.List<int> _monsterUids = new();
        private int _selectedMonsterIndex;

        private Button _btnUseSkill;
        private HelpBox _playModeHelp;

        private ObjectField _timelineField;
        private Button _resolveTimelineByKey;
        private Button _registerTimelineKey;

        private Button _bakeButton;

        private Toggle _forceReload;

        private TableSkill _tableSkill;
        private System.Collections.Generic.List<StruckTableSkill> _skillListSource;

        private StruckTableSkill _selectedSkill;
        private StruckTableSkill _cachedSkillOriginal;
        private StruckTableSkill _editingSkill;
        private bool _editingDirty;

        private bool _lastPlayModeState;

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            var top = new Toolbar();
            _forceReload = new Toggle("ForceReload") { value = false };
            top.Add(_forceReload);

            var reload = new Button(LoadSkills) { text = "Reload" };
            top.Add(reload);
            rootVisualElement.Add(top);

            var body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };

            // Left list
            _skillList = new ListView { selectionType = SelectionType.Single, style = { flexGrow = 1, minWidth = 360 } };
            _skillList.makeItem = () => new Label();
            _skillList.bindItem = (ve, i) =>
            {
                var list = (System.Collections.Generic.List<StruckTableSkill>)_skillList.itemsSource;
                var s = list[i];
                ((Label)ve).text = s != null ? $"{s.Uid} - {s.Memo}" : "(null)";
            };
            _skillList.onSelectionChange += items =>
            {
                _selectedSkill = items.FirstOrDefault() as StruckTableSkill;
                RefreshSelected();
            };
            body.Add(_skillList);

            _rightScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 2,
                    paddingLeft = 10,
                }
            };

            _editRoot = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1,
                }
            };
            _rightScroll.Add(_editRoot);

            _timelineField = new ObjectField("TimelineAsset") { objectType = typeof(TimelineAsset) };
            _editRoot.Add(_timelineField);

            BuildEditFields();

            var editButtons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            _btnRevert = new Button(RevertEdits) { text = "되돌리기", style = { marginRight = 6 } };
            _btnApplyTest = new Button(ApplyTestEdits) { text = "테스트 적용하기", style = { marginRight = 6 } };
            _btnSaveTable = new Button(SaveEditsToSkillTxt) { text = "저장하기(skill.txt)" };
            editButtons.Add(_btnRevert);
            editButtons.Add(_btnApplyTest);
            editButtons.Add(_btnSaveTable);
            _editRoot.Add(editButtons);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _resolveTimelineByKey = new Button(ResolveTimelineByKey) { text = "Resolve Timeline by Key", style = { marginRight = 6 } };
            _registerTimelineKey = new Button(RegisterTimelineKey) { text = "Register Timeline(Key)", style = { marginRight = 6 } };
            row.Add(_resolveTimelineByKey);
            row.Add(_registerTimelineKey);
            _editRoot.Add(row);

            _bakeButton = new Button(BakeRuntimeSequence) { text = "Bake RuntimeSequence + Register Addressables" };
            _editRoot.Add(_bakeButton);

            // PlayMode 테스트 UI
            _playModeHelp = new HelpBox(
                "Play Mode에서만 동작합니다. '스킬 사용하기'는 현재 Input Field 값 + (선택 시) Timeline 이벤트를 사용해 실행합니다.\n" +
                "- TimelineAsset이 지정되어 있으면, 런타임 시퀀스를 메모리에서 Bake하여 Addressables 로딩을 우회합니다.",
                HelpBoxMessageType.Info);
            _editRoot.Add(_playModeHelp);

            BuildPlayModeMonsterUI();

            _btnUseSkill = new Button(UseSkillInPlayMode) { text = "스킬 사용하기(PlayMode)" };
            _editRoot.Add(_btnUseSkill);

            var help = new HelpBox(
                "필수: skill 테이블의 TimelineKey / RuntimeSequenceKey 컬럼을 채워주세요.\n" +
                "- TimelineKey: 제작용 TimelineAsset Addressables Key\n" +
                "- RuntimeSequenceKey: 런타임용 SkillRuntimeSequence Addressables Key\n" +
                "Bake는 Timeline의 이벤트 클립(SkillEventTrack)만 수집합니다.",
                HelpBoxMessageType.Info);
            _editRoot.Add(help);

            body.Add(_rightScroll);
            rootVisualElement.Add(body);

            LoadSkills();
            RefreshSelected();

            _lastPlayModeState = Application.isPlaying;
        }

        private void BuildPlayModeMonsterUI()
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

            _editRoot.Add(box);

            RefreshMonsterDropdown();
        }

        private void LoadSkills()
        {
            _tableSkill = TableLoaderManagerSkill.LoadTableSkill(_forceReload.value);
            var list = new System.Collections.Generic.List<StruckTableSkill>(128);

            if (_tableSkill != null)
            {
                foreach (var kv in _tableSkill.GetDatas())
                {
                    if (kv.Value == null) continue;
                    list.Add(kv.Value);
                }
            }

            list.Sort((a, b) => a.Uid.CompareTo(b.Uid));
            _skillListSource = list;
            _skillList.itemsSource = _skillListSource;
            _skillList.Rebuild();
        }

        private void RefreshSelected()
        {
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
            UpdateEditButtonsState();
        }

        private void SetEditUIEnabled(bool enabled)
        {
            _timelineField.SetEnabled(enabled);
            _resolveTimelineByKey.SetEnabled(enabled);
            _registerTimelineKey.SetEnabled(enabled);
            _bakeButton.SetEnabled(enabled);

            _fName.SetEnabled(enabled);
            _fMemo.SetEnabled(enabled);
            _fIconFileName.SetEnabled(enabled);
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
            bool canUse = Application.isPlaying && _selectedSkill != null;
            _btnUseSkill?.SetEnabled(canUse);

            if (_btnSpawnMonster != null)
                _btnSpawnMonster.SetEnabled(Application.isPlaying);

            // PlayMode 진입/종료 시 몬스터 드롭다운 갱신
            if (_lastPlayModeState != Application.isPlaying)
            {
                _lastPlayModeState = Application.isPlaying;
                RefreshMonsterDropdown();
            }

            if (_playModeHelp != null)
            {
                _playModeHelp.messageType = Application.isPlaying ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            }
        }

        private void UseSkillInPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return;
            }

            if (_selectedSkill == null)
            {
                EditorUtility.DisplayDialog(Title, "스킬을 먼저 선택하세요.", "OK");
                return;
            }

            // 입력값이 바뀐 상태라면, 테스트 적용(테이블 오버라이드)까지 포함해서 즉시 반영한다.
            if (_editingDirty)
            {
                ApplyEditingToSelectedRow();
                UpdateInGameSkillTableInfo(_editingSkill);
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

                bool started = executor.TryUse(_selectedSkill.Uid, ctx);
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
                error = "스킬을 사용할 몬스터가 선택되지 않았습니다. 먼저 '선택 몬스터 소환'을 실행하세요.";
                return false;
            }

            // 타겟은 Player 고정
            if (!hub.TryBindPlayerAsTarget() || hub.LockedTarget == null)
            {
                error = "Player를 찾지 못했습니다. SceneGame.player 또는 Tag=Player 오브젝트가 필요합니다.";
                return false;
            }

            var lockedTarget = hub.LockedTarget;
            var groundPoint = lockedTarget.position;
            var d = lockedTarget.position - caster.transform.position;
            var forward = new Vector2(d.x, d.y);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector2.right;

            hub.SetGroundPoint(groundPoint);
            hub.SetLockedTarget(lockedTarget);
            hub.SetForward(forward);

            target = new GGemCo2DCore.MonsterSkillTarget(lockedTarget, groundPoint, forward);
            return true;
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

            try
            {
                await hub.SpawnMonster(uid);
                ShowNotification(new GUIContent($"몬스터 소환: {uid}"));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog(Title, "몬스터 소환 중 예외가 발생했습니다. Console을 확인하세요.", "OK");
            }
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
                SkillRuntimeSequenceRepository.RegisterEditorOverride(key, seq);
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
                    def.durationOverrideSeconds = c.Duration;
                    def.chance01 = 1f;
                    return def;
                }
                default:
                    return null;
            }
        }

        private void SetEditUIValues(StruckTableSkill s, bool withoutNotify)
        {
            if (withoutNotify)
            {
                _fUid.SetValueWithoutNotify(s?.Uid ?? 0);
                _fName.SetValueWithoutNotify(s?.Name ?? string.Empty);
                _fMemo.SetValueWithoutNotify(s?.Memo ?? string.Empty);
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
            SetEditUIValues(_editingSkill, withoutNotify: true);
            UpdateEditButtonsState();
            ShowNotification(new GUIContent("되돌리기 완료"));
        }

        private void ApplyTestEdits()
        {
            if (!ApplyEditingToSelectedRow())
                return;

            // 플레이 모드에서는 런타임 TableLoaderManagerSkill에도 적용
            UpdateInGameSkillTableInfo(_editingSkill);

            _editingDirty = false;
            UpdateEditButtonsState();
            _skillList?.RefreshItems();
            ShowNotification(new GUIContent("테스트 적용 완료"));
        }

        private void SaveEditsToSkillTxt()
        {
            if (!ApplyEditingToSelectedRow())
                return;

            if (!TrySaveSkillTableFile(out var err))
            {
                EditorUtility.DisplayDialog(Title, err, "OK");
                return;
            }

            // 저장 후 재로드
            int keepUid = _selectedSkill.Uid;

            // Editor 캐시 언로드 → 재로드
            TableLoaderManagerBase.Unload(ConfigAddressableTableSkill.TableSkill.Path);
            LoadSkills();

            // UID로 재선택
            var idx = _skillListSource != null ? _skillListSource.FindIndex(s => s != null && s.Uid == keepUid) : -1;
            if (idx >= 0)
            {
                _skillList.SetSelection(idx);
                _skillList.ScrollToItem(idx);
            }

            // 플레이 모드에서는 런타임 TableLoaderManagerSkill에도 적용
            UpdateInGameSkillTableInfo(_editingSkill);

            _editingDirty = false;
            UpdateEditButtonsState();
            ShowNotification(new GUIContent("skill.txt 저장 완료"));
        }

        private bool ApplyEditingToSelectedRow()
        {
            if (_selectedSkill == null || _editingSkill == null)
                return false;

            // Uid는 키이므로 편집하지 않습니다.
            _selectedSkill.Name = _editingSkill.Name;
            _selectedSkill.Memo = _editingSkill.Memo;
            _selectedSkill.IconFileName = _editingSkill.IconFileName;
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

        private static StruckTableSkill CloneSkillRow(StruckTableSkill row)
        {
            if (row == null) return null;
            return new StruckTableSkill
            {
                Uid = row.Uid,
                Name = row.Name,
                Memo = row.Memo,
                IconFileName = row.IconFileName,
                CastTime = row.CastTime,
                CoolTime = row.CoolTime,
                TargetingMode = row.TargetingMode,
                Range = row.Range,
                MaxTargets = row.MaxTargets,
                CastStartClip = row.CastStartClip,
                CastLoopClip = row.CastLoopClip,
                CastEndClip = row.CastEndClip,
                UseClip = row.UseClip,
            };
        }

        private static string FormatFloat(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private bool TrySaveSkillTableFile(out string error)
        {
            error = null;

            if (_tableSkill == null)
            {
                error = "Skill 테이블이 로드되지 않았습니다.";
                return false;
            }

            try
            {
                var assetPath = ConfigAddressableTableSkill.TableSkill.Path; // Assets/.../Tables/Skill.txt
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var fullPath = Path.Combine(projectRoot ?? string.Empty, assetPath);

                var header = string.Join("\t", SkillTableHeaders);
                var sb = new System.Text.StringBuilder(1024 * 32);
                sb.AppendLine(header);

                var datas = _tableSkill.GetDatas();
                var uids = datas.Keys.ToList();
                uids.Sort();

                foreach (var uid in uids)
                {
                    if (!datas.TryGetValue(uid, out var r) || r == null)
                        continue;

                    sb.Append(r.Uid).Append('\t');
                    sb.Append(r.Name ?? string.Empty).Append('\t');
                    sb.Append(r.Memo ?? string.Empty).Append('\t');
                    sb.Append(r.IconFileName ?? string.Empty).Append('\t');
                    sb.Append(FormatFloat(r.CastTime)).Append('\t');
                    sb.Append(FormatFloat(r.CoolTime)).Append('\t');
                    sb.Append(r.TargetingMode).Append('\t');
                    sb.Append(FormatFloat(r.Range)).Append('\t');
                    sb.Append(r.MaxTargets).Append('\t');
                    sb.Append(r.CastStartClip ?? string.Empty).Append('\t');
                    sb.Append(r.CastLoopClip ?? string.Empty).Append('\t');
                    sb.Append(r.CastEndClip ?? string.Empty).Append('\t');
                    sb.Append(r.UseClip ?? string.Empty);
                    sb.AppendLine();
                }

                File.WriteAllText(fullPath, sb.ToString(), new System.Text.UTF8Encoding(false));

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception e)
            {
                error = $"Skill 테이블 저장 중 오류: {e.Message}";
                return false;
            }
        }

        private static void UpdateInGameSkillTableInfo(StruckTableSkill row)
        {
            if (row == null) return;
            if (!Application.isPlaying) return;
            if (!GGemCo2DSkill.TableLoaderManagerSkill.Instance) return;

            var table = GGemCo2DSkill.TableLoaderManagerSkill.Instance.TableSkill;
            if (table == null) return;

            var datas = table.GetDatas();
            if (datas == null) return;

            if (!datas.TryGetValue(row.Uid, out var info) || info == null)
                return;

            info.Name = row.Name;
            info.Memo = row.Memo;
            info.IconFileName = row.IconFileName;
            info.CastTime = row.CastTime;
            info.CoolTime = row.CoolTime;
            info.TargetingMode = row.TargetingMode;
            info.Range = row.Range;
            info.MaxTargets = row.MaxTargets;
            info.CastStartClip = row.CastStartClip;
            info.CastLoopClip = row.CastLoopClip;
            info.CastEndClip = row.CastEndClip;
            info.UseClip = row.UseClip;
        }

        private void ResolveTimelineByKey()
        {
            if (_selectedSkill == null) return;
            // todo. 정리 필요
            var timelineKey = $"GGemCo_Skill_Timeline_{_selectedSkill.Uid}";
            if (string.IsNullOrEmpty(timelineKey))
            {
                Debug.LogWarning("[SkillAuthoringV2] TimelineKey가 비어 있습니다.");
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[SkillAuthoringV2] AddressableAssetSettings가 없습니다.");
                return;
            }

            // address로 찾기
            var entry = settings.groups
                .SelectMany(g => g.entries)
                .FirstOrDefault(e => string.Equals(e.address, timelineKey, StringComparison.Ordinal));
            if (entry == null)
            {
                Debug.LogWarning($"[SkillAuthoringV2] Addressables에서 TimelineKey를 찾을 수 없습니다. key={timelineKey}");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(entry.AssetPath);
            _timelineField.value = asset;
        }

        // todo. 정리 필요
        private void RegisterTimelineKey()
        {
            if (_selectedSkill == null) return;
            var timelineKey = $"GGemCo_Skill_Timeline_{_selectedSkill.Uid}";
            if (string.IsNullOrEmpty(timelineKey))
            {
                Debug.LogWarning("[SkillAuthoringV2] TimelineKey가 비어 있습니다.");
                return;
            }

            var timeline = _timelineField.value as TimelineAsset;
            if (timeline == null)
            {
                Debug.LogWarning("[SkillAuthoringV2] TimelineAsset을 지정하세요.");
                return;
            }

            // EnsureAddressableEntry(
            //     assetPath: AssetDatabase.GetAssetPath(timeline),
            //     addressKey: _selectedSkill.TimelineKey,
            //     groupName: ConfigAddressableGroupNameSkill.SkillTimeline,
            //     label: ConfigAddressableLabelSkill.SkillTimeline
            // );

            Debug.Log($"[SkillAuthoringV2] Timeline 등록 완료: {timelineKey}");
        }

        private void BakeRuntimeSequence()
        {
            if (_selectedSkill == null) return;

            var runtimeSequenceKey = ConfigAddressableKeySkill.GetRuntimeSequenceKey(_selectedSkill.Uid);
            if (string.IsNullOrEmpty(runtimeSequenceKey))
            {
                Debug.LogWarning("[SkillAuthoringV2] RuntimeSequenceKey가 비어 있습니다.");
                return;
            }

            var timeline = _timelineField.value as TimelineAsset;
            if (timeline == null)
            {
                Debug.LogWarning("[SkillAuthoringV2] TimelineAsset을 지정하세요.");
                return;
            }

            string folder = ConfigAddressablePathSkill.Skill.RuntimeSequences;
            Directory.CreateDirectory(folder);

            string assetPath = $"{folder}/SkillRuntimeSequence_{_selectedSkill.Uid}.asset";
            assetPath = assetPath.Replace('\\', '/');

            var seq = SkillTimelineBaker.BakeOrUpdate(_selectedSkill.Uid, timeline, assetPath);

            EnsureAddressableEntry(
                assetPath: AssetDatabase.GetAssetPath(seq),
                addressKey: runtimeSequenceKey,
                groupName: ConfigAddressableGroupNameSkill.SkillRuntimeSequence,
                label: ConfigAddressableLabelSkill.SkillRuntimeSequence
            );

            Debug.Log($"[SkillAuthoringV2] Bake/등록 완료: uid={_selectedSkill.Uid}, key={runtimeSequenceKey}");
        }

        private static void EnsureAddressableEntry(string assetPath, string addressKey, string groupName, string label)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[SkillAuthoringV2] AddressableAssetSettings가 없습니다.");
                return;
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("[SkillAuthoringV2] assetPath가 비어 있습니다.");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[SkillAuthoringV2] GUID를 찾을 수 없습니다. assetPath={assetPath}");
                return;
            }

            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true, settings.DefaultGroup.Schemas);
            }

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = addressKey;

            if (!string.IsNullOrEmpty(label))
            {
                settings.AddLabel(label, true);
                entry.SetLabel(label, true);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            AssetDatabase.SaveAssets();
        }
    }
}
