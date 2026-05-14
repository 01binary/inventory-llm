INSERT INTO items (sku, name, quantity, created_utc, updated_utc)
VALUES
    ('NABU', 'Nestlé Abuelita Hot Chocolate', 25, datetime('now'), datetime('now')),
    ('CLAM', 'Clamato El Original 16Oz', 12, datetime('now'), datetime('now')),
    ('MARU', 'Maruchan Instant Lunch Chicken Flavor', 8, datetime('now'), datetime('now')),
    ('MENU', 'Juanita''s Menudo Picoso', 40, datetime('now'), datetime('now')),
    ('ARIE', 'Ariel Poder Y Cuidado', 6, datetime('now'), datetime('now')),
    ('FABU', 'Fabuloso Multi-Purpose Cleaner 56 Oz', 15, datetime('now'), datetime('now')),
    ('FOCA', 'Foca Detergente Liquido 1L', 30, datetime('now'), datetime('now')),
    ('ROMA', 'Roma Detergente Liquido 33 Oz', 20, datetime('now'), datetime('now')),
    ('BOCH', 'Botanas Chicharron Casero', 18, datetime('now'), datetime('now')),
    ('LATO', 'La Rosa Tostadas', 18, datetime('now'), datetime('now')),
    ('GUTO', 'Guerrero Tostadas Caseras Amarillas', 5, datetime('now'), datetime('now')),
    ('LECH', 'El Super Leon Churritos Mexicanos', 3, datetime('now'), datetime('now')),
    ('JARM', 'Jarritos Mango 1.5L', 3, datetime('now'), datetime('now')),
    ('MCMY', 'McCormick Mayonesa Con Jugo de Limones', 7, datetime('now'), datetime('now')),
    ('DOMV', 'Dona Maria Mole Verde 8.25 oz', 7, datetime('now'), datetime('now'));

INSERT INTO inventory_transactions (item_id, transaction_type, quantity_delta, note, created_utc)
VALUES
    (1, 'seed', 8, 'Initial quantity', datetime('now')),
    (2, 'seed', 12, 'Initial quantity', datetime('now')),
    (3, 'seed', 25, 'Initial quantity', datetime('now')),
    (4, 'seed', 40, 'Initial quantity', datetime('now')),
    (5, 'seed', 6, 'Initial quantity', datetime('now')),
    (1, 'adjustment', -1, 'Sold to customer', datetime('now')),
    (4, 'adjustment', -5, 'Sold to restaurant', datetime('now')),
    (5, 'adjustment', 2, 'Received replenishment', datetime('now'));
