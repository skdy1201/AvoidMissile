using UnityEngine;

[CreateAssetMenu(fileName = "SlideNode", menuName = "BehaviorTree/Player/SlideNode")]
public class SlideNode : LeafNode
{
    protected override NodeResult OnExecute(GameObject owner)
    {
        Player player = owner.GetComponent<Player>();

        if (player.Slide == false)
        {
            return NodeResult.FAILURE;
        }

        Rigidbody rb = player.Rigidbody;
        Vector3 moveVector = owner.transform.forward * (player.Speed + 2.5f) * Time.fixedDeltaTime;

        // 이동
        rb.MovePosition(rb.position + moveVector);

        // 애니메이션 플래그 (슬라이드 중에는 Move = false)
        player.Move = false;

        return NodeResult.SUCCESS;
    }
}
