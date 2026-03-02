using System.Collections.Generic;
using Config;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 테이블 Structure
    /// </summary>
    public class StruckTableSkillPassive
    {
        public int Uid { get; set; }
        public string Name { get; set; }
        public string Memo;
        public ConfigCommonSkill.SkillKind SkillKind;
        public string IconFileName;
        /// <summary>패시브/옵션형 스킬이 참조하는 옵션 그룹 UID</summary>
        public int OptionGroupUid;
    }

    /// <summary>
    /// 스킬 테이블
    /// </summary>
    public class TableSkillPassive : DefaultTable<StruckTableSkillPassive>
    {
        public override string Key => ConfigAddressableTableSkill.SkillPassive;
        
        protected override StruckTableSkillPassive BuildRow(Dictionary<string, string> data)
        {
            int uid = MathHelper.ParseInt(data.GetValueOrDefault("Uid"));
            // 로컬라이즈된 이름/설명
            string name = data.GetValueOrDefault("Name");
            if (LocalizationManagerSkill.Instance != null)
            {
                name = LocalizationManagerSkill.Instance.GetPassiveSkillNameByKey(uid.ToString());
            }
            
            return new StruckTableSkillPassive
            {
                Uid = uid,
                Name = name,
                Memo = data["Memo"],
                IconFileName = data["IconFileName"],
                SkillKind = ConfigCommonSkill.SkillKind.Passive,
                OptionGroupUid = MathHelper.ParseInt(data["OptionGroupUid"]),
            };
        }
    }
}