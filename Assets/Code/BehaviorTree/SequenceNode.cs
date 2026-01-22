using UnityEngine;
using System.Collections.Generic;

public class SequenceNode : CompositeNode
{

    public override NodeResult Execute(GameObject owner)
    {
        foreach(var child in childrens)
        {
            NodeResult childResult = child.Execute(owner);

            if(childResult == NodeResult.FAILURE)
                return childResult;
        }

        return NodeResult.SUCCESS;
    }
}
