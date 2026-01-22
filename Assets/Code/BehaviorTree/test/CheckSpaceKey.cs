using UnityEngine;

[CreateAssetMenu(fileName = "CheckSpaceKey", menuName = "BehaviorTree/Test/CheckSpaceKey")]
public class CheckSpaceKey : LeafNode
{
    protected override NodeResult OnExecute(GameObject owner)
    {
        if (Input.GetKey(KeyCode.Space))
        {
            Debug.Log("[BT] Space Key Pressed - SUCCESS");
            return NodeResult.SUCCESS;
        }

        return NodeResult.FAILURE;
    }
}
