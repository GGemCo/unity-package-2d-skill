using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Skill 패키지의 skill 테이블을 사용해 Core UI에 NeedMp 값을 제공합니다.
    /// </summary>
    internal sealed class SkillNeedMpProvider : ISkillNeedMpProvider
    {
        /// <inheritdoc />
        public bool TryGetNeedMp(int skillUid, out int needMp)
        {
            needMp = 0;
            if (skillUid <= 0)
                return false;

            TableSkill tableSkill = TableLoaderManagerSkill.Instance != null
                ? TableLoaderManagerSkill.Instance.TableSkill
                : null;
            if (tableSkill == null)
                return false;

            StruckTableSkill skill = tableSkill.GetDataByUid(skillUid);
            if (skill == null || skill.Uid <= 0)
                return false;

            needMp = System.Math.Max(0, skill.NeedMp);
            return true;
        }
    }
}
