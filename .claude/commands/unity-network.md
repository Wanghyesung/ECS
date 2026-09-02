---
name: unity-network
description: "멀티플레이어 네트워킹을 설정합니다 — 네트워크 스크립트를 작성하고 MCP를 통해 NetworkManager를 구성합니다. Netcode, Mirror, Photon, Fish-Net을 지원합니다."
user-invocable: true
args: framework_and_feature
---

# /unity-network — 멀티플레이어 설정

다음을 기반으로 네트워킹을 구현합니다: **$ARGUMENTS**

## 워크플로우

`unity-network-dev` 에이전트를 사용하여 다음을 수행합니다:

1. **프레임워크 감지** — `Packages/manifest.json`에서 Netcode/Mirror/Photon/Fish-Net을 확인합니다
2. **기능 계획** — 무엇을 동기화해야 하는지 식별합니다 (transform, state, RPC)
3. **네트워킹 스크립트 작성:**
   - NetworkBehaviour 컴포넌트
   - 동기화된 상태를 위한 NetworkVariable
   - 액션을 위한 ServerRpc/ClientRpc
   - 소유권 확인 (`if (!IsOwner) return`)
4. MCP를 통해 **씬을 설정합니다:**
   - NetworkManager GameObject
   - Transport 구성
   - 플레이어 스폰 지점
   - 네트워크 프리팹 등록
5. `read_console`을 통해 **검증합니다**

## 핵심 규칙
- 서버가 권한(authority)을 가집니다 — 클라이언트를 절대 신뢰하지 마십시오
- RPC 사용을 최소화하십시오 — 지속적인 상태에는 NetworkVariable을 사용하십시오
- 입력을 처리하기 전에 항상 소유권을 확인하십시오
- 모든 네트워크 프리팹을 NetworkManager에 등록하십시오
