using UnityEngine;

/*///////////////////////////////////////////
                LineInfo
목적 : 볼렛 예고선(BulletLine) 소환/설정을 전담하는 컴포넌트.
       Bullet이 발사 시점(SetAttack)에 SetLine을 직접 호출해서 쓴다
 *///////////////////////////////////////////

public sealed class LineInfo
{
    [SerializeField] private PoolObject m_refBulletLinePoolObj; // BulletLine 프리팹의 PoolObject (ObjectPool 키)
    [SerializeField] private MeshFilter m_refVisualMeshFilter; // 이 볼렛의 실제 비주얼 메시 (BulletLine에 두께로 그대로 넘겨줌)

    public void SetLine(Vector3 _vStart, Vector3 _vDir, float _fDistance)
    {
        if (m_refBulletLinePoolObj == null || m_refVisualMeshFilter == null)
            return;

        GameObject refLineObj = ObjectPool.m_Instance.GetObject(m_refBulletLinePoolObj);
        if (refLineObj == null)
            return;

        BulletLine refLine = refLineObj.GetComponent<BulletLine>();
        refLine.SetLine(_vStart, _vDir, _fDistance, m_refVisualMeshFilter);
    }
}
