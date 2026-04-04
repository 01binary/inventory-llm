import { useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { api } from "../services/api";

export default function DashboardPage() {
  const [items, setItems] = useState([]);
  const [loadingItems, setLoadingItems] = useState(true);
  const [chatInput, setChatInput] = useState("");
  const [messages, setMessages] = useState([
    {
      id: "welcome",
      role: "assistant",
      text: "Hello. I can help with inventory questions and read responses out loud. Type a message or use dictation."
    }
  ]);
  const [chatBusy, setChatBusy] = useState(false);
  const [isRecording, setIsRecording] = useState(false);
  const [sttBusy, setSttBusy] = useState(false);
  const [error, setError] = useState("");
  const recorderRef = useRef(null);
  const streamRef = useRef(null);
  const chunksRef = useRef([]);
  const currentAudioUrlRef = useRef(null);
  const currentAudioRef = useRef(null);
  const messageEndRef = useRef(null);

  useEffect(() => {
    api.getItems()
      .then((response) => setItems(response))
      .catch((err) => setError(err.message))
      .finally(() => setLoadingItems(false));
  }, []);

  useEffect(() => {
    messageEndRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages]);

  useEffect(() => {
    return () => {
      if (currentAudioRef.current) {
        currentAudioRef.current.pause();
        currentAudioRef.current.currentTime = 0;
      }
      if (currentAudioUrlRef.current) {
        URL.revokeObjectURL(currentAudioUrlRef.current);
      }
      streamRef.current?.getTracks().forEach((track) => track.stop());
    };
  }, []);

  function stripMarkdownForSpeech(markdownText) {
    return markdownText
      .replace(/```[\s\S]*?```/g, " ")
      .replace(/`([^`]+)`/g, "$1")
      .replace(/!\[([^\]]*)\]\([^)]+\)/g, "$1")
      .replace(/\[([^\]]+)\]\([^)]+\)/g, "$1")
      .replace(/^\s{0,3}#{1,6}\s+/gm, "")
      .replace(/^\s*>\s?/gm, "")
      .replace(/^\s*[-*+]\s+/gm, "")
      .replace(/^\s*\d+\.\s+/gm, "")
      .replace(/[*_~#]/g, "")
      .replace(/\|/g, " ")
      .replace(/\s+/g, " ")
      .trim();
  }

  async function speakText(text) {
    if (currentAudioRef.current) {
      currentAudioRef.current.pause();
      currentAudioRef.current.currentTime = 0;
    }
    const speechText = stripMarkdownForSpeech(text);
    if (!speechText) {
      return;
    }

    const blob = await api.speak(speechText);
    if (currentAudioUrlRef.current) {
      URL.revokeObjectURL(currentAudioUrlRef.current);
    }
    currentAudioUrlRef.current = URL.createObjectURL(blob);
    const audio = new Audio(currentAudioUrlRef.current);
    currentAudioRef.current = audio;
    await audio.play();
  }

  async function handleSendMessage() {
    const prompt = chatInput.trim();
    if (!prompt || chatBusy) {
      return;
    }

    const userMessage = {
      id: `user-${Date.now()}`,
      role: "user",
      text: prompt
    };

    setMessages((current) => [...current, userMessage]);
    setChatInput("");
    setChatBusy(true);
    setError("");

    try {
      const response = await api.completeChat(prompt);
      const aiText = (response.text || "").trim() || "I did not get a response from the model.";
      const assistantMessage = {
        id: `assistant-${Date.now()}`,
        role: "assistant",
        text: aiText
      };
      setMessages((current) => [...current, assistantMessage]);
      await speakText(aiText);
    } catch (err) {
      setError(err.message);
    } finally {
      setChatBusy(false);
    }
  }

  async function startRecording() {
    if (chatBusy || sttBusy) {
      return;
    }

    setError("");
    try {
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
        setSttBusy(true);
        try {
          const blob = new Blob(chunksRef.current, { type: "audio/webm" });
          const file = new File([blob], "dictation.webm", { type: "audio/webm" });
          const response = await api.transcribeAudio(file);
          const transcript = (response.text || "").trim();
          if (!transcript) {
            return;
          }
          setChatInput((current) => {
            if (!current.trim()) {
              return transcript;
            }
            return `${current.trim()} ${transcript}`;
          });
        } catch (err) {
          setError(err.message);
        } finally {
          setSttBusy(false);
          streamRef.current?.getTracks().forEach((track) => track.stop());
        }
      };

      recorderRef.current = recorder;
      recorder.start();
      setIsRecording(true);
    } catch (err) {
      setError(err.message || "Could not start recording.");
    }
  }

  function stopRecording() {
    recorderRef.current?.stop();
    setIsRecording(false);
  }

  return (
    <div className="dashboard-full">
      <header className="dashboard-header">
        <div>
          <p className="eyebrow">Overview</p>
          <h2>AI Inventory Assistant</h2>
          <p className="page-description">Ask inventory questions, dictate prompts, and hear responses instantly.</p>
        </div>
      </header>

      <section className="dashboard-split">
        <article className="card pane chat-pane">
          <div className="pane-header">
            <h3>Assistant Chat</h3>
            <span className="muted-text">{chatBusy ? "Thinking..." : isRecording ? "Recording..." : "Ready"}</span>
          </div>

          <div className="chat-scroll">
            {messages.map((message) => (
              <div
                key={message.id}
                className={message.role === "user" ? "message-row user" : "message-row assistant"}
              >
                <div className={message.role === "user" ? "speech-bubble user" : "speech-bubble assistant"}>
                  {message.role === "assistant" ? (
                    <ReactMarkdown remarkPlugins={[remarkGfm]} className="markdown-content">
                      {message.text}
                    </ReactMarkdown>
                  ) : (
                    message.text
                  )}
                </div>
              </div>
            ))}
            <div ref={messageEndRef} />
          </div>

          <div className="chat-composer">
            <textarea
              rows={3}
              placeholder="Type your inventory question..."
              value={chatInput}
              onChange={(event) => setChatInput(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter" && !event.shiftKey) {
                  event.preventDefault();
                  handleSendMessage();
                }
              }}
            />
            <div className="button-row">
              {!isRecording ? (
                <button className="secondary-button" onClick={startRecording} disabled={chatBusy || sttBusy}>
                  {sttBusy ? "Transcribing..." : "Dictate"}
                </button>
              ) : (
                <button className="danger-button" onClick={stopRecording}>
                  Stop
                </button>
              )}
              <button
                className="primary-button"
                onClick={handleSendMessage}
                disabled={chatBusy || sttBusy || !chatInput.trim()}
              >
                Send
              </button>
            </div>
          </div>
          {error ? <p className="error-text">{error}</p> : null}
        </article>

        <article className="card pane inventory-pane">
          <div className="pane-header">
            <h3>Current Inventory</h3>
            <span className="muted-text">{loadingItems ? "Loading..." : `${items.length} items`}</span>
          </div>
          <div className="activity-scroll">
            {items.map((item) => (
              <div className="activity-row" key={item.id}>
                <div>
                  <strong>{item.name}</strong>
                  <p className="muted-text">SKU: {item.sku}</p>
                </div>
                <div>
                  <p className="muted-text">
                    Qty: {item.quantity}
                  </p>
                </div>
              </div>
            ))}
          </div>
        </article>
      </section>
    </div>
  );
}
