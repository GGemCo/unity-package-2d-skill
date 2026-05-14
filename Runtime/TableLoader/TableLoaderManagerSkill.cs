using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 어펙트 관련 테이블 로딩을 총괄하는 매니저 클래스.
    /// </summary>
    /// <remarks>
    /// - TableLoaderBase를 상속하여 테이블 로딩 파이프라인에 참여한다.
    /// - Affect / AffectModifier 테이블을 함께 관리하며,
    ///   테이블 간 참조 관계를 고려해 등록 순서를 제어한다.
    /// - Unity 씬 전환 간에도 유지되도록 Singleton + DontDestroyOnLoad 패턴을 사용한다.
    /// </remarks>
    public class TableLoaderManagerSkill : TableLoaderBase
    {
        /// <summary>
        /// <see cref="TableLoaderManagerSkill"/>의 전역 접근을 위한 Singleton 인스턴스.
        /// </summary>
        /// <remarks>
        /// Awake 시점에 최초 1회만 설정되며,
        /// 이후 중복 생성된 객체는 즉시 파괴된다.
        /// </remarks>
        public static TableLoaderManagerSkill Instance;

        /// <summary>
        /// 어펙트 기본 정의 테이블.
        /// </summary>
        public TableSkill TableSkill { get; private set; } = new TableSkill();
        public TableSkillPassive TableSkillPassive { get; private set; } = new TableSkillPassive();

        /// <summary>
        /// 스킬 옵션(패시브/확장 효과) 테이블.
        /// </summary>
        public TableSkillPassiveOption TableSkillPassiveOption { get; private set; } = new TableSkillPassiveOption();
        public TableSkillMonster TableSkillMonster { get; private set; } = new TableSkillMonster();

        /// <summary>
        /// 스킬 사용 전 차징 단계 테이블입니다.
        /// </summary>
        public TableSkillChargeStage TableSkillChargeStage { get; private set; } = new TableSkillChargeStage();

        /// <summary>
        /// Unity Awake 생명주기 메서드.
        /// </summary>
        /// <remarks>
        /// - Singleton 인스턴스를 초기화한다.
        /// - 테이블 레지스트리를 생성하고,
        ///   테이블 간 의존성을 고려한 순서로 등록한다.
        /// - 이미 인스턴스가 존재하면 중복 객체를 파괴한다.
        /// </remarks>
        protected void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // 테이블 간 참조 및 의존성 해결을 위해 등록 순서가 중요하다.
                // (Modifier → Affect 순으로 로드됨)
                registry = new TableRegistry();
                registry.Register(TableSkill);
                registry.Register(TableSkillPassive);
                registry.Register(TableSkillPassiveOption);
                registry.Register(TableSkillMonster);
                registry.Register(TableSkillChargeStage);
}
            else
            {
                // 이미 Singleton 인스턴스가 존재하는 경우 중복 생성을 방지한다.
                Destroy(gameObject);
            }
        }
    }
}