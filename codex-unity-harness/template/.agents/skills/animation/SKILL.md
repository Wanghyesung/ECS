---
name: animation
description: "Unity 애니메이션 시스템 — Animator 컨트롤러, 레이어, Blend Tree, State Machine Behaviour, 루트 모션, 애니메이션 이벤트, Timeline. 캐릭터 애니메이션과 상태 전이 작업에 사용합니다."
globs: ["**/*.controller", "**/*Anim*.cs", "**/*.anim"]
---

# Animation System

## Animator Controller

### 파라미터
```csharp
// 해시 ID를 캐싱 — Update에서 문자열 버전은 절대 사용하지 말 것
private static readonly int SpeedHash = Animator.StringToHash("Speed");
private static readonly int JumpHash = Animator.StringToHash("Jump");
private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
private static readonly int AttackHash = Animator.StringToHash("Attack");

private void Update()
{
    m_refAnimator.SetFloat(SpeedHash, m_fCurrentSpeed);
    m_refAnimator.SetBool(IsGroundedHash, m_bIsGrounded);
}

// 트리거: 한 번 발동되고 자동으로 리셋됨
public void Attack() => m_refAnimator.SetTrigger(AttackHash);
```

### 트랜지션 설정
- **Has Exit Time:** 애니메이션이 끝난 뒤 트랜지션됨 (공격 모션에는 적합하지만, 즉각적인 반응이 필요할 때는 부적합)
- **Fixed Duration:** 트랜지션 시간을 초 단위로 할지, 정규화된 값으로 할지
- **Transition Duration:** 블렌드 시간 (0이면 즉시 전환, 0.1~0.25면 부드러운 전환)
- **Interruption Source:** 어떤 트랜지션이 현재 트랜지션을 인터럽트할 수 있는지

### 레이어
- Base Layer: 로코모션 (걷기, 달리기, 대기)
- Upper Body Layer (Avatar Mask): 조준, 공격 (Base Layer를 덮어씀)
- Additive Layer: 호흡, 피격 리액션 (기존 애니메이션 위에 가산됨)

## Blend Tree

**1D:** Speed 파라미터 → 걷기/달리기 블렌드
**2D Simple Directional:** X/Y 입력 → 방향성 이동 (전진, 후진, 좌우 이동)
**2D Freeform:** 모션 클립을 더 자유롭게 배치 가능

## State Machine Behaviour

```csharp
public sealed class AttackStateBehavior : StateMachineBehaviour
{
    public override void OnStateEnter(Animator _refAnimator, AnimatorStateInfo _stateInfo, int _iLayerIndex)
    {
        // 히트박스 활성화
        _refAnimator.GetComponent<CombatSystem>().EnableHitbox();
    }

    public override void OnStateExit(Animator _refAnimator, AnimatorStateInfo _stateInfo, int _iLayerIndex)
    {
        // 히트박스 비활성화
        _refAnimator.GetComponent<CombatSystem>().DisableHitbox();
    }
}
```

## 루트 모션

- Animator에서 `Apply Root Motion`을 활성화
- 커스텀 제어가 필요하면 `OnAnimatorMove()`를 오버라이드:

```csharp
private void OnAnimatorMove()
{
    // 애니메이션의 루트 모션을 위치에 반영
    Vector3 vDeltaPosition = m_refAnimator.deltaPosition;
    transform.position += vDeltaPosition;

    // 애니메이션의 회전 값을 반영
    transform.rotation *= m_refAnimator.deltaRotation;
}
```

## 애니메이션 이벤트

애니메이션 클립의 특정 프레임에서 메서드를 호출합니다.
```csharp
// 애니메이션 이벤트에 의해 12프레임에서 호출됨
public void OnFootstep()
{
    m_refAudioSource.PlayOneShot(m_refFootstepClip);
}

public void OnAttackHit()
{
    // 바로 이 프레임에서 히트박스 충돌을 체크
}
```

## IK (역운동학)

```csharp
private void OnAnimatorIK(int _iLayerIndex)
{
    if (m_refLookTarget != null)
    {
        m_refAnimator.SetLookAtWeight(1f, 0.3f, 0.6f, 1f);
        m_refAnimator.SetLookAtPosition(m_refLookTarget.position);
    }

    // 고르지 않은 지형에 대응하기 위한 발 IK
    m_refAnimator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
    m_refAnimator.SetIKPosition(AvatarIKGoal.LeftFoot, m_vLeftFootTarget);
}
```

## Timeline 연동

- Animation Track: 임의의 Animator에서 애니메이션 클립 재생
- Custom Playable: `PlayableAsset` + `PlayableBehaviour`로 커스텀 Timeline 클립 제작
- Signal Track: 특정 시점에 이벤트 발동 (애니메이션 이벤트와 유사하지만 Timeline에서 동작)
