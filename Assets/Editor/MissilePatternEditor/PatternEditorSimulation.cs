using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 미사일 패턴 에디터 시뮬레이션 — 재생 루프, 타임라인, 배속 컨트롤, 이벤트 마커 시스템.
/// static class. PatternEditorToolbar(UIElements)와 SceneInteraction(IMGUI)에서 참조.
/// </summary>
public static class PatternEditorSimulation
{
    #region Types

    public enum EndOfPatternPolicy { Destroy, KeepLast }
    public enum PatternEventType { Spawn, StatChange, Destroy }

    public class PatternEvent
    {
        public float Time;
        public PatternEventType EventType;
        public List<int> LinkedMissileIds = new List<int>();
    }

    #endregion

    #region Constants

    private static readonly float[]  SpeedPresets = { 0.25f, 0.5f, 1f, 2f };
    private static readonly string[] SpeedLabels  = { "0.25x", "0.5x", "1x", "2x" };
    private const int   CustomSpeedIndex       = 4;
    private const float DefaultTotalDuration   = 10f;

    // 타임라인 레이아웃
    private const float TimelineHeight  = 60f;
    private const float ControlRowH     = 22f;
    private const float TrackH          = 26f;
    private const float TrackTopOffset  = 30f;

    // 타임라인 색상
    private static readonly Color PlaybarBackground   = new Color(0.12f, 0.12f, 0.12f, 0.92f);
    private static readonly Color TrackBackground     = new Color(0.06f, 0.06f, 0.06f);
    private static readonly Color MarkerSpawn          = new Color(0.30f, 0.55f, 0.90f, 0.90f);
    private static readonly Color MarkerStatChange     = new Color(0.90f, 0.80f, 0.20f, 0.90f);
    private static readonly Color MarkerDestroy        = new Color(0.90f, 0.30f, 0.30f, 0.90f);
    private static readonly Color MarkerSelected       = new Color(1f, 1f, 1f, 0.95f);
    private const float MarkerRadius = 5f;
    private static readonly Color TickColor           = new Color(0.45f, 0.45f, 0.45f);

    #endregion

    #region State

    private static float  currentTime;
    private static float  totalDuration      = DefaultTotalDuration;
    private static bool   isPlaying;
    private static bool   loopPlayback;
    private static int    speedIndex         = 2;   // SpeedPresets[2] = 1x
    private static float  playSpeed          = 1f;
    private static float  savedCustomSpeed   = 3f;
    private static bool   customSpeedFieldFocused;
    private static bool   pendingCustomFieldFocus;
    private static double lastEditorTime;
    private static EndOfPatternPolicy endPolicy = EndOfPatternPolicy.Destroy;

    // 타임라인 줌/팬
    private static float timelineZoom      = 1f;
    private static float timelineViewStart;

    // 이벤트 마커
    private static readonly List<PatternEvent> patternEvents = new List<PatternEvent>();
    private static int selectedEventIndex = -1;


    private static bool initialized;

    #endregion

    #region Public Properties

    public static float CurrentTime      => currentTime;
    public static float TotalDuration    => totalDuration;
    public static bool  IsPlaying        => isPlaying;
    public static bool  LoopPlayback     { get => loopPlayback; set { loopPlayback = value; NotifyStateChanged(); } }
    public static float PlaySpeed        => playSpeed;
    public static int   SpeedIndex       => speedIndex;
    public static float SavedCustomSpeed => savedCustomSpeed;
    public static EndOfPatternPolicy EndPolicy
    {
        get => endPolicy;
        set { endPolicy = value; NotifyStateChanged(); }
    }
    public static IReadOnlyList<PatternEvent> Events => patternEvents;
    public static int SelectedEventIndex { get => selectedEventIndex; set => selectedEventIndex = value; }

    /// <summary>현재 배속 라벨 (툴바 표시용).</summary>
    public static string CurrentSpeedLabel =>
        speedIndex < SpeedPresets.Length ? SpeedLabels[speedIndex] : $"{playSpeed:G3}x";

    #endregion

    #region Events

