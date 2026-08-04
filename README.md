# LLMConversationRuntime

여러 LLM·사람·시스템 참여자가 같은 세션에서 자율 또는 장면 지향 대화를 나누도록 조율하는 공급자 중립 Unity Package Manager(UPM) 런타임입니다.

## 설계 계약

### Intent

Codex·Claude·LM Studio 같은 서로 다른 공급자와 플레이어가 사람의 복사·붙여넣기 없이 발화·침묵·호출·퇴장·종료를 주고받게 합니다. 독립 실험 프로젝트에서는 대화 품질과 한계를 관찰하고, 게임 프로젝트에서는 허용된 장면 문맥만 전달하는 역할놀이 코어로 사용합니다.

### Architecture

```text
Provider·Human UI·System event
        ↓ ParticipantAction
ConversationRuntime
├─ Session·Participant
├─ SceneContract projection
├─ turn·recipient·lifecycle validation
└─ Event·Snapshot
        ↓
Consumer authority
├─ LLMConversationLab visualization
└─ StoryLLMMaster game validation
```

Core는 Provider 호출, MCP transport, 게임 상태를 참조하지 않습니다. 소비 프로젝트가 현재 `TurnOpportunity`를 Provider 또는 사람 UI에 전달하고 반환된 `ParticipantAction`을 Runtime에 제출합니다.

### 경계

- `GameRuntimeMcpHost`의 localhost·token·MCP stdio 책임을 복제하지 않습니다.
- LLM 응답만으로 지갑·인벤토리·퀘스트·세계 상태를 변경하지 않습니다.
- StoryLLMMaster·FateWeaver·특정 LLM SDK를 Core에서 참조하지 않습니다.
- 첫 버전은 메모리 세션과 스냅샷을 소유하며 영속 저장소는 소비 Adapter 뒤로 미룹니다.
- 자유 텍스트의 의미를 Core가 추측해 게임 명령으로 실행하지 않습니다.

## 소비 구조

```text
Modules/LLMConversationRuntime
├─ Projects/LLMConversationLab
└─ Projects/StoryLLMMaster
```

## Python 원형과의 관계

초기 Python 원형은 공급자 중립 대화 계약과 Runtime MCP 연결을 검증했습니다. 본 UPM이 Unity 런타임 정본이며, 원형의 회귀 사례는 UPM과 `LLMConversationLab` 테스트로 이관했습니다. 같은 대화 상태 머신을 두 곳에서 독립 확장하지 않습니다.

## Spec 판정

```text
Spec 적용: 적용
판정: SPEC-PASS
근거 정본: README.md#설계-계약
누락·회수 조건: 없음
```
