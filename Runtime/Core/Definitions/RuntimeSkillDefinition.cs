using Config;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임에서 player/monster 스킬 테이블을 공통으로 다루기 위한 정의 모델입니다.
    /// </summary>
    public sealed class RuntimeSkillDefinition
    {
        public int Uid;
        public string Name;
        public string Memo;
        public string SoFileName;
        public ConfigCommonSkill.SkillOwnerType OwnerType;
        
        public ConfigCommonSkill.SkillKind SkillKind;
        public float CastTime;
        public float CoolTime;
        public int NeedMp;

        /// <summary>스킬 데미지 이벤트가 사용할 기본 데미지 타입입니다.</summary>
        public ConfigCommon.DamageType DamageType;

        /// <summary>스킬 데미지 이벤트가 Damage 값을 해석할 방식입니다.</summary>
        public ConfigCommonSkill.SkillDamageValueType DamageValueType;

        /// <summary>스킬 데미지 이벤트가 사용할 기본 데미지 값입니다.</summary>
        public long Damage;

        /// <summary>공중에서 플레이어 스킬을 시작했을 때 추가로 적용할 데미지 배율입니다. 1이면 보너스를 적용하지 않습니다.</summary>
        public float AirborneDamageMultiplier = 1f;

        public ConfigCommonSkill.SkillTargetingMode TargetingMode;

        /// <summary>스킬 사용 가능 거리입니다.</summary>
        public float CastRange;

        /// <summary>Projectile/Effect/Damage 중심점 계산에 사용하는 기본 배치 거리입니다.</summary>
        public float PlacementRange;

        public int MaxTargets;
        public string CastStartClip;
        public string CastLoopClip;
        public string CastEndClip;

        /// <summary>스킬 사용 단계에서 재생할 애니메이션 클립 이름입니다.</summary>
        public string UseClip;

        /// <summary>UseClip 애니메이션 재생 속도 배율입니다.</summary>
        public float UseClipTimeScale = 1f;

        /// <summary>UseClip 실제 재생 시간을 스킬 런타임 시퀀스에 반영하는 정책입니다.</summary>
        public ConfigCommonSkill.SkillUseClipTimingPolicy UseClipTimingPolicy;

        /// <summary>UseClip 시간 보정 기준이 되는 시퀀스 길이입니다. 0이면 RuntimeSequence.Duration을 사용합니다.</summary>
        public float UseClipReferenceDurationSeconds;

        public ConfigCommonSkill.SkillFacingMode FacingMode;

        /// <summary>스킬 사용 전 차징 설정입니다.</summary>
        public RuntimeSkillChargeDefinition Charge;

        public static RuntimeSkillDefinition From(StruckTableSkill row)
        {
            if (row == null) return null;
            return new RuntimeSkillDefinition
            {
                Uid = row.Uid,
                Name = row.Name,
                OwnerType = ConfigCommonSkill.SkillOwnerType.Player,
                SoFileName = row.SoFileName,
                SkillKind = row.SkillKind,
                CastTime = row.CastTime,
                CoolTime = row.CoolTime,
                NeedMp = row.NeedMp,
                DamageType = row.DamageType,
                DamageValueType = row.DamageValueType,
                Damage = row.Damage,
                AirborneDamageMultiplier = row.AirborneDamageMultiplier,
                TargetingMode = row.TargetingMode,
                CastRange = row.CastRange,
                PlacementRange = row.PlacementRange,
                MaxTargets = row.MaxTargets,
                CastStartClip = row.CastStartClip,
                CastLoopClip = row.CastLoopClip,
                CastEndClip = row.CastEndClip,
                UseClip = row.UseClip,
                UseClipTimeScale = row.UseClipTimeScale,
                UseClipTimingPolicy = row.UseClipTimingPolicy,
                UseClipReferenceDurationSeconds = row.UseClipReferenceDurationSeconds,
                FacingMode = row.FacingMode,
                Charge = RuntimeSkillChargeDefinition.From(row, TableLoaderManagerSkill.Instance != null ? TableLoaderManagerSkill.Instance.TableSkillChargeStage : null),
            };
        }

        public static RuntimeSkillDefinition From(StruckTableSkillMonster row)
        {
            if (row == null) return null;
            return new RuntimeSkillDefinition
            {
                Uid = row.Uid,
                Name = row.Name,
                OwnerType = ConfigCommonSkill.SkillOwnerType.Monster,
                SoFileName = row.SoFileName,
                SkillKind = row.SkillKind,
                CastTime = row.CastTime,
                CoolTime = row.CoolTime,
                NeedMp = 0,
                DamageType = row.DamageType,
                DamageValueType = row.DamageValueType,
                Damage = row.Damage,
                AirborneDamageMultiplier = 1f,
                TargetingMode = row.TargetingMode,
                CastRange = row.CastRange,
                PlacementRange = row.PlacementRange,
                MaxTargets = row.MaxTargets,
                CastStartClip = row.CastStartClip,
                CastLoopClip = row.CastLoopClip,
                CastEndClip = row.CastEndClip,
                UseClip = row.UseClip,
                UseClipTimeScale = row.UseClipTimeScale,
                UseClipTimingPolicy = row.UseClipTimingPolicy,
                UseClipReferenceDurationSeconds = row.UseClipReferenceDurationSeconds,
                FacingMode = row.FacingMode,
                Charge = RuntimeSkillChargeDefinition.From(row, TableLoaderManagerSkill.Instance != null ? TableLoaderManagerSkill.Instance.TableSkillChargeStage : null),
            };
        }
    }
}
