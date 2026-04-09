import { useEffect, useState } from "react";
import OrderList from "../components/OrderList";
import PageHeader from "../components/PageHeader";
import { api } from "../services/api";

export default function OrdersPage() {
  const [orders, setOrders] = useState([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  function loadOrders() {
    setLoading(true);
    setError("");
    api.getOrders(50)
      .then(setOrders)
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    loadOrders();
  }, []);

  return (
    <div className="stack">
      <PageHeader
        eyebrow="Ordering"
        title="Orders"
        description="View created orders and expand each order to inspect its item lines."
      />

      <section className="card">
        <OrderList orders={orders} />
        {loading ? <p className="muted-text">Loading orders...</p> : null}
        {error ? <p className="error-text">{error}</p> : null}
      </section>
    </div>
  );
}
