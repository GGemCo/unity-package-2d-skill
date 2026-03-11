using System.IO;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    public partial class CreateSkillWindow
    {
        private TimelineAsset _timelineField;
        
        private void OnGUITimelineBake()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("타임라인 Bake", EditorStyles.boldLabel);

                _timelineField =
                    (TimelineAsset)EditorGUILayout.ObjectField("타임라인 파일 지정", _timelineField, typeof(TimelineAsset),
                        false);

                using (new EditorGUI.DisabledScope(!_timelineField))
                {
                    if (GUILayout.Button("런타임 시퀀스 생성 + Addressables 등록", EditorConstants.GUILayoutButtonHeight22))
                    {
                        if (BakeRuntimeSequence(_selectedData, _timelineField, out var message))
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
        }

        private static bool BakeRuntimeSequence(StruckTableSkill selectedSkill, TimelineAsset timeline, out string message)
        {
            message = null;

            if (selectedSkill == null)
            {
                message = "선택된 스킬이 없습니다.";
                return false;
            }

            var runtimeSequenceKey = ConfigAddressableKeySkill.GetRuntimeSequenceKey(selectedSkill.Uid);
            if (string.IsNullOrEmpty(runtimeSequenceKey))
            {
                message = "[SkillAuthoringV2] RuntimeSequenceKey가 비어 있습니다.";
                return false;
            }

            if (timeline == null)
            {
                message = "[SkillAuthoringV2] TimelineAsset을 지정하세요.";
                return false;
            }

            string folder = ConfigAddressablePathSkill.Skill.RuntimeSequences;
            Directory.CreateDirectory(folder);

            string assetPath = $"{folder}/SkillRuntimeSequence_{selectedSkill.Uid}.asset";
            assetPath = assetPath.Replace('\\', '/');

            var seq = SkillTimelineBaker.BakeOrUpdate(selectedSkill.Uid, timeline, assetPath);

            EnsureAddressableEntry(
                assetPath: AssetDatabase.GetAssetPath(seq),
                addressKey: runtimeSequenceKey,
                groupName: ConfigAddressableGroupNameSkill.SkillRuntimeSequence,
                label: ConfigAddressableLabelSkill.SkillRuntimeSequence);

            message = $"[SkillAuthoringV2] Bake/등록 완료: uid={selectedSkill.Uid}, key={runtimeSequenceKey}";
            return true;
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