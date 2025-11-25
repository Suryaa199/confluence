Interview Copilot (Windows/desktop) [![CI (Windows build)](https://github.com/Suryaa199/AI_AGENT/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Suryaa199/AI_AGENT/actions/workflows/ci.yml)

UI (Parakeet AI style and add scroll when ever it requireds  ) always-on-top Windows assistant for live interviews. Captures meeting audio (per-app/System/Mic), transcribes with OpenAI Whisper, and streams answers token-by-token using GPT-4o/4o-mini. Answers include key points, concrete examples, and CLI commands. Includes follow-up predictor, company cheat sheet, keywords, and offline ASR buffer.

Quick Start
- Requirements: Windows 10/11, .NET 8 SDK, OpenAI API key (or local providers).
- Models: Uses OpenAI Whisper (Transcriptions) and GPT-4o/4o-mini. Optional local: Faster‑Whisper + Ollama.
- Build: Open with Visual Studio 2022 or build from CLI.

Features (MVP)
- Per-app/System/Microphone audio capture selector.
- Real-time transcription via OpenAI Whisper.
- Streaming answers (token-by-token) via GPT-4o/4o-mini.
- UI (Parakeet AI style)/ Interview (Question small size, Answer large size in the new window only question+answers /start-stop listen).
- Company quick context (paste blurb → cheat sheet).
- Follow-up predictor chips.
- Offline ASR buffer (queues audio when offline; backfills later).
- Story Bank: save answers; review/search from Settings.
- Resume/JD: parse .txt/.docx automatically. (PDF parsing deferred for CI stability; convert PDFs to text/docx for now.)

Enhancements
- TTS isolated to Communications device for earpiece-only coaching.
- Per-app session picker (with process names and window titles).
- Adjustable ASR chunk size and VAD thresholds in Settings.
- Offline spooler for audio chunks to backfill on reconnect.

Repo Structure
- `src/InterviewCopilot/` WPF app (.NET 8, UseWPF)
  - Shell: `App.xaml`, `MainWindow`, `MainViewModel`
  - Windows: `Windows/OverlayWindow`, `Windows/SettingsWindow`, `Windows/PerAppPickerWindow`
  - Pages: `SettingsPage`, `InterviewPage` (auxiliary)
  - Services
    - Abstractions: `ISettingsStore`, `ISecretStore`, `IAudioService`, `IAsrService`, `ILlmService`, `ICoachingService`, `IStoryRepository`, `IOfflineSpooler`, `ITtsService`, `IVadService`
    - Audio: `NaudioAudioService`, `Resampler`, `VadEnergyGate`, `SileroVadService`
    - OpenAI: `OpenAiLlmService`, `OpenAiWhisperAsrService`
    - Local: `OllamaLlmService`, `LocalAsrService`
    - Storage: `JsonSettingsStore`, `DpapiSecretStore`, `FileStoryRepository`, `DiskOfflineSpooler`
  - Resources: `Colors.xaml`, `Typography.xaml`, `Styles.xaml`
  - Models: `Settings`, `CoachingState`

Setup
1) Set `OPENAI_API_KEY` in your user environment (if using OpenAI).
2) Build and run:
   - Visual Studio: open `InterviewCopilot.sln`, restore, run.
   - CLI: `dotnet restore && dotnet build && dotnet run --project src/InterviewCopilot`
3) API key in app: On Settings, paste your OpenAI key and click “Test Key” → “Save Key”. The key is stored encrypted (Windows DPAPI, user scope). If empty, the app uses `OPENAI_API_KEY` when present.
4) Package (Windows): `powershell -ExecutionPolicy Bypass -File scripts/publish-win.ps1` → `dist/win-x64/InterviewCopilot.exe`
5) Optional MSIX (Windows SDK): `powershell -ExecutionPolicy Bypass -File scripts/make-msix.ps1 -Publisher "CN=Your Name"` → `dist/InterviewCopilot.msix`
6) First run tips:
   - Paste resume/JD text or load .txt/.docx; add keywords and optional company blurb; click “Generate Cheat Sheet”.
   - Choose Audio Source: Per‑app (picker), System (loopback), or Microphone.
   - Click Start Listening to begin.

Notes
- Streaming uses OpenAI Chat Completions with `stream=true`.
- Transcription sends short WAV chunks to OpenAI Whisper.
- Per-app capture: Windows does not expose a stable public API to capture a single app’s audio stream directly on all versions. This app provides a per-app picker (Teams/Zoom/Browser) and uses a best-effort approach with loopback on the Communications device. For more isolation, set your meeting app to output to the Communications device and keep other apps on the Default device.
- Offline buffer stores short WAV files and transcribes when network returns.
 - ONNX VAD (Silero): optional toggle in Settings. Place `models/silero_vad.onnx` under the app directory. The app uses it for tighter start/stop gating if present; otherwise energy-based VAD is used. (Inference pipeline is conservative at first; we aim for reliable boundaries.)

Hotkeys
- Alt+P: Toggle click-through overlay
- Alt+C: Copy current answer
- Alt+G: Regenerate answer
- Alt+S: Pause capture

Per-App Best Practices
- In Teams/Zoom/Meet, set speaker/output device to “Communications”.
- In Settings → Audio, choose Source: PerApp and System Device: Communications.
- Use the “Test” buttons to verify levels before starting.
- The app uses Windows audio session meters to prefer audio from the selected app’s process during loopback capture. Other system sounds are largely ignored when the meeting app is active.

Windows 11 Per-Window Capture
- A per-window audio capture path is experimental and feature-detected. The app automatically falls back to session-gated loopback when unsupported. In practice, PerApp + Communications device isolation yields reliable results.

 Silero VAD Setup
 - Download `silero_vad.onnx` and place it at `models/silero_vad.onnx` (create the folder next to the EXE).
 - Example script:
   `powershell -Command "New-Item -ItemType Directory -Force models; Invoke-WebRequest -Uri https://github.com/snakers4/silero-vad/raw/master/files/silero_vad.onnx -OutFile models/silero_vad.onnx"`
 - Enable Silero in Settings → “Enable Silero VAD”, then set:
   - Window: 30 ms (common default; try 20–40 ms)
   - Threshold: 0.50–0.60 (speech probability)
 - Tips:
   - Increase Threshold if you get false positives (background noise).
   - Decrease Threshold if speech is clipped too often.
   - Silero uses small incremental windows; larger windows increase stability but may add latency.

Roadmap
- Voice output, per-company prompt presets, improved PDF/DOCX parsing, story bank, rubric-based scoring view.
 - Investigate Windows 11 GraphicsCapture + MediaCapture for tighter per-window audio when supported (with fallback).
UI Scrolling
- Settings and coaching/answer panes are wrapped in scroll containers so options remain visible on smaller screens.

Low-Latency Tips
- Use Local providers: Faster‑Whisper (ASR) and Ollama (LLM) on a capable GPU.
- Reduce `Chunk Size (ms)` in Settings (e.g., 400–600 ms) to send smaller ASR chunks.
- Keep Silero Window moderate (20–40 ms); increase slightly if you hear choppy boundaries.
- Keep Threshold ~0.50–0.60 initially; tune higher if noisy environments cause false triggers.
- PerApp capture: set your meeting app to output to the Communications device and select that in the app for isolation.
