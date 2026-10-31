using System;
using System.Reflection;
using UnityEngine;

internal static class GridBenchmarkSceneSetup
{
    internal static void VerifyHitResponses()
    {
        // Play 모드 없이 실제 콜라이더 이벤트 → 이동기 연결과 재발사만 확인한다.
        // 충돌 알고리즘 성능 측정이나 프레임 진행은 수행하지 않는다.
        Type[] arrTypes = { typeof(LegacyBruteForceCircleCollider), typeof(LegacyBruteForceCircleColliderJob),
            typeof(LegacyBruteForceCircleColliderUnified), typeof(LegacyBruteForceCircleColliderFinal),
            typeof(LegacyBruteForceCircleColliderBrute) };
        const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo refCount = typeof(BenchmarkBulletPresentation).GetField("m_iHitCount", FLAGS);
        MethodInfo refAwake = typeof(LegacyBruteForceMover).GetMethod("Awake", FLAGS);
        MethodInfo refUpdate = typeof(LegacyBruteForceMover).GetMethod("Update", FLAGS);
        for (int i = 0; i < arrTypes.Length; i++)
        {
            var refObject = new GameObject("BenchmarkHitResponseCheck");
            refObject.hideFlags = HideFlags.HideAndDontSave;
            refObject.SetActive(false);
            try
            {
                Component refCollider = refObject.AddComponent(arrTypes[i]);
                LegacyBruteForceMover refMover = refObject.AddComponent<LegacyBruteForceMover>();
                refAwake.Invoke(refMover, null);
                var refPresentation = new BenchmarkBulletPresentation();
                refMover.Init(Vector3.forward * 20f, Vector3.zero, 100f, new System.Random(12345), refPresentation);
                EventInfo refEvent = arrTypes[i].GetEvent("OnHitTargetEnter");
                Delegate refHandler = refCollider switch
                {
                    LegacyBruteForceCollider _ => new Action<LegacyBruteForceCollider>(refMover.OnHit),
                    LegacyBruteForceColliderJob _ => new Action<LegacyBruteForceColliderJob>(refMover.OnHit),
                    LegacyBruteForceColliderUnified _ => new Action<LegacyBruteForceColliderUnified>(refMover.OnHit),
                    LegacyBruteForceColliderFinal _ => new Action<LegacyBruteForceColliderFinal>(refMover.OnHit),
                    LegacyBruteForceColliderBrute _ => new Action<LegacyBruteForceColliderBrute>(refMover.OnHit),
                    _ => throw new InvalidOperationException("Unsupported benchmark collider.")
                };
                refEvent.AddEventHandler(refCollider, refHandler);
                MethodInfo refEnter = arrTypes[i].GetMethod("OnEnterCollider");
                object[] arrArguments = { refCollider };
                refEnter.Invoke(refCollider, arrArguments);
                refEnter.Invoke(refCollider, arrArguments);
                if ((int)refCount.GetValue(refPresentation) != 1)
                    throw new InvalidOperationException("Hit callback or duplicate guard failed: " + arrTypes[i].Name);
                if (refObject.transform.position != Vector3.zero)
                    throw new InvalidOperationException("Position changed during collision callback.");
                refUpdate.Invoke(refMover, null);
                float fDistance = refObject.transform.position.magnitude;
                if (fDistance <= 0f || fDistance > 100.001f)
                    throw new InvalidOperationException("Bullet did not recycle within bounds: " + arrTypes[i].Name);
                refEnter.Invoke(refCollider, arrArguments);
                if ((int)refCount.GetValue(refPresentation) != 2)
                    throw new InvalidOperationException("Recycled bullet cannot receive the next hit.");
                refEvent.RemoveEventHandler(refCollider, refHandler);
                Debug.Log("BENCHMARK_HIT_RESPONSE_PASS " + arrTypes[i].Name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(refObject);
            }
        }
    }

}
