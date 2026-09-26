---
name: bt-node
description: "몬스터 Behavior Tree 노드(ScriptableObject) 작성 가이드 — SONode/SOListNode, Execute(BlackBoard _refBB) → eNodeState, leaf 는 무상태(상태는 Blackboard), Composite 는 몬스터별 Instantiate 클론, CreateAssetMenu 경로. 새 BT 행동·조건·컴포지트 노드를 만들거나 고칠 때 사용합니다."
globs: ["**/BTScript/*.cs", "**/*Node.cs", "**/BehaviorTree.cs"]
---

# Behavior Tree 노드

## 구조

| 타입 | 역할 |
|---|---|
| `SONode` (abstract SO) | `public abstract eNodeState Execute(BlackBoard _refBB);` |
| `SOListNode` (abstract) | 자식 목록 `listNode`. `CloneChildren`으로 몬스터별 복제 |
| `BlackBoard` (`[Serializable]` 데이터 클래스, public PascalCase) | 몬스터별 런타임 상태 — `Owner`, `OwnerOffset`, `TargetTr`, `ObjInfo`, 타이머류 |
| `BehaviorTree` (MonoBehaviour) | `Awake`에서 루트가 `SOListNode`면 트리 전체를 복제. `Evaluate(BlackBoard)`로 실행 |
| `eNodeState` | `Success` / `Failure` / `Running` |

모두 `BehaviorTree.cs` 한 파일에 있다. 노드 파일은 `BTScript/` 폴더에 노드당 하나.

## Leaf 노드 (행동 · 조건)

```csharp
using UnityEngine;

/*///////////////////////////////////////////
            SOCheckLength
기능 : 플레이어와 몬스터의 거리가 일정범위 안, 밖에 있는지 체크
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_CheckLength", menuName = "Game/Monster/ActionNode/CheckLength")]
public sealed class SOCheckLength : SONode
{
    [Tooltip("해당 범위 안을 체크할지 밖을 체크할지")]
    [SerializeField] private bool m_bIsIn;
    [SerializeField] private float m_fCheckLength = 20.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
            return eNodeState.Failure;

        Transform refOwnerTr = _refBB.OwnerOffset == null ? _refBB.Owner.transform : _refBB.OwnerOffset;
        float fLength = (_refBB.TargetTr.position - refOwnerTr.position).magnitude;

        if (m_bIsIn == true)
            return m_fCheckLength >= fLength ? eNodeState.Success : eNodeState.Failure;

        return m_fCheckLength <= fLength ? eNodeState.Success : eNodeState.Failure;
    }
}
```

- 필드는 `[SerializeField] private` **에디터 세팅만**. leaf 는 클론되지 않고 여러 몬스터가 같은 에셋을 공유하므로 **몬스터별 상태를 필드에 두지 않는다**
- 타이머 · 방향 · 카운트 같은 상태는 `BlackBoard`에 `[Header]` 묶음으로 public 필드를 추가하고 거기에 쓴다 (`SOStrafeNode` → `_refBB.StrafeTimer`)
- 실행 불가(대상 없음 등) → `Failure`, 아직 진행 중 → `Running`, 끝남 → `Success`
- 위치 기준은 `OwnerOffset`이 있으면 그것, 없으면 `Owner.transform`
- 몬스터 수 × 노드 수 × 매 프레임 도는 핫 패스다 — 할당, `GetComponent`, LINQ 금지

## Composite 노드

`SOListNode`를 상속하면 `BehaviorTree.Awake`가 몬스터마다 `Instantiate`로 클론한다. **클론 인스턴스에 한해** 인덱스·타이머를 필드로 들 수 있다.

```csharp
[CreateAssetMenu(fileName = "SO_SequenceNode", menuName = "Game/Monster/SequenceNode")]
public sealed class SOSequenceNode : SOListNode
{
    private int m_iCurrentIdx = 0;

    private void OnEnable()
    {
        m_iCurrentIdx = 0;
    }

    public override eNodeState Execute(BlackBoard _refBB)
    {
        for (int i = m_iCurrentIdx; i < listNode.Count; ++i)
        {
            eNodeState eState = listNode[i].Execute(_refBB);
            if (eState == eNodeState.Failure)
            {
                m_iCurrentIdx = 0;
                return eNodeState.Failure;
            }

            if (eState == eNodeState.Running)
            {
                m_iCurrentIdx = i;
                return eNodeState.Running;
            }
        }

        m_iCurrentIdx = 0;
        return eNodeState.Success;
    }
}
```

## 새 노드 추가 순서

1. `BTScript/SO<이름>Node.cs` — leaf 면 `SONode`, composite 면 `SOListNode` 상속
2. 상태가 필요하면 `BlackBoard`에 필드 추가
3. 에셋 생성 — leaf 메뉴 `Game/Monster/ActionNode/<이름>`, composite 메뉴 `Game/Monster/<이름>`, 파일 이름 `SO_<이름>`
4. 부모 `SOListNode` 에셋의 `listNode`에 연결
