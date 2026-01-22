using UnityEngine;

[CreateAssetMenu(fileName = "RotateNode", menuName = "BehaviorTree/Test/RotateNode")]
public class RotateNode : LeafNode
{
    [SerializeField] private float rotateSpeed = 100f;

    protected override NodeResult OnExecute(GameObject owner)
    {
        owner.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        Debug.Log("[BT] Rotating - SUCCESS");
        return NodeResult.SUCCESS;
    }
}
