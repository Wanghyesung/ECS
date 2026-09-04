using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
          LegacyBenchmarkSpawner
목적 : 커밋 fdf901d 시점의 실제 Bullet.cs 이동/충돌 메커니즘(Rigidbody(kinematic)
       + FixedUpdate MovePosition + TriggerEnterObject)을 그대로 쓰는 PhysX 벤치마크.
       장애물(BoxCollider, 정적) + 몬스터(SphereCollider, 정적) + 탄(LegacyPhysXBullet)
       풀을 만들고 LegacyBulletFireController가 꾸준히 순환 발사한다.

       ColliderManager/CircleCollider/ObbCollider(자체 시스템) 쪽은 이번 벤치마크에서
       아예 안 씀 - PhysX 실측만 순수하게 본다. m_iMonsterCount 기본값은 0 - 기존
       ColliderBenchmarkDemo.unity(장애물/탄만 씀)에 영향을 주지 않기 위함이고,
       몬스터가 필요한 씬(PXTestScene 등)에서만 인스펙터로 켠다.
 *///////////////////////////////////////////
public class LegacyBenchmarkSpawner : MonoBehaviour
{
    [Header("개수/배치")]
    [SerializeField] private int m_iBulletTargetActive = 1000;
    [SerializeField] private int m_iObstacleCount = 100;
    [SerializeField] private int m_iMonsterCount = 0;
    [SerializeField] private float m_fBoundsRadius = 100f;
    [SerializeField] private float m_fBulletSpeed = 5f;
    [SerializeField] private float m_fBulletRadius = 0.3f;
    [SerializeField] private float m_fBulletLifetime = 3f;
    [SerializeField] private float m_fMonsterRadius = 1.5f;
    [SerializeField] private Vector2 m_vObstacleHalfExtentRange = new Vector2(1f, 3f);
    [SerializeField] private int m_iRandomSeed = 12345;

    private Transform m_refRoot;

    private void Start()
    {
        EnsureRoot();
        Spawn();
    }

    private void EnsureRoot()
    {
        if (m_refRoot == null)
        {
            m_refRoot = new GameObject("Legacy_Root").transform;
            m_refRoot.SetParent(transform, false);
        }
    }

