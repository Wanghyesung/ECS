using UnityEngine;

/*///////////////////////////////////////////
            SOWeaponFireBehavior
목적 : Weapon이 "언제/어떻게 쏠지"를 위임하는 정책 객체. Weapon.Init()이 인스펙터에 할당된
       원본을 Instantiate()로 클론해서 갖고 있으므로, 클론 인스턴스에 한해 런타임 상태
       (타이머 등)를 직접 들고 있어도 된다 - BT Composite 노드와 동일한 전례
       (.claude/rules/architecture.md 데이터 분리 규칙 예외 조항).
 *///////////////////////////////////////////
public abstract class SOWeaponFireBehavior : ScriptableObject
{
    // 매 프레임 Player/Drone이 호출. 언제 Weapon.Fire()를 부를지는 구현체가 결정
    public abstract void UpdateWeapon(Weapon _refWeapon, Vector3 _vTargetPos, Transform _refTargetTr);

    // Weapon.Init() 직후 1회 호출 - 기준값 캐싱 등에 사용
    public virtual void OnInit(Weapon _refWeapon) { }

    // Weapon.OnDisable()에서 호출 - 진행 중이던 상태(차징 등) 리셋에 사용
    public virtual void OnWeaponDisabled() { }

    // Weapon.SetCooldown()이 호출 - 공격속도 카드가 이 정책의 자체 타이밍 값도 비례 조정하고 싶을 때 사용
    public virtual void OnCooldownScaled(float _fClampedRatio) { }
}
