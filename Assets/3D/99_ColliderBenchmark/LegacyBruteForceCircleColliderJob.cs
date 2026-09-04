using UnityEngine;

/*///////////////////////////////////////////
          LegacyBruteForceCircleColliderJob
목적 : daef9e3 단계 전용 Circle 콜라이더(총알/몬스터). GridTestScene0의
       LegacyBruteForceCircleCollider와 로직은 동일하고 베이스 타입만 Job 버전이다.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceCircleColliderJob : LegacyBruteForceColliderJob
{
    [SerializeField] private float m_fRadius = 0.5f;

    public float Radius => m_fRadius;

    public void SetRadius(float _fRadius) => m_fRadius = _fRadius;

    protected override void Awake()
    {
        base.Awake();
        Shape = eLegacyColliderShapeJob.Circle;
    }

    public override void RefreshCenter()
    {
        CachedCenter = transform.position;
    }
}
