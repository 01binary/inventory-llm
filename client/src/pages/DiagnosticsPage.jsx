import { useEffect, useState } from "react";
import PageHeader from "../components/PageHeader";
import StatusPill from "../components/StatusPill";
import { api } from "../services/api";

function DiagnosticRow({ label, check }) {
  return (
    <article className="diagnostic-row">
      <StatusPill ok={check?.isHealthy} label={check?.isHealthy ? "Healthy" : "Needs attention"} />
      <div>
        <h4>{label}</h4>
        <p>{check?.message || "No data"}</p>
      </div>
    </article>
  );
}

export default function DiagnosticsPage() {
  const [health, setHealth] = useState(null);
  const [config, setConfig] = useState(null);
  const [error, setError] = useState("");

  const mcpClientConfig = config?.mcpServerUrl
    ? JSON.stringify(
        {
          mcpServers: {
            "inventory-demo": {
              url: config.mcpServerUrl
            }
          }
        },
        null,
        2
      )
    : "Loading...";

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
        description="Health view across the API, SQLite, and the LM Studio server."
      />

      <section className="card">
        {health ? (
          <>
            {health.overallHealthy
              ? <h2 className="diagnostics-header">All healthy</h2>
              : <h2 className="diagnostics-header danger-text">Issues detected</h2>}
          </>
        ) : null}
        <div className="diagnostics-list">
          <DiagnosticRow label="App API" check={health?.app} />
          <DiagnosticRow label="SQLite" check={health?.database} />
          <DiagnosticRow label="LM Studio" check={health?.llm} />
        </div>
      </section>

      <section className="card">
        <h3>Effective configuration</h3>
        <pre className="config-block">{config ? JSON.stringify(config, null, 2) : "Loading..."}</pre>
      </section>

      <section className="card">
        <h3>MCP client config (copy/paste)</h3>
        <pre className="config-block">{mcpClientConfig}</pre>
      </section>

      {error ? <p className="error-text">{error}</p> : null}
    </div>
  );
}
