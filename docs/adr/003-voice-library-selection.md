# ADR-003: Voice Library Selection

**Status**: Proposed (deferred to US6 implementation)
**Date**: 2026-02-23

## Context

The Focus Assistant requires three voice capabilities: wake-word detection, speech-to-text (STT), and text-to-speech (TTS). These must work offline on Linux, run reliably for 8+ hours, and have low latency.

## Decision

**For MVP (Phases 1-3)**: Stub all voice I/O — stdin for input, stdout for output. This allows full testing of the domain and application logic without voice dependencies.

**For US6 (Phase 8)**: Evaluate and implement:
- **Wake-word detection**: Porcupine (Picovoice) or OpenWakeWord
- **Speech-to-text**: Vosk (offline, open-source) or Azure Speech SDK
- **Text-to-speech**: espeak-ng (Linux native) or Azure Speech SDK

## Rationale

- **Stub-first**: Decouples business logic development from voice library evaluation. The `IVoiceInputService` / `IVoiceOutputService` interfaces ensure voice implementations are swappable.
- **Offline preference**: User works at a personal workstation; offline STT (Vosk) avoids network latency and privacy concerns.
- **Linux-native TTS**: espeak-ng is available on all Linux distributions and requires no API keys.
- **Final selection deferred**: Voice library landscape evolves rapidly; deferring the decision preserves optionality.

## Alternatives Considered

- **Google Cloud Speech**: High accuracy but requires network and API key.
- **Whisper (OpenAI)**: Good accuracy but high resource usage for real-time use.
- **Mozilla DeepSpeech**: Archived project — not actively maintained.

## Consequences

- MVP can be developed and tested without microphone/speaker hardware.
- Voice library integration is a separate phase that won't block core feature development.
- May need to evaluate multiple libraries in US6 to find the best latency/accuracy tradeoff.
