using UnityEngine;

[CreateAssetMenu(fileName = "IdleNode", menuName = "BehaviorTree/Player/IdleNode")]
public class PlayerIdleNode : LeafNode
{
    protected override NodeResult OnExecute(GameObject owner)
    {
        Player playerScript = owner.GetComponent<Player>();

        Vector2 moveValue = playerScript.MoveValue;

        if (moveValue == Vector2.zero)
        {
            // 애니메이션 플래그
            playerScript.Move = false;
            return NodeResult.SUCCESS;
        }

        return NodeResult.FAILURE;
    }
}
