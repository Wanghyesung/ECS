---
name: unity-init
description: "Unity 프로젝트를 스캔하여 감지된 구성, 패키지, 렌더 파이프라인, 추천 스킬이 포함된 맞춤형 CLAUDE.md를 생성합니다."
user-invocable: true
---

# /unity-init — 프로젝트 설정

이 Unity 프로젝트를 스캔하여 맞춤형 CLAUDE.md 구성을 생성합니다.

## 단계

1. MCP `project_info` 리소스를 통해 **프로젝트 정보를 읽어** Unity 버전, 플랫폼, 상태를 가져옵니다.

2. **Packages/manifest.json을 스캔**하여 설치된 패키지를 감지합니다:
   - 렌더 파이프라인(URP, HDRP, 또는 Built-in)
   - 입력 시스템, Addressables, Cinemachine, Timeline, TextMeshPro
   - 네트워킹(Netcode, Mirror, Photon, Fish-Net)
   - 서드파티(DOTween, UniTask, VContainer, Zenject, Odin)

3. **어셈블리 정의 스캔**(`.asmdef` 파일) — 프로젝트의 어셈블리 구조를 매핑합니다.

4. **씬 스캔** — `Assets/`에 있는 모든 `.unity` 파일을 나열합니다.

5. **기존 CLAUDE.md 확인** — 이미 존재하는 경우, 사용자의 커스터마이징을 보존합니다.

6. 다음을 포함한 **CLAUDE.md 생성**:
   - 프로젝트 개요(Unity 버전, 렌더 파이프라인, 감지된 패키지)
   - 어셈블리 구조
   - 씬 목록
   - 규칙 파일 참조(`.claude/rules/*.md`)
   - 감지된 패키지에 기반한 추천 스킬
   - MCP 통합 참고 사항
   - 주요 컨벤션 요약

7. 감지 및 구성된 내용을 **보고**합니다. 다음 단계를 제안합니다:
   - 생성된 CLAUDE.md를 검토하고 커스터마이징
   - unity-mcp가 아직 설치되지 않은 경우 설치
   - 전체 프로젝트 상태 점검을 위해 `/unity-audit` 시도

## 출력

감지된 내용과 추천된 스킬을 명확한 요약 표로 제시합니다.
</content>
