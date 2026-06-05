namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬 장착 항목 하나를 표현합니다.
    /// 같은 스킬 UID가 여러 슬롯에 들어올 수 있으므로 슬롯 단위 엔트리로 보관합니다.
    /// </summary>
    public readonly struct PassiveSkillLoadoutEntry
    {
        /// <summary>
        /// 패시브 스킬 장착 항목을 생성합니다.
        /// </summary>
        /// <param name="skillUid">패시브 스킬 UID입니다.</param>
        /// <param name="level">적용할 패시브 스킬 레벨입니다.</param>
        public PassiveSkillLoadoutEntry(int skillUid, int level)
        {
            SkillUid = skillUid;
            Level = level;
        }

        /// <summary>
        /// 패시브 스킬 UID입니다.
        /// </summary>
        public int SkillUid { get; }

        /// <summary>
        /// 적용할 패시브 스킬 레벨입니다.
        /// </summary>
        public int Level { get; }
    }
}
