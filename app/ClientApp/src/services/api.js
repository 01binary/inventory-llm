const JSON_HEADERS = {
  "Content-Type": "application/json"
};

async function handleResponse(response) {
  if (!response.ok) {
    let message = `Request failed with HTTP ${response.status}`;
    try {
      const body = await response.json();
      message = body.message || message;
    } catch {
      // Keep fallback message.
    }
    throw new Error(message);
  }

  const contentType = response.headers.get("content-type") || "";
  if (contentType.includes("application/json")) {
    return response.json();
  }

  return response;
}

export const api = {
  getHealth() {
    return fetch("/api/health").then(async (response) => response.json());
  },
  getConfig() {
    return fetch("/api/config").then(handleResponse);
  },
  getItems() {
    return fetch("/api/items").then(handleResponse);
  },
  getItem(id) {
    return fetch(`/api/items/${id}`).then(handleResponse);
  },
  createItem(payload) {
    return fetch("/api/items", {
      method: "POST",
      headers: JSON_HEADERS,
      body: JSON.stringify(payload)
    }).then(handleResponse);
  },
  updateItem(id, payload) {
    return fetch(`/api/items/${id}`, {
      method: "PUT",
      headers: JSON_HEADERS,
      body: JSON.stringify(payload)
    }).then(handleResponse);
  },
  deleteItem(id) {
    return fetch(`/api/items/${id}`, { method: "DELETE" }).then((response) => {
      if (!response.ok) {
        throw new Error(`Delete failed with HTTP ${response.status}`);
      }
    });
  },
  getTransactions() {
    return fetch("/api/transactions").then(handleResponse);
  },
  getSystemPrompt() {
    return fetch("/api/chat/system-prompt").then(handleResponse);
  },
  completeChat(payload) {
    const requestBody = typeof payload === "string"
      ? { prompt: payload, maxTokens: 128 }
      : {
          prompt: payload.prompt ?? "",
          messages: payload.messages ?? null,
          maxTokens: payload.maxTokens ?? 128
        };

    return fetch("/api/chat/complete", {
      method: "POST",
      headers: JSON_HEADERS,
      body: JSON.stringify(requestBody)
    }).then(handleResponse);
  },
  transcribeAudio(file) {
    const formData = new FormData();
    formData.append("audio", file, file.name);
    return fetch("/api/voice/transcribe-proxy", {
      method: "POST",
      body: formData
    }).then(handleResponse);
  },
  speak(text) {
    return fetch("/api/voice/speak", {
      method: "POST",
      headers: JSON_HEADERS,
      body: JSON.stringify({ text })
    }).then(async (response) => {
      if (!response.ok) {
        throw new Error(`Speech request failed with HTTP ${response.status}`);
      }
      return response.blob();
    });
  }
};
