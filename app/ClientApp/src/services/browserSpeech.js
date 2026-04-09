const DEFAULT_STT_LANGUAGE = "es-MX";
const DEFAULT_TTS_LANGUAGE = "es-MX";

function getSpeechRecognitionConstructor() {
  return window.SpeechRecognition || window.webkitSpeechRecognition;
}

export function startBrowserSpeechRecognition({
  language = DEFAULT_STT_LANGUAGE,
  continuous = true,
  autoStopMs = 15000,
  onResult,
  onError,
  onEnd
} = {}) {
  const SpeechRecognition = getSpeechRecognitionConstructor();
  if (!SpeechRecognition) {
    throw new Error("Speech recognition is not supported in this browser. Use Chrome.");
  }

  const recognition = new SpeechRecognition();
  recognition.lang = language;
  recognition.continuous = continuous;
  recognition.interimResults = false;
  recognition.maxAlternatives = 1;
  const stopTimer = autoStopMs > 0 ? window.setTimeout(() => recognition.stop(), autoStopMs) : null;

  recognition.onresult = (event) => {
    const result = event.results?.[event.results.length - 1];
    const transcript = result?.[0]?.transcript?.trim() || "";
    if (transcript) {
      onResult?.(transcript);
    }
  };

  recognition.onerror = (event) => {
    if (stopTimer) {
      window.clearTimeout(stopTimer);
    }
    const code = event?.error || "unknown";
    onError?.(`Speech recognition failed: ${code}`);
  };

  recognition.onend = () => {
    if (stopTimer) {
      window.clearTimeout(stopTimer);
    }
    onEnd?.();
  };
  recognition.start();
  return recognition;
}

export function getBrowserVoices() {
  if (!("speechSynthesis" in window)) {
    return [];
  }
  return window.speechSynthesis.getVoices();
}

export function subscribeToVoiceChanges(handler) {
  if (!("speechSynthesis" in window)) {
    return () => {};
  }

  if (typeof window.speechSynthesis.addEventListener === "function") {
    window.speechSynthesis.addEventListener("voiceschanged", handler);
    return () => window.speechSynthesis.removeEventListener("voiceschanged", handler);
  }

  const previous = window.speechSynthesis.onvoiceschanged;
  window.speechSynthesis.onvoiceschanged = () => {
    previous?.();
    handler();
  };
  return () => {
    window.speechSynthesis.onvoiceschanged = previous || null;
  };
}

export function speakWithBrowser(text, languageOrOptions = DEFAULT_TTS_LANGUAGE) {
  return new Promise((resolve, reject) => {
    if (!("speechSynthesis" in window)) {
      reject(new Error("Text to speech is not supported in this browser."));
      return;
    }

    const content = (text || "").trim();
    if (!content) {
      resolve();
      return;
    }

    window.speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(content);
    const options = typeof languageOrOptions === "string"
      ? { language: languageOrOptions, voiceURI: "" }
      : {
          language: languageOrOptions?.language || DEFAULT_TTS_LANGUAGE,
          voiceURI: languageOrOptions?.voiceURI || ""
        };

    const selectedVoice = getBrowserVoices().find((voice) => voice.voiceURI === options.voiceURI);
    if (selectedVoice) {
      utterance.voice = selectedVoice;
      utterance.lang = selectedVoice.lang;
    } else {
      utterance.lang = options.language;
    }
    utterance.onend = () => resolve();
    utterance.onerror = (event) => reject(new Error(`Text to speech failed: ${event?.error || "unknown"}`));
    window.speechSynthesis.speak(utterance);
  });
}

export function stopBrowserSpeech() {
  if ("speechSynthesis" in window) {
    window.speechSynthesis.cancel();
  }
}
