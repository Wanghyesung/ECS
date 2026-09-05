---
name: unity-profile
description: "심층 프로파일링 세션 — MCP를 통해 프레임을 캡처하고, CPU/GPU 타이밍과 메모리 스냅샷, 렌더링 통계를 분석하여 최적화 권장 사항을 제공합니다."
user-invocable: true
args: focus
---

# /unity-profile — 심층 프로파일링 세션

심층 프로파일링 세션을 실행합니다. 초점: **$ARGUMENTS**

## 워크플로우

`unity-optimizer` 에이전트를 사용하여 다음을 수행합니다.

### 1단계: 캡처
```
manage_profiler action:"start_session"     → 기록 시작
manage_profiler action:"get_frame_timing"  → CPU 및 GPU 프레임 시간
manage_profiler action:"get_counters"      → 특정 성능 카운터
manage_profiler action:"memory_snapshot"   → 상세 메모리 분석
manage_graphics action:"get_rendering_stats" → 드로우 콜, 배치, 삼각형, 세트 패스
manage_physics  action:"get_stats"         → 물리 스텝 시간, 접촉, 바디
```

### 2단계: 분석

프로파일 보고서를 제시합니다.

**프레임 타이밍:**
- CPU 프레임 시간: Xms (목표: 60fps 기준 <16.6ms)
- GPU 프레임 시간: Xms
- 병목 지점: CPU / GPU / 균형 상태

**렌더링:**
- 드로우 콜: X (예산: 모바일 <100, 데스크톱 <2000)
- 배치: X (SRP 배처 효율성)
- 삼각형: X
- 세트 패스 콜: X

**메모리:**
- 총합: X MB
- 텍스처: X MB
- 메시: X MB
- 오디오: X MB
- 스크립트: X MB

**물리:**
- 물리 스텝 시간: X ms
- 활성 리지드바디: X
- 접촉: X

### 3단계: 권장

영향도 순으로 정렬된 구체적이고 실행 가능한 최적화 권장 사항을 제공합니다.
1. 가장 영향이 큰 수정
2. 두 번째로 큰 영향
3. ...

각 권장 사항에는 무엇을 변경할지, 왜 변경해야 하는지, 예상되는 개선 효과가 포함됩니다.
