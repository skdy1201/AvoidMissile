using UnityEngine;

/// <summary>
/// CSV에서 미사일 타입별 초기 스탯을 로딩한다.
/// 에디터 전용 — 패턴 에디터의 기본값 + 개별 오버라이드 기준.
/// </summary>
public static class MissileStats
{
    #region Falling Defaults

    private static float fallingSpeed = -1f;

    #endregion

    #region Hover Defaults

    private static float hoverSpeed      = -1f;
    private static int   hoverHp         = -1;
    private static float hoverFlightTime = -1f;
    private static float hoverTurnTime   = -1f;
    private static float hoverTurnRate   = -1f;

    #endregion

    #region Grand Defaults

    private static float grandSpeed    = -1f;
    private static int   grandDiameter = -1;

    #endregion

    #region Public API

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

    public static int GetDefaultHoverHp()
    {
        EnsureLoaded();
        return hoverHp;
    }

    public static float GetDefaultHoverFlightTime()
    {
        EnsureLoaded();
        return hoverFlightTime;
    }

    public static float GetDefaultHoverTurnTime()
    {
        EnsureLoaded();
        return hoverTurnTime;
    }

    public static float GetDefaultHoverTurnRate()
    {
        EnsureLoaded();
        return hoverTurnRate;
    }

    public static int GetDefaultGrandDiameter()
    {
        EnsureLoaded();
        return grandDiameter;
    }

    #endregion

    #region CSV Loading

    private static void EnsureLoaded()
    {
        if (fallingSpeed >= 0f) return;

        // Falling
        fallingSpeed = ReadFloatFromCSV("FallingMissileSetting", "FallSpeed", 1f);

        // Hover
        hoverSpeed      = ReadFloatFromCSV("HoverMissileSetting", "FlightSpeed", 1f);
        hoverHp         = ReadIntFromCSV("HoverMissileSetting",   "HP",          1);
        hoverFlightTime = ReadFloatFromCSV("HoverMissileSetting", "Flight",      2f);
        hoverTurnTime   = ReadFloatFromCSV("HoverMissileSetting", "Turn",        5f);
        hoverTurnRate   = ReadFloatFromCSV("HoverMissileSetting", "TurnRate",    90f);

        // Grand
        grandSpeed    = ReadFloatFromCSV("GrandMissileSetting", "Speed",    0.5f);
        grandDiameter = ReadIntFromCSV("GrandMissileSetting",   "Diameter", 2);
    }

    private static float ReadFloatFromCSV(string fileName, string rowName, float fallback)
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

    private static int ReadIntFromCSV(string fileName, string rowName, int fallback)
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
                if (int.TryParse(cols[1].Trim(), out int val))
                    return val;
            }
        }
        return fallback;
    }

    #endregion
}
