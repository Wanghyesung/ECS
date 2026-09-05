# ORBITAL (오비탈) — 가제

3D Roguelite Shooter developed with Unity

> "단순하지만, 계속 다시 하게 되는 슈팅"
> 조작은 한 번에 이해되고, 성장과 스킬 조합은 매 판 달라진다.

---

# Preview


---

# Overview

| 항목 | 내용 |
| --- | --- |
| 장르 | 3D 슈팅 · 로그라이트 |
| 플랫폼 | Windows / Android (최적화 시) |
| 타겟 | 짧고 빠른 사이클의 게임을 즐기는 플레이어 |
| 레퍼런스 | 뱀파이어 서바이버즈, 궁수의 전설 |

## 기획 의도

복잡하고 진입 장벽이 높은 게임이 많은 시장에서, 직관성과 단순함이 주는 본질적인 재미에 집중한다.
배우는 데 시간이 들지 않고, 한 판이 짧게 끝나며, 매번 다른 조합으로 다시 하고 싶어지는 게임을 목표로 한다.

---

# Game Loop

로비 → 던전 입장 → 전투 · 보상 → 플레이어 성장 → 클리어 → 다음 던전

---

# Core Mechanics

## 01. 이동 회피
몰려오는 총알을 피하며 위치를 잡는 조작. 플레이의 집중도를 만드는 축.

## 02. 사격
공격은 간결하게 유지해, 판단과 이동에 집중하도록 설계.

## 03. 랜덤 스킬 조합
레벨업마다 제시되는 스킬 중 선택. 매 판 다른 빌드를 완성하는 재미.

---

# 기획 포인트

- **랜덤성**: 매 판 달라지는 스킬 조합
- **직관성**: 설명 없이 바로 이해되는 조작
- **몰입도**: 끊기지 않고 이어지는 한 판

---

# Tech Stack
- Unity 2022.3.62f (URP)
- C#
- Input System
- Cinemachine

---

# Architecture

- Player FSM (Idle / Move / Dead)
- Monster Behavior Tree (Sequence / Selector / Action)
- Blackboard System
- Scriptable Object 기반 Action
- Object Pool
- Event System (Observer Pattern)

---

# Features

## 다양한 스킬
획득 · 조합에 따라 플레이 스타일이 갈리는 스킬 풀

## 무기별 이동 · 사격
무기에 따라 움직임과 공격 방식이 달라지는 운영

## 스테이지별 몬스터 패턴
스테이지마다 다른 배치와 패턴으로 도전 난이도 변화

---

# Performance

- Object Pool
- GC Alloc 0
- SRP Batcher

---

# Screenshots


---
