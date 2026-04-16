import { useEffect, useRef, useState } from "react";
import {
  getBrowserVoices,
  speakWithBrowser,
  startBrowserSpeechRecognition,
  stopBrowserSpeech,
  subscribeToVoiceChanges
} from "../services/browserSpeech";

export default function VoiceDemoPanel() {
  const [isRecording, setIsRecording] = useState(false);
  const [transcript, setTranscript] = useState("");
  const [speakText, setSpeakText] = useState("Inventory check complete.");
  const [voices, setVoices] = useState([]);
  const [selectedVoiceURI, setSelectedVoiceURI] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const recognitionRef = useRef(null);

  useEffect(() => {
    const loadVoices = () => setVoices(getBrowserVoices());
    loadVoices();
    const unsubscribe = subscribeToVoiceChanges(loadVoices);

    return () => {
      unsubscribe();
      recognitionRef.current?.stop();
      stopBrowserSpeech();
    };
  }, []);

  function startRecording() {
    setError("");
    setBusy(true);
    try {
      recognitionRef.current = startBrowserSpeechRecognition({
        onResult: (text) => setTranscript(text),
        onError: (message) => setError(message),
        onEnd: () => {
          setIsRecording(false);
          setBusy(false);
        }
      });
      setIsRecording(true);
    } catch (err) {
      setBusy(false);
      setError(err.message);
    }
  }

  function stopRecording() {
    recognitionRef.current?.stop();
    setIsRecording(false);
  }

  async function handleSpeak() {
    setBusy(true);
    setError("");
    try {
      await speakWithBrowser(speakText, { voiceURI: selectedVoiceURI });
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
          <p>Record audio with Chrome speech recognition, then play responses with Chrome text to speech.</p>
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

      <label className="form-field">
        <span>Voice</span>
        <select value={selectedVoiceURI} onChange={(event) => setSelectedVoiceURI(event.target.value)}>
          <option value="">Default ({voices.length} available)</option>
          {voices.map((voice) => (
            <option key={voice.voiceURI} value={voice.voiceURI}>
              {voice.name} ({voice.lang})
            </option>
          ))}
        </select>
      </label>

      <button className="secondary-button" onClick={handleSpeak} disabled={busy || !speakText.trim()}>
        Speak text
      </button>

      {error ? <p className="error-text">{error}</p> : null}
    </section>
  );
}
