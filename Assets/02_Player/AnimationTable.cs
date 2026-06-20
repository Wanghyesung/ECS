using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public struct AnimationNode
{
    public eEntityState State;
    public string AnimationName;
}

public class AnimationTable : MonoBehaviour
{

    [SerializeField] private List<AnimationNode> m_listAnimationList = new();
    private Dictionary<eEntityState, AnimationNode> m_hashDictionary = new();


    public void ChangeAnimation(eEntityState _eState)
    {

    }


    private AnimationNode 
    public void UpdateAnimationBool()
    {

    }

    public void UpdateAnimationTrigger()
    {

    }
}
