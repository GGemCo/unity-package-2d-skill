using Config;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.UI;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어 스킬 윈도우 - 아이콘 생성
    /// </summary>
    public class SlotIconBuildStrategySkillPassive : ISlotIconBuildStrategy
    {
        public void BuildSlotsAndIcons(UIWindow window, GridLayoutGroup container, int maxCount,
            IconConstants.Type iconType, Vector2 slotSize, Vector2 iconSize, GameObject[] slots, GameObject[] icons)
        {
            if (AddressableLoaderSettings.Instance == null || window.containerIcon == null) return;
            UIWindowSkillPassive uiWindowSkillPassive = window as UIWindowSkillPassive;
            if (uiWindowSkillPassive == null) return;
            GameObject prefabUIElementSkill = uiWindowSkillPassive.prefabUIElementSkill;
            if (prefabUIElementSkill == null)
            {
                GcLogger.LogError("UIElementSkill 프리팹이 없습니다.");
                return;
            }
            var datas = uiWindowSkillPassive.TableSkillPassive.GetDatas();
            uiWindowSkillPassive.maxCountIcon = datas.Count;
            if (datas.Count <= 0) return;
            
            GameObject iconSkill = window.iconPrefab != null ? window.iconPrefab : IconConstants.LoadByIconType(iconType);
            GameObject slot = window.slotPrefab != null ? window.slotPrefab : ConfigResources.Slot.Load();
            if (iconSkill == null) return;

            int index = 0;
            foreach (var data in datas)
            {
                int skillUid = data.Key;
                if (skillUid <= 0) continue;
                var info = data.Value;

                GameObject parent = uiWindowSkillPassive.gameObject;
                // UI Element 프리팹이 있으면 만든다.
                if (prefabUIElementSkill != null)
                {
                    parent = Object.Instantiate(prefabUIElementSkill, uiWindowSkillPassive.containerIcon.gameObject.transform);
                    if (parent == null) continue;
                    UIElementSkillPassive uiElementSkillPassive = parent.GetComponent<UIElementSkillPassive>();
                    if (uiElementSkillPassive == null) continue;
                    uiElementSkillPassive.Initialize(uiWindowSkillPassive, index, info);
                    uiWindowSkillPassive.UIElementPassiveSkills.TryAdd(index, uiElementSkillPassive);
                }

                GameObject slotObject = Object.Instantiate(slot, parent.transform);
                UISlot uiSlot = slotObject.GetComponent<UISlot>();
                if (uiSlot == null) continue;
                uiSlot.Initialize(uiWindowSkillPassive, uiWindowSkillPassive.uid, index, slotSize);
                uiWindowSkillPassive.SetPositionUiSlot(uiSlot, index);
                slots[index] = slotObject;
                
                GameObject icon = Object.Instantiate(iconSkill, slotObject.transform);
                UIIconSkillPassive uiIcon = icon.GetComponent<UIIconSkillPassive>();
                if (uiIcon == null) continue;
                uiIcon.Initialize(uiWindowSkillPassive, uiWindowSkillPassive.uid, index, index, iconSize, slotSize);
                // count, 레벨 1로 초기화
                uiIcon.ChangeInfoByUid(skillUid, 1, 1);
                
                icons[index] = icon;
                index++;
            }
        }
    }
}