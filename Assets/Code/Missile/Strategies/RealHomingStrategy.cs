using UnityEngine;

/// <summary>
/// 실시간 추적 이동 전략
/// </summary>
/// <remarks>
/// 매 프레임 플레이어 방향으로 회전하며 전진.
/// 현실의 유도 미사일과 유사한 동작.
/// </remarks>
public class RealHomingStrategy : IMovementStrategy
{
    public void Execute(HoverMissile missile)
    {
        // 이동 방향 업데이트 및 속도 적용
        missile.UpdateDirection(missile.Forward);
        missile.ApplyMovement();

        // 플레이어 방향으로 점진적 회전 (Y축만)
        Vector3 targetDir = (missile.PlayerPosition - missile.Position).normalized;
        Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);

        targetRot.z = targetRot.x = 0;
        missile.Rotation = Quaternion.RotateTowards(
            missile.Rotation,
            targetRot,
            missile.RotateSpeed * Time.fixedDeltaTime
        );
    }
}
