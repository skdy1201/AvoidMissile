using UnityEngine;

/// <summary>
/// BehaviorTree 테스트용 컴포넌트
/// </summary>
[RequireComponent(typeof(BehaviorTreeRunner))]
public class SimpleBTTest : MonoBehaviour
{
    #region Private/Protected Fields
    private BehaviorTreeRunner runner;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        runner = GetComponent<BehaviorTreeRunner>();
    }

    private void Update()
    {
        if (runner == null || runner.TargetTree == null)
        {
            Debug.LogError("[SimpleBTTest] Runner 또는 TargetTree가 NULL!");
            return;
        }

        runner.RunBT();
    }
    #endregion
}