    /// <summary>상태 변경 시 발생 — 툴바 UI 갱신 트리거.</summary>
    public static event Action OnStateChanged;

    private static void NotifyStateChanged() => OnStateChanged?.Invoke();

    #endregion

    #region Public API

    public static void Initialize()
    {
        if (initialized) return;
        EditorApplication.update += OnEditorUpdate;
        lastEditorTime = EditorApplication.timeSinceStartup;
        initialized = true;
    }

    public static void Cleanup()
    {
        if (!initialized) return;
        EditorApplication.update -= OnEditorUpdate;

        // 상태 초기화
        currentTime         = 0f;
        totalDuration       = DefaultTotalDuration;
        isPlaying           = false;
        loopPlayback        = false;
        speedIndex          = 2;
        playSpeed           = 1f;
        savedCustomSpeed    = 3f;
        customSpeedFieldFocused  = false;
        pendingCustomFieldFocus  = false;
        endPolicy           = EndOfPatternPolicy.Destroy;
        timelineZoom        = 1f;
        timelineViewStart   = 0f;
        patternEvents.Clear();
        selectedEventIndex = -1;
        initialized = false;
    }

    public static void TogglePlay()
    {
        if (!isPlaying && currentTime >= totalDuration)
        {
            currentTime = 0f;
            ResetMissilePositions();
        }

        isPlaying = !isPlaying;

        // 일시정지: 현재 위치 유지 (복원하지 않음)

        NotifyStateChanged();
        SceneView.RepaintAll();
    }

    public static void CycleSpeed()
    {
        speedIndex = (speedIndex + 1) % (SpeedPresets.Length + 1);
        if (speedIndex < SpeedPresets.Length)
            playSpeed = SpeedPresets[speedIndex];
        else
        {
            playSpeed = savedCustomSpeed;
            pendingCustomFieldFocus = true;
        }
        NotifyStateChanged();
        SceneView.RepaintAll();
    }

    public static void SetCustomSpeed(float speed)
    {
        playSpeed = Mathf.Max(float.Epsilon, speed);
        savedCustomSpeed = playSpeed;
        NotifyStateChanged();
    }

    public static void SetCurrentTime(float t)
    {
        currentTime = Mathf.Clamp(t, 0f, totalDuration);
        SceneView.RepaintAll();
    }

    public static void SetTotalDuration(float d)
    {
        totalDuration = Mathf.Clamp(d, 0.1f, 600f);
        currentTime   = Mathf.Clamp(currentTime, 0f, totalDuration);
        // 범위 밖 이벤트 클램프
        foreach (var ev in patternEvents)
            ev.Time = Mathf.Clamp(ev.Time, 0f, totalDuration);
        ClampTimelineView();
        SceneView.RepaintAll();
    }

    public static void JumpToPrevSegmentOrStart()
    {
        float prevTime = 0f;
        foreach (var ev in patternEvents)
            if (ev.Time < currentTime - 0.01f)
                prevTime = Mathf.Max(prevTime, ev.Time);
        currentTime = prevTime;
        ResetMissilePositions();
        NotifyStateChanged();
        SceneView.RepaintAll();
    }

    public static void JumpToNextSegmentOrEnd()
    {
        float nextTime = totalDuration;
        foreach (var ev in patternEvents)
            if (ev.Time > currentTime + 0.01f)
                nextTime = Mathf.Min(nextTime, ev.Time);
        currentTime = nextTime;
        ResetMissilePositions();
        NotifyStateChanged();
        SceneView.RepaintAll();
    }

    /// <summary>이벤트 마커 추가 (미사일 배치 등에서 호출).</summary>
    public static void AddEvent(PatternEventType type, float time, List<int> missileIds = null)
    {
        var ev = new PatternEvent
        {
            Time = Mathf.Clamp(time, 0f, totalDuration),
            EventType = type,
            LinkedMissileIds = missileIds ?? new List<int>()
        };
        patternEvents.Add(ev);
        patternEvents.Sort((a, b) => a.Time.CompareTo(b.Time));
        selectedEventIndex = patternEvents.IndexOf(ev);
        SceneView.RepaintAll();
    }

