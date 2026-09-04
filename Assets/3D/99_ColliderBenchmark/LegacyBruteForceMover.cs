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
    private Vector3 m_vVelocity;
    private Vector3 m_vBoundsCenter;
    private float m_fBoundsRadius;

    public void Init(Vector3 _vVelocity, Vector3 _vBoundsCenter, float _fBoundsRadius)
    {
        m_vVelocity = _vVelocity;
        m_vBoundsCenter = _vBoundsCenter;
        m_fBoundsRadius = _fBoundsRadius;
    }

    private void Update()
    {
        Vector3 vNextPos = transform.position + m_vVelocity * Time.deltaTime;

        Vector3 vOffset = vNextPos - m_vBoundsCenter;
        if (vOffset.sqrMagnitude > m_fBoundsRadius * m_fBoundsRadius)
        {
            Vector3 vNormal = vOffset.normalized;
            m_vVelocity = Vector3.Reflect(m_vVelocity, vNormal);
            vNextPos = m_vBoundsCenter + vNormal * m_fBoundsRadius;
        }

        transform.position = vNextPos;
    }
}
