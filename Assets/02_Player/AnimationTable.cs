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

public class AnimationTable : MonoBehaviour
{
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
       
        //TODO : case가 많아지면 Dictionary<eState, Action<AnimationNode>로 연결하기
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
