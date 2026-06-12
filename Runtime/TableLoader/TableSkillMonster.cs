using System.Collections.Generic;
using Config;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어 엑티브 스킬 테이블 Structure
    /// </summary>
    public class StruckTableSkillMonster
    {
        public int Uid { get; set; }
        public string Name { get; set; }
        public string SoFileName;

        /// <summary>스킬 분류(Active/Passive)</summary>
        public ConfigCommonSkill.SkillKind SkillKind;

        /// <summary>캐스팅 시간(초). 0이면 즉시 사용.</summary>
        public float CastTime;
        
        public float CoolTime;

        /// <summary>스킬 데미지에 사용할 기본 데미지 타입입니다.</summary>
        public ConfigCommon.DamageType DamageType;

        /// <summary>스킬 데미지 이벤트가 Damage 값을 해석할 방식입니다.</summary>
        public ConfigCommonSkill.SkillDamageValueType DamageValueType;

        /// <summary>스킬 데미지 이벤트가 사용할 기본 데미지 값입니다.</summary>
        public long Damage;

        /// <summary>스킬 데미지 계산에 사용할 damage_formula 테이블의 FormulaKey입니다. 비어 있으면 기존 기본 공식을 사용합니다.</summary>
        public string DamageFormulaKey;

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

        /// <summary>UseClip 재생 속도와 스킬 런타임 시퀀스 시간축을 연결하는 정책입니다.</summary>
        public ConfigCommonSkill.SkillUseClipTimingPolicy UseClipTimingPolicy;

        /// <summary>UseClip 길이 동기화 기준 시간입니다. ScaleSequenceToUseClip 정책에서는 UseClipTimeScale이 직접 적용되므로 사용하지 않습니다.</summary>
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

        /// <summary>차징 전체가 진행되는 동안 재생할 사운드 UID입니다.</summary>
        public int ChargeSoundUid;

        /// <summary>차징 전체 사운드를 루프로 재생할지 여부입니다.</summary>
        public bool ChargeSoundLoop;

        /// <summary>스킬 실행 직전에 자동으로 맞출 방향 정책입니다.</summary>
        public ConfigCommonSkill.SkillFacingMode FacingMode;
    }

    /// <summary>
    /// 플레이어 엑티브 스킬 테이블
    /// </summary>
    public class TableSkillMonster : DefaultTable<StruckTableSkillMonster>
    {
        public override string Key => ConfigAddressableTableSkill.SkillMonster;
        
        protected override StruckTableSkillMonster BuildRow(Dictionary<string, string> data)
        {
            TableRowReader reader = ReadRow(data);
            int uid = reader.Int("Uid");
            
            return new StruckTableSkillMonster
            {
                Uid = uid,
                Name = reader.String("Name", ""),
                SoFileName = reader.String("SoFileName"),
                SkillKind = ConfigCommonSkill.SkillKind.Active,
                CastTime = reader.Float("CastTime"),
                CoolTime = reader.Float("CoolTime"),
                DamageType = reader.DamageType("DamageType"),
                DamageValueType = reader.Enum("DamageValueType", ConfigCommonSkill.SkillDamageValueType.Fixed),
                Damage = System.Math.Max(0L, reader.Long("Damage", reader.Long("damage", 0L))),
                DamageFormulaKey = reader.String("DamageFormulaKey", string.Empty),
                TargetingMode = reader.Enum<ConfigCommonSkill.SkillTargetingMode>("TargetingMode"),
                CastRange = reader.Float("CastRange"),
                PlacementRange = reader.Float("PlacementRange"),
                MaxTargets = reader.Int("MaxTargets"),
                CastStartClip = reader.String("CastStartClip"),
                CastLoopClip = reader.String("CastLoopClip"),
                CastEndClip = reader.String("CastEndClip"),
                UseCharge = reader.BoolYN("UseCharge"),
                ChargeGaugeMax = System.Math.Max(0f, reader.Float("ChargeGaugeMax", 0f)),
                ChargeGaugeDamagePerHit = System.Math.Max(0f, reader.Float("ChargeGaugeDamagePerHit", 1f)),
                ChargeCompleteClip = reader.String("ChargeCompleteClip", string.Empty),
                ChargeCompleteDurationSeconds = System.Math.Max(0f, reader.Float("ChargeCompleteDurationSeconds", 0f)),
                ChargeFailClip = reader.String("ChargeFailClip", string.Empty),
                ChargeFailDurationSeconds = System.Math.Max(0f, reader.Float("ChargeFailDurationSeconds", 0f)),
                ChargeSoundUid = System.Math.Max(0, reader.Int("ChargeSoundUid", 0)),
                ChargeSoundLoop = reader.BoolYN("ChargeSoundLoop"),
                UseClip = reader.String("UseClip"),
                UseClipTimeScale = System.Math.Max(0.001f, reader.Float("UseClipTimeScale", 1f)),
                UseClipTimingPolicy = reader.Enum<ConfigCommonSkill.SkillUseClipTimingPolicy>("UseClipTimingPolicy", ConfigCommonSkill.SkillUseClipTimingPolicy.RuntimeSequence),
                UseClipReferenceDurationSeconds = System.Math.Max(0f, reader.Float("UseClipReferenceDurationSeconds", 0f)),
                FacingMode = reader.Enum<ConfigCommonSkill.SkillFacingMode>("FacingMode")
            };
        }

    }
}
