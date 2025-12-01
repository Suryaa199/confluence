Interview Copilot (Windows/desktop) [![CI (Windows build)](https://github.com/Suryaa199/AI_AGENT/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Suryaa199/AI_AGENT/actions/workflows/ci.yml)

Interview Copilot is a Windows assistant inspired by tools like Parakeet AI. It captures live meeting audio, transcribes questions, and streams “co-pilot style” answers that include resume context, mini examples, and CLI commands. The overlay stays on top (or on a second monitor) so you can read answers in real time while screen sharing.

---

## Quick Start

1. **Requirements**
   - Windows 10/11
   - .NET 8 SDK
   - OpenAI API key (optional: local Faster-Whisper + Ollama)

2. **Build**
   - Visual Studio 2022: open `InterviewCopilot.sln`, restore, run.
   - CLI: `dotnet restore && dotnet build && dotnet run --project src/InterviewCopilot`

3. **Configure**
   - Launch the app, open **Settings**, paste your OpenAI key, **Test** → **Save Key** (stored via Windows DPAPI). If `OPENAI_API_KEY` is set, the app can read it automatically.
   - Load or paste resume/JD text, keywords, and the company blurb; click **Generate Cheat Sheet** to pre-seed context.
   - Choose audio source (PerApp/System/Mic). For best isolation, set your meeting app to the Windows “Communications” device and choose PerApp + Communications in the app.

4. **Run**
   - Click **Start Listening**. The overlay appears (keep it on a second monitor for privacy) and answers stream as questions are detected.
   - Use the toolbar **Live Cue** field to inject hints (e.g., “mention ArgoCD”) for the next answer.

---

## Features

- **Audio Capture**
  - Per-app/System/Mic capture via NAudio; per-app picker shows processes and activity levels.
  - Adjustable VAD (energy or Silero ONNX). Optional `models/silero_vad.onnx` for tighter gating.
  - Offline spooler stores WAV chunks if ASR fails and replays later.

- **Real-Time Answers**
  - OpenAI GPT-4o-mini streaming by default; Ollama local LLM supported via Auto mode.
  - Question classifier (definition, challenge, troubleshooting, command, etc.).
  - Context retriever pulls the most relevant resume/JD snippets.
  - Scenario + CLI library injects mini examples and ready commands (git, az, kubectl, terraform, etc.).
  - Conversation-aware tone (concise/detailed) plus small-talk responder for greetings.

- **UI**
  - Main window: start/stop capture, provider presets, live cue input, per-app picker, overlay toggle, cheat sheet/story panes.
  - Overlay window: resizable, auto-scroll, can stay on a secondary monitor.
  - Hotkeys: Alt+P (click-through overlay), Alt+C (copy answer), Alt+G (regenerate), Alt+S (pause).

- **Storage & Logging**
  - Settings stored under `%AppData%\InterviewCopilot`.
  - API keys encrypted via DPAPI.
  - Story Bank saves completed answers; searchable from Settings.
  - Prompt logger writes `prompts.jsonl` so you can review question/prompt/answer pairs.

---

## Provider Modes

- **Auto (Live Interview)**: If an OpenAI key is present, uses OpenAI LLM + OpenAI Whisper; otherwise switches to Ollama Faster-Whisper combo. Also applies low-latency VAD chunking.
- Manual combos: OpenAI only, Local only, or hybrids. Switching providers reloads ASR/LLM services live.

---

## Per-App Capture Tips

- Set Teams/Zoom/Meet output to “Communications”.
- In Interview Copilot → Audio Source = PerApp, Device Preference = Communications.
- Click **Per-App Picker** while audio is playing; select the relevant process. The picker scans all render endpoints and shows process + title + live level.

---

## Low-Latency Tips

- Use Silero VAD with 20–40 ms windows, threshold ~0.5–0.6.
- Reduce chunk size (400–500 ms) for faster ASR turnarounds.
- For unreliable networks, use local providers (Ollama + Faster-Whisper).
- Keep overlay on a second monitor to avoid screen-share leaks.

---

## Scripts

- `scripts/publish-win.ps1` – Produces a self-contained single-file EXE under `dist/win-x64`.
- `scripts/make-msix.ps1` – Optional MSIX packaging (requires Windows SDK).
- `scripts/smoke.ps1` – CI smoke build + publish + artifact verification (Windows).

---

## Roadmap

- Voice output routed to earpiece-only device.
- Per-company prompt presets and rubric scoring.
- Enhanced PDF parsing and story-bank analytics.
- Windows GraphicsCapture integration for per-window audio when available.

---

For support: open an issue or ping the maintainer. Happy interviewing! 💼🧠💬
