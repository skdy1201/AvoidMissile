using UnityEngine;
using System;

public abstract class LeafNode : BehaviorNode
{
    protected Func<GameObject, NodeResult> action;

    protected abstract NodeResult OnExecute(GameObject owner);

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
        return OnExecute(owner);
    }

}
