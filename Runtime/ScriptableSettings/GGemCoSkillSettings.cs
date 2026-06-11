using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    [CreateAssetMenu(
        fileName = ConfigScriptableObjectSkill.Skill.FileName,
        menuName = ConfigScriptableObjectSkill.Skill.MenuName,
        order = ConfigScriptableObjectSkill.Skill.Ordering)]
    public sealed class GGemCoSkillSettings : ScriptableObject
    {
        [Header("Chain Cancel")]
        [Tooltip("플레이어 스킬이 타겟에게 실제 데미지를 확정했을 때, 현재 스킬 애니메이션 종료 전에도 다음 스킬로 연계할 수 있게 허용할지 여부입니다.")]
        public bool enableSkillChainOnConfirmedDamage = false;

        [Header("Charge Gauge UI")]
        [Tooltip("스킬 차징 중 캐릭터 하단에 차징 게이지 Prefab을 자동으로 생성할지 여부입니다.")]
        public bool useSkillChargeGaugePrefab = true;

        [Tooltip("스킬 차징 진행도와 차징 내구도를 표시할 UI Prefab입니다. SceneGame.canvasFromWorldCharacterBottom 하위에 생성됩니다.")]
        public UIElementSkillChargeGauge skillChargeGaugePrefab;

        [Tooltip("플레이어 캐릭터의 차징 게이지를 표시할지 여부입니다.")]
        public bool showChargeGaugeForPlayer = true;

        [Tooltip("몬스터 캐릭터의 차징 게이지를 표시할지 여부입니다.")]
        public bool showChargeGaugeForMonster = true;

        [Tooltip("캐릭터 월드 위치 기준으로 차징 게이지를 표시할 오프셋입니다. Stamina HUD처럼 캐릭터 하단 Canvas에 붙일 때 사용합니다.")]
        public Vector3 skillChargeGaugeWorldOffset = new(0f, -0.35f, 0f);

        [Tooltip("월드 좌표를 Canvas 좌표로 변환한 뒤 추가로 적용할 픽셀 오프셋입니다.")]
        public Vector2 skillChargeGaugeScreenOffset = Vector2.zero;

        [Tooltip("차징이 종료되었을 때 Prefab 인스턴스를 파괴하지 않고 숨긴 뒤 재사용할지 여부입니다.")]
        public bool reuseSkillChargeGaugeInstance = true;

        [Header("Debug")]
        [SerializeField, DebugOption("스킬 패키지 디버그 기능 전체 사용 여부입니다.")]
        private bool enableSkillDebug;
        public bool EnableSkillDebug => DebugOptionRuntimeUtility.Resolve(enableSkillDebug);

#if GGEMCO_ENABLE_CHEAT_TOOLS
        [SerializeField, DebugOption("플레이어 스킬 MP 비용 검사를 무시하고 MP 차감을 건너뛰는 개발 전용 옵션입니다.")]
        [Tooltip("활성화하면 플레이어 스킬 사용 시 필요한 MP가 부족해도 스킬을 발동하고, MP를 차감하지 않습니다. 릴리즈 빌드에는 컴파일되지 않습니다.")]
        private bool ignorePlayerSkillMpCost;

        /// <summary>
        /// 플레이어 스킬의 MP 비용 검사와 차감을 개발 환경에서 무시할지 여부입니다.
        /// </summary>
        public bool IgnorePlayerSkillMpCost => EnableSkillDebug && DebugOptionRuntimeUtility.Resolve(ignorePlayerSkillMpCost);
#endif

        [SerializeField, Tooltip("Damage 이벤트 영역 Gizmo 표시 여부입니다.")]
        private bool enableDamageAreaGizmo;
        public bool EnableDamageAreaGizmo => EnableSkillDebug && DebugOptionRuntimeUtility.Resolve(enableDamageAreaGizmo);

        [Tooltip("Play Mode 진입 시 SkillTestRuntimeHub 자동 생성 여부입니다.")]
        public bool enableSkillTestRuntimeBridgeAutoSpawn = true;

        [Header("Debug - Play Mode Test")]
        [Tooltip("선택 몬스터의 스킬 종료 시 자동으로 스냅샷 위치로 복원할지 여부입니다.")]
        public bool autoResetSelectedMonsterAfterSkill = true;

        [Tooltip("런타임 허브 생성 시 Player를 기본 타겟으로 자동 바인딩할지 여부입니다.")]
        public bool autoBindPlayerAsTarget = true;

        [Tooltip("런타임 허브를 씬 전환 시에도 유지할지 여부입니다.")]
        public bool keepBridgeDontDestroyOnLoad = true;

        [Tooltip("몬스터 테스트 스폰 시 기본 랜덤 반경입니다.")]
        [Min(0f)]
        public float defaultSpawnRadius = 0.5f;

        [Header("Debug - Gizmo")]
        [Tooltip("Damage 이벤트 Gizmo 기본 유지 시간(초)입니다.")]
        [Min(0.01f)]
        public float defaultDamageAreaGizmoDuration = 0.2f;

        [Tooltip("Damage 이벤트 Gizmo 기본 색상입니다.")]
        public Color damageAreaGizmoColor = new(1f, 0.35f, 0.2f, 0.9f);

        [Header("레이저")]
        [SerializeField, Tooltip("레이저 이벤트 영역 Gizmo 표시 여부입니다.")]
        private bool enableLaserGizmo;
        public bool EnableLaserGizmo => EnableSkillDebug && DebugOptionRuntimeUtility.Resolve(enableLaserGizmo);
        
        [Tooltip("Laser 이벤트 Gizmo 기본 유지 시간(초)입니다.")]
        [Min(0.01f)]
        public float defaultLaserGizmoDuration = 0.2f;

        [Tooltip("Laser 이벤트 Gizmo 기본 색상입니다.")]
        public Color laserGizmoColor = new(0.2f, 0.95f, 1f, 0.95f);

        [Tooltip("선택된 캐스터의 영역만 표시할지 여부입니다.")]
        public bool drawOnlyWhenSelectedCaster = false;
    }
}
