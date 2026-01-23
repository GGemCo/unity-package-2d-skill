// using System.Collections.Generic;
// using UnityEditor;
// using UnityEngine.Timeline;
// using UnityEngine.UIElements;
// using UnityEditor.UIElements;
//
// namespace GGemCo2DSkillEditor
// {
//     /// <summary>
//     /// 신규 스킬 제작 툴 V2 (Clip 기반 Timeline -> Bake).
//     /// skill 테이블 연동은 ISkillTableProvider로 분리하여 확장한다.
//     /// </summary>
//     public sealed class SkillAuthoringToolWindowV2 : EditorWindow
//     {
//         [MenuItem("GGemCo/Skill/Skill Authoring V2")]
//         public static void Open()
//         {
//             GetWindow<SkillAuthoringToolWindowV2>("Skill Authoring V2");
//         }
//
//         private IntegerField _skillUidField;
//         private ObjectField _timelineField;
//         private TextField _outputPathField;
//         private TextField _reportField;
//
//         public void CreateGUI()
//         {
//             var root = rootVisualElement;
//             root.style.paddingLeft = 8;
//             root.style.paddingRight = 8;
//             root.style.paddingTop = 8;
//             root.style.paddingBottom = 8;
//
//             _skillUidField = new IntegerField("Skill UID") { value = 0 };
//
//             _timelineField = new ObjectField("TimelineAsset")
//             {
//                 objectType = typeof(TimelineAsset),
//                 allowSceneObjects = false
//             };
//
//             _outputPathField = new TextField("Output Asset Path")
//             {
//                 value = "Assets/SkillRuntimeSequences/SkillRuntimeSequence.asset"
//             };
//
//             var validateBtn = new Button(OnValidateClicked) { text = "Validate Timeline" };
//             var bakeBtn = new Button(OnBakeClicked) { text = "Bake Runtime Sequence" };
//
//             _reportField = new TextField("Report")
//             {
//                 multiline = true,
//                 isReadOnly = true
//             };
//             _reportField.style.height = 200;
//
//             root.Add(_skillUidField);
//             root.Add(_timelineField);
//             root.Add(_outputPathField);
//
//             var row = new VisualElement();
//             row.style.flexDirection = FlexDirection.Row;
//             // row.style.gap = 6;
//             row.Add(validateBtn);
//             row.Add(bakeBtn);
//             root.Add(row);
//
//             root.Add(_reportField);
//         }
//
//         private void OnValidateClicked()
//         {
//             var timeline = _timelineField.value as TimelineAsset;
//             List<string> errors = SkillTimelineValidatorV2.Validate(timeline);
//
//             _reportField.value = errors.Count == 0
//                 ? "Validate OK"
//                 : string.Join("\n", errors);
//         }
//
//         private void OnBakeClicked()
//         {
//             var timeline = _timelineField.value as TimelineAsset;
//             if (timeline == null)
//             {
//                 _reportField.value = "TimelineAsset is null.";
//                 return;
//             }
//
//             int uid = _skillUidField.value;
//             string outputPath = _outputPathField.value;
//
//             var seq = SkillTimelineBakerV2.Bake(uid, timeline, outputPath, out var report);
//             _reportField.value = $"{report}\n\nResult: {AssetDatabase.GetAssetPath(seq)}";
//         }
//     }
// }
