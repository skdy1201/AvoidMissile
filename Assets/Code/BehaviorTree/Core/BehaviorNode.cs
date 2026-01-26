using UnityEngine;
using System.Collections.Generic;

public enum NodeResult
{
    SUCCESS,
    FAILURE,
    RUNNING,
}

public enum BehaviorTreeNodeType
{
    Composite,
    Decorator,
    Leaf,
}

/// <summary>
/// BehaviorTree의 기본 노드 클래스
/// </summary>
public abstract class BehaviorNode : ScriptableObject
{
    #region Serialized Fields
    [SerializeField] protected List<BehaviorNode> children = new List<BehaviorNode>();
    #endregion

    #region Private/Protected Fields
    [System.NonSerialized] private NodeResult lastResult;
    [System.NonSerialized] private bool wasExecutedThisFrame;
    #endregion

    #region Properties
    /// <summary>
    /// 자식 노드 목록
    /// </summary>
    public List<BehaviorNode> Children => children;

    /// <summary>
    /// 마지막 실행 결과 (Play Mode 추적용)
    /// </summary>
    public NodeResult LastResult => lastResult;

    /// <summary>
    /// 이번 프레임에 실행되었는지 여부
    /// </summary>
    public bool WasExecutedThisFrame => wasExecutedThisFrame;

    /// <summary>
    /// 노드 타입 (Composite, Decorator, Leaf)
    /// </summary>
    public BehaviorTreeNodeType NodeType { get; protected set; }
    #endregion

    #region Public Methods
    /// <summary>
    /// 노드 실행 및 결과 저장
    /// </summary>
    public NodeResult Execute(GameObject owner)
    {
        lastResult = OnExecute(owner);
        wasExecutedThisFrame = true;
        return lastResult;
    }

    /// <summary>
    /// 자식 노드 추가
    /// </summary>
    public abstract void AddNode(BehaviorNode childNode);

    /// <summary>
    /// 매 프레임 시작 시 호출하여 실행 플래그 리셋
    /// </summary>
    public void ResetExecutionFlag()
    {
        wasExecutedThisFrame = false;
        foreach (var child in children)
        {
            child?.ResetExecutionFlag();
        }
    }
    #endregion

    #region Private/Protected Methods
    /// <summary>
    /// 실제 노드 로직 구현
    /// </summary>
    protected abstract NodeResult OnExecute(GameObject owner);
    #endregion
}

