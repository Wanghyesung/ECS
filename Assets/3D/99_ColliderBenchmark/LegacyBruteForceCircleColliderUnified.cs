using UnityEngine;

/*///////////////////////////////////////////
       LegacyBruteForceCircleColliderUnified
목적 : 챕터 3 전용 Circle 콜라이더(총알/몬스터).
 *///////////////////////////////////////////
public sealed class LegacyBruteForceCircleColliderUnified : LegacyBruteForceColliderUnified
{
    [SerializeField] private float m_fRadius = 0.5f;

    public float Radius => m_fRadius;
    public override float BoundingRadius => m_fRadius;

    public void SetRadius(float _fRadius) => m_fRadius = _fRadius;

    protected override void Awake()
    {
        base.Awake();
        Shape = eLegacyColliderShapeUnified.Circle;
    }

    public override void RefreshCenter()
    {
        CachedCenter = transform.position;
    }
}
