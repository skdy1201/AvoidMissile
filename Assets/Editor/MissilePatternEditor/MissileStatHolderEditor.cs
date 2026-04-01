using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MissileStatHolder))]
[CanEditMultipleObjects]
public class MissileStatHolderEditor : Editor
{
    private SerializedProperty missileType;
    private SerializedProperty speed;

    // Hover
    private SerializedProperty hp;
    private SerializedProperty hoverType;
    private SerializedProperty flightTime;
    private SerializedProperty turnTime;
    private SerializedProperty turnRate;

    // Grand
    private SerializedProperty grandDiameter;
    private SerializedProperty grandDirection;

    private static readonly string[] GrandDirectionLabels =
    {
        "Vertical (↓)",
        "N → S (↑→↓)",
        "S → N (↓→↑)",
        "E → W (→←)",
        "W → E (←→)",
    };

    private void OnEnable()
    {
        missileType    = serializedObject.FindProperty("missileType");
        speed          = serializedObject.FindProperty("speed");
        hp             = serializedObject.FindProperty("hp");
        hoverType      = serializedObject.FindProperty("hoverType");
        flightTime     = serializedObject.FindProperty("flightTime");
        turnTime       = serializedObject.FindProperty("turnTime");
        turnRate       = serializedObject.FindProperty("turnRate");
        grandDiameter  = serializedObject.FindProperty("grandDiameter");
        grandDirection = serializedObject.FindProperty("grandDirection");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var type = (PlacedMissileType)missileType.enumValueIndex;

        // 멀티 선택 표시
        if (targets.Length > 1)
        {
            EditorGUILayout.HelpBox($"Multi Select — {targets.Length} missiles", MessageType.Info);
            EditorGUILayout.Space(2);
        }

        // 타입 표시 (읽기 전용)
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.EnumPopup("Missile Type", type);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(4);

        // 공통: Speed
        EditorGUILayout.PropertyField(speed, new GUIContent("Speed"));

        switch (type)
        {
            case PlacedMissileType.Falling:
                DrawFallingStats();
                break;
            case PlacedMissileType.Hover:
                DrawHoverStats();
                break;
            case PlacedMissileType.Grand:
                DrawGrandStats();
                break;
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            float now = PatternEditorSimulation.CurrentTime;
            var statChangeIds = new List<int>();
            var statChangeSnaps = new Dictionary<int, PatternEditorSimulation.MissileStatsSnapshot>();
            var spawnUpdateSnaps = new Dictionary<int, PatternEditorSimulation.MissileStatsSnapshot>();

            foreach (var t in targets)
            {
                var holder = t as MissileStatHolder;
                if (holder == null) continue;

                var snap = PatternEditorSimulation.MissileStatsSnapshot.FromHolder(holder);

                if (PatternEditorSimulation.HasSpawnEventAt(now, holder.missileId))
                {
                    // 스폰 시점 변경 → Spawn 이벤트 스냅샷 갱신
                    spawnUpdateSnaps[holder.missileId] = snap;
                }
                else
                {
                    // 별도 시점 → StatChange 이벤트 생성
                    statChangeIds.Add(holder.missileId);
                    statChangeSnaps[holder.missileId] = snap;
                }
            }

            // Spawn 이벤트 스냅샷 갱신
            if (spawnUpdateSnaps.Count > 0)
                PatternEditorSimulation.UpdateSpawnSnapshots(now, spawnUpdateSnaps);

            // StatChange 이벤트 생성
            if (statChangeIds.Count > 0)
                PatternEditorSimulation.AddEvent(PatternEditorSimulation.PatternEventType.StatChange,
                    now, statChangeIds, statChangeSnaps);
        }
    }

    private void DrawFallingStats()
    {
        // Falling은 speed만 — 추가 필드 없음
    }

    private void DrawHoverStats()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Hover Stats", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(hp, new GUIContent("HP"));
        EditorGUILayout.PropertyField(hoverType, new GUIContent("Strategy"));

        var strategy = (HoverMissileType)hoverType.enumValueIndex;
        if (strategy != HoverMissileType.HorizonLinear)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Homing Stats", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(flightTime, new GUIContent("Flight Time"));
            EditorGUILayout.PropertyField(turnTime, new GUIContent("Turn Time"));
            EditorGUILayout.PropertyField(turnRate, new GUIContent("Turn Rate (°/s)"));
        }
    }

    private void DrawGrandStats()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Grand Stats", EditorStyles.boldLabel);

        EditorGUILayout.IntSlider(grandDiameter, 2, 5, new GUIContent("Diameter"));

        int dirIndex = grandDirection.intValue;
        int newDir = EditorGUILayout.Popup("Direction", dirIndex, GrandDirectionLabels);
        if (newDir != dirIndex)
            grandDirection.intValue = newDir;
    }
}
