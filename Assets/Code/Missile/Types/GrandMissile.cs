using UnityEngine;

public enum 

public class GrandMissile : Missile
{
    #region Serialize Field

    
    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

    }

    private void OnEnable()
    {
        
    }

    private void Start()
    {
        
    }

    private void FixedUpdate()
    {
        // 속도 적용 (매 물리 프레임마다 일정한 속도 유지)
        ApplyVelocity();
    }

    #endregion
}
