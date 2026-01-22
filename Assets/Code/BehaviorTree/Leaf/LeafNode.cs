using UnityEngine;

public abstract class LeafNode : BehaviorNode
{

    public override void AddNode(BehaviorNode childNode)
    {
        Debug.Log("This is LeafNode");
    }

    public override NodeResult Execute(GameObject owner)
    {
        return OnExecute(owner);
    }

    protected abstract NodeResult OnExecute(GameObject owner);
}
