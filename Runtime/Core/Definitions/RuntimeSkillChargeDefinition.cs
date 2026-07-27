using System.Collections.Generic;
using System.Linq;
using Config;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 차징 게이지가 활성화된 동안 피격 피해를 처리하는 정책입니다.
    /// </summary>
    public enum SkillChargeIncomingHitPolicy
    {
        /// <summary>
        /// 캐릭터 피해와 차징 게이지 감소를 모두 적용합니다.
        /// </summary>
        DamageAndGauge = 0,

        /// <summary>
        /// 캐릭터 피해를 적용하지 않고 차징 게이지만 감소시킵니다.
        /// </summary>
        GaugeOnly = 1
    }

    /// <summary>
    /// 런타임 스킬 정의에 연결되는 차징 전체 설정입니다.
    /// </summary>
    public sealed class RuntimeSkillChargeDefinition
    {
        /// <summary>차징 사용 여부입니다.</summary>
        public bool UseCharge;

        /// <summary>차징 중 피격으로 감소하는 게이지 최대값입니다.</summary>
        public float GaugeMax;

        /// <summary>피격 1회당 감소시킬 차징 게이지 값입니다.</summary>
        public float GaugeDamagePerHit;

        /// <summary>차징 중 피격 피해를 처리할 정책입니다.</summary>
        public SkillChargeIncomingHitPolicy IncomingHitPolicy;

        /// <summary>차징 완료 후 실제 사용 단계로 넘어가기 전에 재생할 애니메이션 클립입니다.</summary>
        public string CompleteClip;

        /// <summary>차징 완료 애니메이션을 유지할 시간(초)입니다.</summary>
        public float CompleteDurationSeconds;

        /// <summary>차징 실패 시 재생할 애니메이션 클립입니다.</summary>
        public string FailClip;

        /// <summary>차징 실패 애니메이션을 유지할 시간(초)입니다.</summary>
        public float FailDurationSeconds;

        /// <summary>차징 전체가 진행되는 동안 재생할 사운드 요청입니다.</summary>
        public SoundPlayRequest ChargeSound = new SoundPlayRequest();

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
        /// 플레이어 스킬 Row와 차징 단계 테이블을 런타임 차징 정의로 변환합니다.
        /// </summary>
        /// <param name="row">플레이어 스킬 Row입니다.</param>
        /// <param name="stageTable">차징 단계 테이블입니다.</param>
        /// <returns>런타임 차징 정의입니다.</returns>
        public static RuntimeSkillChargeDefinition From(StruckTableSkill row, TableSkillChargeStage stageTable)
        {
            if (row == null)
                return CreateEmpty();

            return Create(
                row.UseCharge,
                row.ChargeGaugeMax,
                row.ChargeGaugeDamagePerHit,
                row.ChargeIncomingHitPolicy,
                row.ChargeCompleteClip,
                row.ChargeCompleteDurationSeconds,
                row.ChargeFailClip,
                row.ChargeFailDurationSeconds,
                row.ChargeSoundUid,
                row.ChargeSoundLoop,
                stageTable,
                row.Uid,
                ConfigCommonSkill.SkillOwnerType.Player);
        }

        /// <summary>
        /// 몬스터 스킬 Row와 차징 단계 테이블을 런타임 차징 정의로 변환합니다.
        /// </summary>
        /// <param name="row">몬스터 스킬 Row입니다.</param>
        /// <param name="stageTable">차징 단계 테이블입니다.</param>
        /// <returns>런타임 차징 정의입니다.</returns>
        public static RuntimeSkillChargeDefinition From(StruckTableSkillMonster row, TableSkillChargeStage stageTable)
        {
            if (row == null)
                return CreateEmpty();

            return Create(
                row.UseCharge,
                row.ChargeGaugeMax,
                row.ChargeGaugeDamagePerHit,
                row.ChargeIncomingHitPolicy,
                row.ChargeCompleteClip,
                row.ChargeCompleteDurationSeconds,
                row.ChargeFailClip,
                row.ChargeFailDurationSeconds,
                row.ChargeSoundUid,
                row.ChargeSoundLoop,
                stageTable,
                row.Uid,
                ConfigCommonSkill.SkillOwnerType.Monster);
        }

        /// <summary>
        /// 비활성 차징 정의를 생성합니다.
        /// </summary>
        /// <returns>차징이 꺼진 런타임 정의입니다.</returns>
        private static RuntimeSkillChargeDefinition CreateEmpty()
        {
            return new RuntimeSkillChargeDefinition
            {
                UseCharge = false,
                GaugeMax = 0f,
                GaugeDamagePerHit = 0f,
                IncomingHitPolicy = SkillChargeIncomingHitPolicy.DamageAndGauge,
                CompleteClip = string.Empty,
                CompleteDurationSeconds = 0f,
                FailClip = string.Empty,
                FailDurationSeconds = 0f,
                ChargeSound = new SoundPlayRequest(),
                Stages = System.Array.Empty<RuntimeSkillChargeStageDefinition>()
            };
        }

        /// <summary>
        /// 공통 차징 Row 필드를 런타임 정의로 조립합니다.
        /// </summary>
        /// <param name="useCharge">차징 사용 여부입니다.</param>
        /// <param name="gaugeMax">차징 게이지 최대값입니다.</param>
        /// <param name="gaugeDamagePerHit">피격 1회당 차징 게이지 감소량입니다.</param>
        /// <param name="incomingHitPolicy">차징 중 피격 피해 처리 정책입니다.</param>
        /// <param name="completeClip">차징 완료 애니메이션 클립입니다.</param>
        /// <param name="completeDurationSeconds">차징 완료 애니메이션 유지 시간입니다.</param>
        /// <param name="failClip">차징 실패 애니메이션 클립입니다.</param>
        /// <param name="failDurationSeconds">차징 실패 애니메이션 유지 시간입니다.</param>
        /// <param name="chargeSoundUid">차징 전체 진행 중 재생할 사운드 UID입니다.</param>
        /// <param name="chargeSoundLoop">차징 전체 사운드 루프 여부입니다.</param>
        /// <param name="stageTable">차징 단계 테이블입니다.</param>
        /// <param name="skillUid">스킬 UID입니다.</param>
        /// <param name="ownerType">스킬 소유자 타입입니다.</param>
        /// <returns>런타임 차징 정의입니다.</returns>
        private static RuntimeSkillChargeDefinition Create(
            bool useCharge,
            float gaugeMax,
            float gaugeDamagePerHit,
            SkillChargeIncomingHitPolicy incomingHitPolicy,
            string completeClip,
            float completeDurationSeconds,
            string failClip,
            float failDurationSeconds,
            int chargeSoundUid,
            bool chargeSoundLoop,
            TableSkillChargeStage stageTable,
            int skillUid,
            ConfigCommonSkill.SkillOwnerType ownerType)
        {
            IReadOnlyList<StruckTableSkillChargeStage> rows = stageTable != null
                ? stageTable.GetStages(skillUid, ownerType)
                : System.Array.Empty<StruckTableSkillChargeStage>();

            var stages = rows
                .Where(row => row != null)
                .Select(RuntimeSkillChargeStageDefinition.From)
                .Where(stage => stage != null && stage.HasTimeline)
                .ToArray();

            return new RuntimeSkillChargeDefinition
            {
                UseCharge = useCharge,
                GaugeMax = System.Math.Max(0f, gaugeMax),
                GaugeDamagePerHit = System.Math.Max(0f, gaugeDamagePerHit),
                IncomingHitPolicy = incomingHitPolicy,
                CompleteClip = completeClip ?? string.Empty,
                CompleteDurationSeconds = System.Math.Max(0f, completeDurationSeconds),
                FailClip = failClip ?? string.Empty,
                FailDurationSeconds = System.Math.Max(0f, failDurationSeconds),
                ChargeSound = SoundPlayRequest.Create(
                    System.Math.Max(0, chargeSoundUid),
                    loop: chargeSoundLoop,
                    useLoopOverride: chargeSoundUid > 0),
                Stages = stages
            };
        }
    }

    /// <summary>
    /// 차징 중 한 단계의 런타임 정의입니다.
    /// </summary>
    public sealed class RuntimeSkillChargeStageDefinition
    {
        /// <summary>테이블 Row UID입니다.</summary>
        public int Uid;

        /// <summary>차징 단계 순서입니다.</summary>
        public int StageIndex;

        /// <summary>이 단계에 진입할 때 1회 재생할 애니메이션 클립입니다.</summary>
        public string StartClip;

        /// <summary>시작 애니메이션을 유지할 시간(초)입니다.</summary>
        public float StartDurationSeconds;

        /// <summary>이 단계에서 유지할 차징 시간(초)입니다.</summary>
        public float DurationSeconds;

        /// <summary>이 단계에서 루프로 재생할 애니메이션 클립입니다.</summary>
        public string LoopClip;

        /// <summary>이 단계가 끝날 때 1회 재생할 애니메이션 클립입니다.</summary>
        public string EndClip;

        /// <summary>종료 애니메이션을 유지할 시간(초)입니다.</summary>
        public float EndDurationSeconds;

        /// <summary>이 단계에 진입할 때 1회 재생할 사운드 요청입니다.</summary>
        public SoundPlayRequest EnterSound = new SoundPlayRequest();

        /// <summary>이 단계의 루프 구간 동안 재생할 사운드 요청입니다.</summary>
        public SoundPlayRequest LoopSound = new SoundPlayRequest();

        /// <summary>이 단계에서 생성할 VFX UID입니다.</summary>
        public int VfxUid;

        /// <summary>VFX Follow 모드입니다.</summary>
        public VfxConstants.FollowMode VfxFollowMode;

        /// <summary>VFX Follow 위치 기준 정책입니다.</summary>
        public VfxConstants.FollowAnchorMode VfxFollowAnchorMode;

        /// <summary>VFX Y 오프셋 계산 방식입니다.</summary>
        public ConfigCommon.PositionYType VfxPositionYType;

        /// <summary>VFX Y 오프셋 값입니다.</summary>
        public float VfxPositionY;

        /// <summary>VFX 스케일 오버라이드입니다.</summary>
        public float VfxScale;

        /// <summary>차징 단계가 실행할 연출 또는 유지 시간을 가지고 있는지 여부입니다.</summary>
        public bool HasTimeline => StartDurationSeconds > 0f
                                   || DurationSeconds > 0f
                                   || EndDurationSeconds > 0f
                                   || !string.IsNullOrWhiteSpace(StartClip)
                                   || !string.IsNullOrWhiteSpace(LoopClip)
                                   || !string.IsNullOrWhiteSpace(EndClip)
                                   || (EnterSound != null && EnterSound.IsValid)
                                   || (LoopSound != null && LoopSound.IsValid)
                                   || VfxUid > 0;

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
                StartClip = row.StartClip ?? string.Empty,
                StartDurationSeconds = System.Math.Max(0f, row.StartDurationSeconds),
                DurationSeconds = System.Math.Max(0f, row.DurationSeconds),
                LoopClip = row.LoopClip ?? string.Empty,
                EndClip = row.EndClip ?? string.Empty,
                EndDurationSeconds = System.Math.Max(0f, row.EndDurationSeconds),
                EnterSound = SoundPlayRequest.Create(System.Math.Max(0, row.EnterSoundUid)),
                LoopSound = SoundPlayRequest.Create(
                    System.Math.Max(0, row.LoopSoundUid),
                    loop: row.LoopSoundLoop,
                    useLoopOverride: row.LoopSoundUid > 0),
                VfxUid = System.Math.Max(0, row.VfxUid),
                VfxFollowMode = row.VfxFollowMode,
                VfxFollowAnchorMode = row.VfxFollowAnchorMode,
                VfxPositionYType = row.VfxPositionYType,
                VfxPositionY = row.VfxPositionY,
                VfxScale = System.Math.Max(0f, row.VfxScale),
            };
        }
    }
}
