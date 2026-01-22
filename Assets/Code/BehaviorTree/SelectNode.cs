using UnityEngine;

public class SelectNode : CompositeNode
{

    public override NodeResult Execute(GameObject owner)
    {
       foreach(var child in childrens)
        {
            NodeResult result = child.Execute(owner);

            if (result == NodeResult.SUCCESS)
                return result;
        }

        return NodeResult.FAILURE;
    }
}
