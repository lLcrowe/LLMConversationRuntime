# LLMConversationRuntime

[English README](README.md)

여러 LLM·사람·시스템 참여자의 결정론적 대화를 조율하는 공급자 중립 Unity Package Manager(UPM) 런타임입니다.

```text
공급자 어댑터 / 사람 UI / 시스템 이벤트
                    ↓ ParticipantAction
ConversationRuntime
├─ 세션·참여자 상태
├─ 장면·비공개 문맥 투영
├─ 턴·수신자·생명주기 검증
├─ 품질·실험 평가
└─ 이벤트·스냅샷
                    ↓
소비자가 소유한 게임 권위와 표현 계층
```

이 패키지는 LLM API를 호출하거나 MCP 전송을 제공하거나 게임 상태를 직접 바꾸지 않습니다. 소비자가 행동을 공급하고, 수락된 제안을 게임에 어떻게 반영할지 결정합니다.

## 요구사항

- Unity 6000.3 이상
- 외부 런타임 의존성 없음

## 설치

Unity Package Manager에서 **Add package from git URL**을 선택하고 고정 릴리스 태그를 입력합니다.

```text
https://github.com/lLcrowe/LLMConversationRuntime.git#v0.1.3
```

로컬 개발에서는 **Add package from disk**를 선택하고 이 저장소의 `package.json`을 지정합니다.

## 빠른 시작

```csharp
using System.Collections.Generic;
using lLCroweTool.LLMConversation;

var runtime = new ConversationRuntime();
var snapshot = runtime.CreateSession(
    new List<ConversationParticipant>
    {
        new ConversationParticipant
        {
            ParticipantId = "buyer",
            DisplayName = "구매자",
            Provider = "human",
            Kind = ConversationParticipantKind.Human
        },
        new ConversationParticipant
        {
            ParticipantId = "seller",
            DisplayName = "판매자",
            Provider = "local-model",
            Kind = ConversationParticipantKind.Llm
        }
    });

ConversationTurnOpportunity turn = runtime.GetTurnOpportunity(snapshot.SessionId);
ConversationAction action = ConversationAction.Speak(
    snapshot.SessionId,
    turn.ParticipantId,
    "배송 조건부터 논의하고 싶습니다.");
ConversationOperationResult result = runtime.SubmitAction(action);
```

공급자 또는 사람 UI가 현재 `ConversationTurnOpportunity`를 읽고 `ConversationAction`을 만들어 제출합니다. 반환된 스냅샷이 완료될 때까지 이 흐름을 반복합니다.

## 핵심 API

| API | 책임 |
|---|---|
| `ConversationRuntime.CreateSession` | 참여자를 검증하고 자율 또는 장면 지향 세션 생성 |
| `GetTurnOpportunity` | 현재 화자, 최근 이벤트, 허용된 비공개 장면 문맥 투영 |
| `SubmitAction` | 발화·침묵·퇴장·참여자 호출·종료 요청 검증 및 기록 |
| `Pause`, `Resume`, `Stop` | 세션 생명주기 제어 |
| `ConversationQualityEvaluator` | 설정된 역할 이탈·장면 위반·메타 누출·반복 탐지 |
| `ConversationExperimentEvaluator` | 완료 스냅샷에서 공급자 중립 모델 지표 집계 |
| `ConversationTurnContentProjection` | 발화·독백·행동 제안·디버그 정보를 가시성에 맞게 제한 |

통합 흐름과 권위 경계는 [Documentation~/index.md](Documentation~/index.md)를 확인하세요.

## 장면 지향 대화

`ConversationMode.SceneGuided`에는 `ConversationSceneContract`가 필요합니다. 턴 기회는 모든 참여자에게 공개 문맥을 제공하고, 현재 참여자에게만 ID가 일치하는 비공개 문맥을 제공합니다.

런타임은 자유 텍스트를 실행 가능한 게임 명령으로 해석하지 않습니다. `ConversationTurnContent.ActionProposal`은 게임 소유 어댑터에서 검증한 뒤 권위 상태에 반영해야 합니다.

## 샘플

Package Manager에서 **Six Participant Negotiation**을 가져옵니다. GameObject에 `SixParticipantNegotiationExample`을 추가하고 Play Mode에 들어간 뒤 컴포넌트 컨텍스트 메뉴의 **Advance Negotiation**을 실행하면 결정론적 샘플 턴을 제출할 수 있습니다.

## 테스트

**Window > General > Test Runner**를 열고 Edit Mode에서 `lLcrowe.LLMConversation.Tests.EditMode`를 실행합니다.

## 문제 해결

| 증상 | 확인할 항목 |
|---|---|
| Package Manager가 URL을 설치하지 못함 | 실제 존재하는 `v0.1.3` 태그를 포함한 전체 Git URL과 Unity 6000.3 이상을 사용합니다. |
| `CreateSession` 예외 | 고유 ID·고유 표시 이름·비어 있지 않은 공급자를 가진 참여자를 두 명 이상 전달합니다. |
| `GetTurnOpportunity`가 `null` | 세션이 일시정지 또는 완료 상태입니다. `GetSnapshot(sessionId).State`를 확인합니다. |
| `SubmitAction`이 `out_of_turn` 반환 | 최신 턴 기회의 `ParticipantId`를 사용합니다. |
| 장면 지향 세션 생성 예외 | null이 아닌 `ConversationSceneContract`를 전달합니다. |
| 비공개 문맥이 보이지 않음 | 현재 참여자와 `ParticipantId`가 정확히 같은 `ParticipantPrivateContext`를 추가합니다. |

## 범위와 제한사항

- 포함: 메모리 세션, 턴 스케줄링, 수신자 검증, 스냅샷, 구조화 콘텐츠 투영, 품질 검사, 실험 지표
- 제외: 공급자 SDK, 프롬프트 조립, MCP 전송, 영속 저장, 검열 정책, 권위 게임 상태 변경
- 스레드: 현재 런타임 API는 하나의 소유 스레드에서 호출해야 하며 동시 변경을 지원하지 않습니다.

## 연관 프로젝트

[GameRuntimeMcpHost](https://github.com/lLcrowe/GameRuntimeMcpHost)는 전송 책임을 이 패키지에 섞지 않고 게임 소유 대화 어댑터를 MCP 클라이언트에 연결합니다.

## 기여와 보안

변경을 제안하기 전에 [CONTRIBUTING.md](CONTRIBUTING.md)를 확인합니다. 보안 문제는 [SECURITY.md](SECURITY.md)의 비공개 경로로 제보해 주세요.

## 라이선스

MIT — [LICENSE](LICENSE)를 확인하세요.
