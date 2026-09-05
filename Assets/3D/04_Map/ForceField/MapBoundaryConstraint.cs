using UnityEngine;

/*///////////////////////////////////////////
              MapBoundaryConstraint
목적 : 구형 맵 경계(이 오브젝트의 position/scale) 밖으로 타겟이 못 나가게 막음.
       콜라이더 대신 위치 클램프를 쓰는 이유 - 구체는 안쪽에서 보면 "속이 꽉 찬 덩어리"라
       일반 SphereCollider/Convex MeshCollider로는 오히려 플레이어를 바깥으로 밀어냄.
 *///////////////////////////////////////////
public sealed class MapBoundaryConstraint : MonoBehaviour
{
    [SerializeField] private Rigidbody m_refTargetBody;

    private void FixedUpdate()
    {
        if (m_refTargetBody == null) return;

        Vector3 vCenter = transform.position;
        float fRadius = transform.lossyScale.x * 0.5f;

        Vector3 vOffset = m_refTargetBody.position - vCenter;
        float fDistance = vOffset.magnitude;

        if (fDistance <= fRadius) return;

        Vector3 vDirection = vOffset / fDistance;
        m_refTargetBody.position = vCenter + vDirection * fRadius;

        float fOutwardSpeed = Vector3.Dot(m_refTargetBody.velocity, vDirection);
        if (fOutwardSpeed > 0f)
        {
            m_refTargetBody.velocity -= vDirection * fOutwardSpeed;
        }
    }
}
