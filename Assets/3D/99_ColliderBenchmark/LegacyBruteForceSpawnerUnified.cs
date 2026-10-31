using UnityEngine;

/*///////////////////////////////////////////
                 LegacyBruteForceSpawnerUnified
목적 : 씬에 미리 배치된 매니저·몬스터·박스를 사용하고 총알만 1회 생성한다.
       각 단계의 기존 충돌 알고리즘과 명중 후 재사용 동작을 유지한다.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceSpawnerUnified : MonoBehaviour
{
    [Header("씬에 배치된 대상")]
    [SerializeField] private LegacyBruteForceColliderManagerUnified m_refColliderManager;
    [SerializeField] private LegacyBruteForceBoxColliderUnified[] m_arrBoxes;
    [SerializeField] private LegacyBruteForceCircleColliderUnified[] m_arrMonsters;
    [SerializeField] private BenchmarkMapRange m_refMapRange;
    [SerializeField] private Transform m_refBulletRoot;

    [Header("총알")]
    [SerializeField] private int m_iBulletCount = 4000;
    [SerializeField] private float m_fBulletSpeed = 20f;
    [SerializeField] private float m_fBulletRadius = 0.3f;
    [SerializeField] private int m_iRandomSeed = 12345;
    [SerializeField] private GameObject m_refBulletVisualPrefab;
    [SerializeField] private bool m_bShowVisuals = true;
    [SerializeField] private bool m_bShowColliderGizmos = true;
    private readonly BenchmarkBulletPresentation m_refPresentation = new BenchmarkBulletPresentation();

    private void Start()
    {
        m_refColliderManager.ConfigureLayerMatrix(LayerMask.NameToLayer("PhysXBulletDemo"),
            LayerMask.NameToLayer("PhysXBenchmark"), LayerMask.NameToLayer("PhysXMonsterDemo"));
        m_refPresentation.Initialize(m_refBulletVisualPrefab);
        var refRandom = new System.Random(m_iRandomSeed);
        Vector3 vCenter = m_refMapRange.Center;
        float fRadius = m_refMapRange.Radius;
        int iBulletLayer = LayerMask.NameToLayer("PhysXBulletDemo");
        for (int i = 0; i < m_iBulletCount; i++)
        {
            Vector3 vPosition = vCenter + RandomPointInSphere(refRandom, fRadius);
            Vector3 vDirection = RandomDirection(refRandom);
            var refBullet = new GameObject("Bullet_" + i);
            refBullet.SetActive(false);
            refBullet.layer = iBulletLayer;
            refBullet.transform.SetParent(m_refBulletRoot, false);
            refBullet.transform.SetPositionAndRotation(vPosition, Quaternion.LookRotation(vDirection));
            LegacyBruteForceCircleColliderUnified refCollider = refBullet.AddComponent<LegacyBruteForceCircleColliderUnified>();
            refCollider.SetRadius(m_fBulletRadius);
            LegacyBruteForceMover refMover = refBullet.AddComponent<LegacyBruteForceMover>();
            refMover.Init(vDirection * m_fBulletSpeed, vCenter, fRadius, refRandom, m_refPresentation);
            refCollider.OnHitTargetEnter += refMover.OnHit;
            m_refPresentation.AddSphere(refBullet.transform, m_fBulletRadius);
            if (m_bShowVisuals)
                m_refPresentation.AttachBullet(refBullet.transform);
            refBullet.SetActive(true);
        }
    }

    private static Vector3 RandomPointInSphere(System.Random _refRandom, float _fRadius)
    {
        for (int i = 0; i < 1000; i++)
        {
            Vector3 vPoint = RandomVector(_refRandom);
            if (vPoint.sqrMagnitude <= 1f)
                return vPoint * _fRadius;
        }
        return Vector3.zero;
    }

    private static Vector3 RandomDirection(System.Random _refRandom)
    {
        Vector3 vDirection = RandomVector(_refRandom);
        return vDirection.sqrMagnitude > 0.0001f ? vDirection.normalized : Vector3.forward;
    }

    private static Vector3 RandomVector(System.Random _refRandom)
        => new Vector3((float)(_refRandom.NextDouble() * 2.0 - 1.0),
            (float)(_refRandom.NextDouble() * 2.0 - 1.0), (float)(_refRandom.NextDouble() * 2.0 - 1.0));

    private void OnDrawGizmos()
    {
        if (m_bShowColliderGizmos == false)
            return;
        m_refPresentation.DrawGizmos();
        if (m_arrBoxes != null)
        {
            for (int i = 0; i < m_arrBoxes.Length; i++)
            {
                LegacyBruteForceBoxColliderUnified refBox = m_arrBoxes[i];
                if (refBox != null && refBox.gameObject.activeInHierarchy)
                    BenchmarkBulletPresentation.DrawSceneBox(refBox.transform, refBox.HalfExtent);
            }
        }
        if (m_arrMonsters != null)
        {
            for (int i = 0; i < m_arrMonsters.Length; i++)
            {
                LegacyBruteForceCircleColliderUnified refMonster = m_arrMonsters[i];
                if (refMonster != null && refMonster.gameObject.activeInHierarchy)
                    BenchmarkBulletPresentation.DrawSceneSphere(refMonster.transform, refMonster.Radius);
            }
        }
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 540, 75), "");
        GUI.Label(new Rect(20, 15, 520, 20), "Unified Grid + Burst Job");
        GUI.Label(new Rect(20, 35, 520, 20), "Bullets " + m_iBulletCount + " / Boxes " + m_arrBoxes.Length
            + " / Monsters " + m_arrMonsters.Length);
        m_refPresentation.DrawHitCounter();
    }
}
