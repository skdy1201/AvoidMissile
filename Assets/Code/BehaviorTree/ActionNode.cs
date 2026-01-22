using UnityEngine;
using System;

public class ActionNode : LeafNode
{
    public ActionNode(Func<GameObject, NodeResult> action) : base(action) { }

    public override NodeResult Execute(GameObject owner)
    {
        return this.action?.Invoke(owner) ?? NodeResult.FAILURE;
    }
}
