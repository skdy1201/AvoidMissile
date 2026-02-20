using UnityEngine;

[CreateAssetMenu(fileName = "MoveNode", menuName = "BehaviorTree/Player/MoveNode")]

public class PlayerMoveNode : LeafNode
{
    protected override NodeResult OnExecute(GameObject owner)
    {
        Player player = owner.GetComponent<Player>();
        Rigidbody rb = player.Rigidbody;

        Vector2 moveValue = player.MoveValue;
        Vector3 moveDir = new Vector3(moveValue.x, 0, moveValue.y);

        // 회전
        Quaternion dirQuat = Quaternion.LookRotation(moveDir);
        Quaternion moveQuat = Quaternion.Slerp(rb.rotation, dirQuat, 0.3f);
        rb.MoveRotation(moveQuat);

        // 애니메이션 플래그
        player.Move = true;

        return NodeResult.SUCCESS;
    }
}
