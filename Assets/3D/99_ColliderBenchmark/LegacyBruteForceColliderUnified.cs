using System;
using UnityEngine;

/*///////////////////////////////////////////
          LegacyBruteForceColliderUnified
목적 : 챕터 3(Burst Job 병렬화 = daef9e3+44b4646 통합) 전용 베이스. Circle/Box
       구분 없이 전부 같은 그리드+Job으로 판정되므로, GridTestScene1(daef9e3, Box만
       Job)과 완전히 별도 타입으로 분리했다("버전별 새 파일" 원칙 유지).
 *///////////////////////////////////////////
public abstract class LegacyBruteForceColliderUnified : MonoBehaviour
{
    public event Action<LegacyBruteForceColliderUnified> OnHitTargetEnter;
    public event Action<LegacyBruteForceColliderUnified> OnHitTargetStay;
    public event Action<LegacyBruteForceColliderUnified> OnHitTargetExit;

    private static int s_iNextId = 0;

    public int Layer { get; private set; }
    public int ID { get; private set; }
    public eLegacyColliderShapeUnified Shape { get; protected set; }
    public Vector3 CachedCenter { get; protected set; }

    // 도형 무관 공통 반지름 상한 - 그리드 셀 크기 산정 + 원-박스 선판정에 쓰인다
    public virtual float BoundingRadius => 0f;

    private bool m_bActivated = false;

    protected virtual void Awake()
    {
        ID = s_iNextId++;
        Layer = gameObject.layer;
    }

    protected virtual void Start()
    {
        LegacyBruteForceColliderManagerUnified.Instance.RegisterCollider(this);

        if (m_bActivated == false)
            Activate();
    }

    private void OnEnable()
    {
        if (LegacyBruteForceColliderManagerUnified.Instance != null)
            Activate();
    }

    private void Activate()
    {
        LegacyBruteForceColliderManagerUnified.Instance.Activate(this);
        m_bActivated = true;
    }

    private void OnDisable()
    {
        if (m_bActivated)
        {
            LegacyBruteForceColliderManagerUnified.Instance.UnActivate(this);
            m_bActivated = false;
        }
    }

    public virtual void RefreshCenter() { }

    public void OnEnterCollider(LegacyBruteForceColliderUnified _refOther) => OnHitTargetEnter?.Invoke(_refOther);
    public void OnStayCollider(LegacyBruteForceColliderUnified _refOther) => OnHitTargetStay?.Invoke(_refOther);
    public void OnExitCollider(LegacyBruteForceColliderUnified _refOther) => OnHitTargetExit?.Invoke(_refOther);
}

public enum eLegacyColliderShapeUnified
{
    Circle,
    Box,
}
