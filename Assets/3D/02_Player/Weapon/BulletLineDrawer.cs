using UnityEngine;

/*///////////////////////////////////////////
                BulletLineDrawer
목적 : 볼렛 예고선(BulletLine) 소환/설정과 조기 종료를 전담하는 일반 클래스.
       Bullet이 필드로 들고 있다가 발사 시점(SetAttack)에 SetLine을 직접 호출해서 쓴다
 *///////////////////////////////////////////

[System.Serializable]
public sealed class BulletLineDrawer
{
    [SerializeField] private PoolObject m_refBulletLinePoolObj; // BulletLine 프리팹의 PoolObject (ObjectPool 키)
    [SerializeField] private MeshFilter m_refVisualMeshFilter; // 이 볼렛의 실제 비주얼 메시 (BulletLine에 두께로 그대로 넘겨줌)

    private PoolObject m_refActiveLinePoolObj; // 현재 떠 있는 라인 인스턴스 (조기 종료 대상)

    public void SetLine(Vector3 _vStart, Vector3 _vDir, float _fDistance)
    {
        CutLine(); // 이전에 떠 있던 라인이 있으면 먼저 끊고 새로 그림

        if (m_refBulletLinePoolObj == null || m_refVisualMeshFilter == null)
            return;

        GameObject refLineObj = ObjectPool.m_Instance.GetObject(m_refBulletLinePoolObj);
        if (refLineObj == null)
            return;

        BulletLine refLine = refLineObj.GetComponent<BulletLine>();
        refLine.SetLine(_vStart, _vDir, _fDistance, m_refVisualMeshFilter);

        m_refActiveLinePoolObj = refLineObj.GetComponent<PoolObject>();
    }

    // 현재 떠 있는 라인을 즉시 반납 - AliveTime을 0으로 재예약해 다음 체크 때 바로 풀로 돌아가게 함
    public void CutLine()
    {
        if (m_refActiveLinePoolObj == null)
            return;

        m_refActiveLinePoolObj.SetAliveTime(0f);
        m_refActiveLinePoolObj = null;
    }
}
