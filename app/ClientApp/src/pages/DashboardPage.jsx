import { useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { api } from "../services/api";
import {
  PREFERRED_TTS_VOICE_NAME,
  speakWithBrowser,
  startBrowserSpeechRecognition,
  stopBrowserSpeech
} from "../services/browserSpeech";

export default function DashboardPage() {
  const helloPrompt = "Hello. Greet the user in one short sentence and ask how you can help track inventory today.";
  const [items, setItems] = useState([]);
  const [loadingItems, setLoadingItems] = useState(true);
  const [initializingChat, setInitializingChat] = useState(true);
  const [chatInput, setChatInput] = useState("");
  const [messages, setMessages] = useState([]);
  const [chatBusy, setChatBusy] = useState(false);
  const [isRecording, setIsRecording] = useState(false);
  const [sttBusy, setSttBusy] = useState(false);
  const [error, setError] = useState("");
  const recognitionRef = useRef(null);
  const messageEndRef = useRef(null);
  const initStartedRef = useRef(false);

  async function refreshItems(showLoading = false) {
    if (showLoading) {
      setLoadingItems(true);
    }

    try {
      const response = await api.getItems();
      setItems(response);
    } catch (err) {
      setError(err.message);
    } finally {
      if (showLoading) {
        setLoadingItems(false);
      }
    }
  }

  useEffect(() => {
    refreshItems(true);
  }, []);

  useEffect(() => {
    async function initializeConversation() {
      if (initStartedRef.current) {
        return;
      }
      initStartedRef.current = true;

      setInitializingChat(true);
      setError("");
      try {
        const promptResponse = await api.getSystemPrompt();
        const systemText = (promptResponse?.text || "").trim();

        const initialMessages = [
          {
            id: "system-initial",
            role: "system",
            text: systemText
          },
          {
            id: "user-hello",
            role: "user",
            text: helloPrompt
          }
        ];

        setMessages(initialMessages);

        const response = await api.completeChat({
          messages: initialMessages.map((message) => ({
            role: message.role,
            content: message.text
          })),
          maxTokens: 128
        });

        const assistantText = (response.text || "").trim() || "Hello, how can I help you track your inventory today?";
        const assistantMessage = {
          id: `assistant-${Date.now()}`,
          role: "assistant",
          text: assistantText
        };
        setMessages((current) => [...current, assistantMessage]);
      } catch (err) {
        setError(err.message || "Failed to initialize chat.");
      } finally {
        setInitializingChat(false);
      }
    }

    initializeConversation();
  }, []);

  useEffect(() => {
    messageEndRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages]);

  useEffect(() => {
    return () => {
      recognitionRef.current?.stop();
      stopBrowserSpeech();
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
    const speechText = stripMarkdownForSpeech(text);
    if (!speechText) {
      return;
    }

    await speakWithBrowser(speechText, { voiceName: PREFERRED_TTS_VOICE_NAME });
  }

  async function handleSendMessage() {
    if (initializingChat) {
      return;
    }

    const prompt = chatInput.trim();
    if (!prompt || chatBusy) {
      return;
    }

    const userMessage = {
      id: `user-${Date.now()}`,
      role: "user",
      text: prompt
    };

    const outboundMessages = [...messages, userMessage];
    setMessages(outboundMessages);
    setChatInput("");
    setChatBusy(true);
    setError("");

    try {
      const response = await api.completeChat({
        messages: outboundMessages.map((message) => ({
          role: message.role,
          content: message.text
        })),
        maxTokens: 256
      });
      const aiText = (response.text || "").trim() || "I did not get a response from the model.";
      const assistantMessage = {
        id: `assistant-${Date.now()}`,
        role: "assistant",
        text: aiText
      };
      setMessages((current) => [...current, assistantMessage]);
      await refreshItems();
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
      setSttBusy(true);
      recognitionRef.current = startBrowserSpeechRecognition({
        onResult: (transcript) => {
          setChatInput((current) => {
            if (!current.trim()) {
              return transcript;
            }
            return `${current.trim()} ${transcript}`;
          });
        },
        onError: (message) => setError(message),
        onEnd: () => {
          setIsRecording(false);
          setSttBusy(false);
        }
      });
      setIsRecording(true);
    } catch (err) {
      setSttBusy(false);
      setError(err.message || "Could not start recording.");
    }
  }

  function stopRecording() {
    recognitionRef.current?.stop();
    setIsRecording(false);
  }

  const visibleMessages = messages.slice(2);
  const chatStatus = initializingChat ? "Initializing..." : chatBusy ? "Thinking..." : isRecording ? "Recording..." : "Ready";

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
            <span className="muted-text">{chatStatus}</span>
          </div>

          <div className="chat-scroll">
            {visibleMessages.map((message) => (
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
                <button className="secondary-button" onClick={startRecording} disabled={initializingChat || chatBusy || sttBusy}>
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
                disabled={initializingChat || chatBusy || sttBusy || !chatInput.trim()}
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
