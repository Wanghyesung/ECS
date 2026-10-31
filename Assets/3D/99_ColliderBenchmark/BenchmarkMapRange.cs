using UnityEngine;

/*///////////////////////////////////////////
                BenchmarkMapRange
목적 : 편집기에 배치된 맵 범위를 총알 생성·재발사·경계 이동의 공통 기준으로 제공한다.
 *///////////////////////////////////////////
public sealed class BenchmarkMapRange : MonoBehaviour
{
    [SerializeField, Min(1f)] private float m_fRadius = 100f;
    public Vector3 Center => transform.position;
    public float Radius => m_fRadius;

    private void OnValidate()
    {
        m_fRadius = Mathf.Max(1f, m_fRadius);
        transform.localScale = Vector3.one * (m_fRadius * 2f);
    }

    private void OnDrawGizmos()
    {
        Color tPrevious = Gizmos.color;
        Gizmos.color = new Color(0.25f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireSphere(Center, m_fRadius);
        Gizmos.color = tPrevious;
    }
}
