# Behavior Tree 테스트 사용법

## 테스트 시나리오
**Space 키를 누르면 Cube가 회전, 안 누르면 정지**

---

## 트리 구조

```
TestBehaviorTree (BehaviorTree 에셋)
└── RootSelector (SelectNode)
    ├── SpaceRotateSequence (SequenceNode)
    │   ├── CheckSpaceKey   ← Space 누르면 SUCCESS
    │   └── RotateNode      ← 회전 실행
    └── IdleNode            ← 아무것도 안 함
```

---

## 구성 순서

### Step 1: 에셋 생성
Project 창에서 **우클릭 > Create > BehaviorTree > ...**

| 메뉴 경로 | 에셋 이름 |
|----------|----------|
| Test/CheckSpaceKey | CheckSpaceKey |
| Test/RotateNode | RotateNode |
| Test/IdleNode | IdleNode |
| SequenceNode | SpaceRotateSequence |
| SelectNode | RootSelector |
| BehaviorTree | TestBehaviorTree |

---

### Step 2: 트리 연결 (Inspector에서)

**1) SpaceRotateSequence (SequenceNode)**
```
Childrens:
  [0]: CheckSpaceKey
  [1]: RotateNode
```

**2) RootSelector (SelectNode)**
```
Childrens:
  [0]: SpaceRotateSequence
  [1]: IdleNode
```

**3) TestBehaviorTree**
```
Root Node: RootSelector
```

---

### Step 3: GameObject 설정

1. Cube (또는 테스트 오브젝트) 선택
2. **Add Component > SimpleBTTest**
3. `Behavior Tree` 필드에 **TestBehaviorTree** 드래그

---

### Step 4: 플레이 & 테스트

| 입력 | 결과 |
|------|------|
| Space 안 누름 | Cube 정지 |
| Space 누름 | Cube 회전 |

---

## 동작 원리

```
매 프레임 Update()
    ↓
SimpleBTTest.Update()
    ↓
BehaviorTree.RootNode.Execute(gameObject)
    ↓
SelectNode (첫 번째 SUCCESS 찾기)
    ↓
SequenceNode (모두 SUCCESS여야 성공)
    ↓
CheckSpaceKey → SUCCESS/FAILURE
    ↓
(SUCCESS면) RotateNode 실행 → 회전!
(FAILURE면) IdleNode 실행 → 대기
```

---

## 새로운 LeafNode 만들기

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "MyNode", menuName = "BehaviorTree/Test/MyNode")]
public class MyNode : LeafNode
{
    protected override NodeResult OnExecute(GameObject owner)
    {
        // 행동 구현
        return NodeResult.SUCCESS;
    }
}
```

1. 스크립트 작성
2. Unity에서 에셋 생성 (Create > BehaviorTree > Test > MyNode)
3. 트리의 Childrens에 추가

---

## 파일 목록

| 파일 | 설명 |
|------|------|
| CheckSpaceKey.cs | Space 키 입력 체크 |
| RotateNode.cs | 오브젝트 회전 |
| IdleNode.cs | 아무것도 안 함 |
| SimpleBTTest.cs | BT 실행 Runner |
