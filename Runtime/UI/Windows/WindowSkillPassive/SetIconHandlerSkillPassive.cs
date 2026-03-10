using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬 장착 슬롯 - 아이콘 세팅/해제 시 세이브 데이터 반영
    /// </summary>
    public class SetIconHandlerSkillPassive : ISetIconHandler
    {
        public void OnSetIcon(UIWindow window, int slotIndex, int iconUid, int iconCount, int iconLevel, bool isLearned, IconConstants.Type iconType)
        {
        }

        public void OnDetachIcon(UIWindow window, int slotIndex)
        {
        }
    }
}