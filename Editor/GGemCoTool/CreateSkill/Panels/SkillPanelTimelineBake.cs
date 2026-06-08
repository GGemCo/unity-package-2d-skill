using System.IO;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 생성 창의 타임라인 Bake 및 런타임 시퀀스 생성 기능을 담당하는 partial 구현입니다.
    /// 선택한 타임라인 에셋을 런타임 시퀀스로 변환하고 Addressables 등록까지 수행합니다.
    /// </summary>
    public partial class CreateSkillWindow
    {
        /// <summary>
        /// Bake 대상 타임라인 에셋입니다.
        /// </summary>
        private TimelineAsset _timelineField;

        /// <summary>
        /// Timeline 자동 선택 결과 안내 메시지입니다.
        /// </summary>
        private string _timelineAutoSelectMessage;

        /// <summary>
        /// Timeline 자동 선택 결과 안내 메시지 타입입니다.
        /// </summary>
        private MessageType _timelineAutoSelectMessageType = MessageType.None;
        
        /// <summary>
        /// 타임라인 Bake UI를 그리고, 선택한 타임라인을 런타임 시퀀스로 생성 및 등록하는 기능을 제공합니다.
        /// </summary>
        private void OnGUITimelineBake()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("타임라인 Bake", EditorStyles.boldLabel);

                DrawExpectedTimelinePath();

                EditorGUI.BeginChangeCheck();
                var selectedTimeline = (TimelineAsset)EditorGUILayout.ObjectField(
                    "타임라인 파일 지정",
                    _timelineField,
                    typeof(TimelineAsset),
                    false);
                if (EditorGUI.EndChangeCheck())
                {
                    _timelineField = selectedTimeline;
                    ClearTimelineAutoSelectMessage();
                }

                // using (new EditorGUILayout.HorizontalScope())
                // {
                //     using (new EditorGUI.DisabledScope(GetSelectedUid() <= 0))
                //     {
                //         if (GUILayout.Button("자동 선택", GUILayout.Width(90)))
                //         {
                //             AutoSelectTimelineForSelectedSkill(true);
                //         }
                //     }
                //
                //     using (new EditorGUI.DisabledScope(_timelineField == null))
                //     {
                //         if (GUILayout.Button("Project 창에서 선택", GUILayout.Width(130)))
                //         {
                //             Selection.activeObject = _timelineField;
                //             EditorGUIUtility.PingObject(_timelineField);
                //         }
                //     }
                // }

                DrawTimelineAutoSelectMessage();

                using (new EditorGUI.DisabledScope(!_timelineField))
                {
                    if (GUILayout.Button("런타임 시퀀스 생성 + Addressables 등록", EditorConstants.GUILayoutButtonHeight22))
                    {
                        if (BakeRuntimeSequence(GetSelectedUid(), _timelineField, out var message))
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


        /// <summary>
        /// 현재 선택된 스킬의 권장 Timeline 원본 에셋 경로를 UI에 표시합니다.
        /// </summary>
        private void DrawExpectedTimelinePath()
        {
            int selectedUid = GetSelectedUid();
            if (selectedUid <= 0)
            {
                EditorGUILayout.HelpBox("스킬을 선택하면 권장 Timeline 경로가 표시됩니다.", MessageType.Info);
                return;
            }

            // string expectedPath = SkillTimelineAuthoringPath.GetAssetPath(_selectedSource, selectedUid);
            // EditorGUILayout.LabelField("권장 Timeline 경로");
            // EditorGUILayout.SelectableLabel(expectedPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        /// <summary>
        /// 현재 선택된 스킬 UID와 소스에 맞는 Timeline 원본 에셋을 자동으로 찾아 Bake 대상 필드에 지정합니다.
        /// </summary>
        /// <param name="pingTimelineObject">찾은 Timeline 에셋을 Project 창에서 강조 표시할지 여부입니다.</param>
        private void AutoSelectTimelineForSelectedSkill(bool pingTimelineObject)
        {
            int selectedUid = GetSelectedUid();
            if (selectedUid <= 0)
            {
                _timelineField = null;
                SetTimelineAutoSelectMessage("선택된 스킬이 없어 Timeline을 자동 선택하지 않았습니다.", MessageType.Info);
                return;
            }

            if (SkillTimelineAuthoringPath.TryFindTimeline(
                    _selectedSource,
                    selectedUid,
                    out var timeline,
                    out var assetPath,
                    out int candidateCount))
            {
                _timelineField = timeline;
                SetTimelineAutoSelectMessage($"Timeline 자동 선택 완료: {assetPath}", MessageType.Info);

                if (pingTimelineObject)
                {
                    Selection.activeObject = timeline;
                    EditorGUIUtility.PingObject(timeline);
                }

                return;
            }

            _timelineField = null;

            string expectedPath = SkillTimelineAuthoringPath.GetAssetPath(_selectedSource, selectedUid);
            if (candidateCount > 1)
            {
                SetTimelineAutoSelectMessage(
                    $"동일한 Timeline 후보가 {candidateCount}개 발견되어 자동 선택하지 않았습니다. 권장 경로에 하나만 유지하세요. 권장 경로: {expectedPath}",
                    MessageType.Warning);
                return;
            }

            SetTimelineAutoSelectMessage(
                $"규칙에 맞는 Timeline 파일을 찾지 못했습니다. 권장 경로: {expectedPath}",
                MessageType.Warning);
        }

        /// <summary>
        /// Timeline 자동 선택 결과 메시지를 UI에 표시합니다.
        /// </summary>
        private void DrawTimelineAutoSelectMessage()
        {
            if (string.IsNullOrWhiteSpace(_timelineAutoSelectMessage) || _timelineAutoSelectMessageType == MessageType.None)
                return;

            EditorGUILayout.HelpBox(_timelineAutoSelectMessage, _timelineAutoSelectMessageType);
        }

        /// <summary>
        /// Timeline 자동 선택 결과 메시지를 설정합니다.
        /// </summary>
        /// <param name="message">표시할 메시지입니다.</param>
        /// <param name="messageType">메시지 표시 타입입니다.</param>
        private void SetTimelineAutoSelectMessage(string message, MessageType messageType)
        {
            _timelineAutoSelectMessage = message;
            _timelineAutoSelectMessageType = messageType;
        }

        /// <summary>
        /// 사용자가 Timeline 필드를 직접 변경했을 때 자동 선택 결과 메시지를 제거합니다.
        /// </summary>
        private void ClearTimelineAutoSelectMessage()
        {
            _timelineAutoSelectMessage = null;
            _timelineAutoSelectMessageType = MessageType.None;
        }

        /// <summary>
        /// 선택한 스킬과 타임라인 정보를 바탕으로 런타임 시퀀스 에셋을 생성하거나 갱신하고,
        /// 해당 에셋을 Addressables에 등록합니다.
        /// </summary>
        /// <param name="selectedSkillUid">현재 선택된 스킬 UID입니다.</param>
        /// <param name="timeline">Bake할 원본 타임라인 에셋입니다.</param>
        /// <param name="message">처리 결과 또는 실패 사유를 반환합니다.</param>
        /// <returns>런타임 시퀀스 생성 및 등록에 성공하면 <see langword="true"/>를 반환합니다.</returns>
        private bool BakeRuntimeSequence(int selectedSkillUid, TimelineAsset timeline, out string message)
        {
            message = null;

            if (selectedSkillUid <= 0)
            {
                message = "선택된 스킬이 없습니다.";
                return false;
            }

            var runtimeSequenceKey = ConfigAddressableKeySkill.GetRuntimeSequenceKeyPlayer(selectedSkillUid);
            if (_selectedSource == ConfigCommon.SkillTableSource.Monster)
            {
                runtimeSequenceKey = ConfigAddressableKeySkill.GetRuntimeSequenceKeyMonster(selectedSkillUid);
            }

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

            string folder = ConfigAddressablePathSkill.Skill.RuntimeSequence.Player;
            var groupName = ConfigAddressableGroupNameSkill.SkillRuntimeSequencePlayer;
            var label = ConfigAddressableLabelSkill.SkillRuntimeSequencePlayer;
            if (_selectedSource == ConfigCommon.SkillTableSource.Monster)
            {
                folder = ConfigAddressablePathSkill.Skill.RuntimeSequence.Monster;
                groupName = ConfigAddressableGroupNameSkill.SkillRuntimeSequenceMonster;
                label = ConfigAddressableLabelSkill.SkillRuntimeSequenceMonster;
            }
            
            Directory.CreateDirectory(folder);

            string assetPath = $"{folder}/SkillRuntimeSequence_{selectedSkillUid}.asset";
            assetPath = assetPath.Replace('\\', '/');

            var seq = SkillTimelineBaker.BakeOrUpdate(selectedSkillUid, timeline, assetPath);

            EnsureAddressableEntry(
                assetPath: AssetDatabase.GetAssetPath(seq),
                addressKey: runtimeSequenceKey,
                groupName: groupName,
                label: label);

            message = $"[SkillAuthoringV2] Bake/등록 완료";
            return true;
        }

        /// <summary>
        /// 지정한 에셋이 Addressables 그룹에 등록되도록 보장하고,
        /// 주소 키와 라벨을 설정한 뒤 변경 사항을 저장합니다.
        /// </summary>
        /// <param name="assetPath">등록할 에셋의 Unity 프로젝트 경로입니다.</param>
        /// <param name="addressKey">Addressables에서 사용할 주소 키입니다.</param>
        /// <param name="groupName">에셋을 등록할 Addressables 그룹 이름입니다.</param>
        /// <param name="label">에셋에 부여할 Addressables 라벨입니다.</param>
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
