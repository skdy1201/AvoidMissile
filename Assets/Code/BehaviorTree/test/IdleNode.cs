using UnityEngine;

/// <summary>
/// 대기 노드 (아무 동작 없음)
/// </summary>
[CreateAssetMenu(fileName = "IdleNode", menuName = "BehaviorTree/Test/IdleNode")]
public class IdleNode : LeafNode
{
    protected override NodeResult OnExecute(GameObject owner)
    {
        return NodeResult.SUCCESS;
    }
}
