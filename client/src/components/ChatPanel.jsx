import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

export default function ChatPanel({
  chatStatus,
  messages,
  chatInput,
  onChatInputChange,
  onClearInput,
  chatInputRef,
  messageEndRef,
  isRecording,
  sttBusy,
  initializingChat,
  chatBusy,
  onDictatePressStart,
  onDictatePressEnd,
  onSend,
  error
}) {
  function handleComposerSubmit(event) {
    event.preventDefault();
    onSend();
  }

  function handleChatInputKeyDown(event) {
    const isPlainEnter =
      (event.key === "Enter" || event.code === "Enter" || event.code === "NumpadEnter") &&
      !event.shiftKey &&
      !event.nativeEvent.isComposing;

    if (!isPlainEnter) {
      return;
    }

    event.preventDefault();
    onSend();
  }

  return (
    <article className="card pane chat-pane">
      <div className="pane-header">
        <h3>Assistant Chat</h3>
        <span className="muted-text">{chatStatus}</span>
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

      <form className="chat-composer" onSubmit={handleComposerSubmit}>
        <div className="chat-input-wrap">
          <textarea
            ref={chatInputRef}
            rows={3}
            placeholder="Type your inventory question..."
            value={chatInput}
            onChange={(event) => onChatInputChange(event.target.value)}
            onKeyDown={handleChatInputKeyDown}
          />
          {chatInput ? (
            <button
              type="button"
              className="chat-clear-button"
              onClick={onClearInput}
              aria-label="Clear chat input"
              title="Clear"
            >
              ×
            </button>
          ) : null}
        </div>
        <div className="button-row">
          <button
            className={isRecording ? "danger-button" : "secondary-button"}
            disabled={initializingChat || chatBusy || sttBusy}
            onMouseDown={onDictatePressStart}
            onMouseUp={onDictatePressEnd}
            onMouseLeave={onDictatePressEnd}
            onTouchStart={onDictatePressStart}
            onTouchEnd={onDictatePressEnd}
            onTouchCancel={onDictatePressEnd}
            onKeyDown={(event) => {
              if (event.key === " " || event.key === "Enter") {
                onDictatePressStart(event);
              }
            }}
            onKeyUp={(event) => {
              if (event.key === " " || event.key === "Enter") {
                onDictatePressEnd(event);
              }
            }}
          >
            {sttBusy ? "Transcribing..." : isRecording ? "Release to stop" : "Hold to dictate"}
          </button>
          <button
            className="primary-button"
            type="submit"
            disabled={initializingChat || chatBusy || sttBusy || !chatInput.trim()}
          >
            Send
          </button>
        </div>
      </form>
      {error ? <p className="error-text">{error}</p> : null}
    </article>
  );
}
