"""
코루틴 vs 큐 기반 스폰 시스템 비교 시뮬레이션
- 300초(5분) 동안 각 시스템의 스폰 횟수/타이밍 비교
- waiting = 3초 기준 (fallingMissileData.waiting)
"""
import random

DURATION = 300  # 시뮬레이션 시간 (초)
WAITING = 3.0   # fallingMissileData.waiting
MAX_QUEUE_SIZE = 20
TRIALS = 1000   # 반복 횟수 (평균 산출용)

def round2(v):
    return round(random.uniform(v / 2, v) * 100) / 100

# ─── 구 버전: 코루틴 3개 독립 ───

def simulate_coroutine():
    falling_count = 0
    hover_count = 0
    grand_count = 0
    falling_intervals = []

    # Falling coroutine
    t = 0
    prev_t = 0
    while True:
        missile_timer = round2(WAITING)
        t += missile_timer
        if t >= DURATION:
            break
        falling_count += 1
        if prev_t > 0:
            falling_intervals.append(t - prev_t)
        prev_t_before_wait = t
        wait_timer = round(random.uniform(0, 5) * 100) / 100
        t += wait_timer
        if falling_count > 1:
            falling_intervals.append(t - prev_t_before_wait)
        prev_t = t

    # 실제 interval: spawn 간 시간 = missile_timer + wait_timer
    falling_spawn_times = []
    t = 0
    while True:
        missile_timer = round2(WAITING)
        t += missile_timer
        if t >= DURATION:
            break
        falling_spawn_times.append(t)
        wait_timer = round(random.uniform(0, 5) * 100) / 100
        t += wait_timer

    falling_count = len(falling_spawn_times)
    falling_intervals = []
    for i in range(1, len(falling_spawn_times)):
        falling_intervals.append(falling_spawn_times[i] - falling_spawn_times[i-1])

    # Hover coroutine (레벨 10 = ~60초 이후 시작 가정)
    hover_start = 60
    hover_spawn_times = []
    t = hover_start
    while True:
        # 스폰 먼저, 대기 후
        if t >= DURATION:
            break
        hover_spawn_times.append(t)
        t += 15
    hover_count = len(hover_spawn_times)

    # Grand coroutine (레벨 25 = ~150초 이후 시작 가정, 100% 확률)
    grand_start = 150
    grand_spawn_times = []
    t = grand_start
    while True:
        if t >= DURATION:
            break
        grand_spawn_times.append(t)
        t += random.uniform(15, 30)
    grand_count = len(grand_spawn_times)

    avg_interval = sum(falling_intervals) / len(falling_intervals) if falling_intervals else 0

    return {
        'falling': falling_count,
        'hover': hover_count,
        'grand': grand_count,
        'falling_avg_interval': avg_interval,
    }

# ─── 신 버전: 큐 기반 ───

def fill_spawn_queue(homing_loop, grand_loop):
    schedules = []
    time = 0

    # Falling
    count = 0
    while count < MAX_QUEUE_SIZE:
        interval = round2(WAITING)
        wait = round(random.uniform(0, 5) * 100) / 100
        time += interval + wait
        schedules.append((time, 'Falling'))
        count += 1

    max_time = time

    # Hover
    if homing_loop:
        ht = 15
        while ht <= max_time:
            schedules.append((ht, 'Hover'))
            ht += 15

    # Grand (30% 확률)
    if grand_loop:
        gt = random.uniform(15, 30)
        while gt <= max_time:
            if random.randint(0, 99) >= 70:
                schedules.append((gt, 'Grand'))
            gt += random.uniform(15, 30)

    schedules.sort(key=lambda x: x[0])
    return schedules

