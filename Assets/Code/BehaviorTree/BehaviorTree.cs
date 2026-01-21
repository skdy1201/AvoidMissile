using UnityEngine;


public class BehaviorTree : MonoBehaviour
{
    private BehaviorNode rootNode = new BehaviorNode();
    private BlackBoard bloackBoard = new BlackBoard();

    public void TestFunc()
    {
        BehaviorNode node = new BehaviorNode();

        //rootNode.AddNode(node);
    }
    
}
