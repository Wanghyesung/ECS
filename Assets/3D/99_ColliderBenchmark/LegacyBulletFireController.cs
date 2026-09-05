using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
        LegacyBulletFireController
목적 : 미리 만들어둔(비활성 상태) LegacyPhysXBullet 풀을 순환하며 일정한 발사율로
       계속 발사한다. "발사율 x 수명 = 평균 동시 활성 개수"이므로, 원하는 동시
       개수를 목표로 발사 간격을 역산한다.
 *///////////////////////////////////////////
public class LegacyBulletFireController : MonoBehaviour
{
    private List<GameObject> m_listPool;
    private Vector3 m_vSpawnCenter;
    private float m_fBoundsRadius;
    private float m_fBulletSpeed;
    private float m_fBulletLifetime;
    private LayerMask m_tHitLayer;
    private float m_fFireInterval;
    private int m_iNextIndex;
    private float m_fTimer;

    public void Init(List<GameObject> _listPool, Vector3 _vSpawnCenter, float _fBoundsRadius,
        float _fBulletSpeed, float _fBulletLifetime, float _fTargetActiveCount, LayerMask _tHitLayer)
    {
        m_listPool = _listPool;
        m_vSpawnCenter = _vSpawnCenter;
        m_fBoundsRadius = _fBoundsRadius;
        m_fBulletSpeed = _fBulletSpeed;
        m_fBulletLifetime = _fBulletLifetime;
        m_tHitLayer = _tHitLayer;
        m_fFireInterval = _fBulletLifetime / Mathf.Max(1f, _fTargetActiveCount);
        m_iNextIndex = 0;
        m_fTimer = 0f;
    }

    private void Update()
    {
        if (m_listPool == null)
            return;

        m_fTimer += Time.deltaTime;
        while (m_fTimer >= m_fFireInterval)
        {
            m_fTimer -= m_fFireInterval;
            FireNext();
        }
    }

    private void FireNext()
    {
        for (int i = 0; i < m_listPool.Count; ++i)
        {
            int idx = (m_iNextIndex + i) % m_listPool.Count;
            GameObject go = m_listPool[idx];
            if (go.activeSelf)
                continue;

            m_iNextIndex = (idx + 1) % m_listPool.Count;

            Vector3 vDir = RandomDirection();
            go.transform.position = m_vSpawnCenter;

            LegacyPhysXBullet bullet = go.GetComponent<LegacyPhysXBullet>();
            bullet.Init(m_vSpawnCenter, m_fBoundsRadius, vDir * m_fBulletSpeed, m_fBulletLifetime, m_tHitLayer);

            go.SetActive(true);
            return;
        }
    }

    private Vector3 RandomDirection()
    {
        Vector3 v = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
