using UnityEngine;

public enum ParallelPolicy
{
    RequireAll,  // 모두 SUCCESS여야 SUCCESS (AND)
    RequireOne   // 하나라도 SUCCESS면 SUCCESS (OR)
}

[CreateAssetMenu(fileName = "ParallelNode", menuName = "BehaviorTree/ParallelNode")]
public class ParallelNode : CompositeNode
{
    [SerializeField] ParallelPolicy policy = ParallelPolicy.RequireAll;

    #region Public Methods
    public override void AddNode(BehaviorNode childNode)
    {
        children.Add(childNode);
    }

    protected override NodeResult OnExecute(GameObject owner)
    {
        int successCount = 0;
        int failureCount = 0;
        bool hasRunning = false;

        // 모든 자식 실행 (중간에 멈추지 않음)
        foreach (var child in children)
        {
            NodeResult result = child.Execute(owner);

            switch (result)
            {
                case NodeResult.SUCCESS:
                    successCount++;
                    break;
                case NodeResult.FAILURE:
                    failureCount++;
                    break;
                case NodeResult.RUNNING:
                    hasRunning = true;
                    break;
            }
        }

        // 하나라도 RUNNING이면 전체 RUNNING
        if (hasRunning)
            return NodeResult.RUNNING;

        return policy switch
        {
            ParallelPolicy.RequireAll => successCount == children.Count ? NodeResult.SUCCESS : NodeResult.FAILURE,
            ParallelPolicy.RequireOne => successCount > 0 ? NodeResult.SUCCESS : NodeResult.FAILURE,
            _ => NodeResult.FAILURE
        };
    }
    #endregion
}
