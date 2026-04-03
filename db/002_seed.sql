INSERT INTO items (sku, name, quantity, created_utc, updated_utc)
VALUES
    ('LAP-13-001', '13-inch Laptop', 8, datetime('now'), datetime('now')),
    ('MON-24-002', '24-inch Monitor', 12, datetime('now'), datetime('now')),
    ('MOU-WL-003', 'Wireless Mouse', 25, datetime('now'), datetime('now')),
    ('CAB-USBC-004', 'USB-C Cable 2m', 40, datetime('now'), datetime('now')),
    ('LAB-RED-005', 'Red Shipping Labels', 6, datetime('now'), datetime('now'));

INSERT INTO inventory_transactions (item_id, transaction_type, quantity_delta, note, created_utc)
VALUES
    (1, 'seed', 8, 'Initial seed quantity', datetime('now')),
    (2, 'seed', 12, 'Initial seed quantity', datetime('now')),
    (3, 'seed', 25, 'Initial seed quantity', datetime('now')),
    (4, 'seed', 40, 'Initial seed quantity', datetime('now')),
    (5, 'seed', 6, 'Initial seed quantity', datetime('now')),
    (1, 'adjustment', -1, 'Allocated to repair bench', datetime('now')),
    (4, 'adjustment', -5, 'Packed in starter kits', datetime('now')),
    (5, 'adjustment', 2, 'Received replenishment', datetime('now'));
