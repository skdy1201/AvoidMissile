using UnityEngine;

/// <summary>
/// CSV에서 미사일 타입별 초기 속도를 로딩한다.
/// 에디터 전용 — 패턴 에디터 시뮬레이션의 기본값으로 사용.
/// </summary>
public static class MissileDefaultStats
{
    private static float fallingSpeed = -1f;
    private static float grandSpeed   = -1f;
    private static float hoverSpeed   = -1f;

    public static float GetDefaultSpeed(PlacedMissileType type)
    {
        EnsureLoaded();
        switch (type)
        {
            case PlacedMissileType.Falling: return fallingSpeed;
            case PlacedMissileType.Grand:   return grandSpeed;
            case PlacedMissileType.Hover:   return hoverSpeed;
            default:                        return 1f;
        }
    }

    private static void EnsureLoaded()
    {
        if (fallingSpeed >= 0f) return;

        fallingSpeed = ReadSpeedFromCSV("FallingMissileSetting", "FallSpeed", 1f);
        grandSpeed   = ReadSpeedFromCSV("GrandMissileSetting",  "Speed",     0.5f);
        hoverSpeed   = ReadSpeedFromCSV("HoverMissileSetting",  "FlightSpeed", 1f);
    }

    private static float ReadSpeedFromCSV(string fileName, string rowName, float fallback)
    {
        var asset = Resources.Load<TextAsset>(fileName);
        if (asset == null) return fallback;

        var lines = asset.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            if (cols.Length < 2) continue;
            if (cols[0].Trim() == rowName)
            {
                if (float.TryParse(cols[1].Trim(), out float val))
                    return val;
            }
        }
        return fallback;
    }
}
