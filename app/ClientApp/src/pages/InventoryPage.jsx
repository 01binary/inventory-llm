import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import InventoryList from "../components/InventoryList";
import PageHeader from "../components/PageHeader";
import { api } from "../services/api";

export default function InventoryPage() {
  const [items, setItems] = useState([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  function loadItems() {
    setLoading(true);
    api.getItems()
      .then(setItems)
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    loadItems();
  }, []);

  async function handleDelete(id) {
    if (!window.confirm("Delete this item?")) {
      return;
    }

    try {
      await api.deleteItem(id);
      loadItems();
    } catch (err) {
      setError(err.message);
    }
  }

  return (
    <div className="stack">
      <PageHeader
        eyebrow="Inventory"
        title="Inventory list"
        description="SQLite-backed inventory managed by the ASP.NET Core API."
        actions={<Link className="primary-button" to="/inventory/new">Add item</Link>}
      />

      <section className="card">
        <InventoryList items={items} readOnly={false} onDelete={handleDelete} />
        {loading ? <p className="muted-text">Loading inventory...</p> : null}
        {error ? <p className="error-text">{error}</p> : null}
      </section>
    </div>
  );
}
