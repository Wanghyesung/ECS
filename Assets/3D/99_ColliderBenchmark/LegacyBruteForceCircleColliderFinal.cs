using UnityEngine;

/*///////////////////////////////////////////
        LegacyBruteForceCircleColliderFinal
목적 : 챕터 4 전용 Circle 콜라이더(총알/몬스터). CachedCenter를 더 이상 스스로
       RefreshCenter()로 갱신하지 않는다 - Activate 시 매니저의
       TransformAccessArray에 등록되고, 매 프레임 결과는 ApplyCachedCenter로
       외부에서 주입된다.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceCircleColliderFinal : LegacyBruteForceColliderFinal
{
    [SerializeField] private float m_fRadius = 0.5f;

    public float Radius => m_fRadius;
    public override float BoundingRadius => m_fRadius;

    public void SetRadius(float _fRadius) => m_fRadius = _fRadius;

    protected override void Awake()
    {
        base.Awake();
        Shape = eLegacyColliderShapeFinal.Circle;
    }
}
