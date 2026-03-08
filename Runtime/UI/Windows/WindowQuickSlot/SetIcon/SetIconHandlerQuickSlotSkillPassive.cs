using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 퀵슬롯 윈도우 - 아이콘 관리
    /// </summary>
    public class SetIconHandlerQuickSlotSkillPassive : ISetIconHandler
    {
        private TableSkillPassive _tableSkillPassive;
        private AddressableLoaderSkill _addressableLoaderSkill;
        private QuickSlotData _quickSlotData;
        private PlayerPassiveSkillController _playerPassiveSkillController;
        
        public void OnSetIcon(UIWindow window, int slotIndex, int iconUid, int iconCount, int iconLevel, bool isLearned, IconConstants.Type iconType)
        {
            UIIcon icon = window.GetIconByIndex(slotIndex);
            if (icon == null) return;
            var uiIconQuickSlot = icon as UIIconQuickSlot;
            if (uiIconQuickSlot == null) return;
            
            _tableSkillPassive ??= TableLoaderManagerSkill.Instance.TableSkillPassive;
            _addressableLoaderSkill ??= AddressableLoaderSkill.Instance;
            _quickSlotData ??= SceneGame.Instance.saveDataManager.QuickSlot;
            if (SceneGame.Instance && SceneGame.Instance.player)
                _playerPassiveSkillController ??= SceneGame.Instance.player.GetComponent<PlayerPassiveSkillController>();
            
            var info = _tableSkillPassive.GetDataByUid(iconUid);
            if (info == null) return;
            
            // 다른 슬롯에 장착되어 있으면 삭제
            var existSlot = _quickSlotData.CheckSkillPassive(iconUid);
            if (existSlot >= 0)
            {
                window.DetachIcon(existSlot);
                _quickSlotData.Remove(existSlot);
            }
            
            // 순서 중요. 다른 슬롯 삭제하고 저장하기
            _quickSlotData.SetIcon(slotIndex, iconUid, iconCount, iconLevel, isLearned, iconType);
            
            var sprite = _addressableLoaderSkill.GetSkillPassiveIconImageByName(info.IconFileName);
            // 아이콘 이미지 변경하기
            uiIconQuickSlot.ChangeIconImage(sprite);
            
            // 패시브 적용하기
            var dict = new Dictionary<int, int>();
            foreach (var kv in _quickSlotData.GetAllSkillPassive())
            {
                dict[kv.Key] = kv.Value;
            }
            _playerPassiveSkillController?.ApplyEquippedPassives(dict);
        }
        public void OnDetachIcon(UIWindow window, int slotIndex)
        {
            UIIcon icon = window.GetIconByIndex(slotIndex);
            if (icon == null) return;
            _quickSlotData ??= SceneGame.Instance.saveDataManager.QuickSlot;
            if (SceneGame.Instance && SceneGame.Instance.player)
                _playerPassiveSkillController ??= SceneGame.Instance.player.GetComponent<PlayerPassiveSkillController>();
            
            _quickSlotData.Remove(slotIndex);
            
            // 패시브 해제하기
            var dict = new Dictionary<int, int>();
            foreach (var kv in _quickSlotData.GetAllSkillPassive())
            {
                dict[kv.Key] = kv.Value;
            }
            _playerPassiveSkillController?.ApplyEquippedPassives(dict);
        }
    }
}