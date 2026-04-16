import { Link } from "react-router-dom";

function formatTimestamp(value) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "-";
  }

  return date.toLocaleString();
}

export default function InventoryList({
  items,
  readOnly = true,
  showUpdated = true,
  onDelete
}) {
  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>SKU</th>
            <th>Name</th>
            <th>Quantity</th>
            {showUpdated ? <th>Updated</th> : null}
            {readOnly ? null : <th></th>}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id}>
              <td><span className="sku-code">{item.sku}</span></td>
              <td>{item.name}</td>
              <td>{item.quantity}</td>
              {showUpdated ? <td>{formatTimestamp(item.updatedUtc)}</td> : null}
              {readOnly ? null : (
                <td>
                  <Link
                    className="text-button"
                    to={`/inventory/${item.id}/edit`}
                  >
                    Edit
                  </Link>
                  {' | '}
                  <button
                    className="text-button danger-text"
                    onClick={() => onDelete?.(item.id)}
                  >
                    Delete
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
