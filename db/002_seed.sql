INSERT INTO items (sku, name, description, quantity, location, unit, created_utc, updated_utc)
VALUES
    ('LAP-13-001', '13-inch Laptop', 'Refurbished demo laptops for field technicians.', 8, 'Shelf A1', 'each', datetime('now'), datetime('now')),
    ('MON-24-002', '24-inch Monitor', 'Standard office monitors with HDMI cable included.', 12, 'Shelf A2', 'each', datetime('now'), datetime('now')),
    ('MOU-WL-003', 'Wireless Mouse', 'Battery-powered ergonomic wireless mouse.', 25, 'Bin B4', 'each', datetime('now'), datetime('now')),
    ('CAB-USBC-004', 'USB-C Cable 2m', 'Braided USB-C charging and data cable.', 40, 'Bin C1', 'each', datetime('now'), datetime('now')),
    ('LAB-RED-005', 'Red Shipping Labels', 'Thermal label roll for outbound inventory tags.', 6, 'Cabinet D2', 'roll', datetime('now'), datetime('now'));

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
