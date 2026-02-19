using System.Collections;
using UnityEngine;

/// <summary>
/// 이동-회전 분리 전략
/// </summary>
/// <remarks>
/// 일정 시간 직진 → 정지 → 플레이어 방향으로 회전 → 다시 직진 사이클을 반복.
/// 상태(moveTime, movementActive, rotationActive)를 내부에서 관리.
/// </remarks>
public class CustomHomingStrategy : IMovementStrategy
{
    private float moveTime;
    private bool movementActive = true;
    private bool rotationActive = false;

    public void Execute(HoverMissile missile)
    {
        if (moveTime > 0 && movementActive)
        {
            moveTime -= Time.fixedDeltaTime;

            // 이동 방향 업데이트 및 속도 적용
            missile.UpdateDirection(missile.Forward);
            missile.ApplyMovement();
        }
        else
        {
            // 회전 중에는 이동하지 않음
            movementActive = false;
            moveTime = missile.MaxMoveTime;

            Vector2 playerPoint = new Vector2(
                missile.PlayerPosition.x,
                missile.PlayerPosition.z
            );

            if (!rotationActive)
            {
                missile.StartMissileCoroutine(RotateToPlayer(missile, playerPoint));
                rotationActive = true;
            }
        }
    }

    /// <summary>
    /// 회전시간 동안 플레이어 방향으로 회전
    /// </summary>
    private IEnumerator RotateToPlayer(HoverMissile missile, Vector2 playerPoint)
    {
        float currentRotateTimer = 0f;

        Vector2 currentPosition = new Vector2(missile.Position.x, missile.Position.z);
        Vector2 direction = (playerPoint - currentPosition).normalized;
        Vector2 currentFront = new Vector2(missile.Forward.x, missile.Forward.z);

        // 내적으로 각도 구하기
        float dot = Vector2.Dot(direction, currentFront);
        float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

        // 외적으로 회전 방향 구하기
        float cross = currentFront.x * direction.y - currentFront.y * direction.x;
        float rotationDirection = -Mathf.Sign(cross);

        float rotateTime = missile.RotateTime;

        // 회전 타이머까지 도달하지 않았다면 반복
        while (currentRotateTimer <= rotateTime)
        {
            // 각도를 회전 시간으로 나누어 1초에 회전할 각도 산출, deltaTime으로 한 프레임 회전량 계산
            float rotationThisFrame = (angle / rotateTime) * Time.deltaTime;

            // 회전각도와 방향을 곱해서 해당 방향으로 각도만큼 회전
            missile.Rotation = missile.Rotation * Quaternion.Euler(0, rotationThisFrame * rotationDirection, 0);

            currentRotateTimer += Time.deltaTime;

            yield return null;
        }

        // 이동 플래그 활성화 && 회전 플래그 비활성화
        movementActive = true;
        rotationActive = false;
    }
}
