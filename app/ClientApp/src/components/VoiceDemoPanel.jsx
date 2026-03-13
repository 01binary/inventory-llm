import { useEffect, useRef, useState } from "react";
import { api } from "../services/api";

export default function VoiceDemoPanel() {
  const [isRecording, setIsRecording] = useState(false);
  const [transcript, setTranscript] = useState("");
  const [speakText, setSpeakText] = useState("Inventory check complete.");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const recorderRef = useRef(null);
  const streamRef = useRef(null);
  const chunksRef = useRef([]);
  const audioUrlRef = useRef(null);

  useEffect(() => {
    return () => {
      if (audioUrlRef.current) {
        URL.revokeObjectURL(audioUrlRef.current);
      }
      streamRef.current?.getTracks().forEach((track) => track.stop());
    };
  }, []);

  async function startRecording() {
    setError("");
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    streamRef.current = stream;
    chunksRef.current = [];

    const recorder = new MediaRecorder(stream);
    recorder.ondataavailable = (event) => {
      if (event.data.size > 0) {
        chunksRef.current.push(event.data);
      }
    };
    recorder.onstop = async () => {
      const blob = new Blob(chunksRef.current, { type: "audio/webm" });
      const file = new File([blob], "recording.webm", { type: "audio/webm" });
      setBusy(true);
      try {
        const response = await api.transcribeAudio(file);
        setTranscript(response.text || "");
      } catch (err) {
        setError(err.message);
      } finally {
        setBusy(false);
        streamRef.current?.getTracks().forEach((track) => track.stop());
      }
    };

    recorderRef.current = recorder;
    recorder.start();
    setIsRecording(true);
  }

  function stopRecording() {
    recorderRef.current?.stop();
    setIsRecording(false);
  }

  async function handleSpeak() {
    setBusy(true);
    setError("");
    try {
      const blob = await api.speak(speakText);
      if (audioUrlRef.current) {
        URL.revokeObjectURL(audioUrlRef.current);
      }
      audioUrlRef.current = URL.createObjectURL(blob);
      const audio = new Audio(audioUrlRef.current);
      await audio.play();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card voice-panel">
      <div className="panel-header-row">
        <div>
          <h3>Voice demo</h3>
          <p>Record audio, proxy it through whisper.cpp, then send text to Piper and play the returned WAV.</p>
        </div>
      </div>

      <div className="voice-controls">
        {!isRecording ? (
          <button className="primary-button" onClick={startRecording} disabled={busy}>
            Record
          </button>
        ) : (
          <button className="danger-button" onClick={stopRecording}>
            Stop recording
          </button>
        )}
        <span className="muted-text">{busy ? "Working..." : "Browser microphone access required."}</span>
      </div>

      <label className="form-field">
        <span>Transcript</span>
        <textarea value={transcript} onChange={(event) => setTranscript(event.target.value)} rows={4} />
      </label>

      <label className="form-field">
        <span>Text to speak</span>
        <textarea value={speakText} onChange={(event) => setSpeakText(event.target.value)} rows={3} />
      </label>

      <button className="secondary-button" onClick={handleSpeak} disabled={busy || !speakText.trim()}>
        Speak text
      </button>

      {error ? <p className="error-text">{error}</p> : null}
    </section>
  );
}
