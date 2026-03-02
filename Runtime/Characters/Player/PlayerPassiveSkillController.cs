namespace GGemCo2DSkill
{
    public class PlayerPassiveSkillController : CharacterPassiveSkillController
    {
        
        protected override void Start()
        {
            RefreshFromSaveData();
        }

        /// <summary>
        /// 세이브 데이터에 저장된 패시브 장착 정보를 다시 읽어서 적용합니다.
        /// - 내부적으로 <see cref="SkillPackageManager"/>의 <see cref="SaveDataManagerSkill"/>을 참조합니다.
        /// </summary>
        public override void RefreshFromSaveData()
        {
            var mgr = SkillPackageManager.Instance?.SaveDataManagerSkill;
            RefreshFromSaveData(mgr?.Skill);
        }
        /// <summary>
        /// 전달된 <see cref="SkillData"/>의 패시브 장착 정보를 적용합니다.
        /// </summary>
        private void RefreshFromSaveData(SkillData skillData)
        {
            if (skillData == null)
            {
                Clear();
                return;
            }

            ApplyEquippedPassives(skillData.BuildEquippedPassiveSkillLevels());
        }

    }
}