    [ContextMenu("Spawn")]
    private void Spawn()
    {
        EnsureRoot();

        // Default 레이어끼리 물리 충돌이 프로젝트 전역에서 꺼져 있어서(자체 콜라이더 시스템으로
        // 완전히 넘어가며 꺼둔 것으로 보임) 벤치마크 전용 레이어 둘을 새로 만들어 런타임에만 켠다.
        // 장애물/탄을 분리하는 이유는 탄끼리 서로 트리거를 쏘는 걸 막기 위함(자체 시스템도
        // PlayerAttack끼리는 검사 안 하므로 조건을 맞춤)
        int obstacleLayer = LayerMask.NameToLayer("PhysXBenchmark");
        int bulletLayer = LayerMask.NameToLayer("PhysXBulletDemo");
        int monsterLayer = LayerMask.NameToLayer("PhysXMonsterDemo");
        Physics.IgnoreLayerCollision(bulletLayer, obstacleLayer, false);
        Physics.IgnoreLayerCollision(bulletLayer, bulletLayer, true);
        Physics.IgnoreLayerCollision(obstacleLayer, obstacleLayer, true);
        LayerMask hitMask = 1 << obstacleLayer;
        if (m_iMonsterCount > 0 && monsterLayer >= 0)
        {
            Physics.IgnoreLayerCollision(bulletLayer, monsterLayer, false);
            Physics.IgnoreLayerCollision(monsterLayer, monsterLayer, true);
            Physics.IgnoreLayerCollision(monsterLayer, obstacleLayer, true);
            hitMask |= 1 << monsterLayer;
        }

        System.Random rnd = new System.Random(m_iRandomSeed);
        Color tColor = new Color(1f, 0.6f, 0.15f);
        Color tMonsterColor = new Color(1f, 0.25f, 0.2f);

        Transform obstacleRoot = new GameObject("Obstacles").transform;
        obstacleRoot.SetParent(m_refRoot, false);
        for (int i = 0; i < m_iObstacleCount; ++i)
        {
            Vector3 vPos = RandomPointInSphere(rnd, m_fBoundsRadius);
            Quaternion tRot = RandomRotation(rnd);
            Vector3 vHalfExtent = RandomHalfExtent(rnd);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Obstacle_" + i;
            go.transform.SetParent(obstacleRoot, false);
            go.transform.position = vPos;
            go.transform.rotation = tRot;
            go.transform.localScale = vHalfExtent * 2f;
            go.layer = obstacleLayer;
            SetColor(go, tColor);
            // CreatePrimitive가 붙여준 BoxCollider를 그대로 사용(원본 Bullet 프리팹도 BoxCollider)
        }

        if (m_iMonsterCount > 0 && monsterLayer >= 0)
        {
            Transform monsterRoot = new GameObject("Monsters").transform;
            monsterRoot.SetParent(m_refRoot, false);
            for (int i = 0; i < m_iMonsterCount; ++i)
            {
                Vector3 vPos = RandomPointInSphere(rnd, m_fBoundsRadius);

                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Monster_" + i;
                go.transform.SetParent(monsterRoot, false);
                go.transform.position = vPos;
                go.transform.localScale = Vector3.one * (m_fMonsterRadius * 2f);
                go.layer = monsterLayer;
                SetColor(go, tMonsterColor);
                // CreatePrimitive가 붙여준 SphereCollider를 그대로 사용 - Rigidbody는 필요 없음
                // (트리거 쌍 중 총알 쪽이 이미 kinematic Rigidbody를 갖고 있어서 그걸로 충분함)
            }
        }

        Transform bulletRoot = new GameObject("Bullets").transform;
        bulletRoot.SetParent(m_refRoot, false);
        List<GameObject> pool = new List<GameObject>(m_iBulletTargetActive);
        for (int i = 0; i < m_iBulletTargetActive; ++i)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Bullet_" + i;
            go.transform.SetParent(bulletRoot, false);
            go.transform.localScale = Vector3.one * (m_fBulletRadius * 2f);
            go.layer = bulletLayer;
            SetColor(go, tColor);

            // 원본 프리팹처럼 BoxCollider(isTrigger)로 교체 - 캡슐 메시는 시각적 모양 전용
            DestroyImmediate(go.GetComponent<Collider>());
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one * (m_fBulletRadius * 2f);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            go.AddComponent<TriggerEnterObject>();
            go.AddComponent<LegacyPhysXBullet>();
            go.SetActive(false);
            pool.Add(go);
        }

        LegacyBulletFireController fireCtrl = bulletRoot.gameObject.AddComponent<LegacyBulletFireController>();
        fireCtrl.Init(pool, Vector3.zero, m_fBoundsRadius, m_fBulletSpeed, m_fBulletLifetime, m_iBulletTargetActive, hitMask);

        LegacyPhysXBullet.ResetCounter();
    }

    [ContextMenu("Clear")]
    private void Clear()
    {
        EnsureRoot();
        for (int i = m_refRoot.childCount - 1; i >= 0; --i)
            DestroyImmediate(m_refRoot.GetChild(i).gameObject);
    }

    private void SetColor(GameObject _refGo, Color _tColor)
    {
        Renderer refRenderer = _refGo.GetComponent<Renderer>();
        if (refRenderer != null)
            refRenderer.material.color = _tColor;
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

    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 340, 60), "");
        GUI.Label(new Rect(20, 15, 320, 20), "Legacy PhysX (Rigidbody+TriggerEnterObject)");
        GUI.Label(new Rect(20, 35, 320, 20), "탄 목표 " + m_iBulletTargetActive + " x 장애물 " + m_iObstacleCount
            + " x 몬스터 " + m_iMonsterCount + "  Enter=" + LegacyPhysXBullet.s_iEnterCount);
    }
}
