import { useEffect, useRef, useState } from "react";
import ChatPanel from "../components/ChatPanel";
import InventoryPanel from "../components/InventoryPanel";
import { api } from "../services/api";
import {
  PREFERRED_TTS_VOICE_NAME,
  speakWithBrowser,
  startBrowserSpeechRecognition,
  stopBrowserSpeech
} from "../services/browserSpeech";

export default function DashboardPage() {
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
  const chatInputRef = useRef(null);
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
        const [systemPromptResponse, helloPromptResponse] = await Promise.all([
          api.getSystemPrompt(),
          api.getHelloPrompt()
        ]);
        const systemText = (systemPromptResponse?.text || "").trim();
        const helloPrompt = (helloPromptResponse?.text || "").trim();

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

        try {
          await speakText(assistantText);
        } catch {
          // Browser autoplay policies can block TTS before first user interaction.
        }
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
          chatInputRef.current?.focus();
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

  function handleDictatePressStart(event) {
    event.preventDefault();
    if (isRecording) {
      return;
    }
    startRecording();
  }

  function handleDictatePressEnd(event) {
    event.preventDefault();
    if (!isRecording) {
      return;
    }
    stopRecording();
    chatInputRef.current?.focus();
  }

  function handleClearChatInput() {
    setChatInput("");
    chatInputRef.current?.focus();
  }

  const visibleMessages = messages.slice(2);
  const chatStatus = initializingChat ? "Initializing..." : chatBusy ? "Thinking..." : isRecording ? "Recording..." : "Ready";

  return (
    <div className="dashboard-full">
      <header className="dashboard-header">
        <div>
          <p className="eyebrow">Overview</p>
          <h2>AI Inventory Assistant</h2>
          <p className="page-description">Demonstrates how to integrate AI into applications by using Model Context Protocol.</p>
        </div>
      </header>

      <section className="dashboard-split">
        <ChatPanel
          chatStatus={chatStatus}
          messages={visibleMessages}
          chatInput={chatInput}
          onChatInputChange={setChatInput}
          onClearInput={handleClearChatInput}
          onChatInputKeyDown={(event) => {
            if (event.key === "Enter" && !event.shiftKey) {
              event.preventDefault();
              handleSendMessage();
            }
          }}
          chatInputRef={chatInputRef}
          messageEndRef={messageEndRef}
          isRecording={isRecording}
          sttBusy={sttBusy}
          initializingChat={initializingChat}
          chatBusy={chatBusy}
          onDictatePressStart={handleDictatePressStart}
          onDictatePressEnd={handleDictatePressEnd}
          onSend={handleSendMessage}
          error={error}
        />
        <InventoryPanel
          items={items}
          loading={loadingItems}
          readOnly
          showUpdated={false}
        />
      </section>
    </div>
  );
}
