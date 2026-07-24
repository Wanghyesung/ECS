using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Flags]
public enum eAnimParamType
{
    None = 0,
    Bool = 1 << 0,
    Trigger = 1 << 1,
    Int = 1 << 2,
    Float = 1 << 3
}

[Serializable]
public class AnimationNode
{
    public eEntityState State;
    public eAnimParamType ParamType;

    public string ParamName;

    public bool Bool;
    public int Int;
    public float Float;
}

/*///////////////////////////////////////////
             AnimationTable

기능 : 애니메이션 실행 관리, Entity상태에 따른 애니메이션 파리미터 관리
 *///////////////////////////////////////////

[RequireComponent(typeof(Animator))]
public class AnimationTable : MonoBehaviour
{
    [SerializeField] private bool m_bOnTimeScale = false;

    [SerializeField] private List<AnimationNode> m_listAnimationList = new();
    private Dictionary<eEntityState, AnimationNode> m_hashAnimation = new();
    private Animator m_refAnimator = null;
    private string m_currentBoolParam = null;

    
    private void Awake()
    {
        m_hashAnimation.Clear();
        foreach (var tNode in m_listAnimationList)
            m_hashAnimation[tNode.State] = tNode;

        m_refAnimator = GetComponentInChildren<Animator>();

        m_listAnimationList.Clear();


        if (m_bOnTimeScale == false)
            m_refAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    public void SetTrigger(eEntityState _eState)
    {
        AnimationNode refAnim = FindAnimNode(_eState);
        if (refAnim == null)
            return;

        UpdateAnimation(refAnim);
    }
    public void SetBool(eEntityState _eState, bool _iValue)
    {
        AnimationNode refAnim = FindAnimNode(_eState);
        if (refAnim==null)
            return;

        refAnim.Bool = _iValue;
        UpdateAnimation(refAnim);
    }
    public void SetInt(eEntityState _eState, int _iValue)
    {
        AnimationNode refAnim = FindAnimNode(_eState);
        if (refAnim == null)
            return;

        refAnim.Int = _iValue;
        UpdateAnimation(refAnim);
    }
    public void SetFloat(eEntityState _eState, float _fValue)
    {
        AnimationNode refAnim = FindAnimNode(_eState);
        if (refAnim == null)
            return;

        refAnim.Float = _fValue;
        UpdateAnimation(refAnim);
    }

    private void UpdateAnimation(AnimationNode _refAnim)
    {
       
        switch (_refAnim.ParamType)
        {
            case eAnimParamType.Trigger:
                {
                    UpdateAnimationTrigger(_refAnim);
                    break;
                }
            case eAnimParamType.Bool:
                {
                    UpdateAnimationBool(_refAnim);
                    break;
                }
            case eAnimParamType.Int:
                {
                    UpdateAnimationInt(_refAnim);
                    break;
                }
            case eAnimParamType.Float:
                {
                    UpdateAnimationFloat(_refAnim);
                    break;
                }
        }
    }

    public async UniTask PlayAimation(eEntityState _eEntityState)
    {
        if (m_hashAnimation.TryGetValue(_eEntityState, out var refAnim) == false)
            return;

        UpdateAnimation(refAnim);

        await UniTask.Yield(); // 트리거가 실제 상태 전환에 반영될 때까지 한 프레임 대기
        await UniTask.WaitUntil(() => m_refAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
    }

    private void UpdateAnimationTrigger(AnimationNode _refAnimNode)
    {
        m_refAnimator.SetTrigger(_refAnimNode.ParamName);
    }
    private void UpdateAnimationBool(AnimationNode _refAnimNode)
    {
        m_refAnimator.SetBool(_refAnimNode.ParamName, _refAnimNode.Bool);
    }
    private void UpdateAnimationInt(AnimationNode _refAnimNode)
    {
        m_refAnimator.SetInteger(_refAnimNode.ParamName, _refAnimNode.Int);
    }
    private void UpdateAnimationFloat(AnimationNode _refAnimNode)
    {
        m_refAnimator.SetFloat(_refAnimNode.ParamName, _refAnimNode.Float);
    }
    
    private AnimationNode FindAnimNode(eEntityState _eState)
    {
        if (m_hashAnimation.TryGetValue(_eState, out AnimationNode tNode))
            return tNode;
        return null;
    }



}
