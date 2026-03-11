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
            
            if (_editingDirty)
            {
                if (!ApplyEditingToCachedRow())
                    return;
                UpdateInGameTableInfo(_editingRow);
            }

            if (!TryGetPlayModeCasterAndTarget(out var caster, out var target, out string error))
            {
                EditorUtility.DisplayDialog(Title, error, "OK");
                return;
            }

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

                bool started = executor.TryUse(_selectedData.Uid, ctx, _selectedData.Source);
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

            Transform lockedTarget = null;

            if (hub.UseManualLockedTarget && hub.LockedTarget != null)
            {
                if (hub.LockedTarget.gameObject != caster)
                {
                    lockedTarget = hub.LockedTarget;
                }
            }

            if (SceneGame.Instance != null && SceneGame.Instance.player != null && caster == SceneGame.Instance.player.gameObject)
            {
                if (lockedTarget == null)
                {
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
