using UnityEngine;

/// <summary>
/// 수평 직선 이동 전략
/// </summary>
/// <remarks>
/// 추적 없이 초기 방향으로 직선 이동.
/// 스폰 시 설정된 forward 방향을 유지하며 전진.
/// </remarks>
public class HorizonLinearStrategy : IMovementStrategy
{
    public void Execute(HoverMissile missile)
    {
        missile.UpdateDirection(missile.Forward);
        missile.ApplyMovement();
    }
}
