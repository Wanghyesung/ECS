using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_ObjectInfo", menuName = "Game/ObjectInfo")]
public class SOObjectInfo : ScriptableObject
{
    [Header("Identity")]
    public string ObjectName = "New Monster";

    [TextArea]
    public string Description;
    
    [Header("Stats")]
    public int MaxHP = 10;
    public float MaxSpeed = 3f;
}
