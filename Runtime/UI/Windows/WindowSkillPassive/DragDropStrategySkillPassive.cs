using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬 장착 슬롯 - 드래그 앤 드랍 전략
    /// 현재 프로젝트의 기본 DragDropStrategy들이 비어있어, 최소 검증/차단만 수행합니다.
    /// (향후: Skill 윈도우에서 드래그하여 PassiveSkill 슬롯에 드랍하는 로직을 확장 가능)
    /// </summary>
    public class DragDropStrategySkillPassive : IDragDropStrategy
    {
        public void HandleDragInIcon(UIWindow window, UIIcon dropped, UIIcon target)
        {
            // 현재: 드래그&드랍 스왑/이동은 별도 정책이 정해지지 않아 동작을 강제하지 않습니다.
            // (아이콘 이동을 구현하려면 dropped/target의 정보를 읽고 window.SetIconCount를 호출하는 방식으로 확장)
        }

        public void HandleDragOut(UIWindow window, Vector3 worldPosition, GameObject droppedIcon, GameObject targetIcon, Vector3 originalPosition)
        {
            // 창 밖으로 드랍 시 해제 (옵션)
            // window.DetachIcon(droppedIcon.GetComponent<UIIcon>().slotIndex);
        }
    }
}
