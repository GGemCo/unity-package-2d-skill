using System.Collections.Generic;
using System.Linq;
using Config;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 차징 단계 테이블의 한 행을 표현합니다.
    /// </summary>
    public sealed class StruckTableSkillChargeStage
    {
        /// <summary>차징 단계 행의 고유 식별자입니다.</summary>
        public int Uid { get; set; }

        /// <summary>차징 단계가 연결될 스킬 UID입니다.</summary>
        public int SkillUid;

        /// <summary>플레이어/몬스터 중 어떤 스킬 테이블에 연결되는지 나타냅니다.</summary>
        public ConfigCommonSkill.SkillOwnerType OwnerType;

        /// <summary>차징 단계 순서입니다. 낮은 값부터 순서대로 진행됩니다.</summary>
        public int StageIndex;

        /// <summary>이 단계에 진입할 때 1회 재생할 애니메이션 클립 이름입니다.</summary>
        public string StartClip;

        /// <summary>시작 애니메이션을 유지할 시간(초)입니다. 0이면 클립 길이를 사용합니다.</summary>
        public float StartDurationSeconds;

        /// <summary>이 단계에서 유지할 차징 시간(초)입니다.</summary>
        public float DurationSeconds;

        /// <summary>이 단계에서 루프로 재생할 애니메이션 클립 이름입니다.</summary>
        public string LoopClip;

        /// <summary>이 단계가 끝날 때 1회 재생할 애니메이션 클립 이름입니다.</summary>
        public string EndClip;

        /// <summary>종료 애니메이션을 유지할 시간(초)입니다. 0이면 클립 길이를 사용합니다.</summary>
        public float EndDurationSeconds;

        /// <summary>이 단계에서 표시할 VFX UID입니다. 0이면 VFX를 생성하지 않습니다.</summary>
        public int VfxUid;

        /// <summary>VFX를 캐스터에 붙여 따라가게 할지 결정하는 Follow 모드입니다.</summary>
        public VfxConstants.FollowMode VfxFollowMode;

        /// <summary>VFX가 Follow 중 유지할 위치 기준 정책입니다.</summary>
        public VfxConstants.FollowAnchorMode VfxFollowAnchorMode;

        /// <summary>VFX Y 오프셋 계산 방식입니다.</summary>
        public ConfigCommon.PositionYType VfxPositionYType;

        /// <summary>VFX Y 오프셋 값입니다.</summary>
        public float VfxPositionY;

        /// <summary>VFX 스케일 오버라이드입니다. 0이면 테이블 기본값을 사용합니다.</summary>
        public float VfxScale;

        /// <summary>기획/디버그 메모입니다.</summary>
        public string Memo;
    }

    /// <summary>
    /// 스킬 사용 전 차징 단계 데이터를 로드하고 스킬 UID 기준으로 조회하는 테이블입니다.
    /// </summary>
    public sealed class TableSkillChargeStage : DefaultTable<StruckTableSkillChargeStage>
    {
        private readonly Dictionary<SkillChargeStageKey, List<StruckTableSkillChargeStage>> _bySkill = new();

        /// <summary>Addressables 테이블 키를 반환합니다.</summary>
        public override string Key => ConfigAddressableTableSkill.SkillChargeStage;

        /// <summary>
        /// 테이블 로드 전 보조 인덱스를 초기화합니다.
        /// </summary>
        protected override void PreLoad()
        {
            _bySkill.Clear();
        }

        /// <summary>
        /// 한 행 데이터를 강타입 차징 단계 Row로 변환합니다.
        /// </summary>
        /// <param name="data">헤더 이름과 원본 문자열 값 사전입니다.</param>
        /// <returns>변환된 차징 단계 Row입니다.</returns>
        protected override StruckTableSkillChargeStage BuildRow(Dictionary<string, string> data)
        {
            TableRowReader reader = ReadRow(data);
            return new StruckTableSkillChargeStage
            {
                Uid = reader.Int("Uid"),
                SkillUid = reader.Int("SkillUid"),
                OwnerType = reader.Enum<ConfigCommonSkill.SkillOwnerType>("OwnerType", ConfigCommonSkill.SkillOwnerType.Player),
                StageIndex = System.Math.Max(0, reader.Int("StageIndex")),
                StartClip = reader.String("StartClip", string.Empty),
                StartDurationSeconds = System.Math.Max(0f, reader.Float("StartDurationSeconds", 0f)),
                DurationSeconds = System.Math.Max(0f, reader.Float("DurationSeconds")),
                LoopClip = reader.String("LoopClip", string.Empty),
                EndClip = reader.String("EndClip", string.Empty),
                EndDurationSeconds = System.Math.Max(0f, reader.Float("EndDurationSeconds", 0f)),
                VfxUid = System.Math.Max(0, reader.Int("VfxUid", 0)),
                VfxFollowMode = reader.Enum<VfxConstants.FollowMode>("VfxFollowMode", VfxConstants.FollowMode.Position),
                VfxFollowAnchorMode = reader.Enum<VfxConstants.FollowAnchorMode>("VfxFollowAnchorMode", VfxConstants.FollowAnchorMode.FollowTargetOrigin),
                VfxPositionYType = reader.Enum<ConfigCommon.PositionYType>("VfxPositionYType", ConfigCommon.PositionYType.None),
                VfxPositionY = reader.Float("VfxPositionY", 0f),
                VfxScale = System.Math.Max(0f, reader.Float("VfxScale", 0f)),
                Memo = reader.String("Memo", string.Empty),
            };
        }

        /// <summary>
        /// 로드된 차징 단계를 스킬 UID와 소유자 타입 기준 인덱스에 추가합니다.
        /// </summary>
        /// <param name="row">방금 로드된 차징 단계 Row입니다.</param>
        protected override void OnLoadedData(StruckTableSkillChargeStage row)
        {
            if (row == null || row.SkillUid <= 0)
                return;

            AddToSkillIndex(row);
        }

        /// <summary>
        /// 에디터 테스트 중 수정한 차징 단계 Row를 런타임 테이블 캐시에 즉시 반영합니다.
        /// </summary>
        /// <param name="row">반영할 차징 단계 Row입니다.</param>
        public void UpsertRuntimeRow(StruckTableSkillChargeStage row)
        {
            if (row == null || row.Uid <= 0)
                return;

            GetDatas()[row.Uid] = row;
            RebuildSkillIndex();
        }

        private void RebuildSkillIndex()
        {
            _bySkill.Clear();
            foreach (var pair in GetDatas())
            {
                AddToSkillIndex(pair.Value);
            }
        }

        private void AddToSkillIndex(StruckTableSkillChargeStage row)
        {
            if (row == null || row.SkillUid <= 0)
                return;

            var key = new SkillChargeStageKey(row.SkillUid, row.OwnerType);
            if (!_bySkill.TryGetValue(key, out var list))
            {
                list = new List<StruckTableSkillChargeStage>();
                _bySkill[key] = list;
            }

            list.Add(row);
            list.Sort(CompareStageRows);
        }

        /// <summary>
        /// 지정한 스킬 UID와 소유자 타입에 연결된 차징 단계 목록을 반환합니다.
        /// </summary>
        /// <param name="skillUid">조회할 스킬 UID입니다.</param>
        /// <param name="ownerType">플레이어/몬스터 소유자 타입입니다.</param>
        /// <returns>StageIndex 순서로 정렬된 차징 단계 목록입니다.</returns>
        public IReadOnlyList<StruckTableSkillChargeStage> GetStages(int skillUid, ConfigCommonSkill.SkillOwnerType ownerType)
        {
            if (skillUid <= 0)
                return System.Array.Empty<StruckTableSkillChargeStage>();

            return _bySkill.TryGetValue(new SkillChargeStageKey(skillUid, ownerType), out var list)
                ? list
                : System.Array.Empty<StruckTableSkillChargeStage>();
        }

        /// <summary>
        /// 지정한 스킬 UID와 소유자 타입에 연결된 차징 단계 목록을 복사해서 반환합니다.
        /// 에디터 테스트처럼 외부에서 정렬/필터링이 필요한 경우 사용합니다.
        /// </summary>
        public List<StruckTableSkillChargeStage> GetStageList(int skillUid, ConfigCommonSkill.SkillOwnerType ownerType)
        {
            return GetStages(skillUid, ownerType).ToList();
        }

        private static int CompareStageRows(StruckTableSkillChargeStage left, StruckTableSkillChargeStage right)
        {
            int stage = left.StageIndex.CompareTo(right.StageIndex);
            return stage != 0 ? stage : left.Uid.CompareTo(right.Uid);
        }

        private readonly struct SkillChargeStageKey
        {
            private readonly int _skillUid;
            private readonly ConfigCommonSkill.SkillOwnerType _ownerType;

            public SkillChargeStageKey(int skillUid, ConfigCommonSkill.SkillOwnerType ownerType)
            {
                _skillUid = skillUid;
                _ownerType = ownerType;
            }

            public override int GetHashCode()
            {
                return (_skillUid * 397) ^ (int)_ownerType;
            }

            public override bool Equals(object obj)
            {
                return obj is SkillChargeStageKey other && other._skillUid == _skillUid && other._ownerType == _ownerType;
            }
        }
    }
}
