import { useEffect, useState } from "react";
import PageHeader from "../components/PageHeader";
import VoiceDemoPanel from "../components/VoiceDemoPanel";
import { api } from "../services/api";

export default function DashboardPage() {
  const [items, setItems] = useState([]);
  const [transactions, setTransactions] = useState([]);
  const [chatPrompt, setChatPrompt] = useState("Summarize the inventory status in one short paragraph.");
  const [chatResult, setChatResult] = useState("");
  const [loading, setLoading] = useState(true);
  const [chatBusy, setChatBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.all([api.getItems(), api.getTransactions()])
      .then(([itemsResponse, transactionsResponse]) => {
        setItems(itemsResponse);
        setTransactions(transactionsResponse);
      })
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  async function handleChat() {
    setChatBusy(true);
    setError("");
    try {
      const response = await api.completeChat(chatPrompt);
      setChatResult(response.text || "");
    } catch (err) {
      setError(err.message);
    } finally {
      setChatBusy(false);
    }
  }

  const lowStock = items.filter((item) => item.quantity <= 5).length;

  return (
    <div className="stack">
      <PageHeader
        eyebrow="Overview"
        title="Dashboard"
        description="A compact local inventory demo with health checks and simple AI integrations."
      />

      <section className="stat-grid">
        <article className="card stat-card">
          <span className="stat-label">Items</span>
          <strong>{loading ? "..." : items.length}</strong>
        </article>
        <article className="card stat-card">
          <span className="stat-label">Low stock items</span>
          <strong>{loading ? "..." : lowStock}</strong>
        </article>
        <article className="card stat-card">
          <span className="stat-label">Transactions</span>
          <strong>{loading ? "..." : transactions.length}</strong>
        </article>
      </section>

      <section className="dashboard-grid">
        <div className="card">
          <h3>Recent inventory activity</h3>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>SKU</th>
                  <th>Name</th>
                  <th>Qty</th>
                </tr>
              </thead>
              <tbody>
                {items.slice(0, 5).map((item) => (
                  <tr key={item.id}>
                    <td>{item.sku}</td>
                    <td>{item.name}</td>
                    <td>{item.quantity}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="card">
          <h3>Chat completion demo</h3>
          <p>Proxies a prompt to the local LM Studio server.</p>
          <label className="form-field">
            <span>Prompt</span>
            <textarea rows={6} value={chatPrompt} onChange={(event) => setChatPrompt(event.target.value)} />
          </label>
          <button className="primary-button" onClick={handleChat} disabled={chatBusy || !chatPrompt.trim()}>
            {chatBusy ? "Generating..." : "Generate"}
          </button>
          <label className="form-field">
            <span>Model response</span>
            <textarea rows={8} value={chatResult} readOnly />
          </label>
        </div>
      </section>

      <VoiceDemoPanel />
      {error ? <p className="error-text">{error}</p> : null}
    </div>
  );
}
