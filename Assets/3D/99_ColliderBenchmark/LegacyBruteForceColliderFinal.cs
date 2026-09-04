using System;
using UnityEngine;

/*///////////////////////////////////////////
            LegacyBruteForceColliderFinal
목적 : 챕터 4(스케줄링 최적화 = 4fcbb13+284bec5 통합) 전용 베이스 - 여기까지 오면
       실제 BattleScene의 ColliderManager와 판정 알고리즘이 동일해진다. GridTestScene2
       (챕터 3)와 별도 타입인 이유: Circle 콜라이더는 이제 매 프레임 RefreshCenter()가
       아니라 TransformAccessArray+Job으로 위치가 갱신되고, 그 결과를
       ApplyCachedCenter()로 되돌려 받는다는 점이 다르다.

       Box(정적)는 여전히 Start()에서 한 번만 CachedCenter를 계산하고 이후 갱신하지
       않는다 - TransformAccessArray에도 등록되지 않는다(Docs/Collider.md §13:
       "Static 콜라이더는 위치가 안 바뀌므로 애초에 등록하지 않는다").
 *///////////////////////////////////////////
public abstract class LegacyBruteForceColliderFinal : MonoBehaviour
{
    public event Action<LegacyBruteForceColliderFinal> OnHitTargetEnter;
    public event Action<LegacyBruteForceColliderFinal> OnHitTargetStay;
    public event Action<LegacyBruteForceColliderFinal> OnHitTargetExit;

    private static int s_iNextId = 0;

    public int Layer { get; private set; }
    public int ID { get; private set; }
    public eLegacyColliderShapeFinal Shape { get; protected set; }
    public Vector3 CachedCenter { get; protected set; }

    public virtual float BoundingRadius => 0f;

    private bool m_bActivated = false;

    protected virtual void Awake()
    {
        ID = s_iNextId++;
        Layer = gameObject.layer;
    }

    protected virtual void Start()
    {
        LegacyBruteForceColliderManagerFinal.Instance.RegisterCollider(this);

        if (m_bActivated == false)
            Activate();
    }

    private void OnEnable()
    {
        if (LegacyBruteForceColliderManagerFinal.Instance != null)
            Activate();
    }

    private void Activate()
    {
        LegacyBruteForceColliderManagerFinal.Instance.Activate(this);
        m_bActivated = true;
    }

    private void OnDisable()
    {
        if (m_bActivated)
        {
            LegacyBruteForceColliderManagerFinal.Instance.UnActivate(this);
            m_bActivated = false;
        }
    }

    // TransformAccessArray Job의 결과를 되돌려 쓰는 용도(Circle 전용 - Box는 절대
    // 호출되지 않음). 원본 BaseCollider.ApplyCachedCenter와 동일한 역할
    public void ApplyCachedCenter(Vector3 _vCenter)
    {
        CachedCenter = _vCenter;
    }

    public void OnEnterCollider(LegacyBruteForceColliderFinal _refOther) => OnHitTargetEnter?.Invoke(_refOther);
    public void OnStayCollider(LegacyBruteForceColliderFinal _refOther) => OnHitTargetStay?.Invoke(_refOther);
    public void OnExitCollider(LegacyBruteForceColliderFinal _refOther) => OnHitTargetExit?.Invoke(_refOther);
}

public enum eLegacyColliderShapeFinal
{
    Circle,
    Box,
}
