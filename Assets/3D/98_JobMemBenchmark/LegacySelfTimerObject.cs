using UnityEngine;

/*///////////////////////////////////////////
           LegacySelfTimerObject
목적 : 커밋 1748abc 이전의 PoolObject.Update() 수명 관리 재현 - 오브젝트마다
       매 프레임 자기 남은 시간을 감산하고, 0 이하가 되면 반납한다.

           m_fAliveTime -= Time.deltaTime;
           if (m_fAliveTime <= 0f) ObjectPool.m_Instance.PushObject(gameObject);

       LegacyPQExpireManager(현재 방식)와의 비교군. 이쪽 비용은 "활성 개수 N"에
       비례하고, 큐 방식은 "그 프레임에 실제로 만료된 개수"에 비례한다 - 그래서
       개수를 늘릴수록 두 방식의 기울기가 갈린다.

       실제 반납(SetActive 토글) 대신 스포너의 Respawn으로 자리만 옮긴다 -
       측정 대상은 만료를 확인하는 방식이지 풀 토글 비용이 아니기 때문.
 *///////////////////////////////////////////
public sealed class LegacySelfTimerObject : MonoBehaviour
{
    private LegacyJobMemSpawner m_refOwner;
    private int m_iIndex = -1;
    private float m_fSettingAliveTime = 1.0f;
    private float m_fAliveTime = 1.0f;

    public void Init(LegacyJobMemSpawner _refOwner, int _iIndex, float _fAliveTime)
    {
        m_refOwner = _refOwner;
        m_iIndex = _iIndex;
        m_fSettingAliveTime = _fAliveTime;

        // 첫 만료가 한 프레임에 몰리지 않도록 시작 시각을 흩어둔다
        m_fAliveTime = Random.Range(0.01f, _fAliveTime);
    }

    private void Update()
    {
        m_fAliveTime -= Time.deltaTime;
        if (m_fAliveTime > 0.0f)
            return;

        m_fAliveTime = m_fSettingAliveTime;
        if (m_refOwner != null)
            m_refOwner.Respawn(m_iIndex);
    }
}
