using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "SO_PoolData", menuName = "Game/Load/PoolData")]

public class SOPoolData : MonoBehaviour
{
    public PoolObject prefabRef;
    public int PreLoad = 8;
    public int MaX = 12;
}
