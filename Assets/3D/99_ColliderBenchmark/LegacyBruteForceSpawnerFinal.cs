using UnityEngine;

/*///////////////////////////////////////////
           LegacyBruteForceSpawnerFinal
목적 : LegacyBruteForceColliderManagerFinal(챕터 4: 4fcbb13+284bec5 통합, 실제
       BattleScene과 동일한 최종 구조) 위에서 부하 조건을 만드는 스포너. 절차/
       기본값은 이전 챕터들과 동일하고 컴포넌트 타입만 Final 버전이다.
       GridTestScene3 전용.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceSpawnerFinal : MonoBehaviour
{
    private int m_iBulletLayer = -1;
    private int m_iObstacleLayer = -1;
    private int m_iMonsterLayer = -1;

    [Header("개수/배치")]
    [SerializeField] private int m_iBulletCount = 3500;
    [SerializeField] private int m_iObstacleCount = 140;
    [SerializeField] private int m_iMonsterCount = 20;
    [SerializeField] private float m_fBoundsRadius = 300f;
    [SerializeField] private float m_fBulletSpeed = 20f;
    [SerializeField] private float m_fBulletRadius = 0.3f;
    [SerializeField] private float m_fMonsterRadius = 1.5f;
    [SerializeField] private Vector2 m_vObstacleHalfExtentRange = new Vector2(1f, 4f);
    [SerializeField] private int m_iRandomSeed = 12345;

    [Header("비주얼 (끄면 드로우콜 0 - 순수 판정 비용만 측정)")]
    [SerializeField] private bool m_bShowVisuals = true;
    [SerializeField] private Color m_tBulletColor = new Color(0.35f, 0.85f, 1f);
    [SerializeField] private Color m_tObstacleColor = new Color(0.5f, 0.46f, 0.42f);
    [SerializeField] private Color m_tMonsterColor = new Color(1f, 0.32f, 0.22f);

    private Transform m_refRoot;
    private int m_iActiveColliderCount;
    private Material m_matBullet;
    private Material m_matObstacle;
    private Material m_matMonster;

    private void Start()
    {
        EnsureManager();
        EnsureRoot();
        Spawn();
    }

    private void EnsureManager()
    {
        if (m_iBulletLayer < 0)
        {
            m_iBulletLayer = LayerMask.NameToLayer("PhysXBulletDemo");
            m_iObstacleLayer = LayerMask.NameToLayer("PhysXBenchmark");
            m_iMonsterLayer = LayerMask.NameToLayer("PhysXMonsterDemo");
        }

        if (LegacyBruteForceColliderManagerFinal.Instance == null)
        {
            GameObject go = new GameObject("LegacyBruteForceColliderManagerFinal");
            go.AddComponent<LegacyBruteForceColliderManagerFinal>();
        }
        LegacyBruteForceColliderManagerFinal.Instance.ConfigureLayerMatrix(m_iBulletLayer, m_iObstacleLayer, m_iMonsterLayer);
    }

    private void EnsureRoot()
    {
        if (m_refRoot == null)
        {
            m_refRoot = new GameObject("BruteForce_Root").transform;
            m_refRoot.SetParent(transform, false);
        }
    }

    private void EnsureMaterials()
    {
        if (m_matBullet != null)
            return;

        Shader tShader = Shader.Find("Universal Render Pipeline/Unlit");
        m_matBullet = new Material(tShader) { color = m_tBulletColor };
        m_matObstacle = new Material(tShader) { color = m_tObstacleColor };
        m_matMonster = new Material(tShader) { color = m_tMonsterColor };
    }

    private void AttachVisual(Transform _refParent, PrimitiveType _eType, Vector3 _vLocalScale, Material _refSharedMat)
    {
        GameObject go = GameObject.CreatePrimitive(_eType);
        go.name = "Visual";
        go.transform.SetParent(_refParent, false);
        go.transform.localScale = _vLocalScale;
        DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = _refSharedMat;
    }

    [ContextMenu("Spawn")]
    private void Spawn()
    {
        EnsureManager();
        EnsureRoot();
        EnsureMaterials();

        System.Random rnd = new System.Random(m_iRandomSeed);

        Transform obstacleRoot = new GameObject("Obstacles").transform;
        obstacleRoot.SetParent(m_refRoot, false);
        for (int i = 0; i < m_iObstacleCount; ++i)
        {
            Vector3 vPos = RandomPointInSphere(rnd, m_fBoundsRadius);
            Quaternion tRot = RandomRotation(rnd);
            Vector3 vHalfExtent = RandomHalfExtent(rnd);

            GameObject go = new GameObject("Obstacle_" + i);
            go.SetActive(false);
            go.layer = m_iObstacleLayer;
            go.transform.SetParent(obstacleRoot, false);
            go.transform.position = vPos;
            go.transform.rotation = tRot;

            LegacyBruteForceBoxColliderFinal refCollider = go.AddComponent<LegacyBruteForceBoxColliderFinal>();
            refCollider.SetHalfExtent(vHalfExtent);

            if (m_bShowVisuals)
                AttachVisual(go.transform, PrimitiveType.Cube, vHalfExtent * 2f, m_matObstacle);

            go.SetActive(true);
        }

        Transform monsterRoot = new GameObject("Monsters").transform;
        monsterRoot.SetParent(m_refRoot, false);
        for (int i = 0; i < m_iMonsterCount; ++i)
        {
            Vector3 vPos = RandomPointInSphere(rnd, m_fBoundsRadius);

            GameObject go = new GameObject("Monster_" + i);
            go.SetActive(false);
            go.layer = m_iMonsterLayer;
            go.transform.SetParent(monsterRoot, false);
            go.transform.position = vPos;

            LegacyBruteForceCircleColliderFinal refCollider = go.AddComponent<LegacyBruteForceCircleColliderFinal>();
            refCollider.SetRadius(m_fMonsterRadius);

            if (m_bShowVisuals)
                AttachVisual(go.transform, PrimitiveType.Sphere, Vector3.one * (m_fMonsterRadius * 2f), m_matMonster);

            go.SetActive(true);
        }

        Transform bulletRoot = new GameObject("Bullets").transform;
        bulletRoot.SetParent(m_refRoot, false);
        for (int i = 0; i < m_iBulletCount; ++i)
        {
            Vector3 vPos = RandomPointInSphere(rnd, m_fBoundsRadius);
            Vector3 vDir = RandomDirection(rnd);

            GameObject go = new GameObject("Bullet_" + i);
            go.SetActive(false);
            go.layer = m_iBulletLayer;
            go.transform.SetParent(bulletRoot, false);
            go.transform.position = vPos;

            LegacyBruteForceCircleColliderFinal refCollider = go.AddComponent<LegacyBruteForceCircleColliderFinal>();
            refCollider.SetRadius(m_fBulletRadius);

            LegacyBruteForceMover refMover = go.AddComponent<LegacyBruteForceMover>();
            refMover.Init(vDir * m_fBulletSpeed, Vector3.zero, m_fBoundsRadius);

            if (m_bShowVisuals)
                AttachVisual(go.transform, PrimitiveType.Sphere, Vector3.one * (m_fBulletRadius * 2f), m_matBullet);

            go.SetActive(true);
        }

        m_iActiveColliderCount = m_iObstacleCount + m_iMonsterCount + m_iBulletCount;
    }

    [ContextMenu("Clear")]
    private void Clear()
    {
        EnsureRoot();
        for (int i = m_refRoot.childCount - 1; i >= 0; --i)
            DestroyImmediate(m_refRoot.GetChild(i).gameObject);
        m_iActiveColliderCount = 0;
    }

    private Vector3 RandomPointInSphere(System.Random _rnd, float _fRadius)
    {
        for (int i = 0; i < 1000; ++i)
        {
            float x = (float)(_rnd.NextDouble() * 2 - 1) * _fRadius;
            float y = (float)(_rnd.NextDouble() * 2 - 1) * _fRadius;
            float z = (float)(_rnd.NextDouble() * 2 - 1) * _fRadius;
            if (x * x + y * y + z * z <= _fRadius * _fRadius)
                return new Vector3(x, y, z);
        }
        return Vector3.zero;
    }

    private Quaternion RandomRotation(System.Random _rnd)
    {
        return Quaternion.Euler(
            (float)_rnd.NextDouble() * 360f,
            (float)_rnd.NextDouble() * 360f,
            (float)_rnd.NextDouble() * 360f);
    }

    private Vector3 RandomHalfExtent(System.Random _rnd)
    {
        return new Vector3(
            Mathf.Lerp(m_vObstacleHalfExtentRange.x, m_vObstacleHalfExtentRange.y, (float)_rnd.NextDouble()),
            Mathf.Lerp(m_vObstacleHalfExtentRange.x, m_vObstacleHalfExtentRange.y, (float)_rnd.NextDouble()),
            Mathf.Lerp(m_vObstacleHalfExtentRange.x, m_vObstacleHalfExtentRange.y, (float)_rnd.NextDouble()));
    }

    private Vector3 RandomDirection(System.Random _rnd)
    {
        Vector3 v = new Vector3(
            (float)(_rnd.NextDouble() * 2 - 1),
            (float)(_rnd.NextDouble() * 2 - 1),
            (float)(_rnd.NextDouble() * 2 - 1));
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 340, 60), "");
        GUI.Label(new Rect(20, 15, 320, 20), "Legacy BruteForce Final (TransformAccessArray + 실행순서 분리)");
        GUI.Label(new Rect(20, 35, 320, 20), "총알 " + m_iBulletCount + " x Box " + m_iObstacleCount
            + " x 몬스터 " + m_iMonsterCount + "  콜라이더 합계 " + m_iActiveColliderCount);
    }
}
