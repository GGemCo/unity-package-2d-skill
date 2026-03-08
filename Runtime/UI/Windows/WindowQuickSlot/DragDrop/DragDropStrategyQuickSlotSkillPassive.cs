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
            
            // 장착 조건 정책 체크
            //      배웠는지
            //      플레이어 레벨이 되는지 
            // 장착이 가능할 때, SetIcon 처리를 한다
            // 그러면 SetIcon 전략 패턴에서 스킬 전략으로 처리한다.

            // 아이콘 타입을 변경해주어야 한다.
            window.SetIconCount(targetIconSlotIndex, dropIconUid, dropIconCount, dropIconLevel, dropIconIsLearn, type: IconConstants.Type.SkillPassive);
            
        }

        public void HandleDragOut(UIWindow window, Vector3 worldPosition, GameObject droppedIcon, GameObject targetIcon, Vector3 originalPosition)
        {
            
        }
    }
}
