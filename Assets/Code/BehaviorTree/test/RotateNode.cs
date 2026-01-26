using UnityEngine;

/// <summary>
/// Y축 회전 노드
/// </summary>
[CreateAssetMenu(fileName = "RotateNode", menuName = "BehaviorTree/Test/RotateNode")]
public class RotateNode : LeafNode
{
    [SerializeField] private float rotateSpeed = 100f;

    protected override NodeResult OnExecute(GameObject owner)
    {
        owner.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        return NodeResult.SUCCESS;
    }
}
