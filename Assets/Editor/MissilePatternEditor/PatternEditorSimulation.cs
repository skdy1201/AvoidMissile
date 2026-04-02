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

    public enum PatternEventType { Spawn, StatChange, Destroy }

    /// <summary>특정 시점의 미사일 스탯 스냅샷.</summary>
    /// <remarks> 미사일 스탯이 좀 더 늘어나면 상속을 고려할 것 </remarks>
    public class MissileStatsSnapshot
    {
        public float Speed;
        public int Hp;
        public int HoverType;   // HoverMissileType enum int
        public float FlightTime;
        public float TurnTime;
        public float TurnRate;
        public int GrandDiameter;
        public int GrandDirection;

        /// <summary>
        /// 미사일 홀더의 값을 읽어온다.
        /// </summary>
        public static MissileStatsSnapshot FromHolder(MissileStatHolder h)
        {
            return new MissileStatsSnapshot
            {
                Speed          = h.speed,
                Hp             = h.hp,
                HoverType      = (int)h.hoverType,
                FlightTime     = h.flightTime,
                TurnTime       = h.turnTime,
                TurnRate       = h.turnRate,
                GrandDiameter  = h.grandDiameter,
                GrandDirection = h.grandDirection,
            };
        }

        /// <summary>
        /// 미사일 홀더에 현재 설정 값을 적용한다.
        /// </summary>
        public void ApplyToHolder(MissileStatHolder h)
        {
            h.speed          = Speed;
            h.hp             = Hp;
            h.hoverType      = (HoverMissileType)HoverType;
            h.flightTime     = FlightTime;
            h.turnTime       = TurnTime;
            h.turnRate       = TurnRate;
            h.grandDiameter  = GrandDiameter;
            h.grandDirection = GrandDirection;
        }
    }

    /// <summary>
    /// 패턴 이벤트는 생성, 스탯 변경, 파괴 3가지 타입이 존재
    /// 이벤트가 적용될 시간, 종류, 연결된 미사일 id, 스냅샷 정보가 들어가 있다.
    /// </summary>
    public class PatternEvent
    {
        public float Time;
        public PatternEventType EventType;
        public List<int> LinkedMissileIds = new List<int>();

        /// <summary>Spawn/StatChange 이벤트에 저장된 미사일별 스탯 스냅샷.</summary>
        public Dictionary<int, MissileStatsSnapshot> StatsSnapshots = new Dictionary<int, MissileStatsSnapshot>();
    }

    #endregion

    #region Constants

    private static readonly float[]  SpeedPresets = { 0.25f, 0.5f, 1f, 2f };
    private static readonly string[] SpeedLabels  = { "0.25x", "0.5x", "1x", "2x" };
    private const int   CustomSpeedIndex       = 4;
    private const float DefaultTotalDuration   = 10f;

    // 타임라인 레이아웃
    private const float TimelineHeight  = 76f;
    private const float ControlRowH     = 22f;
    private const float TrackH          = 42f;
    private const float TrackTopOffset  = 30f;

    // 타임라인 색상
    private static readonly Color PlaybarBackground   = new Color(0.12f, 0.12f, 0.12f, 0.92f);
    private static readonly Color TrackBackground     = new Color(0.06f, 0.06f, 0.06f);
    private static readonly Color MarkerSpawn          = new Color(0.30f, 0.55f, 0.90f, 0.90f);
    private static readonly Color MarkerStatChange     = new Color(0.90f, 0.80f, 0.20f, 0.90f);
    private static readonly Color MarkerDestroy        = new Color(0.90f, 0.30f, 0.30f, 0.90f);
    private static readonly Color MarkerSelected       = new Color(1f, 1f, 1f, 0.95f);
    private const float MarkerRadius = 3f;
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

    // 타임라인 줌/팬
    private static float timelineZoom      = 1f;
    private static float timelineViewStart;

    // 이벤트 마커
    private static readonly List<PatternEvent> patternEvents = new List<PatternEvent>();
    private static int selectedEventIndex = -1;

    // 패턴 이름
    private static string patternName = "NewPattern";

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
    public static IReadOnlyList<PatternEvent> Events => patternEvents;
    public static int SelectedEventIndex { get => selectedEventIndex; set => selectedEventIndex = value; }
    public static string PatternName { get => patternName; set => patternName = value; }

    /// <summary>해당 미사일 ID가 현재 시간에 Spawn 이벤트를 가지고 있는지 확인.</summary>
    /// <remarks> 정확한 시간으로 비교를 하면 놓칠 가능성이 있어, Approximately로 비교 한다. </remarks>
    public static bool HasSpawnEventAt(float time, int missileId)
    {
        for (int i = 0; i < patternEvents.Count; i++)
        {
            var patternEvent = patternEvents[i];
            if (patternEvent.EventType == PatternEventType.Spawn
                && Mathf.Approximately(patternEvent.Time, time)
                && patternEvent.LinkedMissileIds.Contains(missileId))
                return true;
        }
        return false;
    }

    /// <summary>스폰 시점의 Spawn 이벤트 스냅샷 갱신 (인스펙터에서 스폰 시점 스탯 변경 시).</summary>
    public static void UpdateSpawnSnapshots(float time, Dictionary<int, MissileStatsSnapshot> snapshots)
    {
        foreach (var patternEvent in patternEvents)
        {
            if (patternEvent.EventType != PatternEventType.Spawn) continue;
            if (!Mathf.Approximately(patternEvent.Time, time)) continue;
            foreach (var entry in snapshots)
            {
                if (patternEvent.LinkedMissileIds.Contains(entry.Key))
                    patternEvent.StatsSnapshots[entry.Key] = entry.Value;
            }
        }
    }

    /// <summary>현재 배속 라벨 (툴바 표시용).</summary>
    public static string CurrentSpeedLabel =>
        speedIndex < SpeedPresets.Length ? SpeedLabels[speedIndex] : $"{playSpeed:G3}x";

    #endregion

    #region Events

    /// <summary>상태 변경 시 발생 — 툴바 UI 갱신 트리거.</summary>
    public static event Action OnStateChanged;

    /// <summary>
    /// 재생/정지, 배속, 루프 
    /// </summary>
    private static void NotifyStateChanged() => OnStateChanged?.Invoke();

    #endregion

    #region Public API

    /// <summary>
    /// 직접 업데이트를 하기 위해서, lastEditorTime과 timesincestartup으로 dt를 구한다.
    /// </summary>
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
        timelineZoom        = 1f;
        timelineViewStart   = 0f;
        patternEvents.Clear();
        selectedEventIndex = -1;
        initialized = false;
    }

    /// <summary>
    /// 재생/정지 토글
    /// 정지 상태 && 시간이 끝 구간이라면, 리셋후 다시 재생
    /// 아니라면 일시정지
    /// 일시정지에서 다시 누르면 중간 부분부터 다시 재생
    /// </summary>
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

    /// <summary>
    /// 배속 설정 함수
    /// </summary>
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

    /// <summary>
    /// Epsilon을 쓰는 이유는 0이 되면 시뮬레이션이 멈추기 때문에, 가장 작은 양수로 보장하기 위함.
    /// </summary>
    public static void SetCustomSpeed(float speed)
    {
        playSpeed = Mathf.Max(float.Epsilon, speed);
        savedCustomSpeed = playSpeed;
        NotifyStateChanged();
    }


    /// <summary>
    /// 시뮬레이션의 시간을 조정 
    /// </summary>
    public static void SetCurrentTime(float t)
    {
        currentTime = Mathf.Clamp(t, 0f, totalDuration);
        SeekMissilesToTime(currentTime);
        SceneView.RepaintAll();
    }

    /// <summary>
    /// 전체 시뮬레이션 구간 설정 함수
    /// </summary>
    public static void SetTotalDuration(float d)
    {
        totalDuration = Mathf.Clamp(d, 0.1f, 600f);
        currentTime   = Mathf.Clamp(currentTime, 0f, totalDuration);
        // 범위 밖 이벤트 클램프
        foreach (var patternEvent in patternEvents)
            patternEvent.Time = Mathf.Clamp(patternEvent.Time, 0f, totalDuration);
        ClampTimelineView();
        SceneView.RepaintAll();
    }

    /// <summary>
    /// 이전 이벤트 구간이 있으면 해당 구간으로 가고 아니라면, 처음으로 간다.
    /// 이후, 미사일 위치, 스탯 업데이트
    /// </summary>
    public static void JumpToPrevSegmentOrStart()
    {
        float prevTime = 0f;
        foreach (var patternEvent in patternEvents)
            if (patternEvent.Time < currentTime - 0.01f)
                prevTime = Mathf.Max(prevTime, patternEvent.Time);
        currentTime = prevTime;
        SeekMissilesToTime(currentTime);
        NotifyStateChanged();
        SceneView.RepaintAll();
    }

    /// <summary>
    /// 다음 이벤트가 있으면 해당 구간으로 이동, 아니라면 맨 끝으로 간다.
    /// </summary>
    public static void JumpToNextSegmentOrEnd()
    {
        float nextTime = totalDuration;
        foreach (var patternEvent in patternEvents)
            if (patternEvent.Time > currentTime + 0.01f)
                nextTime = Mathf.Min(nextTime, patternEvent.Time);
        currentTime = nextTime;
        SeekMissilesToTime(currentTime);
        NotifyStateChanged();
        SceneView.RepaintAll();
    }

    /// <summary>
    /// 이벤트 마커 추가. 같은 시간+같은 타입의 기존 이벤트가 있으면 ID를 병합.
    /// </summary>
    public static void AddEvent(PatternEventType type, float time, List<int> missileIds = null,
        Dictionary<int, MissileStatsSnapshot> snapshots = null)
    {
        float clampedTime = Mathf.Clamp(time, 0f, totalDuration);
        var ids = missileIds ?? new List<int>();

        // 같은 시간·같은 타입의 기존 이벤트에 병합
        for (int i = 0; i < patternEvents.Count; i++)
        {
            var existing = patternEvents[i];
            if (existing.EventType == type && Mathf.Approximately(existing.Time, clampedTime))
            {
                foreach (int id in ids)
                {
                    if (!existing.LinkedMissileIds.Contains(id))
                        existing.LinkedMissileIds.Add(id);
                }
                // 스냅샷 병합/갱신
                if (snapshots != null)
                    foreach (var entry in snapshots)
                        existing.StatsSnapshots[entry.Key] = entry.Value;
                selectedEventIndex = i;
                SceneView.RepaintAll();
                return;
            }
        }

        // 기존 이벤트 없음 → 새로 생성
        var newEvent = new PatternEvent
        {
            Time = clampedTime,
            EventType = type,
            LinkedMissileIds = ids
        };
        if (snapshots != null)
            foreach (var entry in snapshots)
                newEvent.StatsSnapshots[entry.Key] = entry.Value;
        patternEvents.Add(newEvent);
        // 정렬: 시간 → 타입 우선순위 (Spawn=0 → StatChange=1 → Destroy=2)
        patternEvents.Sort((a, b) =>
        {
            int cmp = a.Time.CompareTo(b.Time);
            return cmp != 0 ? cmp : a.EventType.CompareTo(b.EventType);
        });
        selectedEventIndex = patternEvents.IndexOf(newEvent);
        SceneView.RepaintAll();
    }

    /// <summary>선택된 이벤트 마커 삭제. Spawn 이벤트면 연결된 미사일도 제거.</summary>
    /// <remarks> 스탯 변경은 이벤트만 지우면 없앨 수 있다. </remarks>
    public static void DeleteSelectedEvent()
    {
        if (selectedEventIndex < 0 || selectedEventIndex >= patternEvents.Count) return;

        var patternEvent = patternEvents[selectedEventIndex];

        // Spawn 이벤트 삭제 → 연결된 미사일을 완전 제거 (Ghost 파괴)
        if (patternEvent.EventType == PatternEventType.Spawn && patternEvent.LinkedMissileIds.Count > 0)
            PatternEditorSceneInteraction.RemoveMissilesByIds(patternEvent.LinkedMissileIds);

        // Destroy 이벤트 삭제 → 연결된 미사일 복원 (DestroyTime 해제)
        if (patternEvent.EventType == PatternEventType.Destroy && patternEvent.LinkedMissileIds.Count > 0)
            PatternEditorSceneInteraction.RestoreMissilesByIds(patternEvent.LinkedMissileIds);

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
            patternEvents[i].StatsSnapshots.Remove(missileId);
            if (patternEvents[i].LinkedMissileIds.Count == 0)
            {
                patternEvents.RemoveAt(i);
                if (selectedEventIndex == i) selectedEventIndex = -1;
                else if (selectedEventIndex > i) selectedEventIndex--;
            }
        }
        SceneView.RepaintAll();
    }

    /// <summary>로드 시 이벤트 목록 직접 세팅. 기존 이벤트 초기화 후 재설정.</summary>
    public static void LoadEventsDirectly(List<PatternEvent> events, float duration, string name)
    {
        patternName        = name;
        totalDuration      = Mathf.Clamp(duration, 0.1f, 600f);
        currentTime        = 0f;
        selectedEventIndex = -1;

        patternEvents.Clear();
        foreach (var ev in events)
            patternEvents.Add(ev);

        patternEvents.Sort((a, b) =>
        {
            int cmp = a.Time.CompareTo(b.Time);
            return cmp != 0 ? cmp : a.EventType.CompareTo(b.EventType);
        });

        SeekMissilesToTime(0f);
        ClampTimelineView();
        SceneView.RepaintAll();
    }

    #endregion

    #region dt Loop

    /// <summary>
    /// 시뮬레이션의 dt
    /// </summary>
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
            if (m.Ghost == null) continue;

            // 스폰 이전 또는 파괴 이후 → 비활성
            if (currentTime < m.SpawnTime || currentTime >= m.DestroyTime)
            {
                if (!m.Hidden) { m.Ghost.SetActive(false); m.Hidden = true; }
                continue;
            }

            // 스폰~파괴 구간 → 활성화
            if (m.Hidden && !HasReachedPlatform(m))
            {
                m.Ghost.SetActive(true);
                m.Hidden = false;
            }

            if (m.Hidden) continue;

            // 현재 시점의 스탯 적용
            ApplyStatsAtTime(m, currentTime);

            m.Ghost.transform.position += m.Direction * m.Speed * dt;

            // 데칼: 플랫폼 표면에 고정 + 높이 비율 스케일
            UpdateDecal(m);

            // 충돌 감지: 플랫폼 Y 도달 시 숨김
            if (HasReachedPlatform(m))
            {
                m.Ghost.SetActive(false);
                m.Hidden = true;
            }
        }
    }

    /// <summary>데칼을 플랫폼 표면에 고정 + 높이 비율로 크기 조절.</summary>
    private static void UpdateDecal(PlacedMissile m)
    {
        if (m.DecalTransform == null) return;

        // 데칼 위치를 플랫폼 표면 XZ에 고정 (Ghost 자식이라 같이 움직이므로 매 프레임 보정)
        var missilePos = m.Ghost.transform.position;
        float decalYOffset = m.Type == PlacedMissileType.Grand ? -0.5f : 1.1f;
        float decalY = m.PlatformY + decalYOffset;
        m.DecalTransform.position = new Vector3(missilePos.x, decalY, missilePos.z);

        var projector = m.DecalTransform.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
        if (projector == null) return;

        // grand horizen 미사일이 아닌 이상, platform y보다 낮을 일은 없다.
        float totalDrop = m.SpawnHeight - m.PlatformY;
        if (totalDrop <= 0f) return;

        float currentHeight = missilePos.y - m.PlatformY;
        float ratio = 1f - Mathf.Clamp01(currentHeight / totalDrop);

        projector.size = new Vector3(
            m.DecalMaxSize.x * ratio,
            m.DecalMaxSize.y * ratio,
            m.DecalMaxSize.z);
    }

    /// <summary>
    /// 이벤트 타임라인에서 특정 시점에 유효한 스탯을 조회.
    /// Spawn/StatChange 이벤트 중 time 이하인 가장 마지막 스냅샷을 반환.
    /// </summary>
    /// <remarks> 지금은 계속 앞부터 스탯을 덮어씌운다. 이벤트가 많아지면, 최적화를 고민해볼 대상 </remarks>
    private static MissileStatsSnapshot GetStatsAtTime(int missileId, float time)
    {
        MissileStatsSnapshot result = null;
        foreach (var patternEvent in patternEvents)
        {
            if (patternEvent.Time > time + 0.001f) break;    // 정렬되어 있으므로 이후는 볼 필요 없음
            if (patternEvent.EventType != PatternEventType.Spawn && patternEvent.EventType != PatternEventType.StatChange)
                continue;
            if (patternEvent.StatsSnapshots.TryGetValue(missileId, out var snap))
                result = snap;
        }
        return result;
    }

    /// <summary>시간 기반 스탯을 PlacedMissile + MissileStatHolder에 적용.</summary>
    private static void ApplyStatsAtTime(PlacedMissile m, float time)
    {
        var snap = GetStatsAtTime(m.Id, time);
        if (snap == null) return;

        m.Speed = snap.Speed;

        var holder = m.Ghost.GetComponent<MissileStatHolder>();
        if (holder != null)
            snap.ApplyToHolder(holder);
    }

    /// <summary>미사일 선두가 플랫폼/바운더리에 도달했는지 확인.</summary>
    private static bool HasReachedPlatform(PlacedMissile m)
    {
        Vector3 pos = m.Ghost.transform.position;

        // Renderer bounds로 선두 오프셋 계산
        float frontOffset = 0f;
        var renderer = m.Ghost.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            // 실제 프리팹 메시의 바운딩 박스와, 프리팹의 방향을 구해서 얼만큼 떨어져 있는지 내적한다.
            // 이를 통해 정면 방향에서 메시가 갖는 반지름을 구할 수 있다.
            var bounds = renderer.bounds;
            // 이동 방향 축의 extent (중심에서 가장자리까지 거리)
            frontOffset = Mathf.Abs(Vector3.Dot(bounds.extents, m.Direction));
        }

        // Falling / Grand Vertical: 선두(아랫면)가 플랫폼 이하
        if (m.Direction.y < 0f)
            return (pos.y - frontOffset) <= m.PlatformY;

        // Hover / Grand Horizontal: 선두가 게임 바운더리(±50) 밖
        Vector3 frontPos = pos + m.Direction * frontOffset;
        const float boundaryLimit = 50f;
        return frontPos.x < -boundaryLimit || frontPos.x > boundaryLimit
            || frontPos.z < -boundaryLimit || frontPos.z > boundaryLimit;
    }

    /// <summary>
    /// spawnTime~targetTime 구간의 스탯 변경을 반영한 총 이동 거리 계산.
    /// 구간별로 다른 속도를 적용하여 정확한 위치를 산출.
    /// </summary>
    /// <remarks> 방향 전환이 있는 이벤트가 생기면 이 이벤트를 고쳐야 한다. </remarks>
    private static float CalculateDisplacement(int missileId, float spawnTime, float targetTime)
    {
        // spawnTime 이하 → 아직 이동 없음
        if (targetTime <= spawnTime) return 0f;

        // 이 미사일에 영향을 주는 Spawn/StatChange 이벤트를 시간순으로 수집
        // (patternEvents는 이미 시간순 정렬됨)
        float displacement = 0f;
        float segStart = spawnTime;
        float currentSpeed = 0f;

        // 초기 속도: Spawn 이벤트에서 가져옴
        var spawnSnap = GetStatsAtTime(missileId, spawnTime);
        if (spawnSnap != null)
            currentSpeed = spawnSnap.Speed;

        foreach (var patternEvent in patternEvents)
        {
            if (patternEvent.Time <= spawnTime + 0.001f) continue;   // 스폰 이전/동시 이벤트 스킵
            if (patternEvent.Time > targetTime + 0.001f) break;       // 목표 시간 이후
            if (patternEvent.EventType != PatternEventType.StatChange) continue;
            if (!patternEvent.StatsSnapshots.ContainsKey(missileId)) continue;

            // segStart ~ patternEvent.Time 구간은 currentSpeed로 이동
            float segDuration = patternEvent.Time - segStart;
            displacement += currentSpeed * segDuration;
            segStart = patternEvent.Time;
            currentSpeed = patternEvent.StatsSnapshots[missileId].Speed;
        }

        // 마지막 구간: segStart ~ targetTime
        displacement += currentSpeed * (targetTime - segStart);
        return displacement;
    }

    /// <summary>모든 미사일을 특정 시점의 위치로 이동 (스크러빙/점프용).</summary>
    private static void SeekMissilesToTime(float t)
    {
        var missiles = PatternEditorSceneInteraction.PlacedMissiles;
        for (int i = 0; i < missiles.Count; i++)
        {
            var m = missiles[i];
            if (m.Ghost == null) continue;

            // 스폰 이전 또는 파괴 이후 → 비활성화 + 위치 리셋
            if (t < m.SpawnTime || t >= m.DestroyTime)
            {
                if (!m.Hidden)
                {
                    m.Ghost.SetActive(false);
                    m.Hidden = true;
                }
                m.Ghost.transform.position = m.OriginalPosition;
                continue;
            }

            // 현재 시점의 스탯 적용 (인스펙터 + PlacedMissile 동기화)
            ApplyStatsAtTime(m, t);

            // 구간별 속도 변화를 반영한 위치 계산
            float displacement = CalculateDisplacement(m.Id, m.SpawnTime, t);
            Vector3 newPos = m.OriginalPosition + m.Direction * displacement;
            m.Ghost.transform.position = newPos;

            // 충돌 판정
            bool shouldHide = HasReachedPlatform(m);
            if (shouldHide && !m.Hidden)
            {
                m.Ghost.SetActive(false);
                m.Hidden = true;
            }
            else if (!shouldHide && m.Hidden)
            {
                m.Ghost.SetActive(true);
                m.Hidden = false;
            }

            UpdateDecal(m);
        }
    }

    /// <summary>모든 미사일 ghost를 원래 배치 위치로 복원. 스폰 시간 이전이면 비활성화.</summary>
    private static void ResetMissilePositions()
    {
        var missiles = PatternEditorSceneInteraction.PlacedMissiles;
        for (int i = 0; i < missiles.Count; i++)
        {
            var m = missiles[i];
            if (m.Ghost == null) continue;

            m.Ghost.transform.position = m.OriginalPosition;

            // 스폰 이전 또는 파괴 이후 → 비활성화
            bool shouldHide = currentTime < m.SpawnTime || currentTime >= m.DestroyTime;
            if (shouldHide)
            {
                if (!m.Hidden) { m.Ghost.SetActive(false); m.Hidden = true; }
            }
            else if (m.Hidden)
            {
                m.Ghost.SetActive(true);
                m.Hidden = false;
            }

            // 데칼 크기 원래대로 복원
            ResetDecalScale(m);
        }
    }

    /// <summary>데칼을 배치 시 최대 크기로 복원.</summary>
    private static void ResetDecalScale(PlacedMissile m)
    {
        if (m.DecalTransform == null) return;

        var projector = m.DecalTransform.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();
        if (projector == null) return;

        projector.size = m.DecalMaxSize;
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

    /// <summary>
    /// 타임라인 영역 위에 마우스가 있는지 확인 (클릭 차단용).
    /// </summary>
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

        // 패턴 이름
        string newName = EditorGUILayout.DelayedTextField(patternName, GUILayout.Width(120));
        if (newName != patternName) patternName = newName;
        GUILayout.Space(6);

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

        // 파괴 이벤트 주입
        if (GUILayout.Button("파괴", GUILayout.Height(20), GUILayout.Width(34)))
        {
            var selected = PatternEditorSceneInteraction.SelectedMissiles;
            if (selected.Count > 0)
                PatternEditorSceneInteraction.DestroySelectedMissiles();
            else
                EditorUtility.DisplayDialog("파괴", "파괴할 미사일을 먼저 선택해주세요.", "확인");
        }

        GUILayout.Space(4);

        // 저장 / 불러오기
        if (GUILayout.Button("저장", GUILayout.Height(20), GUILayout.Width(34)))
            PatternSave.Save();

        if (GUILayout.Button("불러오기", GUILayout.Height(20), GUILayout.Width(60)))
            PatternSave.ShowLoadMenu();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    #endregion

    #region Timeline Track

    /// <summary>
    /// 타임라인 x 좌표 변환
    /// rect의 시작점 + (이벤트의 시작 시간 - 타임라인 시작시간 / 확대 비율 * 트랙의 너비)
    /// </summary>
    /// <remarks> rect의 상대 좌표이기 때문에, 절대좌표로 변환하기 위해선 시작점을 더해줘야한다. </remarks>
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

    /// <summary>
    /// 타임라인바가 바뀌면, 보이는 시뮬레이션 시간 범위가 달라져야 한다.
    /// </summary>
    private static void ClampTimelineView()
    {
        float visibleDuration = totalDuration / timelineZoom;
        timelineViewStart = Mathf.Clamp(timelineViewStart, 0f,
            Mathf.Max(0f, totalDuration - visibleDuration));
    }

    /// <summary>
    /// 타임라인을 그리는 함수
    /// 타입별 레인, 시간 간격 바, 이벤트 마커, 재생헤드를 그리고있다.
    /// </summary>
    /// <param name="track"></param>
    private static void DrawTimelineTrack(Rect track)
    {
        EditorGUI.DrawRect(track, TrackBackground);
        if (totalDuration <= 0f) return;

        float visibleDuration = totalDuration / timelineZoom;
        float viewEnd         = timelineViewStart + visibleDuration;

        // 타입별 레인 Y (Spawn=상단, StatChange=중간, Destroy=하단)
        float laneH    = track.height / 3f;
        float spawnY   = track.y + laneH * 0.5f;
        float statY    = track.y + laneH * 1.5f;
        float destroyY = track.y + laneH * 2.5f;

        // 레인 구분선
        EditorGUI.DrawRect(new Rect(track.x, track.y + laneH, track.width, 1f),
            new Color(0.25f, 0.25f, 0.25f));
        EditorGUI.DrawRect(new Rect(track.x, track.y + laneH * 2f, track.width, 1f),
            new Color(0.25f, 0.25f, 0.25f));

        // 이벤트 마커
        for (int i = 0; i < patternEvents.Count; i++)
        {
            var   patternEvent = patternEvents[i];
            float x  = TimeToTrackX(track, patternEvent.Time);
            if (x < track.x - MarkerRadius || x > track.xMax + MarkerRadius) continue;

            Color col = GetEventColor(patternEvent.EventType);
            bool selected = i == selectedEventIndex;
            if (selected) col = MarkerSelected;

            // 타입별 Y 위치
            float cy;
            switch (patternEvent.EventType)
            {
                case PatternEventType.Spawn:      cy = spawnY;   break;
                case PatternEventType.StatChange:  cy = statY;    break;
                case PatternEventType.Destroy:     cy = destroyY; break;
                default:                           cy = statY;    break;
            }

            // 마커: 작은 사각형
            EditorGUI.DrawRect(new Rect(x - MarkerRadius, cy - MarkerRadius,
                MarkerRadius * 2f, MarkerRadius * 2f), col);

            // 선택 시 외곽선 강조
            if (selected)
            {
                float o = MarkerRadius + 1f;
                Color outline = col * 0.7f;
                EditorGUI.DrawRect(new Rect(x - o, cy - o, o * 2f, 1f), outline);
                EditorGUI.DrawRect(new Rect(x - o, cy + o - 1f, o * 2f, 1f), outline);
                EditorGUI.DrawRect(new Rect(x - o, cy - o, 1f, o * 2f), outline);
                EditorGUI.DrawRect(new Rect(x + o - 1f, cy - o, 1f, o * 2f), outline);
            }
        }

        // 시간 눈금
        float interval  = GetTimeMarkInterval(visibleDuration, track.width);
        
        // 뷰가 0초에서 시작하지 않을 수 있다. 그래서 눈금을 interval의 배수인 위치부터 시작하도록 맞춰준다.
        // 확대 되었을때, 시작 구간을 애매하게 잡지 않기 위해서, interval의 몇 번째 구간인지 올림한 값과 곱하는 것.
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

    /// <summary>
    /// 시간 간격 눈금을 조절해주는 함수
    /// </summary>
    /// <remarks>
    /// 너무 눈금이 빽빽하면 가독성이 떨어질 수 있어서, 표현할 시간과 간격을 통해 타임라인 바를 구한다.
    /// 전체 width / (전체 시간 / 마커 구간) = 픽셀 사이 구간. 40px을 최저 기준으로 잡는다.
    /// </remarks>
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

    /// <summary>
    /// 타임라인 입력을 관리하는 함수
    /// </summary>
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
            SeekMissilesToTime(currentTime);

            // 클릭 시 이벤트 마커 선택 — 마커 근처(X 10px, Y 레인 내) 겹침 순환
            if (e.type == EventType.MouseDown)
            {
                float laneH    = trackRect.height / 3f;
                float[] laneYs = {
                    trackRect.y + laneH * 0.5f,   // Spawn
                    trackRect.y + laneH * 1.5f,   // StatChange
                    trackRect.y + laneH * 2.5f    // Destroy
                };
                var nearbyIndices = new List<int>();
                for (int i = 0; i < patternEvents.Count; i++)
                {
                    float mx = TimeToTrackX(trackRect, patternEvents[i].Time);
                    float my = laneYs[(int)patternEvents[i].EventType];
                    if (Mathf.Abs(e.mousePosition.x - mx) < 10f
                        && Mathf.Abs(e.mousePosition.y - my) < laneH * 0.5f)
                        nearbyIndices.Add(i);
                }

                if (nearbyIndices.Count == 0)
                {
                    selectedEventIndex = -1;
                }
                else if (nearbyIndices.Count == 1)
                {
                    selectedEventIndex = nearbyIndices[0];
                }
                else
                {
                    // 현재 선택이 근처 목록에 있으면 다음으로 순환
                    int pos = nearbyIndices.IndexOf(selectedEventIndex);
                    selectedEventIndex = nearbyIndices[(pos + 1) % nearbyIndices.Count];
                }

                // 선택된 이벤트의 연결 미사일 포커스
                if (selectedEventIndex >= 0 && selectedEventIndex < patternEvents.Count)
                    PatternEditorSceneInteraction.SelectMissilesByIds(
                        patternEvents[selectedEventIndex].LinkedMissileIds);
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
