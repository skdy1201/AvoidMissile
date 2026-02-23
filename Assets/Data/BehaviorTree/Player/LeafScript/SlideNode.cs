using UnityEngine;

[CreateAssetMenu(fileName = "SlideNode", menuName = "BehaviorTree/Player/SlideNode")]
public class SlideNode : LeafNode
{
    protected override NodeResult OnExecute(GameObject owner)
    {
        Player player = owner.GetComponent<Player>();

        if (player.Slide == false)
            return NodeResult.FAILURE;

        // 슬라이드 속도 설정 (이동은 Player.ApplyVelocity에서 통합 처리)
        player.SlideVelocity = owner.transform.forward * (player.Speed + 2.5f);

        // 애니메이션 플래그 (슬라이드 중에는 Move = false)
        player.Move = false;

        return NodeResult.SUCCESS;
    }
}
