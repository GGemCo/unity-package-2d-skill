using GGemCo2DCore;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    public partial class CreateSkillWindow
    {
        private const string DummyTargetName = "SkillTest_DummyTarget";
        private CharacterBase _dummyTargetCharacter;
        /// <summary>
        /// 테스트 대상 더미 캐릭터 참조입니다.
        /// </summary>
        private static readonly Vector3 DummyTargetOffset = new(150f, 0f, 0f);

        private void OnGUITarget()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("더미 Target", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "선택한 Caster를 기준으로 더미 캐릭터를 생성하거나, 이미 생성된 더미를 재사용합니다.\n" +
                    "생성된 더미는 SkillTestRuntimeHub의 수동 Target으로도 함께 지정합니다.",
                    MessageType.Info);

                using (new EditorGUI.DisabledScope(!Application.isPlaying || selectedCharacter == null))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("더미 Target 생성/재사용", GUILayout.Height(22)))
                        {
                            if (TryCreateOrReuseDummyTargetCharacter(
                                    Title,
                                    selectedCharacter,
                                    out var dummyCharacter,
                                    toolOwnerKey: GetType().FullName,
                                    dummyName: DummyTargetName,
                                    spawnOffset: DummyTargetOffset))
                            {
                                _dummyTargetCharacter = dummyCharacter;
                                BindDummyTargetToHub(_dummyTargetCharacter);
                                RefreshSceneCharacters();
                                ShowNotification(new GUIContent($"더미 Target 준비 완료: {_dummyTargetCharacter.name}"));
                                Repaint();
                            }
                        }

                        if (GUILayout.Button("수동 Target 해제", GUILayout.Height(22)))
                            ClearDummyTargetFromHub();
                    }
                }

                var newDummyTarget = (CharacterBase)EditorGUILayout.ObjectField(
                    "더미 Target(직접 지정)",
                    _dummyTargetCharacter,
                    typeof(CharacterBase),
                    true);

                if (newDummyTarget != _dummyTargetCharacter)
                {
                    _dummyTargetCharacter = newDummyTarget;
                    if (_dummyTargetCharacter != null)
                        BindDummyTargetToHub(_dummyTargetCharacter);
                }

                if (selectedCharacter != null)
                    EditorGUILayout.LabelField("기준 Caster", selectedCharacter.name);

                if (_dummyTargetCharacter != null)
                {
                    EditorGUILayout.LabelField("현재 더미 이름", _dummyTargetCharacter.name);
                    EditorGUILayout.Vector3Field("더미 위치", _dummyTargetCharacter.transform.position);
                }
                else
                {
                    EditorGUILayout.HelpBox("아직 생성된 더미 Target이 없습니다.", MessageType.None);
                }
            }
        }
        private void BindDummyTargetToHub(CharacterBase dummyCharacter)
        {
            if (dummyCharacter == null)
                return;

            var hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub == null)
                return;

            hub.SetManualLockedTarget(dummyCharacter.transform);
            hub.SetGroundPoint(dummyCharacter.transform.position);
        }

        private void ClearDummyTargetFromHub()
        {
            var hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub == null)
                return;

            hub.ClearManualLockedTarget();
            ShowNotification(new GUIContent("수동 Target 해제"));
        }
    }
}