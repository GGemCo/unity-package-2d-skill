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

        [Header("Debug")]
        [SerializeField, DebugOption("스킬 패키지 디버그 기능 전체 사용 여부입니다.")]
        private bool enableSkillDebug;
        public bool EnableSkillDebug => DebugOptionRuntimeUtility.Resolve(enableSkillDebug);

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
