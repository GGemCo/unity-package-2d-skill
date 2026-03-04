using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬 장착 슬롯 - 아이콘 세팅/해제 시 세이브 데이터 반영
    /// </summary>
    public class SetIconHandlerSkillPassive : ISetIconHandler
    {
        public void OnSetIcon(UIWindow window, int slotIndex, int iconUid, int iconCount, int iconLevel, bool isLearned)
        {
            var skillData = SkillPackageManager.Instance?.SaveDataManagerSkill?.Skill;
            if (skillData == null) return;

            // UI 장착 시에만 "임시 HP Current도 채움" 정책을 적용합니다.
            // (기본 런타임 정책은 임시 최대 HP 변경 시 Current를 자동 충전하지 않습니다.)
            var player = SceneGame.Instance.player.GetComponent<Player>();
            var before = PassiveTempHpFillUtility.Capture(player);

            skillData.SetPassiveEquip(slotIndex, iconUid, iconCount, iconLevel, isLearned);
            RefreshPlayerPassiveController();

            PassiveTempHpFillUtility.FillCurrentIfTempMaxIncreased(player, before);
        }

        public void OnDetachIcon(UIWindow window, int slotIndex)
        {
            var skillData = SkillPackageManager.Instance?.SaveDataManagerSkill?.Skill;
            if (skillData == null) return;

            skillData.RemovePassiveEquip(slotIndex);
            RefreshPlayerPassiveController();
        }

        private static void RefreshPlayerPassiveController()
        {
            if (SceneGame.Instance == null || SceneGame.Instance.player == null) return;
            var ctrl = SceneGame.Instance.player.GetComponent<CharacterPassiveSkillController>();
            if (ctrl == null) return;
            ctrl.RefreshFromSaveData();
        }
    }
}