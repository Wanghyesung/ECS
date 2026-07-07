using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI;
using UnityEngine.AI;


public enum eStatusEffect
{
    Wait,
    Lock,
    Stun,
    Poison,
    Burn,
    End,
}

public enum eEntityState
{
    None,
    Idle,
    Move,
    Attack,
    Hit,
    Lock,
    Dead,
    End,
}


[Serializable]
public struct EffectEntry
{
    public float EndTime;
    public float TickInterval;
    public float NextTickTime;
    public long  TickDamage;
}

[Serializable]
public class ObjectInfo
{
    public eEntityState State;

    public long CurrentHP;
    public float Speed;

    public ushort CurrentEffects; 
    public EffectEntry[] Effects = new EffectEntry[(int)eStatusEffect.End];
}


public class Player : MonoBehaviour, IDamageable
{
    [SerializeField] private List<Weapon> m_listWeapon = null;
    private List<Weapon> m_listFireWeapon = null; 

    [SerializeField] private AnimationTable m_refAnimTable = null;
    [SerializeField] private Aim m_refAim= null;
    [SerializeField] private VisualPlayer m_refVisualPlayer = null;

    [SerializeField] private ObjectInfo m_refObjectInfo = new ObjectInfo();


    [SerializeField] LayerMask m_tAttackLayer;
    private Transform m_refNearTargetTr = null; 
    private Collider[] m_arrNearCollider = new Collider[20];
    [SerializeField] private float m_fAttackRaius;


    private Coroutine m_CoNockback = null;
    private Rigidbody m_refRigidbody = null;
    private void Awake()
    {
        UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);

        m_listFireWeapon = new List<Weapon>(m_listWeapon.Count);
        m_refRigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        
        if (Input.GetKey(KeyCode.Q))
            Fire();
        else
            Cansle();
    }

    private void OnTriggerEnter(Collider other)
    {

    }

    private void OnDisable()
    {
        System.Array.Clear(m_arrNearCollider, 0, m_arrNearCollider.Length);
    }

    public void UpdateOnAnimation(eEntityState _eState, bool _bOn)
    {
        m_refAnimTable.SetBool(_eState, _bOn);
    }

    public void UpdateOnTriggerAnimation(eEntityState _eState)
    {
        m_refAnimTable.SetTrigger(_eState);
    }

    private void Fire()
    {
        bool bFindNearTarget = false;
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].CheckTime() == true)
            {
                if (m_listWeapon[i].NeeadNearTarget == true)
                    bFindNearTarget = true;

                m_listFireWeapon.Add(m_listWeapon[i]);
            }
        }


        if(bFindNearTarget == true)
            FindNearestTarget();

        Vector3 vTargetPos = m_refAim.TargetPosition;

        for (int i = 0; i < m_listFireWeapon.Count; ++i)
            m_listWeapon[i].Fire(vTargetPos, m_refNearTargetTr);


        m_listFireWeapon.Clear();
        m_refNearTargetTr = null;
    }
    private void Cansle()
    {
       
    }

    private void FindNearestTarget()
    {
        Physics.OverlapSphereNonAlloc(transform.position, m_fAttackRaius, m_arrNearCollider, m_tAttackLayer);

        Transform refTarget = null;
        float fBestDist = float.MaxValue;
        Vector3 vPos = transform.position;
        foreach (var refMon in m_arrNearCollider)
        {
            if (refMon == null)
                continue;

            float fDist = Vector3.SqrMagnitude(refMon.transform.position - vPos);
            if (fDist < fBestDist)
            {
                fBestDist = fDist;
                refTarget = refMon.transform;
            }
        }

        m_refNearTargetTr = refTarget;
    }


    public void TakeDamage(AttackInfo _refAttackInfo)
    {
        if (m_CoNockback != null)
            StopCoroutine(m_CoNockback);

        m_CoNockback = StartCoroutine(CoNockback(_refAttackInfo));
    }

    private IEnumerator CoNockback(AttackInfo _refAttackInfo)
    {
        Vector3 vDir = _refAttackInfo.MoveDir;
        float fDuration = Mathf.Max(_refAttackInfo.KnockbackDuration, 0.0001f);
        float fPower = _refAttackInfo.AttackPower;
        if (m_refVisualPlayer != null)
            m_refVisualPlayer.PlayHitShake(vDir, fPower);


        float fNockPower = _refAttackInfo.KnockbackForce;
        float fElapsed = 0f;
        while (fElapsed < fDuration)
        {
            float fRevElaps = 1.0f - (fElapsed / fDuration);
            Vector3 vDelta = vDir * fNockPower * fRevElaps * Time.deltaTime;

            m_refRigidbody.MovePosition(m_refRigidbody.position + vDelta);

            fElapsed += Time.deltaTime;

            yield return null;
        }

        m_CoNockback = null;
    }

   
}
