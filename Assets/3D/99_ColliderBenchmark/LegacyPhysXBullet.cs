using UnityEngine;

/*///////////////////////////////////////////
            LegacyPhysXBullet
목적 : 커밋 fdf901d([Add] 로비창 스프라이트 UI 추가) 시점의 실제 Bullet.cs가 쓰던
       이동/충돌 메커니즘을 그대로 옮긴 벤치마크 전용 컴포넌트 - Rigidbody(kinematic)를
       FixedUpdate()에서 MovePosition으로 옮기고, TriggerEnterObject(ITriggerable)의
       OnHitTargetEnter로 충돌을 받는다. 클래스 이름은 실제 Bullet과 겹치면 안 되니
       다르게 뒀지만 이동/충돌 로직 자체는 원본과 동일하다.

       AttackInfo/SOBulletAction/데미지 처리는 뺐다 - 이번 벤치마크가 원래도 "충돌
       판정+콜백 발동" 비용만 재는 목적이라(자체 시스템 쪽도 카운터만 증가시켰음)
       무기/데미지 시스템까지 끌어올 필요가 없음.
 *///////////////////////////////////////////
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(TriggerEnterObject))]
public class LegacyPhysXBullet : MonoBehaviour
{
    public static int s_iEnterCount = 0;

    public static void ResetCounter() => s_iEnterCount = 0;

    private Rigidbody m_refRigidbody;
    private TriggerEnterObject m_refTrigger;

    private Vector3 m_vVelocity;
    private Vector3 m_vBoundsCenter;
    private float m_fBoundsRadius;
    private float m_fLifetime;
    private float m_fAliveTimer;

    private void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refTrigger = GetComponent<TriggerEnterObject>();
    }

    private void OnEnable()
    {
        m_refTrigger.OnHitTargetEnter += OnHit;
        m_fAliveTimer = 0f;
    }

    private void OnDisable()
    {
        m_refTrigger.OnHitTargetEnter -= OnHit;
    }

    public void Init(Vector3 _vBoundsCenter, float _fBoundsRadius, Vector3 _vVelocity, float _fLifetime, LayerMask _tHitLayer)
    {
        m_vBoundsCenter = _vBoundsCenter;
        m_fBoundsRadius = _fBoundsRadius;
        m_vVelocity = _vVelocity;
        m_fLifetime = _fLifetime;
        m_refTrigger.LayerMask = _tHitLayer;
    }

    // 원본 Bullet.FixedUpdate()와 동일 - Rigidbody.MovePosition으로 물리 스텝에 맞춰 이동.
    // 경계 밖으로 나가면 반사시키는 부분만 벤치마크용으로 추가(원본은 AliveTime 되면
    // 그냥 풀 반납, 방향 전환 없이 직진만 함 - 여기선 한정된 영역 안에서 지속적으로
    // 겹칠 기회를 만들기 위해 튕기게 함)
    private void FixedUpdate()
    {
        m_fAliveTimer += Time.fixedDeltaTime;
        if (m_fAliveTimer >= m_fLifetime)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector3 vNextPos = m_refRigidbody.position + m_vVelocity * Time.fixedDeltaTime;

        Vector3 vOffset = vNextPos - m_vBoundsCenter;
        if (vOffset.sqrMagnitude > m_fBoundsRadius * m_fBoundsRadius)
        {
            Vector3 vNormal = vOffset.normalized;
            m_vVelocity = Vector3.Reflect(m_vVelocity, vNormal);
            vNextPos = m_vBoundsCenter + vNormal * m_fBoundsRadius;
        }

        m_refRigidbody.MovePosition(vNextPos);
    }

    // 원본 Bullet.AttackMonster(Collider)와 동일한 시그니처 - 실제 데미지 대신 카운트만
    private void OnHit(Collider _tOther)
    {
        ++s_iEnterCount;
    }
}
