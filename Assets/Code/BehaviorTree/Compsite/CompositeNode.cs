using UnityEngine;
using System.Collections.Generic;

public abstract class CompositeNode : BehaviorNode
{
    public override void AddNode(BehaviorNode childNode)
    {
        childrens.Add(childNode);
    }

}
