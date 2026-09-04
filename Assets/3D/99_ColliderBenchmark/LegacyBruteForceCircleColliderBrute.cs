using UnityEngine;

/*///////////////////////////////////////////
        LegacyBruteForceCircleColliderBrute
목적 : 421d1b0 시점 CircleCollider 포팅 - 총알/몬스터 전용 원(구) 콜라이더.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceCircleColliderBrute : LegacyBruteForceColliderBrute
{
    [SerializeField] private float m_fRadius = 0.5f;

    public float Radius => m_fRadius;

    public void SetRadius(float _fRadius) => m_fRadius = _fRadius;

    protected override void Awake()
    {
        base.Awake();
        Shape = eLegacyColliderShapeBrute.Circle;
    }

    public override void RefreshCenter()
    {
        CachedCenter = transform.position;
    }
}
