using UnityEngine;
using System.Collections.Generic;

public enum NodeResult
{
    SUCCESS,
    FAILURE,
    RUNNING,
}


public abstract class BehaviorNode : ScriptableObject
{
    [SerializeField] protected List<BehaviorNode> childrens = new List<BehaviorNode>();

    public abstract NodeResult Execute(GameObject owner);
    public abstract void AddNode(BehaviorNode childNode);

}

