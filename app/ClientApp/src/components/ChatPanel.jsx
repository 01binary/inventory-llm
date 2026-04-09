import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

export default function ChatPanel({
  chatStatus,
  messages,
  chatInput,
  onChatInputChange,
  onChatInputKeyDown,
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

      <div className="chat-composer">
        <textarea
          ref={chatInputRef}
          rows={3}
          placeholder="Type your inventory question..."
          value={chatInput}
          onChange={(event) => onChatInputChange(event.target.value)}
          onKeyDown={onChatInputKeyDown}
        />
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
            onClick={onSend}
            disabled={initializingChat || chatBusy || sttBusy || !chatInput.trim()}
          >
            Send
          </button>
        </div>
      </div>
      {error ? <p className="error-text">{error}</p> : null}
    </article>
  );
}
