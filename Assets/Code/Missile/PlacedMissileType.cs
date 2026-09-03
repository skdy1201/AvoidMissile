/// <summary>
/// 패턴 에디터에서 배치된 미사일의 타입 분류.
/// Runtime 어셈블리에 배치하여 MissileStatHolder 등에서 참조 가능.
/// </summary>
//  이 값은 패턴 .bytes에 그대로 직렬화된다(PatternSave.ConvertPattern).
//   순서를 바꾸거나 중간에 끼워 넣으면 기존 패턴 파일이 깨진다. 바꾸려면 파일 마이그레이션이 함께 가야 한다.
public enum PlacedMissileType { Falling = 0, Hover = 1, Grand = 2 }
