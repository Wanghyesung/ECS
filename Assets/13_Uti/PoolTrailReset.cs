using UnityEngine;

/*///////////////////////////////////////////
                PoolTrailReset
목적 : TrailRenderer를 쓰는 풀링 오브젝트가 재사용될 때 이전 위치에서부터
       선이 이어져 보이는 문제 방지. 풀에 반납되는 시점(OnPush)에 즉시
       버퍼를 비워, 다음에 다른 위치로 Pop되어도 잔상이 남지 않게 함
 *///////////////////////////////////////////


public class PoolTrailReset : MonoBehaviour
{
    private PoolObject m_refPoolObj;
    private TrailRenderer m_refTrail;

    private void Awake()
    {
        m_refPoolObj = GetComponentInChildren<PoolObject>();
        m_refTrail = GetComponentInChildren<TrailRenderer>();

        // 혹시나 자식에게도 없는 경우를 대비한 Null 체크 예외 처리
        if (m_refPoolObj == null || m_refTrail == null)
        {
            Debug.LogError($"{gameObject.name}의 자식 오브젝트에서 필요한 컴포넌트를 찾을 수 없습니다!");
        }
    }

    private void OnEnable()
    {
        m_refPoolObj.OnPush += m_refTrail.Clear;
    }

    private void OnDisable()
    {
        m_refPoolObj.OnPush -= m_refTrail.Clear;
    }
}
