using UnityEngine;
using System;

public  class LeafNode : BehaviorNode
{
    protected Func<GameObject, NodeResult> action;

    public LeafNode(Func<GameObject, NodeResult> action)
    {
        this.action = action;
    }

    public override void AddNode(BehaviorNode childNode)
    {
        Debug.Log("Ths is LeafNode");
        return;
    }

    public override NodeResult Execute(GameObject owner)
    {
        return action?.Invoke(owner) ?? NodeResult.FAILURE;
    }
}
