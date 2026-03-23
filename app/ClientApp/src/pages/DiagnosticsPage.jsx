import { useEffect, useState } from "react";
import PageHeader from "../components/PageHeader";
import StatusPill from "../components/StatusPill";
import { api } from "../services/api";

function DiagnosticRow({ label, check }) {
  return (
    <article className="diagnostic-row">
      <div>
        <h4>{label}</h4>
        <p>{check?.message || "No data"}</p>
      </div>
      <StatusPill ok={check?.isHealthy} label={check?.isHealthy ? "Healthy" : "Needs attention"} />
    </article>
  );
}

export default function DiagnosticsPage() {
  const [health, setHealth] = useState(null);
  const [config, setConfig] = useState(null);
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.all([api.getHealth(), api.getConfig()])
      .then(([healthResponse, configResponse]) => {
        setHealth(healthResponse);
        setConfig(configResponse);
      })
      .catch((err) => setError(err.message));
  }, []);

  return (
    <div className="stack">
      <PageHeader
        eyebrow="Operations"
        title="Diagnostics"
        description="Health view across the API, SQLite, the LM Studio server, whisper.cpp, and Piper."
      />

      <section className="card">
        <div className="panel-header-row">
          <h3>Status</h3>
          {health ? (
            <StatusPill ok={health.overallHealthy} label={health.overallHealthy ? "All healthy" : "Issues detected"} />
          ) : null}
        </div>
        <div className="diagnostics-list">
          <DiagnosticRow label="App API" check={health?.app} />
          <DiagnosticRow label="SQLite" check={health?.database} />
          <DiagnosticRow label="LM Studio" check={health?.llm} />
          <DiagnosticRow label="whisper.cpp" check={health?.stt} />
          <DiagnosticRow label="Piper executable" check={health?.piperExecutable} />
          <DiagnosticRow label="Piper voice model" check={health?.piperVoiceModel} />
        </div>
      </section>

      <section className="card">
        <h3>Effective configuration</h3>
        <pre className="config-block">{config ? JSON.stringify(config, null, 2) : "Loading..."}</pre>
      </section>

      {error ? <p className="error-text">{error}</p> : null}
    </div>
  );
}
