using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 패턴 에디터 툴에서 만든 미사일 패턴을 바이너리로 저장/불러오기.
/// 저장 위치: Assets/Resources/Patterns/&lt;이름&gt;.bytes
/// </summary>
public static class PatternSave
{
    private const int    FileType      = 0;   // 0=Pattern (Item=1, Missile=2 등 추후 확장)
    private const int    FormatVersion = 1;
    private const string SaveFolder    = "Assets/Resources/Patterns";

    #region Save

    public static void Save()
    {
        string name = PatternEditorSimulation.PatternName;
        if (string.IsNullOrWhiteSpace(name)) name = "NewPattern";

        string absFolder = Path.Combine(Application.dataPath, "Resources/Patterns");
        if (!Directory.Exists(absFolder))
            Directory.CreateDirectory(absFolder);

        string path = Path.Combine(absFolder, $"{name}.bytes");

        using (var stream = File.Open(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            ConvertPattern(writer);

        AssetDatabase.Refresh();
        Debug.Log($"[PatternSave] 저장 완료: {path}");
    }

    private static void ConvertPattern(BinaryWriter writer)
    {
        var events   = PatternEditorSimulation.Events;
        var missiles = PatternEditorSceneInteraction.PlacedMissiles;

        writer.Write(FileType);
        writer.Write(FormatVersion);
        writer.Write(PatternEditorSimulation.PatternName);
        writer.Write(PatternEditorSimulation.TotalDuration);
        writer.Write(events.Count);

        foreach (var ev in events)
        {
            writer.Write(ev.Time);
            writer.Write((int)ev.EventType);
            writer.Write(ev.LinkedMissileIds.Count);

            foreach (int savedId in ev.LinkedMissileIds)
            {
                writer.Write(savedId);

                if (ev.EventType == PatternEventType.Spawn)
                {
                    var m   = FindMissileById(missiles, savedId);
                    var pos = m?.OriginalPosition ?? Vector3.zero;
                    writer.Write((int)(m?.Type ?? 0));
                    writer.Write(m?.LocationKey ?? "");
                    writer.Write(pos.x);
                    writer.Write(pos.y);
                    writer.Write(pos.z);
                }

                if (ev.EventType != PatternEventType.Destroy)
                {
                    ev.StatsSnapshots.TryGetValue(savedId, out var snap);
                    WriteSnapshot(writer, snap);
                }
            }
        }
    }

    private static void WriteSnapshot(BinaryWriter writer, MissileStatsSnapshot snap)
    {
        if (snap == null) snap = new MissileStatsSnapshot();
        writer.Write(snap.Speed);
        writer.Write(snap.Hp);
        writer.Write(snap.HoverType);
        writer.Write(snap.FlightTime);
        writer.Write(snap.TurnTime);
        writer.Write(snap.TurnRate);
        writer.Write(snap.GrandDiameter);
        writer.Write(snap.GrandDirection);
    }

    #endregion

    #region Load

    public static void ShowLoadMenu()
    {
        string absFolder = Path.Combine(Application.dataPath, "Resources/Patterns");
        if (!Directory.Exists(absFolder))
        {
            EditorUtility.DisplayDialog("불러오기", "저장된 패턴이 없습니다.", "확인");
            return;
        }

        string[] files = Directory.GetFiles(absFolder, "*.bytes");
        if (files.Length == 0)
        {
            EditorUtility.DisplayDialog("불러오기", "저장된 패턴이 없습니다.", "확인");
            return;
        }

        var menu = new GenericMenu();
        foreach (string file in files)
        {
            string captured = file;
            string label    = Path.GetFileNameWithoutExtension(file);
            menu.AddItem(new GUIContent(label), false, () => Load(captured));
        }
        menu.ShowAsContext();
    }

    private static void Load(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"[PatternSave] 파일 없음: {path}");
            return;
        }

        using (var stream = File.Open(path, FileMode.Open))
        using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
        {
            int fileType = reader.ReadInt32();
            if (fileType != FileType)
            {
                Debug.LogError($"[PatternSave] 파일 타입 불일치 (파일={fileType}, 기대={FileType}). 로드 중단.");
                return;
            }

            int version = reader.ReadInt32();
            if (version != FormatVersion)
            {
                Debug.LogWarning($"[PatternSave] 버전 불일치 (파일={version}, 현재={FormatVersion}). 호환 시도 중.");
                // 추후 버전별 분기 처리 위치
            }

            string name     = reader.ReadString();
            float  duration = reader.ReadSingle();
            int    evCount  = reader.ReadInt32();

            PatternEditorSceneInteraction.ClearMissilesForLoad();

            var idMapping    = new Dictionary<int, int>();
            var loadedEvents = new List<PatternEvent>();

            for (int i = 0; i < evCount; i++)
            {
                float time      = reader.ReadSingle();
                var   eventType = (PatternEventType)reader.ReadInt32();
                int   mCount    = reader.ReadInt32();

                var patternEvent = new PatternEvent
                {
                    Time      = time,
                    EventType = eventType,
                };

                for (int j = 0; j < mCount; j++)
                {
                    int savedId = reader.ReadInt32();

                    PlacedMissileType type        = PlacedMissileType.Falling;
                    string            locationKey  = "";
                    Vector3           savedPosition = Vector3.zero;

                    if (eventType == PatternEventType.Spawn)
                    {
                        type          = (PlacedMissileType)reader.ReadInt32();
                        locationKey   = reader.ReadString();
                        savedPosition = new Vector3(
                            reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    }

                    MissileStatsSnapshot snap = null;
                    if (eventType != PatternEventType.Destroy)
                        snap = ReadSnapshot(reader);

                    if (eventType == PatternEventType.Spawn)
                    {
                        int newId = PatternEditorSceneInteraction.PlaceMissileForLoad(
                            type, locationKey, savedPosition, snap, time);
                        if (newId >= 0)
                            idMapping[savedId] = newId;
                    }

                    if (idMapping.TryGetValue(savedId, out int mappedId))
                    {
                        patternEvent.LinkedMissileIds.Add(mappedId);
                        if (snap != null)
                            patternEvent.StatsSnapshots[mappedId] = snap;
                    }
                }

                loadedEvents.Add(patternEvent);
            }

            PatternEditorSimulation.LoadEventsDirectly(loadedEvents, duration, name);
        }

        Debug.Log($"[PatternSave] 불러오기 완료: {path}");
    }

    private static MissileStatsSnapshot ReadSnapshot(BinaryReader reader)
    {
        return new MissileStatsSnapshot
        {
            Speed          = reader.ReadSingle(),
            Hp             = reader.ReadInt32(),
            HoverType      = reader.ReadInt32(),
            FlightTime     = reader.ReadSingle(),
            TurnTime       = reader.ReadSingle(),
            TurnRate       = reader.ReadSingle(),
            GrandDiameter  = reader.ReadInt32(),
            GrandDirection = reader.ReadInt32(),
        };
    }

    #endregion

    #region Utility

    private static PlacedMissile FindMissileById(
        IReadOnlyList<PlacedMissile> missiles, int id)
    {
        foreach (var m in missiles)
            if (m.Id == id) return m;
        return null;
    }

    #endregion
}
