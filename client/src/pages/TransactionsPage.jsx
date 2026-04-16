import { useEffect, useState } from "react";
import PageHeader from "../components/PageHeader";
import { api } from "../services/api";

function formatTransactionTimestamp(value) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "-";
  }

  return date.toLocaleString(undefined, {
    year: "numeric",
    month: "numeric",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit"
  });
}

export default function TransactionsPage() {
  const [transactions, setTransactions] = useState([]);
  const [error, setError] = useState("");

  useEffect(() => {
    api.getTransactions()
      .then(setTransactions)
      .catch((err) => setError(err.message));
  }, []);

  return (
    <div className="stack">
      <PageHeader
        title="Transactions"
        description="All quantity changes recorded by the backend service layer."
      />

      <section className="card">
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>When</th>
                <th>SKU</th>
                <th>Item</th>
                <th>Type</th>
                <th>Delta</th>
                <th>Note</th>
              </tr>
            </thead>
            <tbody>
              {transactions.map((transaction) => (
                <tr key={transaction.id}>
                  <td>{formatTransactionTimestamp(transaction.createdUtc)}</td>
                  <td><span className="sku-code">{transaction.itemSku}</span></td>
                  <td>{transaction.itemName}</td>
                  <td>{transaction.transactionType}</td>
                  <td>{transaction.quantityDelta}</td>
                  <td>{transaction.note || "n/a"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {error ? <p className="error-text">{error}</p> : null}
      </section>
    </div>
  );
}
