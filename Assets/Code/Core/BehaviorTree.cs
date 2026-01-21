using UnityEngine;

public class BehaviorNode
{
    private enum NodeResult
    {
        SUCCESS,
        FAILURE,
        RUNNING,
    }


    BehaviorNode parent = null;
    BehaviorNode leftChild = null;
    BehaviorNode rightChild = null;

    [SerializeField] static public int count = 0;


    public bool EmptyChild()
    {
        return leftChild == null || rightChild == null;
    }

    public void AddNode(BehaviorNode childNode)
    {
        childNode.parent = this;

        if (leftChild == null)
        {
            leftChild = childNode;
            Debug.Log("왼쪽 자식 성공");
            count++;
            Debug.Log($"{count} 번째 노드");
            return;
        }
        
        if (rightChild == null)
        {
            rightChild = childNode;
            Debug.Log("오른쪽 자식 성공");
            count++;
            Debug.Log($"{count} 번째 노드");

            return;
        }


        if (leftChild.EmptyChild())
        {
            Debug.Log("왼쪽 자식 출발");
            leftChild.AddNode(childNode);
            return;
        }
        else if (rightChild.EmptyChild())
        {
            Debug.Log("오른쪽 자식 출발");
            rightChild.AddNode(childNode);
            return;

        }
        else
        {
            Debug.Log("양쪽 꽉 참");
            leftChild.AddNode(childNode);
        }

    }
}

public class BehaviorTree : MonoBehaviour
{
    private BehaviorNode rootNode = new BehaviorNode();

    public void TestFunc()
    {
        BehaviorNode node = new BehaviorNode();

        rootNode.AddNode(node);
    }
    
}
