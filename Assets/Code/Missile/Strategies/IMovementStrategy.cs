/// <summary>
/// 호버 미사일의 이동 전략 인터페이스
/// </summary>
/// <remarks>
/// 전략 패턴: 이동 로직을 별도 클래스로 분리하여
/// HoverMissile의 switch 분기를 제거하고 새 이동 유형을 쉽게 추가할 수 있게 함
/// </remarks>
public interface IMovementStrategy
{
    /// <summary>
    /// 매 FixedUpdate마다 호출되는 이동 로직
    /// </summary>
    /// <param name="missile">이동할 미사일</param>
    void Execute(HoverMissile missile);
}
