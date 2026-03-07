using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 윈도우에서 퀵슬롯 윈도우로 드래그 앤 드랍 했을 때
    /// </summary>
    public class DragDropStrategyQuickSlotSkill : IDragDropStrategy
    {
        private TableSkill _tableSkill;
        private AddressableLoaderSkill _addressableLoaderSkill;
        private QuickSlotData _quickSlotData;
        
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
            _tableSkill ??= TableLoaderManagerSkill.Instance.TableSkill;
            _addressableLoaderSkill ??= AddressableLoaderSkill.Instance;
            _quickSlotData ??= SceneGame.Instance.saveDataManager.QuickSlot;
            
            var info = _tableSkill.GetDataByUid(dropIconUid);
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
            
            // 장착 하기
            // 아이콘 정보 변경하기
            var result = uiIconQuickSlot.ApplyEntry(IconConstants.Type.Skill, dropIconUid, dropIconCount, dropIconLevel, dropIconIsLearn);
            if (!result) return;
            
            var sprite = _addressableLoaderSkill.GetSkillIconImageByName(info.IconFileName);
            // 아이콘 이미지 변경하기
            uiIconQuickSlot.ChangeIconImage(sprite);
            
            // 퀵슬롯 정보 저장
            _quickSlotData?.SetSkill(targetIconSlotIndex, dropIconUid, dropIconCount, dropIconLevel, dropIconIsLearn);
        }

        public void HandleDragOut(UIWindow window, Vector3 worldPosition, GameObject droppedIcon, GameObject targetIcon, Vector3 originalPosition)
        {
            // 창 밖으로 드랍 시 해제 (옵션)
            // window.DetachIcon(droppedIcon.GetComponent<UIIcon>().slotIndex);
        }
    }
}
