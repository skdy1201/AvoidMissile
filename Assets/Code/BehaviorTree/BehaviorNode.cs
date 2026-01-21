using UnityEngine;
using System.Collections.Generic;

public enum NodeResult
{
    SUCCESS,
    FAILURE,
    RUNNING,
}

public class BehaviorNode
{
    BehaviorNode parent = null;
    BehaviorNode leftChild = null;
    BehaviorNode rightChild = null;
    NodeResult nodeState;

    public NodeResult NodeResult
    {
        get { return nodeState; }
    }

    public void AddNode(BehaviorNode childNode)
    {
        Queue<BehaviorNode> q = new Queue<BehaviorNode>();

        q.Enqueue(this);

        while (q.Count > 0)
        {
            BehaviorNode node = q.Dequeue();

            if (node.leftChild == null)
            {
                node.leftChild = childNode;
                childNode.parent = node;
                break;
            }
            else if (node.rightChild == null)
            {
                node.rightChild = childNode;
                childNode.parent = node;
                break;
            }

            q.Enqueue(node.leftChild);
            q.Enqueue(node.rightChild);
        }
    }
}

