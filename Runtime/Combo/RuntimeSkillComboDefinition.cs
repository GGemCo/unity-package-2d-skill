using System;
using System.Collections.Generic;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 콤보 노드가 콤보 안에서 맡는 역할을 정의합니다.
    /// </summary>
    public enum SkillComboNodeType
    {
        /// <summary>
        /// 다음 메인 스킬 또는 마무리 스킬로 이어질 수 있는 노드입니다.
        /// </summary>
        Main = 0,

        /// <summary>
        /// 사용 후 콤보를 종료하는 마무리 노드입니다.
        /// </summary>
        Last = 1,
    }

    /// <summary>
    /// 플레이어 입력이나 상위 시스템이 콤보에 전달하는 진행 명령을 정의합니다.
    /// </summary>
    public enum SkillComboCommand
    {
        /// <summary>
        /// 현재 콤보를 시작하거나 다음 메인 노드로 진행합니다.
        /// </summary>
        Main = 0,

        /// <summary>
        /// 현재 메인 노드에서 마무리 노드로 진행합니다.
        /// </summary>
        Last = 1,
    }

    /// <summary>
    /// 런타임에서 사용할 플레이어 스킬 콤보 그래프 정의입니다.
    /// </summary>
    [Serializable]
    public sealed class RuntimeSkillComboDefinition
    {
        /// <summary>
        /// 연결되지 않은 노드를 표현하는 인덱스 값입니다.
        /// </summary>
        public const int InvalidNodeIndex = -1;

        /// <summary>
        /// 콤보 정의를 구분하기 위한 UID입니다.
        /// </summary>
        public int ComboUid;

        /// <summary>
        /// 콤보가 시작될 메인 노드 인덱스입니다.
        /// </summary>
        public int StartNodeIndex = InvalidNodeIndex;

        /// <summary>
        /// 콤보 그래프를 구성하는 노드 목록입니다.
        /// </summary>
        public List<RuntimeSkillComboNode> Nodes = new();

        /// <summary>
        /// 콤보 정의에 하나 이상의 노드가 있는지 반환합니다.
        /// </summary>
        public bool HasNodes => Nodes != null && Nodes.Count > 0;
    }

    /// <summary>
    /// 스킬 UID와 다음 연결 정보를 담는 콤보 그래프의 단일 노드입니다.
    /// </summary>
    [Serializable]
    public sealed class RuntimeSkillComboNode
    {
        /// <summary>
        /// 콤보 그래프 안에서 노드를 식별하는 인덱스입니다.
        /// </summary>
        public int Index = RuntimeSkillComboDefinition.InvalidNodeIndex;

        /// <summary>
        /// 이 노드가 실행할 플레이어 스킬 UID입니다.
        /// </summary>
        public int SkillUid;

        /// <summary>
        /// 이 노드의 콤보 타입입니다.
        /// </summary>
        public SkillComboNodeType Type = SkillComboNodeType.Main;

        /// <summary>
        /// 메인 명령으로 이어질 다음 메인 노드 인덱스입니다.
        /// </summary>
        public int NextMainNodeIndex = RuntimeSkillComboDefinition.InvalidNodeIndex;

        /// <summary>
        /// 마무리 명령으로 이어질 마무리 노드 인덱스입니다.
        /// </summary>
        public int LastNodeIndex = RuntimeSkillComboDefinition.InvalidNodeIndex;

        /// <summary>
        /// 이 노드가 메인 타입인지 반환합니다.
        /// </summary>
        public bool IsMain => Type == SkillComboNodeType.Main;

        /// <summary>
        /// 이 노드가 마무리 타입인지 반환합니다.
        /// </summary>
        public bool IsLast => Type == SkillComboNodeType.Last;
    }
}
