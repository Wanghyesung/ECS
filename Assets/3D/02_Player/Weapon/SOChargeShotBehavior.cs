using UnityEngine;

/*///////////////////////////////////////////
            SOChargeShotBehavior
목적 : 입력 없이 자동으로 차지 -> 완충되면 자동 발사하는 발사 정책. 완충된 총알은 크기/속도가
       커진다. 몬스터 BT의 SOChargeNode/SOChargeFireNode와 이름이 겹치지 않도록
       "ShotBehavior"로 구분함(용도가 다름 - BT 전용이 아니라 Weapon 조합용).
 *///////////////////////////////////////////
[CreateAssetMenu(menuName = "Game/Weapon Fire Behavior/Charge Shot")]
public sealed class SOChargeShotBehavior : SOWeaponFireBehavior
{
    [SerializeField] private float m_fMaxChargeTime = 1.5f;       // 완전 충전까지 걸리는 시간(초). 0 이하 = 차징 없이 즉시 완충 취급
    [SerializeField] private float m_fMaxChargeSizeScale = 2.5f;  // 완충 시 총알 크기 배수
    [SerializeField] private float m_fMaxChargeSpeedScale = 1.5f; // 완충 시 총알 속도 배수

    // Weapon.Init()이 Instantiate()로 클론한 인스턴스에 한해 상태를 들고 있음(클래스 주석 참고)
    private bool m_bIsCharging;
    private float m_fChargeStartTime;
    private float m_fBaseChargeTime; // OnCooldownScaled가 매번 원본 기준으로 재계산하기 위한 기준값

    public override void OnInit(Weapon _refWeapon)
    {
        m_fBaseChargeTime = m_fMaxChargeTime;
    }

    public override void UpdateWeapon(Weapon _refWeapon, Vector3 _vTargetPos, Transform _refTargetTr)
    {
        if (m_bIsCharging == false)
        {
            if (_refWeapon.CheckTime() == false) // 직전 발사 후 재충전 대기(SOAttackInfo.Cooldown 재사용)
                return;

            m_bIsCharging = true;
            m_fChargeStartTime = Time.time;
            return;
        }

        float fElapsed = Time.time - m_fChargeStartTime;
        if (GetChargeRatio(fElapsed, m_fMaxChargeTime) < 1f)
            return;

        bool bFired = _refWeapon.Fire(_vTargetPos, _refTargetTr, m_fMaxChargeSpeedScale, m_fMaxChargeSizeScale);
        if (bFired)
            m_bIsCharging = false;
        // 실패(오브젝트 풀 고갈) 시 m_bIsCharging을 유지해서 다음 프레임에 재시도 - 다 채운 차지가
        // 풀 고갈 한 번으로 조용히 증발하는 걸 방지 (ObjectPool.GetObject가 null을 반환할 수 있음)
    }

    // 무기가 비활성화되는 동안(카드로 UnActiveWweapon 등) m_fChargeStartTime이 얼어붙어
    // 재활성 시 "경과시간 수십 초"로 즉시 완충 발사되는 걸 막는다
    public override void OnWeaponDisabled()
    {
        m_bIsCharging = false;
    }

    // 공격속도 강화 카드가 차지 시간에도 비례해서 체감되도록 스케일
    public override void OnCooldownScaled(float _fClampedRatio)
    {
        m_fMaxChargeTime = m_fBaseChargeTime * _fClampedRatio;
    }

    // 순수 정적 함수 - TDD 대상 API. 씬 의존 없는 순수 함수만 EditMode 테스트로 검증하는
    // 이 프로젝트의 기존 컨벤션(ColliderManager.IsCircleBoxOverlap)과 동일 패턴
    public static float GetChargeRatio(float _fElapsedTime, float _fMaxChargeTime)
    {
        if (_fMaxChargeTime <= 0f)
            return 1f;

        return Mathf.Clamp01(_fElapsedTime / _fMaxChargeTime);
    }
}
