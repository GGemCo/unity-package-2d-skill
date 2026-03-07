using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬 윈도우에서 퀵슬롯으로 드래그 앤 드랍 했을 때
    /// </summary>
    public class DragDropStrategyQuickSlotSkillPassive : IDragDropStrategy
    {
        private TableSkillPassive _tableSkillPassive;
        private AddressableLoaderSkill _addressableLoaderSkill;
        private QuickSlotData _quickSlotData;
        private PlayerPassiveSkillController _playerPassiveSkillController;
        
        public void HandleDragInIcon(UIWindow window, UIIcon droppedUIIcon, UIIcon targetUIIcon)
        {
            UIWindowQuickSlot uiWindowQuickSlot = window as UIWindowQuickSlot;
            if (uiWindowQuickSlot == null) return;
            
            UIWindow droppedWindow = droppedUIIcon.window;
            UIWindowConstants.WindowUid droppedWindowUid = droppedUIIcon.windowUid;
            int dropIconSlotIndex = droppedUIIcon.slotIndex;
            int dropIconUid = droppedUIIcon.uid;
            int dropIconLevel = droppedUIIcon.GetLevel();
            int dropIconCount = droppedUIIcon.GetCount();
            bool dropIconIsLearn = droppedUIIcon.IsLearn();
            if (dropIconUid <= 0)
            {
                return;
            }
            _tableSkillPassive ??= TableLoaderManagerSkill.Instance.TableSkillPassive;
            _addressableLoaderSkill ??= AddressableLoaderSkill.Instance;
            _quickSlotData ??= SceneGame.Instance.saveDataManager.QuickSlot;
            _playerPassiveSkillController ??= SceneGame.Instance.player.GetComponent<PlayerPassiveSkillController>();
            
            var info = _tableSkillPassive.GetDataByUid(dropIconUid);
            if (info == null) return;
            
            // 드래그앤 드랍 한 곳에 아무것도 없을때 
            if (targetUIIcon == null)
            {
                return;
            }
            UIWindow targetWindow = targetUIIcon.window;
            UIWindowConstants.WindowUid targetWindowUid = targetUIIcon.windowUid;
            int targetIconSlotIndex = targetUIIcon.slotIndex;
            int targetIconUid = targetUIIcon.uid;
            int targetIconCount = targetUIIcon.GetCount();

            var uiIconQuickSlot = targetUIIcon as UIIconQuickSlot;
            if (uiIconQuickSlot == null) return;
            
            // 장착 조건 정책 체크
            
            // 다른 슬롯에 장착되어 있으면 삭제
            var existSlot = _quickSlotData.CheckSkillPassive(dropIconUid);
            if (existSlot >= 0)
            {
                targetWindow.DetachIcon(existSlot);
            }
            
            // 퀵슬롯 정보 저장
            bool result = _quickSlotData.SetSkillPassive(targetIconSlotIndex, dropIconUid, dropIconCount, dropIconLevel, dropIconIsLearn);
            if (!result) return;
            
            // 장착 하기
            // 아이콘 정보 변경하기
            result = uiIconQuickSlot.ApplyEntry(IconConstants.Type.SkillPassive, dropIconUid, dropIconCount, dropIconLevel, dropIconIsLearn);
            if (!result) return;
            
            var sprite = _addressableLoaderSkill.GetSkillPassiveIconImageByName(info.IconFileName);
            // 아이콘 이미지 변경하기
            uiIconQuickSlot.ChangeIconImage(sprite);
            
            // 패시브 적용하기
            var dict = new Dictionary<int, int>();
            foreach (var kv in _quickSlotData.GetAllSkillPassive())
            {
                dict[kv.Key] = kv.Value;
            }
            _playerPassiveSkillController.ApplyEquippedPassives(dict);
        }

        public void HandleDragOut(UIWindow window, Vector3 worldPosition, GameObject droppedIcon, GameObject targetIcon, Vector3 originalPosition)
        {
            // 패시브 적용하기
            var dict = new Dictionary<int, int>();
            foreach (var kv in _quickSlotData.GetAllSkillPassive())
            {
                dict[kv.Key] = kv.Value;
            }
            _playerPassiveSkillController.ApplyEquippedPassives(dict);
        }
    }
}
