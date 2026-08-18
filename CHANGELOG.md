# Changelog

## 0.1.3 — 2026-08-18

- MIT 라이선스와 공개 기여·보안 정책을 추가했습니다.
- Git UPM 설치, 빠른 시작, API, 권위 경계, 문제 해결을 영어·한국어로 문서화했습니다.
- Package Manager에서 가져올 수 있도록 `Six Participant Negotiation` 샘플을 등록했습니다.
- 공개 Runtime API에 XML 문서를 추가했습니다.
- 정의되지 않은 액션 종류가 세션을 변경하거나 종료하지 않도록 거부합니다.

## 0.1.2 — 2026-08-05

- `ConversationExperimentEvaluator`를 추가했습니다.
- 같은 장면·정책으로 실행한 모델별 발화 수, 품질 재생성, 반복, 역할 이탈, 메타 누출을 공급자 중립 비교 기록으로 집계합니다.
- 결과 영속 저장은 Core가 아니라 Tracker·Plan 또는 소비자 보고 계층이 소유합니다.

## 0.1.1 — 2026-08-05

- `Runtime/Quality/ConversationQualityEvaluator`를 추가했습니다.
- 역할 이탈, 장면 금지 표현, 메타 누출, 같은 화자의 최근 발화 반복을 `Accept`·`Retry`·`Reject`로 판정합니다.
- LLM 호출과 재생성은 공용 UPM이 아닌 소비자 Adapter의 책임으로 유지합니다.

## 0.1.0 — 2026-08-04

- 공급자 중립 세션·참여자·장면 계약을 추가했습니다.
- 발화·침묵·퇴장·참여자 호출·종료 요청을 지원합니다.
- 화자별 비공개 문맥 투영과 결정론적 종료 이유를 지원합니다.
- Unity 소비 프로젝트용 `ConversationRuntimeController`를 추가했습니다.
