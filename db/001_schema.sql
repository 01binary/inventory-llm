PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sku TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    quantity INTEGER NOT NULL DEFAULT 0,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS inventory_transactions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    item_id INTEGER NOT NULL,
    transaction_type TEXT NOT NULL,
    quantity_delta INTEGER NOT NULL,
    note TEXT NULL,
    created_utc TEXT NOT NULL,
    FOREIGN KEY(item_id) REFERENCES items(id)
);

CREATE INDEX IF NOT EXISTS idx_inventory_transactions_item_id
    ON inventory_transactions(item_id);

CREATE INDEX IF NOT EXISTS idx_inventory_transactions_created_utc
    ON inventory_transactions(created_utc);
