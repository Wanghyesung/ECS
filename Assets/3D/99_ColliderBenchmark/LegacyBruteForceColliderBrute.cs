using System;
using UnityEngine;

/*///////////////////////////////////////////
            LegacyBruteForceColliderBrute
목적 : 커밋 421d1b0(그리드 도입 직전, 순수 브루트포스) 시점의 BaseCollider를 그대로
       옮긴 벤치마크 전용 베이스. GridTestScene0(8edea44, Box만 그리드 얹음)/
       GridTestScene1(daef9e3, Box Burst Job)와는 완전히 별도 타입 - 나중 단계에서
       LegacyBruteForceCollider(V1)가 그리드를 얹으며 더 이상 "순수 브루트포스"가
       아니게 됐기 때문에, BruteForceTestScene은 이 독립 스냅샷으로 421d1b0을
       계속 재현한다.
 *///////////////////////////////////////////
public abstract class LegacyBruteForceColliderBrute : MonoBehaviour
{
    public event Action<LegacyBruteForceColliderBrute> OnHitTargetEnter;
    public event Action<LegacyBruteForceColliderBrute> OnHitTargetStay;
    public event Action<LegacyBruteForceColliderBrute> OnHitTargetExit;

    private static int s_iNextId = 0;

    public int Layer { get; private set; }
    public int ID { get; private set; }
    public eLegacyColliderShapeBrute Shape { get; protected set; }
    public Vector3 CachedCenter { get; protected set; }

    private bool m_bActivated = false;

    protected virtual void Awake()
    {
        ID = s_iNextId++;
    }

    protected virtual void Start()
    {
        LegacyBruteForceColliderManagerBrute.Instance.RegisterCollider(this);

        if (m_bActivated == false)
            Activate();
    }

    private void OnEnable()
    {
        if (LegacyBruteForceColliderManagerBrute.Instance != null)
            Activate();
    }

    private void Activate()
    {
        LegacyBruteForceColliderManagerBrute.Instance.Activate(this);
        m_bActivated = true;
    }

    private void OnDisable()
    {
        if (m_bActivated)
        {
            LegacyBruteForceColliderManagerBrute.Instance.UnActivate(this);
            m_bActivated = false;
        }
    }

    // 스포너가 생성 직후(이 오브젝트의 OnEnable이 돌기 전) 한 번 호출해서 레이어를 지정한다
    public void SetLayer(int _iLayer)
    {
        Layer = _iLayer;
    }

    public virtual void RefreshCenter() { }

    public void OnEnterCollider(LegacyBruteForceColliderBrute _refOther) => OnHitTargetEnter?.Invoke(_refOther);
    public void OnStayCollider(LegacyBruteForceColliderBrute _refOther) => OnHitTargetStay?.Invoke(_refOther);
    public void OnExitCollider(LegacyBruteForceColliderBrute _refOther) => OnHitTargetExit?.Invoke(_refOther);
}

public enum eLegacyColliderShapeBrute
{
    Circle,
    Box,
}
