using UnityEngine;
using System.Collections.Generic;

public enum NodeResult
{
    SUCCESS,
    FAILURE,
    RUNNING,
}

public abstract class BehaviorNode
{
    protected List<BehaviorNode> childrens = new List<BehaviorNode>();

    NodeResult nodeState;

    public abstract NodeResult Execute(GameObject owner);
    public abstract void AddNode(BehaviorNode childNode);

    public NodeResult NodeResult
    {
        get { return nodeState; }
    }

}

