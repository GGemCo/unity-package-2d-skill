using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 선택 변경 시 테스트 캐스터와 테스트 Target을 자동으로 연결하는 CreateSkillWindow partial 구현입니다.
    /// </summary>
    public partial class CreateSkillWindow
    {
        /// <summary>
        /// 현재 선택된 스킬 소스와 타겟팅 정책에 맞춰 캐스터와 테스트 Target을 자동으로 보정합니다.
        /// </summary>
        /// <remarks>
        /// Play Mode에서만 씬 캐릭터를 해석하며, 사용자가 스킬 선택 후 바로 실행할 수 있도록 Hub의 Target 상태까지 함께 동기화합니다.
        /// </remarks>
        private void ApplySkillSelectionAutoBinding()
        {
            if (!Application.isPlaying || GetSelectedUid() <= 0)
                return;

            RefreshSceneCharacters();
            ApplyCasterAutoBindingForCurrentSkill();
            ApplyTargetAutoBindingForCurrentSkill();
            Repaint();
        }

        /// <summary>
        /// 현재 스킬 테이블 소스에 맞는 캐스터를 자동으로 선택합니다.
        /// </summary>
        private void ApplyCasterAutoBindingForCurrentSkill()
        {
            if (_selectedSource == ConfigCommon.SkillTableSource.Player)
            {
                TryAssignAutoSelectedCaster(FindPlayerCharacter());
                return;
            }

            if (TryGetSelectedMonsterCharacter(out var selectedMonster))
            {
                TryAssignAutoSelectedCaster(selectedMonster);
                return;
            }

            TryAssignAutoSelectedCaster(FindFirstMonsterCharacter());
        }

        /// <summary>
        /// 현재 스킬 테이블 소스에 맞는 테스트 Target을 자동으로 선택하고 Hub에 반영합니다.
        /// </summary>
        private void ApplyTargetAutoBindingForCurrentSkill()
        {
            CharacterBase preferredTarget = null;

            if (_selectedSource == ConfigCommon.SkillTableSource.Player)
            {
                preferredTarget = FindNearestVisibleMonsterCharacter(selectedCharacter);
            }
            else
            {
                preferredTarget = FindPlayerCharacter();
            }

            if (preferredTarget != null && preferredTarget != selectedCharacter)
            {
                _dummyTargetCharacter = preferredTarget;
                BindDummyTargetToHub(_dummyTargetCharacter);
                return;
            }

            _dummyTargetCharacter = null;
            ClearResolvedTestTargetFromHub();
        }

        /// <summary>
        /// 자동 선택된 캐스터를 공통 선택 상태와 SkillTestRuntimeHub에 반영합니다.
        /// </summary>
        /// <param name="character">자동 선택할 캐스터입니다.</param>
        /// <returns>캐스터를 반영했으면 <see langword="true"/>를 반환합니다.</returns>
        private bool TryAssignAutoSelectedCaster(CharacterBase character)
        {
            if (character == null)
                return false;

            if (selectedCharacter == character)
            {
                SyncSelectedCharacterIndex();
                return true;
            }

            selectedCharacter = character;
            SyncSelectedCharacterIndex();
            OnSelectedCharacterChanged(selectedCharacter);
            return true;
        }

        /// <summary>
        /// 씬에서 현재 플레이어 캐릭터를 찾습니다.
        /// </summary>
        /// <returns>플레이어 캐릭터를 찾으면 해당 참조를, 없으면 null을 반환합니다.</returns>
        private CharacterBase FindPlayerCharacter()
        {
            if (SceneGame.Instance != null && SceneGame.Instance.player != null)
                return SceneGame.Instance.player.GetComponent<CharacterBase>();

            for (int i = 0; i < sceneCharacters.Count; i++)
            {
                var character = sceneCharacters[i];
                if (character != null && character.IsPlayer())
                    return character;
            }

            return null;
        }

        /// <summary>
        /// SkillTestRuntimeHub가 현재 선택한 몬스터 캐릭터를 반환합니다.
        /// </summary>
        /// <param name="character">선택된 몬스터 캐릭터입니다.</param>
        /// <returns>유효한 선택 몬스터가 있으면 <see langword="true"/>를 반환합니다.</returns>
        private static bool TryGetSelectedMonsterCharacter(out CharacterBase character)
        {
            character = null;
            var hub = SkillTestRuntimeHubEditorFacade.FindHub();
            if (hub == null || hub.SelectedMonster == null)
                return false;

            character = hub.SelectedMonster.GetComponent<CharacterBase>() ??
                        hub.SelectedMonster.GetComponentInChildren<CharacterBase>();
            return character != null && character.IsMonster();
        }

        /// <summary>
        /// 씬 캐릭터 목록에서 첫 번째 몬스터 캐릭터를 찾습니다.
        /// </summary>
        /// <returns>몬스터 캐릭터를 찾으면 해당 참조를, 없으면 null을 반환합니다.</returns>
        private CharacterBase FindFirstMonsterCharacter()
        {
            for (int i = 0; i < sceneCharacters.Count; i++)
            {
                var character = sceneCharacters[i];
                if (character != null && character.IsMonster())
                    return character;
            }

            return null;
        }

        /// <summary>
        /// 기준 캐스터 주변에서 화면에 보이는 가장 가까운 몬스터 캐릭터를 찾습니다.
        /// </summary>
        /// <param name="origin">거리 기준으로 사용할 캐스터입니다.</param>
        /// <returns>화면에 보이는 몬스터가 있으면 가장 가까운 몬스터를, 없으면 가장 가까운 몬스터를 반환합니다.</returns>
        private CharacterBase FindNearestVisibleMonsterCharacter(CharacterBase origin)
        {
            var originPosition = origin != null ? origin.transform.position : Vector3.zero;
            var camera = Camera.main;
            CharacterBase nearestVisible = null;
            CharacterBase nearestFallback = null;
            float nearestVisibleSqrDistance = float.MaxValue;
            float nearestFallbackSqrDistance = float.MaxValue;

            for (int i = 0; i < sceneCharacters.Count; i++)
            {
                var character = sceneCharacters[i];
                if (character == null || !character.IsMonster())
                    continue;

                float sqrDistance = (character.transform.position - originPosition).sqrMagnitude;
                if (sqrDistance < nearestFallbackSqrDistance)
                {
                    nearestFallbackSqrDistance = sqrDistance;
                    nearestFallback = character;
                }

                if (!IsCharacterVisibleByCamera(character, camera))
                    continue;

                if (sqrDistance >= nearestVisibleSqrDistance)
                    continue;

                nearestVisibleSqrDistance = sqrDistance;
                nearestVisible = character;
            }

            return nearestVisible != null ? nearestVisible : nearestFallback;
        }

        /// <summary>
        /// 캐릭터가 현재 메인 카메라 화면 안에 있는지 확인합니다.
        /// </summary>
        /// <param name="character">화면 노출 여부를 확인할 캐릭터입니다.</param>
        /// <param name="camera">기준 카메라입니다.</param>
        /// <returns>카메라가 없거나 화면 안에 있으면 <see langword="true"/>를 반환합니다.</returns>
        private static bool IsCharacterVisibleByCamera(CharacterBase character, Camera camera)
        {
            if (character == null || camera == null)
                return true;

            Vector3 viewportPoint = camera.WorldToViewportPoint(character.transform.position);
            return viewportPoint.z >= 0f &&
                   viewportPoint.x >= 0f &&
                   viewportPoint.x <= 1f &&
                   viewportPoint.y >= 0f &&
                   viewportPoint.y <= 1f;
        }

        /// <summary>
        /// 자동 해석된 테스트 Target을 Hub에서 제거합니다.
        /// </summary>
        private static void ClearResolvedTestTargetFromHub()
        {
            var hub = SkillTestRuntimeHubEditorFacade.FindHub();
            if (hub == null)
                return;

            hub.ClearManualLockedTarget();
            hub.SetLockedTarget(null);
        }
    }
}
