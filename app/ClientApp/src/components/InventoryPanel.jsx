import InventoryList from "./InventoryList";

export default function InventoryPanel({
  title = "Current Inventory",
  items,
  loading,
  readOnly = true,
  showUpdated = true,
  onDelete
}) {
  return (
    <article className="card pane inventory-pane">
      <div className="pane-header">
        <h3>{title}</h3>
        <span className="muted-text">{loading ? "Loading..." : `${items.length} items`}</span>
      </div>
      <InventoryList items={items} readOnly={readOnly} showUpdated={showUpdated} onDelete={onDelete} />
    </article>
  );
}
