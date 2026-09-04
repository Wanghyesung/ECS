using UnityEngine;

/*///////////////////////////////////////////
          LegacyBruteForceCircleCollider
목적 : 421d1b0 시점 CircleCollider 포팅 - 총알/몬스터 전용 원(구) 콜라이더.
       회전을 쓰지 않는 벤치마크라 오프셋 없이 transform.position을 그대로
       중심으로 쓴다.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceCircleCollider : LegacyBruteForceCollider
{
    [SerializeField] private float m_fRadius = 0.5f;

    public float Radius => m_fRadius;

    public void SetRadius(float _fRadius) => m_fRadius = _fRadius;

    protected override void Awake()
    {
        base.Awake();
        Shape = eLegacyColliderShape.Circle;
    }

    public override void RefreshCenter()
    {
        CachedCenter = transform.position;
    }
}
