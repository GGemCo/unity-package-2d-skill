using Config;
using GGemCo2DCore;
using GGemCo2DSkill;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// CreateSkillWindow에서 player/monster 스킬 데이터를 공통 형식으로 다루기 위한 편집용 Row 모델입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 원본 테이블 구조체인 <see cref="StruckTableSkill"/> 및 <see cref="StruckTableSkillMonster"/>를
    /// 에디터 UI에서 동일한 방식으로 표시하고 수정할 수 있도록 필드를 통합합니다.
    /// </para>
    /// <para>
    /// 일부 필드는 플레이어 스킬에만 의미가 있으며, 몬스터 스킬에서 해당 값은 기본값으로 유지될 수 있습니다.
    /// </para>
    /// </remarks>
    public sealed class SkillEditorRow
    {
        /// <summary>
        /// 이 Row가 어떤 스킬 테이블(player/monster)에서 생성되었는지를 나타냅니다.
        /// </summary>
        public ConfigCommon.SkillTableSource Source;

        /// <summary>
        /// 스킬의 고유 식별자입니다.
        /// </summary>
        public int Uid { get; set; }

        /// <summary>
        /// 에디터에 표시할 스킬 이름입니다.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 스킬에 대한 메모 또는 설명입니다.
        /// </summary>
        public string Memo;

        /// <summary>
        /// 플레이어가 기본적으로 습득하는 스킬인지 여부입니다.
        /// </summary>
        public bool DefaultLearn;

        /// <summary>
        /// 스킬 습득 또는 사용에 필요한 플레이어 레벨입니다.
        /// </summary>
        public int NeedPlayerLevel;

        /// <summary>
        /// 스킬 아이콘 파일 이름입니다.
        /// </summary>
        public string IconFileName;

        /// <summary>
        /// 스킬 Runtime Sequence 파일 이름입니다.
        /// </summary>
        public string SoFileName;

        /// <summary>
        /// 스킬 종류를 나타냅니다.
        /// </summary>
        public ConfigCommonSkill.SkillKind SkillKind;

        /// <summary>
        /// 스킬 시전 시간입니다.
        /// </summary>
        public float CastTime;

        /// <summary>
        /// 스킬 재사용 대기시간입니다.
        /// </summary>
        public float CoolTime;

        /// <summary>
        /// 스킬 타게팅 방식입니다.
        /// </summary>
        public ConfigCommonSkill.SkillTargetingMode TargetingMode;

        /// <summary>
        /// 스킬 사거리 또는 적용 범위입니다.
        /// </summary>
        public float Range;

        /// <summary>
        /// 동시에 지정하거나 적용할 수 있는 최대 대상 수입니다.
        /// </summary>
        public int MaxTargets;

        /// <summary>
        /// 시전 시작 애니메이션 클립 이름입니다.
        /// </summary>
        public string CastStartClip;

        /// <summary>
        /// 시전 유지 애니메이션 클립 이름입니다.
        /// </summary>
        public string CastLoopClip;

        /// <summary>
        /// 시전 종료 애니메이션 클립 이름입니다.
        /// </summary>
        public string CastEndClip;

        /// <summary>
        /// 실제 사용 시 재생할 애니메이션 클립 이름입니다.
        /// </summary>
        public string UseClip;

        /// <summary>
        /// 플레이어 스킬 테이블 데이터를 편집용 Row 모델로 변환합니다.
        /// </summary>
        /// <param name="row">변환할 플레이어 스킬 원본 데이터입니다.</param>
        /// <returns>
        /// 변환된 <see cref="SkillEditorRow"/> 인스턴스를 반환합니다.
        /// 입력값이 <see langword="null"/>이면 <see langword="null"/>을 반환합니다.
        /// </returns>
        public static SkillEditorRow From(StruckTableSkill row)
        {
            if (row == null) return null;

            return new SkillEditorRow
            {
                Source = ConfigCommon.SkillTableSource.Player,
                Uid = row.Uid,
                Name = row.Name,
                Memo = row.Memo,
                DefaultLearn = row.DefaultLearn,
                NeedPlayerLevel = row.NeedPlayerLevel,
                IconFileName = row.IconFileName,
                SoFileName = row.SoFileName,
                SkillKind = row.SkillKind,
                CastTime = row.CastTime,
                CoolTime = row.CoolTime,
                TargetingMode = row.TargetingMode,
                Range = row.Range,
                MaxTargets = row.MaxTargets,
                CastStartClip = row.CastStartClip,
                CastLoopClip = row.CastLoopClip,
                CastEndClip = row.CastEndClip,
                UseClip = row.UseClip,
            };
        }

        /// <summary>
        /// 몬스터 스킬 테이블 데이터를 편집용 Row 모델로 변환합니다.
        /// </summary>
        /// <param name="row">변환할 몬스터 스킬 원본 데이터입니다.</param>
        /// <returns>
        /// 변환된 <see cref="SkillEditorRow"/> 인스턴스를 반환합니다.
        /// 입력값이 <see langword="null"/>이면 <see langword="null"/>을 반환합니다.
        /// </returns>
        /// <remarks>
        /// 몬스터 스킬에는 플레이어 전용 필드가 없을 수 있으므로,
        /// 해당 값들은 기본값으로 유지됩니다.
        /// </remarks>
        public static SkillEditorRow From(StruckTableSkillMonster row)
        {
            if (row == null) return null;

            return new SkillEditorRow
            {
                Source = ConfigCommon.SkillTableSource.Monster,
                Uid = row.Uid,
                Name = row.Name,
                Memo = row.Memo,
                SoFileName = row.SoFileName,
                SkillKind = row.SkillKind,
                CastTime = row.CastTime,
                CoolTime = row.CoolTime,
                TargetingMode = row.TargetingMode,
                Range = row.Range,
                MaxTargets = row.MaxTargets,
                CastStartClip = row.CastStartClip,
                CastLoopClip = row.CastLoopClip,
                CastEndClip = row.CastEndClip,
                UseClip = row.UseClip,
            };
        }
    }
}