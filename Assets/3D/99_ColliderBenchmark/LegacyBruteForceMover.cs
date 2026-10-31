using UnityEngine;

/*///////////////////////////////////////////
            LegacyBruteForceMover
목적 : 벤치마크 총알을 경계 구 안에서 계속 움직이게 하는 용도 - 판정이 매 프레임
       실제로 다른 위치에서 다시 계산되도록(캐시가 아니라 진짜 재판정) 만든다.
       Update()에서 Transform을 직접 이동시킨다 - 421d1b0 시점엔 이미 실제
       Bullet도 FixedUpdate+Rigidbody가 아니라 Update+Transform 직접 이동으로
       바뀐 뒤였으므로 그 방식을 그대로 따른다.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceMover : MonoBehaviour
{
    private Transform m_refTransform;
    private Vector3 m_vVelocity;

    private Vector3 m_vBoundsCenter;
    private float m_fBoundsRadius;
    private float m_fSpeed;
    private bool m_bHitPending;
    private System.Random m_refRandom;
    private BenchmarkBulletPresentation m_refPresentation;

    private void Awake() => m_refTransform = transform;

    public void Init(Vector3 _vVelocity, Vector3 _vBoundsCenter, float _fBoundsRadius,
        System.Random _refRandom, BenchmarkBulletPresentation _refPresentation)
    {
        m_vVelocity = _vVelocity;
        m_vBoundsCenter = _vBoundsCenter;
        m_fBoundsRadius = _fBoundsRadius;
        m_fSpeed = _vVelocity.magnitude;
        m_refRandom = _refRandom;
        m_refPresentation = _refPresentation;
    }

    // 각 버전의 콜라이더가 같은 GameObject의 이동기에 알린다. 콜백 중에는
    // Transform/등록 목록을 바꾸지 않아 Job 결과 처리 도중 상태가 달라지지 않는다.
    public void OnHit(Component _refOther)
    {
        if (m_bHitPending)
            return;
        m_bHitPending = true;
        m_refPresentation.RegisterHit(m_refTransform.position);
    }

    private void Update()
    {
        if (m_bHitPending)
        {
            m_bHitPending = false;
            Vector3 vSpawnOffset = Vector3.zero;
            for (int i = 0; i < 32; i++)
            {
                vSpawnOffset = RandomVector();
                if (vSpawnOffset.sqrMagnitude <= 1f)
                    break;
            }
            vSpawnOffset = Vector3.ClampMagnitude(vSpawnOffset, 1f);
            Vector3 vDirection = RandomVector();
            vDirection = vDirection.sqrMagnitude > 0.0001f ? vDirection.normalized : Vector3.forward;
            m_vVelocity = vDirection * m_fSpeed;
            m_refTransform.SetPositionAndRotation(m_vBoundsCenter + vSpawnOffset * m_fBoundsRadius,
                Quaternion.LookRotation(vDirection));
            return;
        }

        Vector3 vNextPos = m_refTransform.position + m_vVelocity * Time.deltaTime;

        Vector3 vOffset = vNextPos - m_vBoundsCenter;
        if (vOffset.sqrMagnitude > m_fBoundsRadius * m_fBoundsRadius)
        {
            Vector3 vNormal = vOffset.normalized;
            m_vVelocity = Vector3.Reflect(m_vVelocity, vNormal);
            m_refTransform.rotation = Quaternion.LookRotation(m_vVelocity);
            vNextPos = m_vBoundsCenter + vNormal * m_fBoundsRadius;
        }

        m_refTransform.position = vNextPos;
    }

    private Vector3 RandomVector()
        => new Vector3((float)m_refRandom.NextDouble() * 2f - 1f,
            (float)m_refRandom.NextDouble() * 2f - 1f,
            (float)m_refRandom.NextDouble() * 2f - 1f);
}
