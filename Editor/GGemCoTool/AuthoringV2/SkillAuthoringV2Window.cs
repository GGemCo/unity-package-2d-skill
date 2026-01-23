using System;
using System.IO;
using System.Linq;
using GGemCo2DSkill;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace GGemCo2DSkillEditor.AuthoringV2
{
    /// <summary>
    /// 스킬 제작 툴(V2)
    /// - SSOT: skill 테이블
    /// - Marker 미사용, 이벤트 클립 기반
    /// - Bake 결과는 SkillRuntimeSequence(Addressables)로 저장
    /// </summary>
    public sealed class SkillAuthoringV2Window : EditorWindow
    {
        private const string Title = "Skill Authoring V2";

        [MenuItem("GGemCo/Skill/Development/Skill Authoring V2", false, (int)ConfigEditorSkill.ToolOrdering.Development + 10)]
        public static void Open()
        {
            var w = GetWindow<SkillAuthoringV2Window>();
            w.titleContent = new GUIContent(Title);
            w.minSize = new Vector2(900, 260);
        }

        private ListView _skillList;
        private Label _selectedInfo;

        private ObjectField _timelineField;
        private Button _resolveTimelineByKey;
        private Button _registerTimelineKey;

        private Button _bakeButton;

        private Toggle _forceReload;

        private StruckTableSkill _selectedSkill;

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

            var right = new VisualElement { style = { flexGrow = 2, paddingLeft = 10 } };

            _selectedInfo = new Label("Select a skill row.");
            right.Add(_selectedInfo);

            _timelineField = new ObjectField("TimelineAsset") { objectType = typeof(TimelineAsset) };
            right.Add(_timelineField);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _resolveTimelineByKey = new Button(ResolveTimelineByKey) { text = "Resolve Timeline by Key", style = { marginRight = 6 } };
            _registerTimelineKey = new Button(RegisterTimelineKey) { text = "Register Timeline(Key)", style = { marginRight = 6 } };
            row.Add(_resolveTimelineByKey);
            row.Add(_registerTimelineKey);
            right.Add(row);

            _bakeButton = new Button(BakeRuntimeSequence) { text = "Bake RuntimeSequence + Register Addressables" };
            right.Add(_bakeButton);

            var help = new HelpBox(
                "필수: skill 테이블의 TimelineKey / RuntimeSequenceKey 컬럼을 채워주세요.\n" +
                "- TimelineKey: 제작용 TimelineAsset Addressables Key\n" +
                "- RuntimeSequenceKey: 런타임용 SkillRuntimeSequence Addressables Key\n" +
                "Bake는 Timeline의 이벤트 클립(SkillEventTrackV2)만 수집합니다.",
                HelpBoxMessageType.Info);
            right.Add(help);

            body.Add(right);
            rootVisualElement.Add(body);

            LoadSkills();
        }

        private void LoadSkills()
        {
            var table = TableLoaderManagerSkill.LoadTableSkill(_forceReload.value);
            var list = new System.Collections.Generic.List<StruckTableSkill>(128);

            if (table != null)
            {
                foreach (var kv in table.GetDatas())
                {
                    if (kv.Value == null) continue;
                    list.Add(kv.Value);
                }
            }

            list.Sort((a, b) => a.Uid.CompareTo(b.Uid));
            _skillList.itemsSource = list;
            _skillList.Rebuild();
        }

        private void RefreshSelected()
        {
            if (_selectedSkill == null)
            {
                _selectedInfo.text = "Select a skill row.";
                return;
            }

            _selectedInfo.text =
                $"Uid: {_selectedSkill.Uid}\n" +
                $"Name: {_selectedSkill.Memo}\n" +
                $"CoolTime: {_selectedSkill.CoolTime}\n" +
                $"CastTime: {_selectedSkill.CastTime}\n" +
                $"CastStartClip: {_selectedSkill.CastStartClip}\n" +
                $"CastLoopClip: {_selectedSkill.CastLoopClip}\n" +
                $"CastEndClip: {_selectedSkill.CastEndClip}\n" +
                $"Range: {_selectedSkill.Range}\n" +
                $"TargetingMode: {_selectedSkill.TargetingMode}\n" +
                $"MaxTargets: {_selectedSkill.MaxTargets}\n";
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

            var seq = SkillTimelineBakerV2.BakeOrUpdate(_selectedSkill.Uid, timeline, assetPath);

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

            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true, settings.DefaultGroup.Schemas);
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
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
