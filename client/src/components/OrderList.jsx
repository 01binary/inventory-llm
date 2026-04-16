import { useEffect, useMemo, useState } from "react";

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

export default function OrderList({ orders }) {
  const [openOrderNumbers, setOpenOrderNumbers] = useState(() => new Set());

  const sortedOrders = useMemo(
    () => [...orders].sort((a, b) => b.orderNumber - a.orderNumber),
    [orders]
  );

  useEffect(() => {
    if (sortedOrders.length === 0) {
      setOpenOrderNumbers(new Set());
      return;
    }

    setOpenOrderNumbers((current) => {
      if (current.size > 0) {
        return current;
      }

      return new Set([sortedOrders[0].orderNumber]);
    });
  }, [sortedOrders]);

  function toggleOrder(orderNumber) {
    setOpenOrderNumbers((current) => {
      const next = new Set(current);
      if (next.has(orderNumber)) {
        next.delete(orderNumber);
      } else {
        next.add(orderNumber);
      }

      return next;
    });
  }

  if (sortedOrders.length === 0) {
    return <p className="muted-text">No orders yet.</p>;
  }

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th></th>
            <th>Order #</th>
            <th>Lines</th>
            <th>Total Qty</th>
            <th>Updated</th>
          </tr>
        </thead>
        <tbody>
          {sortedOrders.map((order) => {
            const isOpen = openOrderNumbers.has(order.orderNumber);
            const items = Array.isArray(order.items) ? order.items : [];

            return (
              <FragmentRow
                key={order.orderNumber}
                isOpen={isOpen}
                order={order}
                items={items}
                onToggle={toggleOrder}
              />
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function FragmentRow({ order, items, isOpen, onToggle }) {
  return (
    <>
      <tr>
        <td>
          <button
            type="button"
            className="text-button order-toggle"
            onClick={() => onToggle(order.orderNumber)}
            aria-expanded={isOpen}
          >
            {isOpen ? "−" : "+"}
          </button>
        </td>
        <td>{order.orderNumber}</td>
        <td>{order.lineCount}</td>
        <td>{order.totalQuantity}</td>
        <td>{formatTimestamp(order.updatedUtc)}</td>
      </tr>
      {isOpen ? (
        <tr>
          <td colSpan={5} className="order-items-cell">
            <table className="order-items-table">
              <thead>
                <tr>
                  <th>SKU</th>
                  <th>Item</th>
                  <th>Quantity</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={`${order.orderNumber}-${item.sku}`}>
                    <td><span className="sku-code">{item.sku}</span></td>
                    <td>{item.itemName || "Unknown item"}</td>
                    <td>{item.quantity}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </td>
        </tr>
      ) : null}
    </>
  );
}
