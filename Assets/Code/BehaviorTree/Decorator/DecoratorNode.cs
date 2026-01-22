using UnityEngine;

public abstract class DecoratorNode : BehaviorNode
{
    public override void AddNode(BehaviorNode node)
    {
        if (childrens.Count == 0)
            childrens.Add(node);
        else
            Debug.LogWarning("Already Child Exist");
    }
}
