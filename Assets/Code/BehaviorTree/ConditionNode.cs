using UnityEngine;
using System;

public class ConditionNode : LeafNode
{

    public ConditionNode(Func<GameObject, NodeResult> action) : base(action) { }

    public override NodeResult Execute(GameObject owner)
    {
       return action?.Invoke(owner) ?? NodeResult.FAILURE;
    }
}
