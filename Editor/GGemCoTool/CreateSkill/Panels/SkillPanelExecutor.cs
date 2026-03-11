using Config;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    public partial class CreateSkillWindow
    {
        private void OnGUIExecutor()
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !SceneGame.Instance))
            {
                if (GUILayout.Button("스킬 사용하기", EditorConstants.GUILayoutButtonHeight22))
                {
                    UseSkillInPlayMode();
                }
            }
        }
        
        private void UseSkillInPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return;
            }

            if (_selectedData == null || _selectedData.Uid <= 0)
            {
                EditorUtility.DisplayDialog(Title, "스킬을 먼저 선택하세요.", "OK");
                return;
            }
            
            // 입력값이 바뀐 상태라면, 테스트 적용(테이블 오버라이드)까지 포함해서 즉시 반영한다.
            if (_editingDirty)
            {
                if (!ApplyEditingToCachedRow())
                    return;
                UpdateInGameTableInfo(_editingRow);
            }

            // Timeline이 지정되어 있으면, 메모리에서 Bake한 시퀀스를 Repository에 주입하여
            // SkillExecutor가 Addressables 로딩 없이 실행하도록 한다.
            // TryRegisterEditorSequenceOverrideFromTimeline(_selectedSkill.Uid);
            //
            if (!TryGetPlayModeCasterAndTarget(out var caster, out var target, out string error))
            {
                EditorUtility.DisplayDialog(Title, error, "OK");
                return;
            }

            // Skill 시스템이 붙어있는 캐스터에서 실행
            // Unity의 GetComponent<T>()는 interface를 직접 받을 수 없으므로, Component 스캔으로 찾는다.
            GGemCo2DCore.IMonsterSkillDriver driver = null;
            var comps = selectedCharacter.GetComponents<Component>();
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
                var executor = selectedCharacter.GetComponent<SkillExecutor>();
                if (executor == null)
                {
                    EditorUtility.DisplayDialog(Title, "캐스터에 SkillExecutor(또는 MonsterSkillDriverAdapter)가 없습니다.", "OK");
                    return;
                }

                var ctx = new SkillTargetContext(
                    caster: selectedCharacter.gameObject,
                    lockedTarget: target.LockedTarget != null ? target.LockedTarget.gameObject : null,
                    groundPoint: target.GroundPoint,
                    forward: new Vector3(target.Forward.x, target.Forward.y, 0f));

                bool started = executor.TryUse(_selectedData.Uid, ctx, preferMonsterTable: false);
                if (!started)
                    ShowNotification(new GUIContent("스킬 실행 실패(진행 중이거나 테이블/시퀀스 누락)"));
                else
                    ShowNotification(new GUIContent("스킬 실행"));
                return;
            }

            var result = driver.TryUseSkill(_selectedData.Uid, target);
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

            caster = selectedCharacter.gameObject;
            if (caster == null)
            {
                error = "스킬을 사용할 캐스터가 선택되지 않았습니다.";
                return false;
            }

            // TargetingMode가 Self이면 타겟은 캐스터 자신으로 고정한다.
            // (이 경우 수동 Target 지정/기본 정책 타겟 결정은 무시한다.)
            var targetingMode = _editingRow?.TargetingMode ?? (_selectedData?.TargetingMode ?? default);

            if (targetingMode == ConfigCommonSkill.SkillTargetingMode.Self)
            {
                var self = caster.transform;
                var p = self.position;
                var forwardSelf = (Vector2)caster.transform.right;
                if (forwardSelf.sqrMagnitude < 1e-6f) forwardSelf = Vector2.right;

                hub.SetGroundPoint(p);
                hub.SetLockedTarget(self);
                hub.SetForward(forwardSelf);

                target = new MonsterSkillTarget(self, p, forwardSelf);
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
    }
}