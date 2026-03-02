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

        // 패시브 스킬 장착 슬롯 데이터 (slotIndex -> SaveDataIcon)
        public Dictionary<int, SaveDataIcon> PassiveEquipDatas = new();

        /// <summary>
        /// 초기화. Awake 단계에서 실행
        /// </summary>
        /// <param name="loader"></param>
        /// <param name="saveDataContainer"></param>
        public void Initialize(TableLoaderManager loader, SaveDataContainerSkill saveDataContainer = null)
        {
            SkillDatas.Clear();
            PassiveEquipDatas.Clear();
            if (saveDataContainer?.SkillData != null)
            {
                SkillDatas = new Dictionary<int, SaveDataIcon>(saveDataContainer.SkillData.SkillDatas);
                if (saveDataContainer.SkillData.PassiveEquipDatas != null)
                {
                    PassiveEquipDatas =
                        new Dictionary<int, SaveDataIcon>(saveDataContainer.SkillData.PassiveEquipDatas);
                }
            }
        }

        protected override int GetMaxSlotCount()
        {
            return SceneGame.Instance.uIWindowManager
                .GetUIWindowByUid<UIWindowSkill>(UIWindowConstants.WindowUid.Skill)?.maxCountIcon ?? 0;
        }

        /// <summary>
        /// 스킬 레벨업
        /// </summary>
        /// <param name="slotIndex"></param>
        /// <param name="skillUid"></param>
        /// <param name="skillCount"></param>
        /// <param name="skillLevel"></param>
        /// <param name="skillLearn"></param>
        public ResultCommon SetSkillLevelUp(int slotIndex, int skillUid, int skillCount, int skillLevel,
            bool skillLearn)
        {
            if (skillUid <= 0)
            {
                return ResultCommon.Fail($"QuickSlot_NoSkillInfo"); //$"스킬 정보가 없습니다."
            }

            if (!SkillDatas.ContainsKey(slotIndex))
            {
                return ResultCommon.Fail($"QuickSlot_SkillNotLearned"); //$"아직 스킬을 배우지 않았습니다."
            }

            List<SaveDataIcon> controls = new List<SaveDataIcon>
                { new(slotIndex, skillUid, skillCount, skillLevel, skillLearn) };

            SaveDatas();
            return ResultCommon.SuccessWithIcons(controls);
        }

        /// <summary>
        /// 스킬 설정
        /// </summary>
        public void SetSkill(int slotIndex, int skillUid, int skillCount, int skillLevel, bool skillLearn)
        {
            if (skillUid <= 0) return;

            SkillDatas[slotIndex] = new SaveDataIcon(slotIndex, skillUid, skillCount, skillLevel, skillLearn);
            SaveDatas();
        }

        /// <summary>
        /// 스킬 레벨 설정
        /// </summary>
        public ResultCommon SetSkillLearn(int slotIndex, int skillUid, int skillCount, int skillLevel, bool skillLearn)
        {
            if (skillUid <= 0)
            {
                return ResultCommon.Fail("QuickSlot_NoSkillInfo"); //$"스킬 정보가 없습니다."
            }

            List<SaveDataIcon> controls = new List<SaveDataIcon>
                { new(slotIndex, skillUid, skillCount, skillLevel, skillLearn) };
            SaveDatas();
            return ResultCommon.SuccessWithIcons(controls);
        }

        /// <summary>
        /// 모든 스킬 목록 가져오기
        /// </summary>
        public Dictionary<int, SaveDataIcon> GetAllDatas()
        {
            return SkillDatas;
        }

        public SaveDataIcon GetData(int slotIndex)
        {
            return SkillDatas.GetValueOrDefault(slotIndex);
        }


        #region Passive Equip

        public void SetPassiveEquip(int slotIndex, int skillUid, int skillCount, int skillLevel, bool skillLearn)
        {
            if (skillUid <= 0) return;
            PassiveEquipDatas[slotIndex] = new SaveDataIcon(slotIndex, skillUid, skillCount, skillLevel, skillLearn);
            SaveDatas();
        }

        public void RemovePassiveEquip(int slotIndex)
        {
            if (!PassiveEquipDatas.ContainsKey(slotIndex)) return;
            PassiveEquipDatas.Remove(slotIndex);
            SaveDatas();
        }

        public Dictionary<int, SaveDataIcon> GetAllPassiveEquips()
        {
            return PassiveEquipDatas;
        }

        public SaveDataIcon GetPassiveEquip(int slotIndex)
        {
            return PassiveEquipDatas.GetValueOrDefault(slotIndex);
        }

        /// <summary>
        /// 장착된 패시브 목록을 (skillUid -> level) 형태로 반환합니다.
        /// 슬롯 중복 장착 시 가장 높은 레벨을 우선합니다.
        /// </summary>
        public Dictionary<int, int> BuildEquippedPassiveSkillLevels()
        {
            var result = new Dictionary<int, int>();
            foreach (var kv in PassiveEquipDatas)
            {
                var v = kv.Value;
                if (v == null) continue;
                if (v.Uid <= 0) continue;

                if (result.TryGetValue(v.Uid, out var prev))
                {
                    if (v.Level > prev) result[v.Uid] = v.Level;
                }
                else
                {
                    result[v.Uid] = v.Level;
                }
            }

            return result;
        }

        #endregion

    }
}