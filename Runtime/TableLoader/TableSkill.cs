using System.Collections.Generic;
using Config;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어 엑티브 스킬 테이블 Structure
    /// </summary>
    public class StruckTableSkill
    {
        public int Uid { get; set; }
        public string Name { get; set; }
        public bool DefaultLearn;
        public int NeedPlayerLevel;
        public string IconFileName;
        public string SoFileName;

        public ConfigCommonSkill.SkillKind SkillKind;

        /// <summary>캐스팅 시간(초). 0이면 즉시 사용.</summary>
        public float CastTime;
        
        public float CoolTime;

        /// <summary>스킬 사용에 필요한 MP입니다. 0이면 무비용으로 간주합니다.</summary>
        public int NeedMp;

        /// <summary>타겟팅 모드(스킬 패키지의 SkillTargetingMode enum 값을 int로 저장).</summary>
        public ConfigCommonSkill.SkillTargetingMode TargetingMode;

        /// <summary>스킬 사용 가능 거리입니다.</summary>
        public float CastRange;

        /// <summary>이벤트 기본 생성/배치 거리입니다.</summary>
        public float PlacementRange;

        /// <summary>최대 타겟 수</summary>
        public int MaxTargets;

        /// <summary>애니메이션 클립 이름 규칙: 캐스팅 시작</summary>
        public string CastStartClip;

        /// <summary>애니메이션 클립 이름 규칙: 캐스팅 루프</summary>
        public string CastLoopClip;

        /// <summary>애니메이션 클립 이름 규칙: 캐스팅 종료</summary>
        public string CastEndClip;

        /// <summary>애니메이션 클립 이름 규칙: 사용</summary>
        public string UseClip;

        /// <summary>UseClip 애니메이션 재생 속도 배율입니다. 1보다 크면 빠르게, 1보다 작으면 느리게 재생합니다.</summary>
        public float UseClipTimeScale;

        /// <summary>UseClip 실제 재생 시간을 스킬 런타임 시퀀스에 반영하는 정책입니다.</summary>
        public ConfigCommonSkill.SkillUseClipTimingPolicy UseClipTimingPolicy;

        /// <summary>UseClip 시간 보정 기준이 되는 시퀀스 길이입니다. 0이면 RuntimeSequence.Duration을 사용합니다.</summary>
        public float UseClipReferenceDurationSeconds;


        /// <summary>스킬 사용 전 차징 단계를 사용할지 여부입니다.</summary>
        public bool UseCharge;

        /// <summary>차징 중 피격으로 감소하는 차징 게이지 최대값입니다.</summary>
        public float ChargeGaugeMax;

        /// <summary>피격 1회당 감소시킬 차징 게이지 값입니다.</summary>
        public float ChargeGaugeDamagePerHit;

        /// <summary>차징 완료 후 실제 사용 단계로 넘어가기 전에 재생할 애니메이션 클립입니다.</summary>
        public string ChargeCompleteClip;

        /// <summary>차징 완료 애니메이션을 유지할 시간(초)입니다. 0이면 애니메이션 길이 또는 기본값을 사용합니다.</summary>
        public float ChargeCompleteDurationSeconds;

        /// <summary>차징 실패 시 재생할 애니메이션 클립입니다.</summary>
        public string ChargeFailClip;

        /// <summary>차징 실패 애니메이션을 유지할 시간(초)입니다. 0이면 애니메이션 길이 또는 기본값을 사용합니다.</summary>
        public float ChargeFailDurationSeconds;

        /// <summary>스킬 실행 직전에 자동으로 맞출 방향 정책입니다.</summary>
        public ConfigCommonSkill.SkillFacingMode FacingMode;
    }

    /// <summary>
    /// 플레이어 엑티브 스킬 테이블
    /// </summary>
    public class TableSkill : DefaultTable<StruckTableSkill>
    {
        public override string Key => ConfigAddressableTableSkill.Skill;
        
        protected override StruckTableSkill BuildRow(Dictionary<string, string> data)
        {
            int uid = MathHelper.ParseInt(data.GetValueOrDefault("Uid"));
            // 로컬라이즈된 이름/설명
            string name = data.GetValueOrDefault("Name");
            if (LocalizationManagerSkill.Instance != null)
            {
                name = LocalizationManagerSkill.Instance.GetSkillNameByKey(uid.ToString());
            }
            
            return new StruckTableSkill
            {
                Uid = uid,
                Name = name,
                DefaultLearn = ConvertBoolean(data["DefaultLearn"]),
                NeedPlayerLevel = MathHelper.ParseInt(data["NeedPlayerLevel"]),
                IconFileName = data["IconFileName"],
                SoFileName = data["SoFileName"],
                SkillKind = ConfigCommonSkill.SkillKind.Active,
                CastTime = MathHelper.ParseFloat(data["CastTime"]),
                CoolTime = MathHelper.ParseFloat(data["CoolTime"]),
                NeedMp = System.Math.Max(0, MathHelper.ParseInt(data.GetValueOrDefault("NeedMp", "0"))),
                TargetingMode = EnumHelper.ConvertEnum<ConfigCommonSkill.SkillTargetingMode>(data["TargetingMode"]),
                CastRange = MathHelper.ParseFloat(data["CastRange"]),
                PlacementRange = MathHelper.ParseFloat(data["PlacementRange"]),
                MaxTargets = MathHelper.ParseInt(data["MaxTargets"]),
                CastStartClip = data["CastStartClip"],
                CastLoopClip = data["CastLoopClip"],
                CastEndClip = data["CastEndClip"],
                UseCharge = ConvertBoolean(data.GetValueOrDefault("UseCharge", "N")),
                ChargeGaugeMax = System.Math.Max(0f, MathHelper.ParseFloat(data.GetValueOrDefault("ChargeGaugeMax", "0"))),
                ChargeGaugeDamagePerHit = System.Math.Max(0f, MathHelper.ParseFloat(data.GetValueOrDefault("ChargeGaugeDamagePerHit", "1"))),
                ChargeCompleteClip = data.GetValueOrDefault("ChargeCompleteClip", string.Empty),
                ChargeCompleteDurationSeconds = System.Math.Max(0f, MathHelper.ParseFloat(data.GetValueOrDefault("ChargeCompleteDurationSeconds", "0"))),
                ChargeFailClip = data.GetValueOrDefault("ChargeFailClip", string.Empty),
                ChargeFailDurationSeconds = System.Math.Max(0f, MathHelper.ParseFloat(data.GetValueOrDefault("ChargeFailDurationSeconds", "0"))),
                UseClip = data["UseClip"],
                UseClipTimeScale = System.Math.Max(0.001f, GetFloat(data, "UseClipTimeScale", 1f)),
                UseClipTimingPolicy = GetEnum(data, "UseClipTimingPolicy", ConfigCommonSkill.SkillUseClipTimingPolicy.RuntimeSequence),
                UseClipReferenceDurationSeconds = System.Math.Max(0f, GetFloat(data, "UseClipReferenceDurationSeconds", 0f)),
                FacingMode = EnumHelper.ConvertEnum<ConfigCommonSkill.SkillFacingMode>(data["FacingMode"])
            };
        }

        /// <summary>
        /// 선택 컬럼의 float 값을 읽습니다. 컬럼이 없거나 값이 비어 있으면 기본값을 반환합니다.
        /// </summary>
        /// <param name="data">테이블 Row 원본 데이터입니다.</param>
        /// <param name="key">조회할 컬럼 이름입니다.</param>
        /// <param name="fallback">컬럼이 없거나 비어 있을 때 사용할 기본값입니다.</param>
        /// <returns>파싱된 float 값입니다.</returns>
        private static float GetFloat(Dictionary<string, string> data, string key, float fallback)
        {
            string value = data.GetValueOrDefault(key, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? fallback : MathHelper.ParseFloat(value);
        }

        /// <summary>
        /// 선택 컬럼의 enum 값을 읽습니다. 컬럼이 없거나 값이 비어 있으면 기본값을 반환합니다.
        /// </summary>
        /// <typeparam name="TEnum">변환할 enum 타입입니다.</typeparam>
        /// <param name="data">테이블 Row 원본 데이터입니다.</param>
        /// <param name="key">조회할 컬럼 이름입니다.</param>
        /// <param name="fallback">컬럼이 없거나 비어 있을 때 사용할 기본값입니다.</param>
        /// <returns>파싱된 enum 값입니다.</returns>
        private static TEnum GetEnum<TEnum>(Dictionary<string, string> data, string key, TEnum fallback) where TEnum : struct, System.Enum
        {
            string value = data.GetValueOrDefault(key, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? fallback : EnumHelper.ConvertEnum<TEnum>(value);
        }

    }
}