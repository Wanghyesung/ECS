using UnityEngine;

/*///////////////////////////////////////////
             LegacyUpdateMover
목적 : Job을 전혀 쓰지 않고 총알 하나하나가 자기 Update()에서 Transform을 직접 옮기는
       "이전 방식" 재현. BulletMoveManager(TransformAccessArray + Burst Job)와의 비교군.

       프로파일러에서는 LegacyUpdateMover.Update() [Invoke] 한 줄로 묶여 나오고,
       그 위에 "N instances" 누적 시간이 찍힌다 - 별도 ProfilerMarker가 필요 없다.
       비용은 두 겹이다 : 매니지드 Update() 호출 N번 + Transform 쓰기 N번.
 *///////////////////////////////////////////
public sealed class LegacyUpdateMover : MonoBehaviour
{
    private Vector3 m_vVelocity;
    private float m_fBoundsRadius;

    public void Init(Vector3 _vVelocity, float _fBoundsRadius)
    {
        m_vVelocity = _vVelocity;
        m_fBoundsRadius = _fBoundsRadius;
    }

    private void Update()
    {
        Vector3 vNextPos = transform.position + m_vVelocity * Time.deltaTime;

        // 경계를 벗어나면 반사 - 총알이 계속 살아서 매 프레임 실제로 움직이게 한다
        if (vNextPos.sqrMagnitude > m_fBoundsRadius * m_fBoundsRadius)
        {
            Vector3 vNormal = vNextPos.normalized;
            m_vVelocity = Vector3.Reflect(m_vVelocity, vNormal);
            vNextPos = vNormal * m_fBoundsRadius;
        }

        transform.position = vNextPos;
    }
}
