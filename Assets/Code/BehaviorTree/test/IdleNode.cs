using UnityEngine;

[CreateAssetMenu(fileName = "IdleNode", menuName = "BehaviorTree/Test/IdleNode")]
public class IdleNode : LeafNode
{
    protected override NodeResult OnExecute(GameObject owner)
    {
        // 아무것도 안 함
        return NodeResult.SUCCESS;
    }
}
