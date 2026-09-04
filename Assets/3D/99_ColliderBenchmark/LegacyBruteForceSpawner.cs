using UnityEngine;

/*///////////////////////////////////////////
           LegacyBruteForceSpawner
목적 : LegacyBruteForceColliderManager(421d1b0 브루트포스 포팅) 위에서 실제 부하
       조건(총알 다수 × Box 장애물 × 몬스터)을 만들어 스트레스 테스트하는 스포너.
       LegacyBenchmarkSpawner(PhysX 비교용)와 동일한 절차적 생성 패턴을 따른다 -
       프리팹 없이 Start()에서 전부 코드로 만든다.

       세션 2 재측정(Docs/Collider.md §7, LateUpdate 8.82ms) 부하로 기본값을
       맞췄다 - 총알 수만 인스펙터에서 바로 올려서(3500→4000 등) 재측정 가능.

       m_bShowVisuals가 켜져 있으면 타입별 프리미티브(구/박스)에 공유 Unlit
       머티리얼 하나씩만 물려서(타입당 1장 - 오브젝트마다 renderer.material을
       읽지 않으므로 복제/배칭 깨짐 없음) BattleScene과 비슷하게 눈으로 보이게
       한다. 순수 판정 비용만 재려면 꺼서 드로우콜을 완전히 제거할 것.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceSpawner : MonoBehaviour
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

        if (LegacyBruteForceColliderManager.Instance == null)
        {
            GameObject go = new GameObject("LegacyBruteForceColliderManager");
            go.AddComponent<LegacyBruteForceColliderManager>();
        }
        LegacyBruteForceColliderManager.Instance.ConfigureLayerMatrix(m_iBulletLayer, m_iObstacleLayer, m_iMonsterLayer);
    }

    private void EnsureRoot()
    {
        if (m_refRoot == null)
        {
            m_refRoot = new GameObject("BruteForce_Root").transform;
            m_refRoot.SetParent(transform, false);
        }
    }

    // 타입당 공유 머티리얼 1장씩만 생성 - 인스턴스마다 renderer.material을 건드리지
    // 않으므로(AttachVisual은 sharedMaterial만 씀) 오브젝트 수천 개가 전부 같은
    // 배칭 그룹으로 묶인다
    private void EnsureMaterials()
    {
        if (m_matBullet != null)
            return;

        Shader tShader = Shader.Find("Universal Render Pipeline/Unlit");
        m_matBullet = new Material(tShader) { color = m_tBulletColor };
        m_matObstacle = new Material(tShader) { color = m_tObstacleColor };
        m_matMonster = new Material(tShader) { color = m_tMonsterColor };
    }

    // 판정용 콜라이더 오브젝트(go)는 그대로 두고, 순수 표시 전용 자식 하나만 붙인다 -
    // 콜라이더 판정 로직과 렌더링을 분리해서 비주얼을 꺼도 판정 코드는 안 건드림.
    // 프리미티브가 기본으로 들고 오는 PhysX Collider는 즉시 제거(자체 판정 시스템과
    // 무관한 낭비이자, 두 개의 콜라이더 시스템이 같은 오브젝트에 섞이는 걸 방지)
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
            go.SetActive(false); // OnEnable(Activate)이 컴포넌트 붙는 도중 먼저 도는 걸 막기 위함
            go.layer = m_iObstacleLayer; // AddComponent(Awake) 전에 반드시 먼저 설정 - Awake가 gameObject.layer를 읽는다
            go.transform.SetParent(obstacleRoot, false);
            go.transform.position = vPos;
            go.transform.rotation = tRot;

            LegacyBruteForceBoxCollider refCollider = go.AddComponent<LegacyBruteForceBoxCollider>();
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

            LegacyBruteForceCircleCollider refCollider = go.AddComponent<LegacyBruteForceCircleCollider>();
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

            LegacyBruteForceCircleCollider refCollider = go.AddComponent<LegacyBruteForceCircleCollider>();
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
        GUI.Label(new Rect(20, 15, 320, 20), "Legacy BruteForce (No Grid, No Job)");
        GUI.Label(new Rect(20, 35, 320, 20), "총알 " + m_iBulletCount + " x Box " + m_iObstacleCount
            + " x 몬스터 " + m_iMonsterCount + "  콜라이더 합계 " + m_iActiveColliderCount);
    }
}
