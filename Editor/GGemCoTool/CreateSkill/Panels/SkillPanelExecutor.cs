using Config;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// SkillPanelExecutor.cs  
    /// CreateSkillWindow의 일부 UI 패널을 담당하는 partial 클래스 구현입니다.
    /// 스킬 선택, 실행, 캐스터 정보 표시 및 테이블 Row 편집 기능을 EditorWindow 내부에서 분리하여 구성합니다.
    /// </summary>
    public partial class CreateSkillWindow
    {
        /// <summary>
        /// OnGUIExecutor 동작을 수행하는 내부 UI 처리 메서드입니다.
        /// EditorWindow OnGUI 루프에서 호출되어 패널의 상태 표시 또는 사용자 입력을 처리합니다.
        /// </summary>
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

        /// <summary>
        /// UseSkillInPlayMode 동작을 수행하는 내부 UI 처리 메서드입니다.
        /// EditorWindow OnGUI 루프에서 호출되어 패널의 상태 표시 또는 사용자 입력을 처리합니다.
        /// </summary>
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

            if (!TryResolvePlayModeCaster(out var casterCharacter, out error))
            {
                EditorUtility.DisplayDialog(Title, error, "OK");
                return;
            }

            if (!TryGetSkillDriver(casterCharacter, out var driver))
            {
                var executor = casterCharacter.GetComponent<SkillExecutor>() ??
                               casterCharacter.GetComponentInChildren<SkillExecutor>();
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

        /// <summary>
        /// 현재 플레이 모드 기준 캐스터와 타겟 정보를 결정합니다.
        /// </summary>
        private bool TryGetPlayModeCasterAndTarget(out GameObject caster, out GGemCo2DCore.MonsterSkillTarget target,
            out string error)
        {
            caster = null;
            target = default;
            error = null;

            if (!TryGetSkillTestRuntimeHub(out var hub, out error))
                return false;

            if (!TryResolvePlayModeCaster(out var casterCharacter, out error))
                return false;

            caster = casterCharacter.gameObject;

            var targetingMode = GetCurrentTargetingMode();
            if (targetingMode == ConfigCommonSkill.SkillTargetingMode.Self)
            {
                target = CreateSelfTarget(casterCharacter);
                SyncResolvedTargetToHub(hub, target, preserveManualLockedTarget: false);
                return true;
            }

            if (!TryResolvePlayModeLockedTarget(hub, casterCharacter, out var lockedTarget, out error))
                return false;

            bool preserveManualLockedTarget = hub.UseManualLockedTarget;
            target = CreateTarget(casterCharacter.transform, lockedTarget);
            SyncResolvedTargetToHub(hub, target, preserveManualLockedTarget);
            return true;
        }

        /// <summary>
        /// 스킬 테스트 런타임 허브를 찾습니다.
        /// </summary>
        private bool TryGetSkillTestRuntimeHub(out SkillTestRuntimeHub hub, out string error)
        {
            hub = SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : Object.FindFirstObjectByType<SkillTestRuntimeHub>();

            if (hub != null)
            {
                error = null;
                return true;
            }

            error = "SkillTestRuntimeHub를 찾지 못했습니다. (Play Mode 진입 시 자동 생성되어야 합니다.)";
            return false;
        }

        /// <summary>
        /// 현재 선택된 캐릭터를 플레이 모드 캐스터로 확정합니다.
        /// </summary>
        private bool TryResolvePlayModeCaster(out CharacterBase casterCharacter, out string error)
        {
            casterCharacter = selectedCharacter;
            if (casterCharacter == null)
            {
                error = "스킬을 사용할 캐스터가 선택되지 않았습니다.";
                return false;
            }

            if (casterCharacter.gameObject == null)
            {
                error = "선택된 캐스터의 GameObject를 찾지 못했습니다.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 현재 편집 상태 기준 타겟팅 모드를 반환합니다.
        /// </summary>
        private ConfigCommonSkill.SkillTargetingMode GetCurrentTargetingMode()
        {
            return _editingRow?.TargetingMode ?? (_selectedData?.TargetingMode ?? default);
        }

        /// <summary>
        /// Self가 아닌 경우 사용할 locked target을 결정합니다.
        /// </summary>
        private bool TryResolvePlayModeLockedTarget(
            SkillTestRuntimeHub hub,
            CharacterBase casterCharacter,
            out Transform lockedTarget,
            out string error)
        {
            lockedTarget = null;

            if (TryGetManualLockedTarget(hub, casterCharacter.transform, out lockedTarget))
            {
                error = null;
                return true;
            }

            if (IsPlayerCaster(casterCharacter))
                return TryResolveLockedTargetForPlayerCaster(hub, casterCharacter.transform, out lockedTarget, out error);

            return TryResolveLockedTargetForNonPlayerCaster(hub, out lockedTarget, out error);
        }

        /// <summary>
        /// 수동 지정 Target이 유효하면 반환합니다.
        /// </summary>
        private static bool TryGetManualLockedTarget(
            SkillTestRuntimeHub hub,
            Transform casterTransform,
            out Transform lockedTarget)
        {
            lockedTarget = null;
            if (!hub.UseManualLockedTarget || hub.LockedTarget == null)
                return false;

            if (hub.LockedTarget == casterTransform)
                return false;

            lockedTarget = hub.LockedTarget;
            return true;
        }

        /// <summary>
        /// 현재 캐스터가 Player인지 확인합니다.
        /// </summary>
        private static bool IsPlayerCaster(CharacterBase casterCharacter)
        {
            return SceneGame.Instance != null &&
                   SceneGame.Instance.player != null &&
                   casterCharacter != null &&
                   casterCharacter.gameObject == SceneGame.Instance.player.gameObject;
        }

        /// <summary>
        /// Player 캐스터 기준 자동 타겟을 결정합니다.
        /// </summary>
        private bool TryResolveLockedTargetForPlayerCaster(
            SkillTestRuntimeHub hub,
            Transform casterTransform,
            out Transform lockedTarget,
            out string error)
        {
            lockedTarget = TryFindSpawnedTarget(hub, casterTransform);
            if (lockedTarget != null)
            {
                error = null;
                return true;
            }

            if (_dummyTargetCharacter != null &&
                _dummyTargetCharacter.transform != null &&
                _dummyTargetCharacter.transform != casterTransform)
            {
                lockedTarget = _dummyTargetCharacter.transform;
                error = null;
                return true;
            }

            error = "Player를 캐스터로 선택했지만, 타겟을 찾지 못했습니다.\n" +
                    "- 1) '씬 Target 선택'에서 타겟을 수동 지정하거나\n" +
                    "- 2) 먼저 몬스터를 소환해서 기본 정책 타겟을 만들고\n" +
                    "다시 시도하세요.";
            return false;
        }

        /// <summary>
        /// Non-Player 캐스터 기준 자동 타겟을 결정합니다.
        /// </summary>
        private static bool TryResolveLockedTargetForNonPlayerCaster(
            SkillTestRuntimeHub hub,
            out Transform lockedTarget,
            out string error)
        {
            lockedTarget = null;
            if (!hub.TryBindPlayerAsTarget() || hub.LockedTarget == null)
            {
                error = "Player를 찾지 못했습니다. SceneGame.player 또는 Tag=Player 오브젝트가 필요합니다.";
                return false;
            }

            lockedTarget = hub.LockedTarget;
            error = null;
            return true;
        }

        /// <summary>
        /// 허브가 관리 중인 스폰 목록에서 캐스터 자신을 제외한 첫 타겟을 찾습니다.
        /// </summary>
        private static Transform TryFindSpawnedTarget(SkillTestRuntimeHub hub, Transform casterTransform)
        {
            for (int i = 0; i < hub.Spawned.Count; i++)
            {
                var go = hub.Spawned[i];
                if (go == null || go.transform == null || go.transform == casterTransform)
                    continue;

                return go.transform;
            }

            return null;
        }

        /// <summary>
        /// Self 타겟 컨텍스트를 생성합니다.
        /// </summary>
        private static GGemCo2DCore.MonsterSkillTarget CreateSelfTarget(CharacterBase casterCharacter)
        {
            var self = casterCharacter.transform;
            var position = self.position;
            var forward = (Vector2)self.right;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector2.right;

            return new GGemCo2DCore.MonsterSkillTarget(self, position, forward);
        }

        /// <summary>
        /// locked target 기준 타겟 컨텍스트를 생성합니다.
        /// </summary>
        private static GGemCo2DCore.MonsterSkillTarget CreateTarget(Transform casterTransform, Transform lockedTarget)
        {
            var groundPoint = lockedTarget.position;
            var delta = lockedTarget.position - casterTransform.position;
            var forward = new Vector2(delta.x, delta.y);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector2.right;

            return new GGemCo2DCore.MonsterSkillTarget(lockedTarget, groundPoint, forward);
        }

        /// <summary>
        /// 결정된 타겟 정보를 런타임 허브에 반영합니다.
        /// </summary>
        private static void SyncResolvedTargetToHub(
            SkillTestRuntimeHub hub,
            in GGemCo2DCore.MonsterSkillTarget target,
            bool preserveManualLockedTarget)
        {
            hub.SetGroundPoint(target.GroundPoint);
            hub.SetForward(target.Forward);

            if (preserveManualLockedTarget)
                hub.SetManualLockedTarget(target.LockedTarget);
            else
                hub.SetLockedTarget(target.LockedTarget);
        }

        /// <summary>
        /// 캐릭터에서 스킬 드라이버를 찾습니다.
        /// </summary>
        private static bool TryGetSkillDriver(CharacterBase casterCharacter, out GGemCo2DCore.IMonsterSkillDriver driver)
        {
            driver = null;
            if (casterCharacter == null)
                return false;

            var components = casterCharacter.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is GGemCo2DCore.IMonsterSkillDriver resolvedDriver)
                {
                    driver = resolvedDriver;
                    return true;
                }
            }

            return false;
        }
    }
}
