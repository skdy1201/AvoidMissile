using UnityEngine;

/// <summary>
/// 물리 데이터 구조체
/// </summary>
/// <remarks>
/// 미사일, 플레이어 등 다양한 오브젝트에 적용 가능한 통일된 속도 데이터.
/// Rigidbody.linearVelocity에 적용할 속도 벡터를 계산한다.
/// </remarks>
[System.Serializable]
public struct PhysicsData
{
    #region Fields

    /// <summary>
    /// 속도 크기 (units/second)
    /// </summary>
    public float speed;

    /// <summary>
    /// 이동 방향 (정규화된 벡터)
    /// </summary>
    public Vector3 direction;

    #endregion

    #region Properties

    /// <summary>
    /// 최종 속도 벡터 (direction * speed)
    /// </summary>
    public Vector3 Velocity => direction * speed;

    #endregion

    #region Constructors

    /// <summary>
    /// PhysicsData 생성자
    /// </summary>
    /// <param name="speed">속도 크기 (units/second)</param>
    /// <param name="direction">이동 방향 (자동 정규화)</param>
    public PhysicsData(float speed, Vector3 direction)
    {
        this.speed = speed;
        this.direction = direction.normalized;
    }

    #endregion
}