def simulate_queue():
    falling_count = 0
    hover_count = 0
    grand_count = 0
    falling_spawn_times = []

    spawn_timer = 0
    elapsed = 0
    dt = 0.02  # 50fps 가정

    homing_loop = False
    grand_loop = False
    queue = []
    queue_idx = 0

    # 초기 큐 채움
    queue = fill_spawn_queue(homing_loop, grand_loop)
    queue_idx = 0

    while elapsed < DURATION:
        elapsed += dt
        spawn_timer += dt

        # 레벨 체크 (간소화)
        if elapsed >= 60 and not homing_loop:
            homing_loop = True
        if elapsed >= 150 and not grand_loop:
            grand_loop = True

        # 큐 소비
        while queue_idx < len(queue) and queue[queue_idx][0] <= spawn_timer:
            _, mtype = queue[queue_idx]
            queue_idx += 1
            if mtype == 'Falling':
                falling_count += 1
                falling_spawn_times.append(elapsed)
            elif mtype == 'Hover':
                hover_count += 1
            elif mtype == 'Grand':
                grand_count += 1

        # 큐 소진
        if queue_idx >= len(queue):
            # SpawnRandomOnQueueEmpty
            roll = random.randint(0, 2)
            if roll == 0:
                falling_count += 1
                falling_spawn_times.append(elapsed)
            elif roll == 1:
                if homing_loop:
                    hover_count += 1
            elif roll == 2:
                if grand_loop:
                    grand_count += 1

            spawn_timer = 0
            queue = fill_spawn_queue(homing_loop, grand_loop)
            queue_idx = 0

    falling_intervals = []
    for i in range(1, len(falling_spawn_times)):
        falling_intervals.append(falling_spawn_times[i] - falling_spawn_times[i-1])

    avg_interval = sum(falling_intervals) / len(falling_intervals) if falling_intervals else 0

    return {
        'falling': falling_count,
        'hover': hover_count,
        'grand': grand_count,
        'falling_avg_interval': avg_interval,
    }

# ─── 실행 ───

print(f"시뮬레이션: {DURATION}초, {TRIALS}회 평균")
print(f"파라미터: waiting={WAITING}s, MaxQueueSize={MAX_QUEUE_SIZE}")
print(f"Hover 시작: 60초, Grand 시작: 150초")
print("=" * 65)

cor_results = {'falling': 0, 'hover': 0, 'grand': 0, 'falling_avg_interval': 0}
que_results = {'falling': 0, 'hover': 0, 'grand': 0, 'falling_avg_interval': 0}

for _ in range(TRIALS):
    cr = simulate_coroutine()
    qr = simulate_queue()
    for k in cor_results:
        cor_results[k] += cr[k]
        que_results[k] += qr[k]

for k in cor_results:
    cor_results[k] /= TRIALS
    que_results[k] /= TRIALS

print(f"{'':20s} {'코루틴':>12s} {'큐':>12s} {'차이':>12s}")
print("-" * 65)
print(f"{'Falling 스폰 횟수':20s} {cor_results['falling']:12.1f} {que_results['falling']:12.1f} {que_results['falling']-cor_results['falling']:+12.1f}")
print(f"{'Hover 스폰 횟수':20s} {cor_results['hover']:12.1f} {que_results['hover']:12.1f} {que_results['hover']-cor_results['hover']:+12.1f}")
print(f"{'Grand 스폰 횟수':20s} {cor_results['grand']:12.1f} {que_results['grand']:12.1f} {que_results['grand']-cor_results['grand']:+12.1f}")
print(f"{'총 스폰 횟수':20s} {sum([cor_results['falling'],cor_results['hover'],cor_results['grand']]):12.1f} {sum([que_results['falling'],que_results['hover'],que_results['grand']]):12.1f} {sum([que_results['falling'],que_results['hover'],que_results['grand']])-sum([cor_results['falling'],cor_results['hover'],cor_results['grand']]):+12.1f}")
print("-" * 65)
print(f"{'Falling 평균 간격(초)':20s} {cor_results['falling_avg_interval']:12.2f} {que_results['falling_avg_interval']:12.2f} {que_results['falling_avg_interval']-cor_results['falling_avg_interval']:+12.2f}")
print()
print("※ Grand 차이: 코루틴=매주기 100% 스폰, 큐=30% 확률 필터 적용")
