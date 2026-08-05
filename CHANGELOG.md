# Changelog

## 0.1.1 — 2026-08-05

- `Runtime/Quality/ConversationQualityEvaluator`를 추가했습니다.
- 역할 이탈, 장면 금지 표현, 메타 누출, 같은 화자의 최근 발화 반복을 `Accept`·`Retry`·`Reject`로 판정합니다.
- LLM 호출과 재생성은 공용 UPM이 아닌 소비자 Adapter의 책임으로 유지합니다.

## 0.1.0 — 2026-08-04

- 공급자 중립 세션·참여자·장면 계약을 추가했습니다.
- 발화·침묵·퇴장·참여자 호출·종료 요청을 지원합니다.
- 화자별 비공개 문맥 투영과 결정론적 종료 이유를 지원합니다.
- Unity 소비 프로젝트용 `ConversationRuntimeController`를 추가했습니다.
