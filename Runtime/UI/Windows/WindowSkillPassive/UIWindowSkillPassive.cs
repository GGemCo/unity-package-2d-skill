using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬 장착 슬롯 윈도우
    /// - SkillData.PassiveEquipDatas(slotIndex -> SaveDataIcon)를 시각화한다.
    /// - 실제 장착/해제 로직은 SkillData 갱신 + CharacterPassiveSkillController.RefreshFromSaveData()로 반영한다.
    /// </summary>
    public class UIWindowSkillPassive : UIWindow
    {
        [Header(UIWindowConstants.TitleHeaderIndividual)] [Tooltip("패시브 스킬 Element 프리팹")]
        public GameObject prefabUIElementSkill;
        
        public TableSkillPassive TableSkillPassive;
        public readonly Dictionary<int, UIElementSkillPassive> UIElementPassiveSkills = new Dictionary<int, UIElementSkillPassive>();
        
        private QuickSlotData _quickSlotData;
        
        private UIWindowQuickSlot _uiWindowQuickSlot;
        private UIWindowSkillInfo _uIWindowSkillInfo;
        
        protected override void Awake()
        {
            UIElementPassiveSkills.Clear();
            uid = UIWindowConstants.WindowUid.PassiveSkill;
            if (TableLoaderManagerSkill.Instance == null) return;
            TableSkillPassive = TableLoaderManagerSkill.Instance.TableSkillPassive;
            maxCountIcon = TableSkillPassive.GetDatas().Count;

            // 기본 슬롯/아이콘 전략(DefaultSlotIconBuildStrategy)을 사용한다.
            if (iconType == IconConstants.Type.None)
                iconType = IconConstants.Type.Skill;
            // 순서 중요: IconPoolManager에서 사용 (슬롯 빌드 전략 등록 후 base.Awake 호출)
            SlotIconBuildStrategyRegistry.Register(uid, window => new SlotIconBuildStrategySkillPassive());

            base.Awake();

            IconPoolManager.SetSetIconHandler(new SetIconHandlerSkillPassive());
            DragDropHandler.SetStrategy(new DragDropStrategySkillPassive());
        }

        protected override void Start()
        {
            base.Start();
            _quickSlotData = SceneGame.saveDataManager.QuickSlot;
            _uIWindowSkillInfo =
                SceneGame.uIWindowManager.GetUIWindowByUid<UIWindowSkillInfo>(UIWindowConstants.WindowUid
                    .SkillInfo);
            _uiWindowQuickSlot =
                SceneGame.uIWindowManager.GetUIWindowByUid<UIWindowQuickSlot>(UIWindowConstants.WindowUid
                    .QuickSlot);
        }

        /// <summary>
        /// 슬롯 위치 정해주기
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="index"></param>
        public void SetPositionUiSlot(UISlot slot, int index)
        {
            UIElementSkillPassive uiElementSkillPassive = UIElementPassiveSkills[index];
            if (uiElementSkillPassive == null) return;
            Vector3 position = uiElementSkillPassive.GetIconPosition();
            if (position == Vector3.zero) return;
            slot.transform.localPosition = position;
        }
        public override void OnShow(bool show)
        {
            if (SceneGame.Instance == null || TableLoaderManager.Instance == null) return;
            if (!show)
            {
                _uIWindowSkillInfo?.Show(false);
                return;
            }

            LoadIcons();
        }
        /// <summary>
        /// 저장되어있는 패시브 장착 정보로 아이콘 셋팅
        /// </summary>
        public void LoadIcons()
        {
            if (!gameObject.activeSelf) return;

            var skillData = SkillPackageManager.Instance?.SaveDataManagerSkill?.Skill;
            if (skillData == null) return;

            var datas = skillData.GetAllPassiveEquips();
            for (int index = 0; index < maxCountIcon; index++)
            {
                if (index >= icons.Length) continue;
                var icon = icons[index];
                if (icon == null) continue;
                UIIconSkillPassive uiIcon = icon.GetComponent<UIIconSkillPassive>();
                if (uiIcon == null) continue;
                var saveDataIcon = datas.GetValueOrDefault(index);
                if (saveDataIcon == null) continue;

                int skillUid = saveDataIcon.Uid;
                int skillCount = saveDataIcon.Count;
                int skillLevel = saveDataIcon.Level;
                bool skillIsLearned = saveDataIcon.IsLearned;
                // todo. 정리 필요. 다음 Level 정보
                var info = TableSkillPassive.GetDataByUid(skillUid);
                if (info == null) continue;
                uiIcon.ChangeInfoByUid(skillUid, skillCount, skillLevel, skillIsLearned);
                UIElementSkillPassive uiElementPassiveSkill = UIElementPassiveSkills[index];
                if (uiElementPassiveSkill != null)
                {
                    uiElementPassiveSkill.UpdateInfos(info, saveDataIcon);
                }
            }
        }
        /// <summary>
        /// 아이콘 우클릭했을때 처리 
        /// </summary>
        /// <param name="icon"></param>
        public override void OnRightClick(UIIcon icon)
        {
            if (icon == null) return;
            AddToQuickSlot(icon);
        }

        public void AddToQuickSlot(UIIcon icon)
        {
            float time = SceneGame.uIIconCoolTimeManager.GetCurrentCoolTime(uid, icon.uid);
            if (time > 0)
            {
                SceneGame.systemMessageManager.ShowMessageWarning(
                    "Skill_CannotChangeDuringCooldown"); //"쿨타임 중에는 바꿀 수 없습니다."
                return;
            }

            if (!icon.IsLearn())
            {
                SceneGame.systemMessageManager.ShowMessageWarning("Skill_NotLearned"); //"배운 후 사용할 수 있습니다."
                return;
            }

            if (!icon.CheckRequireLevel()) return;
            if (_uiWindowQuickSlot == null) return;
            // 퀵슬롯에 하나 넣기
            var result = _quickSlotData.AddSkill(icon.uid, icon.GetCount(), icon.GetLevel(), icon.IsLearn());
            _uiWindowQuickSlot.SetIcons(result);
        }
        public UIElementSkillPassive GetElementSkillByIndex(int slotIndex)
        {
            return UIElementPassiveSkills[slotIndex];
        }
        /// <summary>
        /// 패시브 스킬을 첫 번째 빈 패시브 슬롯에 장착합니다.
        /// UI 버튼/컨텍스트 메뉴 등에서 호출하도록 설계합니다.
        /// </summary>
        public bool TryEquipPassiveSkill(int skillUid, int skillLevel)
        {
            if (SkillPackageManager.Instance == null || SkillPackageManager.Instance.SaveDataManagerSkill == null)
                return false;

            var tableInfo = TableSkillPassive?.GetDataByUid(skillUid);
            if (tableInfo == null)
                return false;

            if (tableInfo.SkillKind != ConfigCommonSkill.SkillKind.Passive)
                return false;

            var skillData = SkillPackageManager.Instance.SaveDataManagerSkill.Skill;
            if (skillData == null)
                return false;

            // 첫 빈 슬롯 찾기
            var passiveWindow = SceneGame.Instance.uIWindowManager
                .GetUIWindowByUid<UIWindowSkillPassive>(UIWindowConstants.WindowUid.PassiveSkill);

            int maxSlots = passiveWindow != null && passiveWindow.maxCountIcon > 0 ? passiveWindow.maxCountIcon : 8;
            for (int i = 0; i < maxSlots; i++)
            {
                var current = skillData.GetPassiveEquip(i);
                if (current == null || current.Uid <= 0)
                {
                    skillData.SetPassiveEquip(i, skillUid, 1, skillLevel, true);

                    // PassiveSkill 창이 열려 있으면 즉시 갱신
                    if (passiveWindow != null)
                        passiveWindow.LoadIcons();

                    // 실제 캐릭터 반영
                    if (SceneGame.Instance.player != null)
                    {
                        var ctrl = SceneGame.Instance.player.GetComponent<CharacterPassiveSkillController>();
                        if (ctrl != null)
                            ctrl.RefreshFromSaveData();
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
