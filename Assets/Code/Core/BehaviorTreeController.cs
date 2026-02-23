using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public enum BlackboardType
{
    Player,
}

public class BehaviorTreeController : Singleton<BehaviorTreeController>
{

    private Dictionary<BlackboardType, BlackBoard> Blackboards = new Dictionary<BlackboardType, BlackBoard>();


    protected override void StartProtocol()
    {
        if(SceneManager.GetActiveScene().name == GlobalData.Instance.PlayScene)
        {
            int blackboardCount = Enum.GetValues(typeof(BlackboardType)).Length;

            for(int i = 0; i < blackboardCount; ++i)
            {
                BlackBoard blackBoard = new BlackBoard();
                Blackboards.Add((BlackboardType)i, blackBoard);
            }

            Debug.Log($"BlackBoards size is {Blackboards.Count}");
        }
    }

    protected override void EndProtocol()
    {
        if (SceneManager.GetActiveScene().name == GlobalData.Instance.PlayScene)
        {
           foreach(var value in Blackboards)
            {
                value.Value.ClearBlackboard();
            }
        }
    }
}
