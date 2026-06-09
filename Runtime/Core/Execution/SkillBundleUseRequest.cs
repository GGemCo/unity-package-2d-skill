using System;
using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 하나의 입력으로 여러 플레이어 스킬을 같은 시작 시점에 실행하기 위한 요청 데이터입니다.
    /// </summary>
    public readonly struct SkillBundleUseRequest
    {
        /// <summary>
        /// 묶음 실행에 포함할 스킬 목록입니다.
        /// </summary>
        public readonly IReadOnlyList<SkillBundleSkillEntry> Entries;

        /// <summary>
        /// 실제 캐릭터 애니메이션 재생 기준으로 사용할 대표 스킬 UID입니다.
        /// </summary>
        public readonly int PrimarySkillUid;

        /// <summary>
        /// 묶음 스킬 실행에 공통으로 사용할 타겟팅 요청입니다.
        /// </summary>
        public readonly SkillDriverRequest Request;

        /// <summary>
        /// 스킬 묶음 실행 요청을 생성합니다.
        /// </summary>
        /// <param name="entries">동시에 발동할 스킬 목록입니다.</param>
        /// <param name="primarySkillUid">대표 애니메이션을 제공할 스킬 UID입니다.</param>
        /// <param name="request">공통 타겟팅 요청입니다.</param>
        public SkillBundleUseRequest(
            IReadOnlyList<SkillBundleSkillEntry> entries,
            int primarySkillUid,
            in SkillDriverRequest request)
        {
            Entries = entries;
            PrimarySkillUid = primarySkillUid;
            Request = request;
        }
    }

    /// <summary>
    /// 스킬 묶음 실행에 포함되는 단일 스킬과 1회성 실행 옵션입니다.
    /// </summary>
    public readonly struct SkillBundleSkillEntry
    {
        /// <summary>
        /// 실행할 플레이어 스킬 UID입니다.
        /// </summary>
        public readonly int SkillUid;

        /// <summary>
        /// 해당 스킬에만 적용할 실행 옵션입니다.
        /// </summary>
        public readonly SkillExecutionOptions ExecutionOptions;

        /// <summary>
        /// 스킬 묶음 항목을 생성합니다.
        /// </summary>
        /// <param name="skillUid">실행할 플레이어 스킬 UID입니다.</param>
        /// <param name="executionOptions">해당 스킬에만 적용할 실행 옵션입니다.</param>
        public SkillBundleSkillEntry(int skillUid, in SkillExecutionOptions executionOptions)
        {
            SkillUid = skillUid;
            ExecutionOptions = executionOptions;
        }
    }

    /// <summary>
    /// 여러 스킬을 하나의 플레이어 입력으로 발동하는 드라이버 포트입니다.
    /// </summary>
    public interface ICharacterSkillBundleDriver
    {
        /// <summary>
        /// 스킬 묶음 사용을 시도합니다.
        /// </summary>
        /// <param name="request">묶음 실행 요청입니다.</param>
        /// <returns>묶음 실행이 시작되면 <see cref="SkillUseResult.Started"/>입니다.</returns>
        SkillUseResult TryUseSkillBundle(in SkillBundleUseRequest request);
    }

    /// <summary>
    /// 검증이 끝난 묶음 스킬 항목을 <see cref="SkillExecutor"/>로 전달하기 위한 내부 실행 데이터입니다.
    /// </summary>
    internal readonly struct SkillBundleRuntimeEntry
    {
        /// <summary>
        /// 실행할 스킬 정의입니다.
        /// </summary>
        public readonly RuntimeSkillDefinition Skill;

        /// <summary>
        /// 해당 스킬에 적용할 타겟팅 및 실행 옵션 컨텍스트입니다.
        /// </summary>
        public readonly SkillTargetContext Context;

        /// <summary>
        /// 내부 묶음 실행 항목을 생성합니다.
        /// </summary>
        /// <param name="skill">실행할 스킬 정의입니다.</param>
        /// <param name="context">해당 스킬용 실행 컨텍스트입니다.</param>
        public SkillBundleRuntimeEntry(RuntimeSkillDefinition skill, in SkillTargetContext context)
        {
            Skill = skill;
            Context = context;
        }
    }
}
