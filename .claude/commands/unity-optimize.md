---
name: unity-optimize
description: "성능을 프로파일링하고 최적화합니다 — MCP 프로파일러를 사용해 프레임 타이밍, 메모리, 렌더링 통계를 확인합니다. 병목 지점을 식별하고 수정 사항을 적용합니다."
user-invocable: true
args: focus_area
---

# /unity-optimize — 성능 최적화

프로젝트의 성능을 최적화합니다. 집중 영역: **$ARGUMENTS**

## 워크플로우

`unity-optimizer` 에이전트를 사용하여 다음을 수행합니다:

### 1단계: 프로파일링
```
manage_profiler → start session, capture frame timing
manage_graphics → get rendering stats (draw calls, batches, triangles)
manage_profiler → memory snapshot
```

### 2단계: 병목 유형 식별
- **CPU 바운드** — GC 할당, 비용이 큰 Update 루프, 물리 연산
- **GPU 바운드** — 지나치게 많은 드로우콜, 오버드로우, 복잡한 셰이더
- **메모리** — 큰 텍스처, 비압축 오디오, 누수된 Addressables

### 3단계: 코드 스캔
`.claude/scripts/validate-code-quality.sh`를 실행하여 다음을 찾습니다:
- Update 안의 GetComponent
- 캐싱되지 않은 Camera.main
- 게임플레이 코드 안의 LINQ
- 핫 패스(hot path) 안의 할당

### 4단계: 수정
프로파일링 데이터를 기반으로 targeted fix(목표를 명확히 한 수정)를 적용합니다.

### 5단계: 검증
개선을 확인하기 위해 다시 프로파일링합니다. 전후 지표를 비교합니다.

## 성능 예산 (모바일)
| Metric | Target |
|--------|--------|
| Draw calls | < 100 |
| Frame time | < 33ms (30fps) |
| GC alloc/frame | 0 bytes |
| Texture memory | < 150MB |
