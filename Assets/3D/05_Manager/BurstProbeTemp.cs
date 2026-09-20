using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/*///////////////////////////////////////////
              BurstProbeTemp
목적 : (임시/폐기 예정) ColliderManager.IsCircleBoxOverlap가 이 프로젝트의 Burst
       버전에서 그대로 컴파일되는지 확인하기 위한 일회성 프로브.
       Mathf.Clamp / Mathf.FloorToInt / Vector3 연산 / NativeQueue.ParallelWriter까지
       실제 Job이 쓸 구성요소를 전부 한 번에 태워본다. 확인 후 삭제한다.
 *///////////////////////////////////////////
public static class BurstProbeTemp
{
    public struct tProbeResult
    {
        public int CircleId;
        public int BoxId;
        public bool Overlap;
    }

    [BurstCompile]
    private struct ProbeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> OtherCenter;
        [ReadOnly] public NativeArray<float> OtherRadius;
        [ReadOnly] public NativeArray<bool> OtherHasPair;

        public Vector3 GridOrigin;
        public float CellSize;
        public int CountX;
        public int CountY;
        public int CountZ;

        public NativeQueue<tProbeResult>.ParallelWriter Output;

        public void Execute(int index)
        {
            Vector3 vCenter = OtherCenter[index];
            float fRadius = OtherRadius[index];

            // 그리드 좌표 계산에 쓰는 Mathf.FloorToInt / Mathf.Clamp(int) 경로
            Vector3 vLocal = vCenter - GridOrigin;
            int iX = Mathf.Clamp(Mathf.FloorToInt(vLocal.x / CellSize), 0, CountX - 1);
            int iY = Mathf.Clamp(Mathf.FloorToInt(vLocal.y / CellSize), 0, CountY - 1);
            int iZ = Mathf.Clamp(Mathf.FloorToInt(vLocal.z / CellSize), 0, CountZ - 1);

            int iFlat = (iZ * CountY + iY) * CountX + iX;

            // 바운딩구 얼리컷(Vector3.sqrMagnitude)
            Vector3 vBoxCenter = new Vector3(iFlat * 0.001f, 0f, 0f);
            float fBoundSum = fRadius + 1f;
            if ((vCenter - vBoxCenter).sqrMagnitude > fBoundSum * fBoundSum)
                return;

            // 본체: Mathf.Clamp(float) + Vector3.Dot
            bool bOverlap = ColliderManager.IsCircleBoxOverlap(
                vCenter, fRadius,
                vBoxCenter, Vector3.right, Vector3.up, Vector3.forward, Vector3.one);

            if (bOverlap || OtherHasPair[index])
            {
                Output.Enqueue(new tProbeResult
                {
                    CircleId = index,
                    BoxId = iFlat,
                    Overlap = bOverlap
                });
            }
        }
    }

    public static string Run()
    {
        NativeArray<Vector3> arrCenter = new NativeArray<Vector3>(8, Allocator.TempJob);
        NativeArray<float> arrRadius = new NativeArray<float>(8, Allocator.TempJob);
        NativeArray<bool> arrHasPair = new NativeArray<bool>(8, Allocator.TempJob);
        NativeQueue<tProbeResult> queResult = new NativeQueue<tProbeResult>(Allocator.TempJob);

        for (int i = 0; i < 8; ++i)
        {
            arrCenter[i] = new Vector3(i * 0.1f, 0f, 0f);
            arrRadius[i] = 1f;
            arrHasPair[i] = (i % 2) == 0;
        }

        ProbeJob tJob = new ProbeJob
        {
            OtherCenter = arrCenter,
            OtherRadius = arrRadius,
            OtherHasPair = arrHasPair,
            GridOrigin = new Vector3(-10f, -10f, -10f),
            CellSize = 4f,
            CountX = 8,
            CountY = 8,
            CountZ = 8,
            Output = queResult.AsParallelWriter()
        };

        JobHandle tHandle = tJob.Schedule(8, 4);
        tHandle.Complete();

        int iCount = queResult.Count;

        arrCenter.Dispose();
        arrRadius.Dispose();
        arrHasPair.Dispose();
        queResult.Dispose();

        return "BurstProbeTemp OK, results=" + iCount;
    }
}
