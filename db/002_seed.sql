INSERT INTO items (sku, name, quantity, created_utc, updated_utc)
VALUES
    ('JARR', 'Jarritos', 25, datetime('now'), datetime('now')),
    ('CHOR', 'Olé Chorizo', 12, datetime('now'), datetime('now')),
    ('MASA', 'Masa Flour', 8, datetime('now'), datetime('now')),
    ('CHOL', 'Salsa Picante Cholula Original', 40, datetime('now'), datetime('now')),
    ('CHAY', 'Chayotes Squash', 6, datetime('now'), datetime('now')),
    ('PEP15', 'Pepsi 15 oz', 15, datetime('now'), datetime('now')),
    ('TORT', 'Tortillas', 30, datetime('now'), datetime('now')),
    ('SALS', 'Salsa Verde', 20, datetime('now'), datetime('now')),
    ('FRIJ', 'Frijoles Refritos', 18, datetime('now'), datetime('now'));

INSERT INTO inventory_transactions (item_id, transaction_type, quantity_delta, note, created_utc)
VALUES
    (1, 'seed', 8, 'Initial seed quantity', datetime('now')),
    (2, 'seed', 12, 'Initial seed quantity', datetime('now')),
    (3, 'seed', 25, 'Initial seed quantity', datetime('now')),
    (4, 'seed', 40, 'Initial seed quantity', datetime('now')),
    (5, 'seed', 6, 'Initial seed quantity', datetime('now')),
    (1, 'adjustment', -1, 'Sold to customer', datetime('now')),
    (4, 'adjustment', -5, 'Sold to restaurant', datetime('now')),
    (5, 'adjustment', 2, 'Received replenishment', datetime('now'));
