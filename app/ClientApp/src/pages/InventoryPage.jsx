import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
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
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>SKU</th>
                <th>Name</th>
                <th>Quantity</th>
                <th>Unit</th>
                <th>Location</th>
                <th>Updated</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id}>
                  <td>{item.sku}</td>
                  <td>{item.name}</td>
                  <td>{item.quantity}</td>
                  <td>{item.unit}</td>
                  <td>{item.location || "n/a"}</td>
                  <td>{new Date(item.updatedUtc).toLocaleString()}</td>
                  <td className="actions-cell">
                    <Link className="text-button" to={`/inventory/${item.id}/edit`}>Edit</Link>
                    <button className="text-button danger-text" onClick={() => handleDelete(item.id)}>
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {loading ? <p className="muted-text">Loading inventory...</p> : null}
        {error ? <p className="error-text">{error}</p> : null}
      </section>
    </div>
  );
}
