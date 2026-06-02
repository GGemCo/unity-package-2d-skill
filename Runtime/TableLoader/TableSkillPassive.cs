using System.Collections.Generic;
using Config;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬 테이블 Structure
    /// </summary>
    public class StruckTableSkillPassive : IUidName
    {
        public int Uid { get; set; }
        public string Name { get; set; }
        public string Memo;
        public bool DefaultLearn;
        public int NeedPlayerLevel;
        public ConfigCommonSkill.SkillKind SkillKind;
        public string IconFileName;
    }

    /// <summary>
    /// 패시브 스킬 테이블
    /// </summary>
    public class TableSkillPassive : DefaultTable<StruckTableSkillPassive>
    {
        public override string Key => ConfigAddressableTableSkill.SkillPassive;

        protected override StruckTableSkillPassive BuildRow(Dictionary<string, string> data)
        {
            TableRowReader reader = ReadRow(data);
            int uid = reader.Int("Uid");

            string name = reader.String("Name");
            if (LocalizationManagerSkill.Instance != null)
            {
                name = LocalizationManagerSkill.Instance.GetPassiveSkillNameByKey(uid.ToString());
            }

            return new StruckTableSkillPassive
            {
                Uid = uid,
                Name = name,
                Memo = reader.String("Memo", string.Empty),
                DefaultLearn = reader.BoolYN("DefaultLearn"),
                NeedPlayerLevel = reader.Int("NeedPlayerLevel", 0),
                IconFileName = reader.String("IconFileName", string.Empty),
                SkillKind = ConfigCommonSkill.SkillKind.Passive,
            };
        }
    }
}
