namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 콤보 그래프에서 시작 노드와 다음 노드를 해석하는 유틸리티입니다.
    /// </summary>
    public static class SkillComboGraphResolver
    {
        /// <summary>
        /// 콤보 정의에서 시작 메인 노드를 찾습니다.
        /// </summary>
        /// <param name="definition">해석할 콤보 정의입니다.</param>
        /// <param name="node">찾은 시작 노드입니다.</param>
        /// <param name="failReason">실패 시 콤보 해석 실패 이유입니다.</param>
        /// <returns>시작 노드를 찾으면 true입니다.</returns>
        public static bool TryGetStartNode(
            RuntimeSkillComboDefinition definition,
            out RuntimeSkillComboNode node,
            out SkillComboUseFailReason failReason)
        {
            node = null;

            if (!TryEnsureDefinition(definition, out failReason))
                return false;

            if (!TryFindNode(definition, definition.StartNodeIndex, out node))
            {
                failReason = SkillComboUseFailReason.MissingStartNode;
                return false;
            }

            if (!node.IsMain)
            {
                node = null;
                failReason = SkillComboUseFailReason.InvalidStartNode;
                return false;
            }

            failReason = SkillComboUseFailReason.None;
            return true;
        }

        /// <summary>
        /// 현재 노드와 콤보 명령을 기준으로 다음 실행 노드를 찾습니다.
        /// </summary>
        /// <param name="definition">해석할 콤보 정의입니다.</param>
        /// <param name="currentNodeIndex">현재 메인 노드 인덱스입니다.</param>
        /// <param name="command">다음 노드를 선택할 콤보 명령입니다.</param>
        /// <param name="node">찾은 다음 노드입니다.</param>
        /// <param name="failReason">실패 시 콤보 해석 실패 이유입니다.</param>
        /// <returns>다음 노드를 찾으면 true입니다.</returns>
        public static bool TryResolveNextNode(
            RuntimeSkillComboDefinition definition,
            int currentNodeIndex,
            SkillComboCommand command,
            out RuntimeSkillComboNode node,
            out SkillComboUseFailReason failReason)
        {
            node = null;

            if (!TryEnsureDefinition(definition, out failReason))
                return false;

            if (!TryFindNode(definition, currentNodeIndex, out var currentNode))
            {
                failReason = SkillComboUseFailReason.MissingCurrentNode;
                return false;
            }

            if (currentNode.IsLast)
            {
                failReason = SkillComboUseFailReason.LastNodeCannotChain;
                return false;
            }

            int nextNodeIndex = command == SkillComboCommand.Main
                ? currentNode.NextMainNodeIndex
                : currentNode.LastNodeIndex;

            if (nextNodeIndex == RuntimeSkillComboDefinition.InvalidNodeIndex)
            {
                failReason = command == SkillComboCommand.Main
                    ? SkillComboUseFailReason.MissingNextMainNode
                    : SkillComboUseFailReason.MissingLastNode;
                return false;
            }

            if (!TryFindNode(definition, nextNodeIndex, out node))
            {
                failReason = command == SkillComboCommand.Main
                    ? SkillComboUseFailReason.MissingNextMainNode
                    : SkillComboUseFailReason.MissingLastNode;
                return false;
            }

            if (!IsExpectedNodeType(command, node))
            {
                node = null;
                failReason = SkillComboUseFailReason.InvalidTransition;
                return false;
            }

            failReason = SkillComboUseFailReason.None;
            return true;
        }

        /// <summary>
        /// 지정한 인덱스에 해당하는 콤보 노드를 찾습니다.
        /// </summary>
        /// <param name="definition">검색할 콤보 정의입니다.</param>
        /// <param name="nodeIndex">찾을 노드 인덱스입니다.</param>
        /// <param name="node">찾은 노드입니다.</param>
        /// <returns>노드를 찾으면 true입니다.</returns>
        public static bool TryFindNode(
            RuntimeSkillComboDefinition definition,
            int nodeIndex,
            out RuntimeSkillComboNode node)
        {
            node = null;
            if (definition == null || definition.Nodes == null)
                return false;

            for (int i = 0; i < definition.Nodes.Count; i++)
            {
                var candidate = definition.Nodes[i];
                if (candidate == null || candidate.Index != nodeIndex)
                    continue;

                node = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 콤보 정의가 해석 가능한 최소 조건을 갖추었는지 검사합니다.
        /// </summary>
        /// <param name="definition">검사할 콤보 정의입니다.</param>
        /// <param name="failReason">실패 시 콤보 해석 실패 이유입니다.</param>
        /// <returns>정의가 유효하면 true입니다.</returns>
        private static bool TryEnsureDefinition(
            RuntimeSkillComboDefinition definition,
            out SkillComboUseFailReason failReason)
        {
            if (definition == null)
            {
                failReason = SkillComboUseFailReason.MissingDefinition;
                return false;
            }

            if (!definition.HasNodes)
            {
                failReason = SkillComboUseFailReason.EmptyDefinition;
                return false;
            }

            failReason = SkillComboUseFailReason.None;
            return true;
        }

        /// <summary>
        /// 콤보 명령이 기대하는 노드 타입과 실제 노드 타입이 일치하는지 확인합니다.
        /// </summary>
        /// <param name="command">다음 노드를 선택한 콤보 명령입니다.</param>
        /// <param name="node">검사할 콤보 노드입니다.</param>
        /// <returns>명령과 노드 타입이 일치하면 true입니다.</returns>
        private static bool IsExpectedNodeType(SkillComboCommand command, RuntimeSkillComboNode node)
        {
            if (node == null)
                return false;

            return command == SkillComboCommand.Main
                ? node.IsMain
                : node.IsLast;
        }
    }
}