    /// <summary>선택된 이벤트 마커 삭제.</summary>
    public static void DeleteSelectedEvent()
    {
        if (selectedEventIndex < 0 || selectedEventIndex >= patternEvents.Count) return;
        patternEvents.RemoveAt(selectedEventIndex);
        selectedEventIndex = -1;
        SceneView.RepaintAll();
    }

    /// <summary>미사일 ID가 연결된 이벤트에서 제거. 빈 이벤트는 자동 삭제.</summary>
    public static void RemoveMissileFromEvents(int missileId)
    {
        for (int i = patternEvents.Count - 1; i >= 0; i--)
        {
            patternEvents[i].LinkedMissileIds.Remove(missileId);
            if (patternEvents[i].LinkedMissileIds.Count == 0)
            {
                patternEvents.RemoveAt(i);
                if (selectedEventIndex == i) selectedEventIndex = -1;
                else if (selectedEventIndex > i) selectedEventIndex--;
            }
        }
        SceneView.RepaintAll();
    }

    #endregion

    #region dt Loop

    private static void OnEditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        if (isPlaying)
        {
            float dt = (float)(now - lastEditorTime) * playSpeed;
            currentTime = Mathf.Clamp(currentTime + dt, 0f, totalDuration);

            // ghost 이동
            MoveMissiles(dt);

            if (currentTime >= totalDuration)
            {
                if (loopPlayback)
                {
                    currentTime = 0f;
                    ResetMissilePositions();
                }
                else
                {
                    isPlaying = false;
                    NotifyStateChanged();
                }
            }
            SceneView.RepaintAll();
        }
        lastEditorTime = now;
    }

    /// <summary>배치된 미사일 ghost를 방향×속도로 이동 + 데칼 스케일 + 충돌 감지.</summary>
    private static void MoveMissiles(float dt)
    {
        var missiles = PatternEditorSceneInteraction.PlacedMissiles;
        for (int i = 0; i < missiles.Count; i++)
        {
            var m = missiles[i];
            if (m.ghost == null || m.hidden) continue;

            m.ghost.transform.position += m.direction * m.speed * dt;

            // 데칼: 플랫폼 표면에 고정 + 높이 비율 스케일
            UpdateDecal(m);

            // 충돌 감지: 플랫폼 Y 도달 시 숨김
            if (HasReachedPlatform(m))
            {
                m.ghost.SetActive(false);
                m.hidden = true;
            }
        }
    }

    /// <summary>데칼을 플랫폼 표면에 고정 + 높이 비율로 크기 조절.</summary>
    private static void UpdateDecal(PlacedMissile m)
    {
        if (m.decalTransform == null) return;

        // 데칼 위치를 플랫폼 표면 XZ에 고정 (ghost 자식이라 같이 움직이므로 매 프레임 보정)
        var missilePos = m.ghost.transform.position;
        float decalYOffset = m.type == PlacedMissileType.Grand ? -0.5f : 1.1f;
        float decalY = m.platformY + decalYOffset;
        m.decalTransform.position = new Vector3(missilePos.x, decalY, missilePos.z);

        var projector = m.decalTransform.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
        if (projector == null) return;

        float totalDrop = m.spawnHeight - m.platformY;
        if (totalDrop <= 0f) return;

        float currentHeight = missilePos.y - m.platformY;
        float ratio = 1f - Mathf.Clamp01(currentHeight / totalDrop);

        projector.size = new Vector3(
            m.decalMaxSize.x * ratio,
            m.decalMaxSize.y * ratio,
            m.decalMaxSize.z);
    }

    /// <summary>미사일 선두가 플랫폼/바운더리에 도달했는지 확인.</summary>
    private static bool HasReachedPlatform(PlacedMissile m)
    {
        Vector3 pos = m.ghost.transform.position;

        // Renderer bounds로 선두 오프셋 계산
        float frontOffset = 0f;
        var renderer = m.ghost.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            var bounds = renderer.bounds;
            // 이동 방향 축의 extent (중심에서 가장자리까지 거리)
            frontOffset = Mathf.Abs(Vector3.Dot(bounds.extents, m.direction));
        }

        // Falling / Grand Vertical: 선두(아랫면)가 플랫폼 이하
        if (m.direction.y < 0f)
            return (pos.y - frontOffset) <= m.platformY;

        // Hover / Grand Horizontal: 선두가 게임 바운더리(±50) 밖
        Vector3 frontPos = pos + m.direction * frontOffset;
        const float boundaryLimit = 50f;
        return frontPos.x < -boundaryLimit || frontPos.x > boundaryLimit
            || frontPos.z < -boundaryLimit || frontPos.z > boundaryLimit;
    }

    /// <summary>모든 미사일 ghost를 원래 배치 위치로 복원.</summary>
    private static void ResetMissilePositions()
    {
        var missiles = PatternEditorSceneInteraction.PlacedMissiles;
        for (int i = 0; i < missiles.Count; i++)
        {
            var m = missiles[i];
            if (m.ghost == null) continue;

            m.ghost.transform.position = m.originalPosition;

            // 숨김 복원
            if (m.hidden)
            {
                m.ghost.SetActive(true);
                m.hidden = false;
            }

            // 데칼 크기 원래대로 복원
            ResetDecalScale(m);
        }
    }

    /// <summary>데칼을 배치 시 최대 크기로 복원.</summary>
    private static void ResetDecalScale(PlacedMissile m)
    {
        if (m.decalTransform == null) return;

        var projector = m.decalTransform.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
        if (projector == null) return;

        projector.size = m.decalMaxSize;
    }

    #endregion

    #region Timeline Drawing (IMGUI)

    /// <summary>
    /// 씬뷰 하단에 타임라인 바를 렌더링한다.
    /// SceneInteraction.OnSceneGUI에서 호출.
    /// </summary>
    public static void DrawTimeline(SceneView sceneView)
    {
        float svW = sceneView.position.width;
        float svH = sceneView.position.height;
        Rect area = new Rect(0, svH - TimelineHeight - 20f, svW, TimelineHeight);

        Handles.BeginGUI();

        // 배경
        EditorGUI.DrawRect(area, PlaybarBackground);

        // 컨트롤 행
        DrawControlRow(new Rect(area.x + 4, area.y + 4, area.width - 8, ControlRowH));

        // 타임라인 트랙
        Rect trackRect = new Rect(area.x + 4, area.y + TrackTopOffset, area.width - 8, TrackH);
        if (Event.current.type == EventType.Repaint)
            DrawTimelineTrack(trackRect);
        HandleTimelineInput(trackRect);

        Handles.EndGUI();
    }

    /// <summary>타임라인 영역 위에 마우스가 있는지 확인 (클릭 차단용).</summary>
    public static bool IsMouseOverTimeline(SceneView sceneView, Vector2 mousePos)
    {
        float svH = sceneView.position.height;
        Rect area = new Rect(0, svH - TimelineHeight - 20f, sceneView.position.width, TimelineHeight);
        return area.Contains(mousePos);
    }

    private static void DrawControlRow(Rect rowRect)
    {
        GUILayout.BeginArea(rowRect);
        GUILayout.BeginHorizontal();

        // 현재 시간 표시
        GUILayout.Label($"{currentTime:F2} / {totalDuration:F2} s", EditorStyles.boldLabel,
            GUILayout.Width(116));
        GUILayout.Space(6);

        // 총 재생 길이 편집
        GUILayout.Label("길이:", GUILayout.Width(24));
        float newDur = EditorGUILayout.DelayedFloatField(totalDuration, GUILayout.Width(42));
        if (newDur != totalDuration)
            SetTotalDuration(newDur);
        GUILayout.Label("s", GUILayout.Width(10));
        GUILayout.Space(10);

        // 시간 직접 이동
        GUILayout.Label("이동:", GUILayout.Width(28));
        float jumpInput = EditorGUILayout.DelayedFloatField(currentTime, GUILayout.Width(42));
        if (Mathf.Abs(jumpInput - currentTime) > 0.001f)
            SetCurrentTime(jumpInput);
        GUILayout.Label("s", GUILayout.Width(10));

        GUILayout.FlexibleSpace();

        // Custom 배속 필드 (Custom 모드일 때만)
        if (speedIndex == CustomSpeedIndex)
        {
            GUI.SetNextControlName("SimCustomSpeedField");
            float newSpeed = EditorGUILayout.FloatField(playSpeed, GUILayout.Height(18), GUILayout.Width(40));
            customSpeedFieldFocused = GUI.GetNameOfFocusedControl() == "SimCustomSpeedField";
            if (pendingCustomFieldFocus)
            {
                EditorGUI.FocusTextInControl("SimCustomSpeedField");
                pendingCustomFieldFocus = false;
            }
            if (newSpeed != playSpeed)
                SetCustomSpeed(newSpeed);
            GUILayout.Label("x", GUILayout.Width(10));
            GUILayout.Space(4);
        }

        // 루프 토글
        GUI.backgroundColor = loopPlayback ? new Color(0.4f, 0.8f, 0.4f) : Color.white;
        if (GUILayout.Button("루프", GUILayout.Height(20), GUILayout.Width(34)))
            LoopPlayback = !loopPlayback;
        GUI.backgroundColor = Color.white;
        GUILayout.Space(4);

        // 종료 정책 토글
        string policyLabel = endPolicy == EndOfPatternPolicy.Destroy ? "파괴" : "유지";
        if (GUILayout.Button(policyLabel, GUILayout.Height(20), GUILayout.Width(34)))
            EndPolicy = endPolicy == EndOfPatternPolicy.Destroy
                ? EndOfPatternPolicy.KeepLast : EndOfPatternPolicy.Destroy;

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    #endregion

    #region Timeline Track

    private static float TimeToTrackX(Rect track, float t)
    {
        float visibleDuration = totalDuration / timelineZoom;
        return track.x + (t - timelineViewStart) / visibleDuration * track.width;
    }

    private static float TrackXToTime(Rect track, float x)
    {
        float visibleDuration = totalDuration / timelineZoom;
        return timelineViewStart + (x - track.x) / track.width * visibleDuration;
    }

    private static void ClampTimelineView()
    {
        float visibleDuration = totalDuration / timelineZoom;
        timelineViewStart = Mathf.Clamp(timelineViewStart, 0f,
            Mathf.Max(0f, totalDuration - visibleDuration));
    }

    private static void DrawTimelineTrack(Rect track)
    {
        EditorGUI.DrawRect(track, TrackBackground);
        if (totalDuration <= 0f) return;

        float visibleDuration = totalDuration / timelineZoom;
        float viewEnd         = timelineViewStart + visibleDuration;

        // 이벤트 마커
        for (int i = 0; i < patternEvents.Count; i++)
        {
            var   ev = patternEvents[i];
            float x  = TimeToTrackX(track, ev.Time);
            if (x < track.x - MarkerRadius || x > track.xMax + MarkerRadius) continue;

            Color col = GetEventColor(ev.EventType);
            if (i == selectedEventIndex)
                col = MarkerSelected;

            // 마커: 다이아몬드 형태 (rect로 근사)
            float cy = track.y + track.height * 0.5f;
            EditorGUI.DrawRect(new Rect(x - MarkerRadius, cy - MarkerRadius,
                MarkerRadius * 2f, MarkerRadius * 2f), col);

            // 하단 스템 라인
            EditorGUI.DrawRect(new Rect(x - 0.5f, track.y, 1f, track.height), col * 0.6f);
        }

        // 시간 눈금
        float interval  = GetTimeMarkInterval(visibleDuration, track.width);
        float tickStart = Mathf.Ceil(timelineViewStart / interval) * interval;
        for (float t = tickStart; t <= viewEnd + 0.001f; t += interval)
        {
            float x = TimeToTrackX(track, t);
            if (x < track.x || x > track.xMax) continue;
            EditorGUI.DrawRect(new Rect(x, track.y, 1f, 4f), TickColor);
            string label = interval < 1f ? $"{t:F1}s" : $"{t:F0}s";
            GUI.Label(new Rect(x - 14f, track.y + 4f, 30f, 12f),
                label, EditorStyles.centeredGreyMiniLabel);
        }

        // 재생헤드
        float px = TimeToTrackX(track, currentTime);
        if (px >= track.x && px <= track.xMax)
            EditorGUI.DrawRect(new Rect(px - 1f, track.y, 2f, track.height), Color.white);
    }

    private static Color GetEventColor(PatternEventType type)
    {
        switch (type)
        {
            case PatternEventType.Spawn:      return MarkerSpawn;
            case PatternEventType.StatChange:  return MarkerStatChange;
            case PatternEventType.Destroy:     return MarkerDestroy;
            default:                           return Color.gray;
        }
    }

    private static float GetTimeMarkInterval(float visibleDuration, float trackWidth)
    {
        float[] options = { 0.1f, 0.2f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f };
        foreach (float iv in options)
        {
            if (visibleDuration / iv <= 0f) continue;
            if (trackWidth / (visibleDuration / iv) >= 40f) return iv;
        }
        return 60f;
    }

    private static void HandleTimelineInput(Rect trackRect)
    {
        Event e = Event.current;

        // Custom 배속 필드 입력 필터링
        if (customSpeedFieldFocused && e.type == EventType.KeyDown)
        {
            char c = e.character;
            bool allowed = c < 32 || c == 127 || char.IsDigit(c) || c == '.';
            if (!allowed) { e.Use(); return; }
        }

        // 휠 줌 — 커서 아래 시간 고정
        if (e.type == EventType.ScrollWheel && trackRect.Contains(e.mousePosition))
        {
            float cursorTime      = TrackXToTime(trackRect, e.mousePosition.x);
            float zoomFactor      = e.delta.y > 0 ? 0.85f : 1f / 0.85f;
            float maxZoom         = Mathf.Max(1f, totalDuration / 0.1f);
            timelineZoom          = Mathf.Clamp(timelineZoom * zoomFactor, 1f, maxZoom);
            float visibleDuration = totalDuration / timelineZoom;
            float cursorRatio     = (e.mousePosition.x - trackRect.x) / trackRect.width;
            timelineViewStart     = cursorTime - cursorRatio * visibleDuration;
            ClampTimelineView();
            e.Use();
        }

        // 미들 마우스 드래그 — 횡 팬
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) &&
            e.button == 2 && trackRect.Contains(e.mousePosition))
        {
            float visibleDuration = totalDuration / timelineZoom;
            float secondsPerPixel = visibleDuration / trackRect.width;
            timelineViewStart    -= e.delta.x * secondsPerPixel;
            ClampTimelineView();
            e.Use();
        }

        // 좌클릭/드래그 — 스크러빙 + 이벤트 마커 선택
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) &&
            e.button == 0 && trackRect.Contains(e.mousePosition))
        {
            float t = TrackXToTime(trackRect, e.mousePosition.x);
            currentTime = Mathf.Clamp(t, 0f, totalDuration);
            isPlaying   = false;

            // 클릭 시 이벤트 마커 선택 — 가장 가까운 마커 우선 (10px 이내)
            if (e.type == EventType.MouseDown)
            {
                int   best     = -1;
                float bestDist = 10f;
                for (int i = 0; i < patternEvents.Count; i++)
                {
                    float mx = TimeToTrackX(trackRect, patternEvents[i].Time);
                    float dist = Mathf.Abs(e.mousePosition.x - mx);
                    if (dist < bestDist) { best = i; bestDist = dist; }
                }
                selectedEventIndex = best;
            }

            NotifyStateChanged();
            e.Use();
        }

        // Delete → 선택 이벤트 마커 삭제
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete &&
            selectedEventIndex >= 0 && selectedEventIndex < patternEvents.Count)
        {
            DeleteSelectedEvent();
            e.Use();
        }
    }

    #endregion
}
