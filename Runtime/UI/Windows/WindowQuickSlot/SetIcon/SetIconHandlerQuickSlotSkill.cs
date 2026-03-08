using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 퀵슬롯 윈도우 - 아이콘 관리
    /// </summary>
    public class SetIconHandlerQuickSlotSkill : ISetIconHandler
    {
        private TableSkill _tableSkill;
        private AddressableLoaderSkill _addressableLoaderSkill;
        private QuickSlotData _quickSlotData;
        
        public void OnSetIcon(UIWindow window, int slotIndex, int iconUid, int iconCount, int iconLevel, bool isLearned)
        {
            UIIcon icon = window.GetIconByIndex(slotIndex);
            if (icon == null) return;
            
            var uiIconQuickSlot = icon as UIIconQuickSlot;
            if (uiIconQuickSlot == null) return;
            
            // 장착 하기
            // 아이콘 정보 변경하기
            _tableSkill ??= TableLoaderManagerSkill.Instance.TableSkill;
            _addressableLoaderSkill ??= AddressableLoaderSkill.Instance;
            _quickSlotData ??= SceneGame.Instance.saveDataManager.QuickSlot;
            
            var info = _tableSkill.GetDataByUid(iconUid);
            if (info == null) return;
            
            // 다른 슬롯에 장착되어 있으면 삭제
            var existSlot = _quickSlotData.CheckSkill(iconUid);
            if (existSlot >= 0)
            {
                window.DetachIcon(existSlot);
                _quickSlotData.Remove(existSlot);
            }
            
            // 순서 중요. 다른 슬롯 삭제하고 저장하기
            _quickSlotData.SetIcon(slotIndex, icon.GetIconType(), iconUid, iconCount, iconLevel, isLearned);
            
            var sprite = _addressableLoaderSkill.GetSkillIconImageByName(info.IconFileName);
            // 아이콘 이미지 변경하기
            uiIconQuickSlot.ChangeIconImage(sprite);
        }
        public void OnDetachIcon(UIWindow window, int slotIndex)
        {
            UIIcon icon = window.GetIconByIndex(slotIndex);
            if (icon == null) return;
            _quickSlotData ??= SceneGame.Instance.saveDataManager.QuickSlot;
            _quickSlotData.Remove(slotIndex);
        }
    }
}