using System.Collections.Generic;
using System.Linq;
using Config;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임 스킬 정의에 연결되는 차징 전체 설정입니다.
    /// </summary>
    public sealed class RuntimeSkillChargeDefinition
    {
        /// <summary>차징 사용 여부입니다.</summary>
        public bool UseCharge;

        /// <summary>차징 중 피격으로 감소하는 게이지 최대값입니다.</summary>
        public float GaugeMax;

        /// <summary>피격 1회당 감소시킬 게이지 값입니다.</summary>
        public float GaugeDamagePerHit;

        /// <summary>차징 완료 후 실제 사용 단계로 넘어가기 전에 재생할 애니메이션 클립입니다.</summary>
        public string CompleteClip;

        /// <summary>차징 실패 시 재생할 애니메이션 클립입니다.</summary>
        public string FailClip;

        /// <summary>차징 실패 애니메이션을 유지할 시간(초)입니다.</summary>
        public float FailDurationSeconds;

        /// <summary>단계별 차징 정의 목록입니다.</summary>
        public RuntimeSkillChargeStageDefinition[] Stages = System.Array.Empty<RuntimeSkillChargeStageDefinition>();

        /// <summary>실제 차징 실행이 가능한 설정인지 여부입니다.</summary>
        public bool IsEnabled => UseCharge && Stages != null && Stages.Length > 0;

        /// <summary>전체 차징 시간(초)입니다.</summary>
        public float TotalDurationSeconds
        {
            get
            {
                if (Stages == null || Stages.Length == 0)
                    return 0f;

                float total = 0f;
                for (int i = 0; i < Stages.Length; i++)
                    total += System.Math.Max(0f, Stages[i]?.DurationSeconds ?? 0f);
                return total;
            }
        }

        /// <summary>
        /// 플레이어 스킬 Row와 차징 단계 테이블을 런타임 정의로 변환합니다.
        /// </summary>
        /// <param name="row">플레이어 스킬 Row입니다.</param>
        /// <param name="stageTable">차징 단계 테이블입니다.</param>
        /// <returns>차징 런타임 정의입니다.</returns>
        public static RuntimeSkillChargeDefinition From(StruckTableSkill row, TableSkillChargeStage stageTable)
        {
            if (row == null)
                return CreateEmpty();

            return Create(
                row.UseCharge,
                row.ChargeGaugeMax,
                row.ChargeGaugeDamagePerHit,
                row.ChargeCompleteClip,
                row.ChargeFailClip,
                row.ChargeFailDurationSeconds,
                stageTable,
                row.Uid,
                ConfigCommonSkill.SkillOwnerType.Player);
        }

        /// <summary>
        /// 몬스터 스킬 Row와 차징 단계 테이블을 런타임 정의로 변환합니다.
        /// </summary>
        /// <param name="row">몬스터 스킬 Row입니다.</param>
        /// <param name="stageTable">차징 단계 테이블입니다.</param>
        /// <returns>차징 런타임 정의입니다.</returns>
        public static RuntimeSkillChargeDefinition From(StruckTableSkillMonster row, TableSkillChargeStage stageTable)
        {
            if (row == null)
                return CreateEmpty();

            return Create(
                row.UseCharge,
                row.ChargeGaugeMax,
                row.ChargeGaugeDamagePerHit,
                row.ChargeCompleteClip,
                row.ChargeFailClip,
                row.ChargeFailDurationSeconds,
                stageTable,
                row.Uid,
                ConfigCommonSkill.SkillOwnerType.Monster);
        }

        private static RuntimeSkillChargeDefinition CreateEmpty()
        {
            return new RuntimeSkillChargeDefinition
            {
                UseCharge = false,
                GaugeMax = 0f,
                GaugeDamagePerHit = 0f,
                CompleteClip = string.Empty,
                FailClip = string.Empty,
                FailDurationSeconds = 0f,
                Stages = System.Array.Empty<RuntimeSkillChargeStageDefinition>()
            };
        }

        private static RuntimeSkillChargeDefinition Create(
            bool useCharge,
            float gaugeMax,
            float gaugeDamagePerHit,
            string completeClip,
            string failClip,
            float failDurationSeconds,
            TableSkillChargeStage stageTable,
            int skillUid,
            ConfigCommonSkill.SkillOwnerType ownerType)
        {
            IReadOnlyList<StruckTableSkillChargeStage> rows = stageTable != null
                ? stageTable.GetStages(skillUid, ownerType)
                : System.Array.Empty<StruckTableSkillChargeStage>();

            var stages = rows
                .Where(row => row != null && row.DurationSeconds > 0f)
                .Select(RuntimeSkillChargeStageDefinition.From)
                .ToArray();

            return new RuntimeSkillChargeDefinition
            {
                UseCharge = useCharge,
                GaugeMax = System.Math.Max(0f, gaugeMax),
                GaugeDamagePerHit = System.Math.Max(0f, gaugeDamagePerHit),
                CompleteClip = completeClip ?? string.Empty,
                FailClip = failClip ?? string.Empty,
                FailDurationSeconds = System.Math.Max(0f, failDurationSeconds),
                Stages = stages
            };
        }
    }

    /// <summary>
    /// 차징 한 단계의 런타임 정의입니다.
    /// </summary>
    public sealed class RuntimeSkillChargeStageDefinition
    {
        /// <summary>테이블 행 UID입니다.</summary>
        public int Uid;

        /// <summary>차징 단계 순서입니다.</summary>
        public int StageIndex;

        /// <summary>이 단계에서 유지할 시간(초)입니다.</summary>
        public float DurationSeconds;

        /// <summary>이 단계에서 루프로 재생할 애니메이션 클립입니다.</summary>
        public string LoopClip;

        /// <summary>이 단계에서 생성할 VFX UID입니다.</summary>
        public int VfxUid;

        /// <summary>VFX Follow 모드입니다.</summary>
        public VfxConstants.FollowMode VfxFollowMode;

        /// <summary>VFX Y 오프셋 방식입니다.</summary>
        public ConfigCommon.PositionYType VfxPositionYType;

        /// <summary>VFX Y 오프셋 값입니다.</summary>
        public float VfxPositionY;

        /// <summary>VFX 스케일 오버라이드입니다.</summary>
        public float VfxScale;

        /// <summary>
        /// 테이블 Row를 런타임 차징 단계 정의로 변환합니다.
        /// </summary>
        /// <param name="row">차징 단계 테이블 Row입니다.</param>
        /// <returns>런타임 차징 단계 정의입니다.</returns>
        public static RuntimeSkillChargeStageDefinition From(StruckTableSkillChargeStage row)
        {
            return new RuntimeSkillChargeStageDefinition
            {
                Uid = row.Uid,
                StageIndex = row.StageIndex,
                DurationSeconds = System.Math.Max(0f, row.DurationSeconds),
                LoopClip = row.LoopClip ?? string.Empty,
                VfxUid = System.Math.Max(0, row.VfxUid),
                VfxFollowMode = row.VfxFollowMode,
                VfxPositionYType = row.VfxPositionYType,
                VfxPositionY = row.VfxPositionY,
                VfxScale = System.Math.Max(0f, row.VfxScale),
            };
        }
    }
}
