---
name: scriptable-objects
description: "ScriptableObject 패턴 — 데이터 컨테이너 SO(public PascalCase 필드), 로직을 가진 SO(BT 노드·Feature·BulletAction, [SerializeField] private m_), SO → 런타임 복사본, CreateAssetMenu 규칙, 런타임 상태를 SO 에 쓰지 않는 규칙. SO 를 새로 만들거나 고칠 때 사용합니다."
alwaysApply: true
---

# ScriptableObject

SO 는 **데이터와 에디터 세팅만** 담는다. 런타임에 바뀌는 값은 절대 SO 에 쓰지 않는다 — Model, Blackboard, 매니저 쪽에 둔다.

## 1. 데이터 컨테이너 SO — public PascalCase

순수 데이터는 접두사 없는 `public` 필드로, `[Header]`로 묶는다 (`csharp-unity.md` §4).

```csharp
using UnityEngine;

/*///////////////////////////////////////////
                SOAttackInfo
기능 : 탄 한 종류의 정적 데이터. 발사할 때 AttackInfo 런타임 복사본을 만든다
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_AttackInfo", menuName = "Game/Weapon/AttackInfo")]
public sealed class SOAttackInfo : ScriptableObject
{
    [TextArea]
    public string Description;

    [Header("Stats")]
    public int Damage = 10;
    public float Speed = 12.0f;
    public float AliveTime = 0.2f;

    [Header("Targeting")]
    public LayerMask HitLayers = ~0;

    public AttackInfo MakeAttackInfo()
    {
        AttackInfo refAttackInfo = new AttackInfo();
        refAttackInfo.Damage = Damage;
        refAttackInfo.Speed = Speed;
        refAttackInfo.AliveTime = AliveTime;
        refAttackInfo.HitLayers = HitLayers;
        return refAttackInfo;
    }
}
```

## 2. SO → 런타임 복사본

레벨업·카드로 값이 바뀌는 데이터는 SO 원본을 건드리지 않고 복사본을 만들어 그걸 고친다. SO 는 여러 오브젝트가 공유하는 에셋이라 원본을 고치면 전부 오염되고, 에디터에서는 플레이를 멈춰도 값이 남는다.

```csharp
[SerializeField] private SOAttackInfo m_SOAttackInfo;   // 데이터 본체는 m_SO
private AttackInfo m_refAttackInfo;                     // 런타임 복사본

public void Init()
{
    m_refAttackInfo = m_SOAttackInfo.MakeAttackInfo();
}

public void AddAttackDamage(int _iValue)
{
    m_refAttackInfo.Damage += _iValue;                  // 원본 SO 는 그대로
}
```

## 3. 로직을 가진 SO — [SerializeField] private m_

BT 노드, Feature(카드 효과), BulletAction 처럼 "실행"되는 SO 는 행동 클래스 규칙을 따른다. **인스턴스별 상태를 필드로 들지 않는다** — 상태는 매개변수로 받은 Blackboard/런타임 객체에 쓴다.

```csharp
[CreateAssetMenu(fileName = "SO_StrafeNode", menuName = "Game/Monster/ActionNode/StrafeNode")]
public sealed class SOStrafeNode : SONode
{
    [SerializeField] private float m_fMinTime = 1.0f;   // 에디터 세팅
    [SerializeField] private float m_fMaxTime = 3.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        _refBB.StrafeTimer -= Time.deltaTime;          // 상태는 Blackboard 에
        // ...
        return eNodeState.Success;
    }
}
```

예외: 여러 몬스터가 공유하면 안 되는 Composite 노드는 BT 진입 시 `Instantiate`로 클론해서 쓰고, 클론에 한해 인덱스/타이머를 들 수 있다 (`architecture.md`).

## 4. 목록 SO

여러 에셋을 묶는 SO 도 데이터 컨테이너다.

```csharp
[CreateAssetMenu(fileName = "SO_WaveData", menuName = "Game/Dungeon/WaveData")]
public sealed class SOWaveData : ScriptableObject
{
    public float StartDelay = 1.0f;
    public List<SOPoolData> MonsterPoolList = new List<SOPoolData>();
}
```

## CreateAssetMenu

`[CreateAssetMenu(fileName = "SO_<이름>", menuName = "Game/<분류>/<이름>")]` — 에셋 파일 이름도 `SO_`로 시작한다.

## 쓰지 않는 패턴

SO 이벤트 채널, SO 변수 참조(FloatVariable), SO 런타임 세트는 쓰지 않는다. 알림은 R3 `Subject`/`ReactiveProperty`([[event-systems]]), 활성 목록은 그걸 관리하는 매니저가 `List`로 든다 (`DungeonManager.GetMonsters`).

## 안티패턴

1. **런타임에 SO 필드를 씀** — 공유 오염 + 에디터에서 값이 남는다. 복사본(§2)이나 Blackboard 로
2. **`Resources.Load`로 SO 를 전역 접근** — `[SerializeField]` 참조로
3. **SO 에 시스템 로직을 몰아넣음** — SO 는 데이터 + 실행 단위 하나(노드, 카드 효과)까지. 흐름 제어는 System 쪽
4. **SO 끼리 순환 참조** — 직렬화가 꼬인다
