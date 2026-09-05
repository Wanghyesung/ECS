using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/*///////////////////////////////////////////
           BoxColliderGridTests
목적 : BoxColliderGrid가 Shape 무관(Box든 Circle이든)하게 정상 빌드되는지 검증한다.
       Circle-Circle 브로드페이즈 통합 계획(§4 - ColliderGrid 일반화) 완료 기준.
 *///////////////////////////////////////////
public class BoxColliderGridTests
{
    private readonly List<GameObject> m_listSpawnedGameObject = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < m_listSpawnedGameObject.Count; ++i)
            Object.DestroyImmediate(m_listSpawnedGameObject[i]);

        m_listSpawnedGameObject.Clear();
    }

    // CircleCollider.m_fRadius는 [SerializeField] private + 기본값 0.5f - 테스트에서 리플렉션
    // 없이 손댈 수 없으므로 기본 반지름(0.5f)을 그대로 쓴다(그리드 빌드/셀 산정엔 값 자체보다
    // "BoundingRadius를 다형으로 읽어오는지"가 검증 포인트라 기본값으로 충분)
    private CircleCollider SpawnCircle(Vector3 _vPosition)
    {
        GameObject refGo = new GameObject("TestCircle");
        m_listSpawnedGameObject.Add(refGo);

        CircleCollider refCircle = refGo.AddComponent<CircleCollider>();
        refGo.transform.position = _vPosition;
        refCircle.RefreshCenter();

        return refCircle;
    }

    [Test]
    public void Build_Circle_리스트로도_정상_빌드된다()
    {
        List<BaseCollider> listCollider = new List<BaseCollider>
        {
            SpawnCircle(new Vector3(0f, 0f, 0f)),
            SpawnCircle(new Vector3(2f, 0f, 0f)),
            SpawnCircle(new Vector3(0f, 0f, 2f)),
        };

        BoxColliderGrid refGrid = new BoxColliderGrid();
        try
        {
            refGrid.Build(listCollider);

            Assert.IsTrue(refGrid.IsBuilt);
        }
        finally
        {
            refGrid.Dispose();
        }
    }

    [Test]
    public void Build_Circle_리스트로_빌드한_뒤_같은_셀에_들어간_콜라이더끼리_CellItems로_조회된다()
    {
        // 반지름 0.5(기본값) 두 개를 아주 가깝게(같은 셀에 들어갈 만큼) 배치
        List<BaseCollider> listCollider = new List<BaseCollider>
        {
            SpawnCircle(new Vector3(0f, 0f, 0f)),
            SpawnCircle(new Vector3(0.1f, 0f, 0f)),
        };

        BoxColliderGrid refGrid = new BoxColliderGrid();
        try
        {
            refGrid.Build(listCollider);
            refGrid.BeginRebuild(listCollider.Count);

            for (int i = 0; i < listCollider.Count; ++i)
                refGrid.AddCollider(i, listCollider[i].CachedCenter);

            refGrid.EndRebuild();

            BoxColliderGrid.ComputeCellCoord(listCollider[0].CachedCenter, refGrid.Origin, refGrid.CellSize,
                refGrid.CountX, refGrid.CountY, refGrid.CountZ, out int iX, out int iY, out int iZ);
            int iCell = BoxColliderGrid.FlattenIndex(iX, iY, iZ, refGrid.CountX, refGrid.CountY);

            Assert.AreEqual(2, refGrid.CellCount[iCell]);
        }
        finally
        {
            refGrid.Dispose();
        }
    }
}
