using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 UI 에 스킬별 레벨 정보 저장
    /// </summary>
    public class SkillData : DefaultData, ISaveData
    {
        // public 으로 해야 json 으로 저장된다. 
        public Dictionary<int, SaveDataIcon> SkillDatas = new();

        // 패시브 스킬 레벨 정보
        public Dictionary<int, SaveDataIcon> SkillPassiveDatas = new();

        private const int IconType = (int)IconConstants.Type.Skill;
        private const int IconTypePassive = (int)IconConstants.Type.SkillPassive;
        
        /// <summary>
        /// 초기화. Awake 단계에서 실행
        /// </summary>
        /// <param name="loader"></param>
        /// <param name="saveDataContainer"></param>
        public void Initialize(TableLoaderManagerSkill loader, SaveDataContainerSkill saveDataContainer = null)
        {
            SkillDatas.Clear();
            SkillPassiveDatas.Clear();
            if (saveDataContainer?.SkillData != null)
            {
                SkillDatas = new Dictionary<int, SaveDataIcon>(saveDataContainer.SkillData.SkillDatas);
                if (saveDataContainer.SkillData.SkillPassiveDatas != null)
                {
                    SkillPassiveDatas =
                        new Dictionary<int, SaveDataIcon>(saveDataContainer.SkillData.SkillPassiveDatas);
                }
            }
        }

        protected override void SaveDatas()
        {
            SkillPackageManager.Instance.SaveDataManagerSkill.StartSaveData();
        }

        protected override int GetMaxSlotCount()
        {
            return SceneGame.Instance.uIWindowManager
                .GetUIWindowByUid<UIWindowSkill>(UIWindowConstants.WindowUid.Skill)?.maxCountIcon ?? 0;
        }
        
        /// <summary>
        /// 모든 스킬 목록 가져오기
        /// </summary>
        public Dictionary<int, SaveDataIcon> GetAllDatas()
        {
            return SkillDatas;
        }

        #region Active Skill
        /// <summary>
        /// 스킬 배움 여부 설정
        /// </summary>
        public ResultCommon SetSkillLearn(int slotIndex, int skillUid, int skillCount, int skillLevel, bool skillLearn)
        {
            if (skillUid <= 0)
            {
                return ResultCommon.Fail("QuickSlot_NoSkillInfo"); //$"스킬 정보가 없습니다."
            }
            var info = GetDataSkillBySlotIndex(slotIndex);
            if (info == null)
            {
                if (!SkillDatas.TryAdd(slotIndex,
                        new SaveDataIcon(slotIndex, skillUid, skillCount, skillLevel, skillLearn, iconType:IconType)))
                {
                    GcLogger.LogError($"스킬 배움 여부 저장 실패. slotIndex: {slotIndex} / skillUid: {skillUid}");
                }
            }
            else
            {
                info.SetIsLearn(skillLearn);
            }

            SaveDatas();
            List<SaveDataIcon> controls = new List<SaveDataIcon>
                { new(slotIndex, skillUid, skillCount, skillLevel, skillLearn, iconType:IconType) };
            return ResultCommon.SuccessWithIcons(controls);
        }

        public SaveDataIcon GetDataSkillBySlotIndex(int slotIndex)
        {
            return SkillDatas.GetValueOrDefault(slotIndex);
        }
        #endregion

        #region Passive Skill

        public SaveDataIcon GetDataSkillPassiveBySlotIndex(int slotIndex)
        {
            return SkillPassiveDatas.GetValueOrDefault(slotIndex);
        }
        public ResultCommon SetSkillPassiveLearn(int slotIndex, int skillUid, int skillCount, int skillLevel, bool skillLearn)
        {
            if (skillUid <= 0)
            {
                return ResultCommon.Fail("QuickSlot_NoSkillInfo"); //$"스킬 정보가 없습니다."
            }
            var info = GetDataSkillPassiveBySlotIndex(slotIndex);
            if (info == null)
            {
                if (!SkillPassiveDatas.TryAdd(slotIndex,
                        new SaveDataIcon(slotIndex, skillUid, skillCount, skillLevel, skillLearn, iconType:IconTypePassive)))
                {
                    GcLogger.LogError($"패시브 스킬 배움 여부 저장 실패. slotIndex: {slotIndex} / skillUid: {skillUid}");
                }
            }
            else
            {
                info.SetIsLearn(skillLearn);
            }

            SaveDatas();
            List<SaveDataIcon> controls = new List<SaveDataIcon>
                { new(slotIndex, skillUid, skillCount, skillLevel, skillLearn, iconType:IconTypePassive) };
            return ResultCommon.SuccessWithIcons(controls);
        }
        
        public Dictionary<int, SaveDataIcon> GetAllPassive()
        {
            return SkillPassiveDatas;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 패시브 스킬 사용 툴에서 호출 
        /// </summary>
        /// <param name="slotIndex"></param>
        /// <param name="skillUid"></param>
        /// <param name="skillCount"></param>
        /// <param name="skillLevel"></param>
        /// <param name="skillLearn"></param>
        public void SetPassiveEquip(int slotIndex, int skillUid, int skillCount, int skillLevel, bool skillLearn)
        {
            if (skillUid <= 0) return;
            SkillPassiveDatas[slotIndex] = new SaveDataIcon(slotIndex, skillUid, skillCount, skillLevel, skillLearn, iconType:IconTypePassive);
            SaveDatas();
        }

        /// <summary>
        /// 패시브 스킬 사용 툴에서 호출 
        /// </summary>
        /// <param name="slotIndex"></param>
        public void RemovePassiveEquip(int slotIndex)
        {
            if (!SkillPassiveDatas.ContainsKey(slotIndex)) return;
            SkillPassiveDatas.Remove(slotIndex);
            SaveDatas();
        }
#endif
        #endregion
    }
}