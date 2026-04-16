const FALLBACK_STT_LANGUAGE = "en-US";
const FALLBACK_TTS_LANGUAGE = "en-US";
const FALLBACK_TTS_VOICE_NAME = "Google US English";

let defaultSttLanguage = FALLBACK_STT_LANGUAGE;
let defaultTtsLanguage = FALLBACK_TTS_LANGUAGE;
let preferredTtsVoiceName = FALLBACK_TTS_VOICE_NAME;

export function configureBrowserSpeechDefaults(config = {}) {
  defaultSttLanguage = (config.defaultSttLanguage || FALLBACK_STT_LANGUAGE).trim() || FALLBACK_STT_LANGUAGE;
  defaultTtsLanguage = (config.defaultTtsLanguage || FALLBACK_TTS_LANGUAGE).trim() || FALLBACK_TTS_LANGUAGE;
  preferredTtsVoiceName = (config.preferredTtsVoiceName || FALLBACK_TTS_VOICE_NAME).trim() || FALLBACK_TTS_VOICE_NAME;
}

export function getBrowserSpeechDefaults() {
  return {
    defaultSttLanguage,
    defaultTtsLanguage,
    preferredTtsVoiceName
  };
}

function getSpeechRecognitionConstructor() {
  return window.SpeechRecognition || window.webkitSpeechRecognition;
}

export function startBrowserSpeechRecognition({
  language = defaultSttLanguage,
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

export function speakWithBrowser(text, languageOrOptions = defaultTtsLanguage) {
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
      ? { language: languageOrOptions, voiceURI: "", voiceName: "" }
      : {
          language: languageOrOptions?.language || defaultTtsLanguage,
          voiceURI: languageOrOptions?.voiceURI || "",
          voiceName: languageOrOptions?.voiceName || ""
        };

    const voices = getBrowserVoices();
    const normalizedPreferredName = (options.voiceName || "").trim().toLowerCase();
    const selectedVoiceByName = voices.find((voice) => voice.name === options.voiceName);
    const selectedVoiceByNameContains = normalizedPreferredName
      ? voices.find((voice) => voice.name.toLowerCase().includes(normalizedPreferredName))
      : null;
    const selectedVoiceByUri = voices.find((voice) => voice.voiceURI === options.voiceURI);
    const selectedEnglishVoice = voices.find((voice) => voice.lang?.toLowerCase().startsWith("en"));
    const selectedVoice = selectedVoiceByName || selectedVoiceByNameContains || selectedVoiceByUri || selectedEnglishVoice;
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
