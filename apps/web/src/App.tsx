import { useEffect, useMemo, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import * as SpeechSdk from "microsoft-cognitiveservices-speech-sdk";
import type { OrchestrationRun } from "@ai-dev-orchestrator/shared";

interface AppConfigResponse {
  executionMode: string;
  speechEnabled: boolean;
  speechVoice: string;
  providers: {
    openai: boolean;
    anthropic: boolean;
  };
}

interface SpeechTokenResponse {
  token: string;
  region: string;
  voice: string;
}

const apiBaseUrl = window.__APP_CONFIG__?.apiBaseUrl ?? "http://localhost:8080";

export function App() {
  const [config, setConfig] = useState<AppConfigResponse>();
  const [runs, setRuns] = useState<OrchestrationRun[]>([]);
  const [activeRunId, setActiveRunId] = useState<string>();
  const [text, setText] = useState("");
  const [owner, setOwner] = useState("");
  const [repo, setRepo] = useState("");
  const [issueNumber, setIssueNumber] = useState("");
  const [statusMessage, setStatusMessage] = useState("Ready for your next request.");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isListening, setIsListening] = useState(false);
  const [isSpeaking, setIsSpeaking] = useState(false);

  const activeRun = useMemo(
    () => runs.find((run: OrchestrationRun) => run.id === activeRunId) ?? runs[0],
    [activeRunId, runs],
  );

  useEffect(() => {
    void loadConfig();
    void loadRuns();
  }, []);

  useEffect(() => {
    if (!activeRun) {
      return;
    }

    if (activeRun.status === "completed" || activeRun.status === "failed") {
      return;
    }

    const timer = window.setInterval(() => {
      void loadRuns();
    }, 4000);

    return () => window.clearInterval(timer);
  }, [activeRun]);

  async function loadConfig() {
    const response = await fetch(`${apiBaseUrl}/api/config`);
    const payload = (await response.json()) as AppConfigResponse;
    setConfig(payload);
  }

  async function loadRuns() {
    const response = await fetch(`${apiBaseUrl}/api/runs`);
    const payload = (await response.json()) as OrchestrationRun[];
    setRuns(payload);

    if (!activeRunId && payload[0]) {
      setActiveRunId(payload[0].id);
    }
  }

  async function submitRun(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setStatusMessage("Dispatching your request to the orchestrator.");

    try {
      const response = await fetch(`${apiBaseUrl}/api/runs`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          text,
          repository:
            owner && repo
              ? {
                  owner,
                  repo,
                  issueNumber: issueNumber ? Number(issueNumber) : undefined,
                }
              : undefined,
        }),
      });

      const run = (await response.json()) as OrchestrationRun;
      setActiveRunId(run.id);
      setText("");
      setStatusMessage(`Run ${run.id.slice(0, 8)} is now ${run.status}.`);
      await loadRuns();
    } catch (error) {
      setStatusMessage(
        error instanceof Error ? error.message : "Unable to submit the run.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function captureSpeech() {
    setIsListening(true);
    setStatusMessage("Listening for your requirement.");

    try {
      const tokenResponse = await fetch(`${apiBaseUrl}/api/speech/token`, {
        method: "POST",
      });
      const speechToken = (await tokenResponse.json()) as SpeechTokenResponse;
      const speechConfig = SpeechSdk.SpeechConfig.fromAuthorizationToken(
        speechToken.token,
        speechToken.region,
      );
      speechConfig.speechRecognitionLanguage = "en-US";

      const recognizer = new SpeechSdk.SpeechRecognizer(
        speechConfig,
        SpeechSdk.AudioConfig.fromDefaultMicrophoneInput(),
      );

      const recognizedText = await new Promise<string>((resolve, reject) => {
        recognizer.recognizeOnceAsync(
          (result) => {
            recognizer.close();

            if (result.reason === SpeechSdk.ResultReason.RecognizedSpeech) {
              resolve(result.text);
            } else {
              reject(new Error("No speech was recognized."));
            }
          },
          (error) => {
            recognizer.close();
            reject(new Error(String(error)));
          },
        );
      });

      setText((current: string) =>
        [current, recognizedText].filter(Boolean).join(" ").trim(),
      );
      setStatusMessage("Transcription captured. Review it and launch the run when ready.");
    } catch (error) {
      setStatusMessage(
        error instanceof Error ? error.message : "Voice capture failed.",
      );
    } finally {
      setIsListening(false);
    }
  }

  async function speakSummary() {
    if (!activeRun?.summary) {
      return;
    }

    setIsSpeaking(true);
    setStatusMessage("Reading back the latest orchestration brief.");

    try {
      const tokenResponse = await fetch(`${apiBaseUrl}/api/speech/token`, {
        method: "POST",
      });
      const speechToken = (await tokenResponse.json()) as SpeechTokenResponse;
      const speechConfig = SpeechSdk.SpeechConfig.fromAuthorizationToken(
        speechToken.token,
        speechToken.region,
      );
      speechConfig.speechSynthesisVoiceName = speechToken.voice;

      const synthesizer = new SpeechSdk.SpeechSynthesizer(
        speechConfig,
        SpeechSdk.AudioConfig.fromDefaultSpeakerOutput(),
      );

      await new Promise<void>((resolve, reject) => {
        synthesizer.speakTextAsync(
          activeRun.summary!,
          (result) => {
            synthesizer.close();

            if (result.reason === SpeechSdk.ResultReason.SynthesizingAudioCompleted) {
              resolve();
            } else {
              reject(new Error("Speech synthesis did not complete."));
            }
          },
          (error) => {
            synthesizer.close();
            reject(new Error(String(error)));
          },
        );
      });

      setStatusMessage("Summary playback completed.");
    } catch (error) {
      setStatusMessage(
        error instanceof Error ? error.message : "Unable to speak the summary.",
      );
    } finally {
      setIsSpeaking(false);
    }
  }

  return (
    <div className="app-shell">
      <header className="hero">
        <div>
          <p className="eyebrow">Voice-first GitHub orchestration</p>
          <h1>Run Codex and Claude together with a human in the loop.</h1>
          <p className="lede">
            Speak a requirement, anchor it to a repository or issue, and let the
            orchestrator produce a staged delivery brief.
          </p>
        </div>

        <div className="hero-panel">
          <div className="panel-row">
            <span>Execution mode</span>
            <strong>{config?.executionMode ?? "loading"}</strong>
          </div>
          <div className="panel-row">
            <span>Speech</span>
            <strong>{config?.speechEnabled ? "enabled" : "not configured"}</strong>
          </div>
          <div className="panel-row">
            <span>Providers</span>
            <strong>
              {config?.providers.openai ? "OpenAI" : "OpenAI off"} /{" "}
              {config?.providers.anthropic ? "Claude" : "Claude off"}
            </strong>
          </div>
        </div>
      </header>

      <main className="grid">
        <section className="card composer">
          <h2>New Run</h2>
          <form onSubmit={submitRun}>
            <label>
              Requirement
              <textarea
                value={text}
                  onChange={(event: ChangeEvent<HTMLTextAreaElement>) =>
                    setText(event.target.value)
                  }
                placeholder="Describe the feature, bug, or development goal."
                rows={8}
              />
            </label>

            <div className="two-up">
              <label>
                GitHub owner
                <input
                  value={owner}
                  onChange={(event: ChangeEvent<HTMLInputElement>) =>
                    setOwner(event.target.value)
                  }
                  placeholder="your-org"
                />
              </label>

              <label>
                Repository
                <input
                  value={repo}
                  onChange={(event: ChangeEvent<HTMLInputElement>) =>
                    setRepo(event.target.value)
                  }
                  placeholder="project-repo"
                />
              </label>
            </div>

            <label>
              Issue number
              <input
                value={issueNumber}
                onChange={(event: ChangeEvent<HTMLInputElement>) =>
                  setIssueNumber(event.target.value)
                }
                placeholder="Optional"
              />
            </label>

            <div className="button-row">
              <button
                className="secondary"
                type="button"
                onClick={() => void captureSpeech()}
                disabled={!config?.speechEnabled || isListening}
              >
                {isListening ? "Listening..." : "Speak Requirement"}
              </button>
              <button type="submit" disabled={!text.trim() || isSubmitting}>
                {isSubmitting ? "Submitting..." : "Launch Run"}
              </button>
            </div>
          </form>
        </section>

        <section className="card run-list">
          <h2>Recent Runs</h2>
          <div className="list">
            {runs.length === 0 ? (
              <p className="muted">No runs yet.</p>
            ) : (
              runs.map((run: OrchestrationRun) => (
                <button
                  key={run.id}
                  className={run.id === activeRun?.id ? "list-item active" : "list-item"}
                  onClick={() => setActiveRunId(run.id)}
                  type="button"
                >
                  <span>{run.input.text.slice(0, 70)}</span>
                  <strong>{run.status}</strong>
                </button>
              ))
            )}
          </div>
        </section>

        <section className="card run-detail">
          <div className="detail-header">
            <div>
              <h2>Run Detail</h2>
              <p className="muted">{statusMessage}</p>
            </div>

            <button
              className="secondary"
              type="button"
              onClick={() => void speakSummary()}
              disabled={!config?.speechEnabled || !activeRun?.summary || isSpeaking}
            >
              {isSpeaking ? "Speaking..." : "Read Summary"}
            </button>
          </div>

          {activeRun ? (
            <>
              <div className="status-strip">
                <span>Status</span>
                <strong>{activeRun.status}</strong>
              </div>

              {activeRun.summary ? (
                <article className="summary-block">
                  <h3>Latest Brief</h3>
                  <pre>{activeRun.summary}</pre>
                </article>
              ) : null}

              <div className="artifact-list">
                {activeRun.artifacts.map((artifact) => (
                  <article key={artifact.id} className="artifact-card">
                    <div className="artifact-meta">
                      <span>{artifact.stage}</span>
                      <strong>{artifact.title}</strong>
                    </div>
                    <pre>{artifact.content}</pre>
                  </article>
                ))}
              </div>
            </>
          ) : (
            <p className="muted">Select a run to inspect its timeline.</p>
          )}
        </section>
      </main>
    </div>
  );
}
