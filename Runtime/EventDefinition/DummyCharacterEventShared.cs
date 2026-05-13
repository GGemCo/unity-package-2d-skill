using System;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 더미 캐릭터 생성 이벤트에서 사용할 소스 타입입니다.
    /// </summary>
    public enum DummyCharacterSourceType
    {
        /// <summary>
        /// 몬스터 테이블을 기준으로 더미를 생성합니다.
        /// </summary>
        Monster = 0,

        /// <summary>
        /// NPC 테이블을 기준으로 더미를 생성합니다.
        /// </summary>
        Npc = 1,
    }

    /// <summary>
    /// 더미 캐릭터 생성 위치의 기준점을 정의합니다.
    /// </summary>
    public enum DummySpawnAnchor
    {
        /// <summary>
        /// 시전자 위치를 기준으로 생성합니다.
        /// </summary>
        Caster = 0,

        /// <summary>
        /// 고정 타겟 위치를 기준으로 생성합니다.
        /// </summary>
        Target = 1,

        /// <summary>
        /// 지면 기준점(groundPoint)을 기준으로 생성합니다.
        /// </summary>
        Ground = 2,

        /// <summary>
        /// 같은 스킬 실행 중 저장된 이름 있는 위치 앵커를 기준으로 생성합니다.
        /// </summary>
        NamedPositionAnchor = 3,
    }

    /// <summary>
    /// 더미 캐릭터 이동 목표 위치의 해석 방식을 정의합니다.
    /// </summary>
    public enum DummyMoveTargetMode
    {
        /// <summary>
        /// 지면 기준점(groundPoint)으로 이동합니다.
        /// </summary>
        GroundPoint = 0,

        /// <summary>
        /// 고정 타겟 위치로 이동합니다.
        /// </summary>
        LockedTarget = 1,

        /// <summary>
        /// 같은 스킬 실행 중 저장된 이름 있는 위치 앵커로 이동합니다.
        /// </summary>
        NamedPositionAnchor = 2,

        /// <summary>
        /// 이벤트에 지정한 절대 월드 좌표로 이동합니다.
        /// </summary>
        AbsoluteWorld = 3,
    }

    /// <summary>
    /// 더미 캐릭터를 찾지 못했을 때의 처리 정책입니다.
    /// </summary>
    public enum DummyMissingActorPolicy
    {
        /// <summary>
        /// 조용히 무시하고 이벤트를 종료합니다.
        /// </summary>
        Ignore = 0,

        /// <summary>
        /// 경고 로그를 남기고 이벤트를 종료합니다.
        /// </summary>
        Warn = 1,
    }
}
