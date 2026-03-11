using GGemCo2DCore;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 테스트용 더미 Target 관리 UI를 제공하는 CreateSkillWindow의 partial 구현입니다.
    /// 선택한 Caster 기준으로 더미 캐릭터를 생성하거나 재사용하고,
    /// SkillTestRuntimeHub의 수동 Target으로 연결합니다.
    /// </summary>
    public partial class CreateSkillWindow
    {
        /// <summary>
        /// 씬에 생성되는 더미 캐릭터의 기본 이름입니다.
        /// </summary>
        private const string DummyTargetName = "SkillTest_DummyTarget";

        /// <summary>
        /// 현재 창에서 관리 중인 더미 Target 캐릭터 참조입니다.
        /// </summary>
        private CharacterBase _dummyTargetCharacter;

        /// <summary>
        /// 더미 캐릭터 생성 시 Caster 기준으로 적용되는 기본 위치 오프셋입니다.
        /// </summary>
        private static readonly Vector3 DummyTargetOffset = new(150f, 0f, 0f);

        /// <summary>
        /// 더미 Target 생성 및 관리 UI를 그립니다.
        /// </summary>
        /// <remarks>
        /// 플레이 모드에서만 동작하며, 선택된 Caster를 기준으로 더미 캐릭터를 생성하거나 재사용합니다.
        /// 생성된 더미는 <see cref="SkillTestRuntimeHub"/>의 수동 Target으로 설정됩니다.
        /// </remarks>
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

        /// <summary>
        /// 지정한 더미 캐릭터를 <see cref="SkillTestRuntimeHub"/>의 수동 Target으로 설정합니다.
        /// </summary>
        /// <param name="dummyCharacter">Target으로 사용할 더미 캐릭터입니다.</param>
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

        /// <summary>
        /// 현재 설정된 수동 Target을 <see cref="SkillTestRuntimeHub"/>에서 해제합니다.
        /// </summary>
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