"""
Generates FEW_SHOT_PROMPTS.json: a list of multi-turn conversations that
demonstrate real tool calls against the MCP tools defined in
server/Services/InventoryMcpTools.cs (see ../TOOLS.json for their schema).

Each conversation is {"messages": [...]} with no system message - data-prep.py
prepends SYSTEM_PROMPT.md to every conversation when it compiles training.jsonl.

Run: python training/generate_few_shot_prompts.py
"""

import json
from pathlib import Path

root_directory = Path(__file__).resolve().parent.parent
output_path = root_directory / "FEW_SHOT_PROMPTS.json"

UPDATED_UTC = "2026-09-20T10:15:00Z"

# Mirrors db/002_seed.sql
CATALOG = [
    {"id": 1, "sku": "NABU", "name": "Nestlé Abuelita Hot Chocolate", "quantity": 25},
    {"id": 2, "sku": "CLAM", "name": "Clamato El Original 16Oz", "quantity": 12},
    {"id": 3, "sku": "MARU", "name": "Maruchan Instant Lunch Chicken Flavor", "quantity": 8},
    {"id": 4, "sku": "MENU", "name": "Juanita's Menudo Picoso", "quantity": 40},
    {"id": 5, "sku": "ARIE", "name": "Ariel Poder Y Cuidado", "quantity": 6},
    {"id": 6, "sku": "FABU", "name": "Fabuloso Multi-Purpose Cleaner 56 Oz", "quantity": 15},
    {"id": 7, "sku": "FOCA", "name": "Foca Detergente Liquido 1L", "quantity": 30},
    {"id": 8, "sku": "ROMA", "name": "Roma Detergente Liquido 33 Oz", "quantity": 20},
    {"id": 9, "sku": "BOCH", "name": "Botanas Chicharron Casero", "quantity": 18},
    {"id": 10, "sku": "LATO", "name": "La Rosa Tostadas", "quantity": 18},
    {"id": 11, "sku": "GUTO", "name": "Guerrero Tostadas Caseras Amarillas", "quantity": 5},
    {"id": 12, "sku": "LECH", "name": "El Super Leon Churritos Mexicanos", "quantity": 3},
    {"id": 13, "sku": "JARM", "name": "Jarritos Mango 1.5L", "quantity": 3},
    {"id": 14, "sku": "MCMY", "name": "McCormick Mayonesa Con Jugo de Limones", "quantity": 7},
    {"id": 15, "sku": "DOMV", "name": "Dona Maria Mole Verde 8.25 oz", "quantity": 7},
]
BY_SKU = {item["sku"]: item for item in CATALOG}
LOW_STOCK_THRESHOLD = 5

# One unambiguous search word per product, used for single-match lookups/orders.
SEARCH_WORD = {
    "NABU": "abuelita",
    "CLAM": "clamato",
    "MARU": "maruchan",
    "MENU": "menudo",
    "ARIE": "ariel",
    "FABU": "fabuloso",
    "FOCA": "foca detergente",
    "ROMA": "roma detergente",
    "BOCH": "chicharron",
    "LATO": "la rosa tostadas",
    "GUTO": "guerrero tostadas",
    "LECH": "churritos",
    "JARM": "jarritos",
    "MCMY": "mayonesa",
    "DOMV": "mole verde",
}

AMBIGUOUS_QUERIES = {
    "tostadas": ["LATO", "GUTO"],
    "detergente": ["FOCA", "ROMA"],
}

NO_MATCH_QUERIES = ["queso fresco", "tortillas de harina", "salsa verde enlatada", "pan dulce", "cafe de olla"]

# word -> integer, for exercising quantities written as English words
WORD_QUANTITIES = {
    "two": 2, "a couple of": 2, "three": 3, "four": 4, "five": 5,
    "half a dozen": 6, "six": 6, "eight": 8, "ten": 10, "a dozen": 12, "two dozen": 24,
}


def user_msg(text):
    return {"role": "user", "content": text}


def assistant_msg(text):
    return {"role": "assistant", "content": text}


def tool_call_msg(name, arguments):
    # Plain text, not a "tool_calls" field or "tool" role - see the comment in
    # data-prep.py for why: not every base model's chat template supports those.
    call = {"name": name, "arguments": arguments}
    return {"role": "assistant", "content": f"<tool_call>\n{json.dumps(call)}\n</tool_call>"}


def tool_result_msg(result):
    return {"role": "user", "content": f"<tool_response>\n{json.dumps(result)}\n</tool_response>"}


def search_result(skus, query):
    items = [BY_SKU[sku] for sku in skus]
    return {
        "found": len(items) > 0,
        "query": query,
        "count": len(items),
        "items": [
            {
                "id": item["id"],
                "sku": item["sku"],
                "name": item["name"],
                "quantity": item["quantity"],
                "inStock": item["quantity"] > 0,
            }
            for item in items
        ],
    }


def order_resolved_products(lines):
    return [
        {
            "requestedName": query,
            "sku": BY_SKU[sku]["sku"],
            "name": BY_SKU[sku]["name"],
            "quantity": quantity,
        }
        for sku, quantity, query in lines
    ]


def order_details(order_number, lines, line_id_start=1):
    items = [
        {
            "id": line_id_start + i,
            "orderNumber": order_number,
            "sku": sku,
            "quantity": quantity,
            "itemName": BY_SKU[sku]["name"],
            "createdUtc": UPDATED_UTC,
            "updatedUtc": UPDATED_UTC,
        }
        for i, (sku, quantity, _query) in enumerate(lines)
    ]
    return {
        "orderNumber": order_number,
        "createdUtc": UPDATED_UTC,
        "updatedUtc": UPDATED_UTC,
        "lineCount": len(items),
        "totalQuantity": sum(item["quantity"] for item in items),
        "items": items,
    }


conversations = []


def add(messages):
    conversations.append({"messages": messages})


# --- inventory_list_items -------------------------------------------------

LIST_PHRASINGS = [
    "Give me a quick list of current inventory.",
    "Show me all the stock we have today.",
    "What's in inventory right now?",
    "Can you pull up the full inventory list?",
    "List everything we currently have in stock.",
]

for phrasing in LIST_PHRASINGS:
    result = {
        "total": len(CATALOG),
        "returned": len(CATALOG),
        "items": [
            {
                "id": item["id"],
                "sku": item["sku"],
                "name": item["name"],
                "quantity": item["quantity"],
                "updatedUtc": UPDATED_UTC,
            }
            for item in CATALOG
        ],
    }
    low_stock = [item for item in CATALOG if item["quantity"] <= LOW_STOCK_THRESHOLD]
    low_stock_names = ", ".join(f"{item['name']} ({item['quantity']} left)" for item in low_stock)
    add([
        user_msg(phrasing),
        tool_call_msg("inventory_list_items", {}),
        tool_result_msg(result),
        assistant_msg(
            f"You currently have {len(CATALOG)} products in inventory. "
            f"Low-stock items to watch: {low_stock_names}."
        ),
    ])

# Inventory summary / restock recommendation, explicitly asking for analysis.
add([
    user_msg("Can you give me an inventory summary and flag anything low?"),
    tool_call_msg("inventory_list_items", {}),
    tool_result_msg({
        "total": len(CATALOG),
        "returned": len(CATALOG),
        "items": [
            {"id": i["id"], "sku": i["sku"], "name": i["name"], "quantity": i["quantity"], "updatedUtc": UPDATED_UTC}
            for i in CATALOG
        ],
    }),
    assistant_msg(
        "Overall stock looks healthy across most items. Three products are running low and worth restocking soon: "
        "Guerrero Tostadas Caseras Amarillas (5 left), El Super Leon Churritos Mexicanos (3 left), and "
        "Jarritos Mango 1.5L (3 left). I'd recommend placing restock orders for those three."
    ),
])

# --- inventory_search_status: single match ---------------------------------

SEARCH_PHRASINGS = [
    "How much {word} do we have?",
    "Do I have {word} in my inventory?",
    "What's the current stock on {word}?",
    "Check the status of {word} for me.",
]

for i, (sku, word) in enumerate(SEARCH_WORD.items()):
    phrasing = SEARCH_PHRASINGS[i % len(SEARCH_PHRASINGS)]
    item = BY_SKU[sku]
    add([
        user_msg(phrasing.format(word=word)),
        tool_call_msg("inventory_search_status", {"query": word}),
        tool_result_msg(search_result([sku], word)),
        assistant_msg(
            f"Yes, you have {item['quantity']} units of {item['name']} (SKU {item['sku']}) in stock."
            if item["quantity"] > 0
            else f"{item['name']} (SKU {item['sku']}) is currently out of stock."
        ),
    ])

# --- inventory_search_status: ambiguous match ------------------------------

for query, skus in AMBIGUOUS_QUERIES.items():
    matches = [BY_SKU[sku] for sku in skus]
    options = " or ".join(f"{m['name']} (SKU {m['sku']})" for m in matches)
    add([
        user_msg(f"How much {query} do we have?"),
        tool_call_msg("inventory_search_status", {"query": query}),
        tool_result_msg(search_result(skus, query)),
        assistant_msg(
            f"I found more than one product matching '{query}': {options}. "
            "Which one did you mean?"
        ),
        user_msg(f"I meant the {matches[0]['name']}."),
        assistant_msg(
            f"Got it - {matches[0]['name']} (SKU {matches[0]['sku']}) currently has {matches[0]['quantity']} units in stock."
        ),
    ])

# --- inventory_search_status: no match --------------------------------------

for query in NO_MATCH_QUERIES:
    add([
        user_msg(f"Do we have any {query}?"),
        tool_call_msg("inventory_search_status", {"query": query}),
        tool_result_msg(search_result([], query)),
        assistant_msg(
            f"I couldn't find any product matching '{query}' in inventory. "
            "Could you confirm the name or try a different word?"
        ),
    ])

# --- orders_create: single item, digit and word quantities -----------------

single_order_skus = list(SEARCH_WORD.items())[:10]
word_items = list(WORD_QUANTITIES.items())
for i, (sku, word) in enumerate(single_order_skus):
    item = BY_SKU[sku]
    if i % 2 == 0:
        qty = 3 + i
        qty_text = str(qty)
    else:
        qty_word, qty = word_items[i % len(word_items)]
        qty_text = qty_word
    add([
        user_msg(f"Order {qty_text} {word}."),
        tool_call_msg("orders_create", {"items": [{"productName": word, "quantity": qty}]}),
        tool_result_msg({
            "success": True,
            "message": "Created order 1001.",
            "resolvedProducts": order_resolved_products([(sku, qty, word)]),
            "order": order_details(1001, [(sku, qty, word)]),
        }),
        assistant_msg(
            f"Order placed: {qty} units of {item['name']} (SKU {item['sku']}). Order number 1001."
        ),
    ])

# --- orders_create: multiple items in one request ---------------------------

multi_combos = [
    (("LATO", 12, "la rosa tostadas"), ("BOCH", 6, "chicharron")),
    (("CLAM", 24, "clamato"), ("JARM", "a dozen", "jarritos")),
    (("NABU", 10, "abuelita"), ("MCMY", 5, "mayonesa")),
    (("FABU", 8, "fabuloso"), ("FOCA", 20, "foca detergente")),
    (("MENU", 15, "menudo"), ("ARIE", 6, "ariel")),
    (("DOMV", 7, "mole verde"), ("MARU", 10, "maruchan")),
]

for combo in multi_combos:
    lines = []
    order_phrase_parts = []
    for sku, qty, word in combo:
        if isinstance(qty, str):
            qty = WORD_QUANTITIES[qty]
        lines.append((sku, qty, word))
        order_phrase_parts.append(f"{qty} {word}")
    order_phrase = " and ".join(order_phrase_parts)
    add([
        user_msg(f"Order {order_phrase}."),
        tool_call_msg("orders_create", {
            "items": [{"productName": word, "quantity": qty} for sku, qty, word in lines]
        }),
        tool_result_msg({
            "success": True,
            "message": "Created order 1002.",
            "resolvedProducts": order_resolved_products(lines),
            "order": order_details(1002, lines),
        }),
        assistant_msg(
            "Order placed with " + ", ".join(f"{qty} units of {BY_SKU[sku]['name']}" for sku, qty, _ in lines)
            + ". Order number 1002."
        ),
    ])

# --- multi-turn: create order, then add more items later --------------------

add_more_combos = [
    ((("BOCH", 6, "chicharron"),), (("LATO", 12, "la rosa tostadas"),)),
    ((("CLAM", 12, "clamato"),), (("JARM", 6, "jarritos"), ("NABU", 10, "abuelita"))),
    ((("MENU", 20, "menudo"),), (("MARU", 8, "maruchan"),)),
]

for order_number, (first_lines, added_lines) in enumerate(add_more_combos, start=2001):
    first_phrase = " and ".join(f"{qty} {word}" for _sku, qty, word in first_lines)
    added_phrase = " and ".join(f"{qty} {word}" for _sku, qty, word in added_lines)
    add([
        user_msg(f"Order {first_phrase}."),
        tool_call_msg("orders_create", {
            "items": [{"productName": word, "quantity": qty} for sku, qty, word in first_lines]
        }),
        tool_result_msg({
            "success": True,
            "message": f"Created order {order_number}.",
            "resolvedProducts": order_resolved_products(first_lines),
            "order": order_details(order_number, first_lines),
        }),
        assistant_msg(
            "Order placed with " + ", ".join(f"{qty} units of {BY_SKU[sku]['name']}" for sku, qty, _ in first_lines)
            + f". Order number {order_number}."
        ),
        user_msg(f"Actually, add {added_phrase} to that order too."),
        tool_call_msg("orders_add_items_to_latest", {
            "items": [{"productName": word, "quantity": qty} for sku, qty, word in added_lines]
        }),
        tool_result_msg({
            "success": True,
            "message": f"Updated latest order {order_number}.",
            "resolvedProducts": order_resolved_products(added_lines),
            "order": order_details(order_number, first_lines + added_lines),
        }),
        assistant_msg(
            "Added " + ", ".join(f"{qty} units of {BY_SKU[sku]['name']}" for sku, qty, _ in added_lines)
            + f" to order {order_number}."
        ),
    ])

# --- orders_set_latest_item_quantities (edit an existing line) --------------

edit_cases = [
    (("BOCH", 6, "chicharron"), 10, "Change the chicharron to 10."),
    (("CLAM", 12, "clamato"), 24, "Actually make the clamato two dozen."),
    (("LATO", 12, "la rosa tostadas"), 18, "Set the tostadas order to 18."),
]

for order_number, ((sku, initial_qty, word), new_qty, edit_phrase) in enumerate(edit_cases, start=3001):
    add([
        user_msg(f"Order {initial_qty} {word}."),
        tool_call_msg("orders_create", {"items": [{"productName": word, "quantity": initial_qty}]}),
        tool_result_msg({
            "success": True,
            "message": f"Created order {order_number}.",
            "resolvedProducts": order_resolved_products([(sku, initial_qty, word)]),
            "order": order_details(order_number, [(sku, initial_qty, word)]),
        }),
        assistant_msg(
            f"Order placed: {initial_qty} units of {BY_SKU[sku]['name']}. Order number {order_number}."
        ),
        user_msg(edit_phrase),
        tool_call_msg("orders_set_latest_item_quantities", {
            "items": [{"productName": word, "quantity": new_qty}]
        }),
        tool_result_msg({
            "success": True,
            "message": f"Updated quantities on latest order {order_number}.",
            "resolvedProducts": [{
                "requestedName": word,
                "sku": sku,
                "name": BY_SKU[sku]["name"],
                "previousQuantity": initial_qty,
                "quantity": new_qty,
            }],
            "order": order_details(order_number, [(sku, new_qty, word)]),
        }),
        assistant_msg(
            f"Updated order {order_number}: {BY_SKU[sku]['name']} is now set to {new_qty} units."
        ),
    ])

# --- orders_remove_items_from_latest ----------------------------------------

remove_cases = [
    ((("BOCH", 6, "chicharron"), ("LATO", 12, "la rosa tostadas")), "chicharron", "BOCH"),
    ((("CLAM", 12, "clamato"), ("JARM", 6, "jarritos")), "jarritos", "JARM"),
    ((("MENU", 20, "menudo"), ("MARU", 8, "maruchan")), "maruchan", "MARU"),
]

for order_number, (lines, remove_word, remove_sku) in enumerate(remove_cases, start=4001):
    order_phrase = " and ".join(f"{qty} {word}" for _sku, qty, word in lines)
    remaining_lines = [line for line in lines if line[0] != remove_sku]
    add([
        user_msg(f"Order {order_phrase}."),
        tool_call_msg("orders_create", {
            "items": [{"productName": word, "quantity": qty} for sku, qty, word in lines]
        }),
        tool_result_msg({
            "success": True,
            "message": f"Created order {order_number}.",
            "resolvedProducts": order_resolved_products(lines),
            "order": order_details(order_number, lines),
        }),
        assistant_msg(
            "Order placed with " + ", ".join(f"{qty} units of {BY_SKU[sku]['name']}" for sku, qty, _ in lines)
            + f". Order number {order_number}."
        ),
        user_msg(f"Remove the {remove_word} from that order."),
        tool_call_msg("orders_remove_items_from_latest", {"items": [{"productName": remove_word}]}),
        tool_result_msg({
            "success": True,
            "message": f"Removed items from latest order {order_number}.",
            "removedProducts": [{
                "requestedName": remove_word,
                "sku": remove_sku,
                "name": BY_SKU[remove_sku]["name"],
                "removedQuantity": next(qty for sku, qty, _ in lines if sku == remove_sku),
            }],
            "order": order_details(order_number, remaining_lines) if remaining_lines else None,
        }),
        assistant_msg(
            f"Removed {BY_SKU[remove_sku]['name']} from order {order_number}."
        ),
    ])

# --- inventory_add_transaction (restock / receiving / sale) -----------------

transaction_cases = [
    ("GUTO", "guerrero tostadas", 24, "restock", "We just received 24 more Guerrero tostadas, please add them."),
    ("LECH", "churritos", 30, "restock", "Received a shipment of 30 churritos, log it."),
    ("JARM", "jarritos", 24, "restock", "Add 24 more Jarritos Mango to stock, they just arrived."),
    ("MARU", "maruchan", -3, "sale", "We sold 3 Maruchan instant lunches, deduct them from stock."),
    ("ARIE", "ariel", -2, "sale", "Take 2 Ariel off the shelf, they were sold."),
    ("NABU", "abuelita", -5, "sale", "Deduct 5 units of abuelita hot chocolate, they were sold today."),
]

for sku, word, delta, txn_type, phrase in transaction_cases:
    item = BY_SKU[sku]
    new_quantity = item["quantity"] + delta
    add([
        user_msg(phrase),
        tool_call_msg("inventory_add_transaction", {
            "productName": word,
            "quantityDelta": delta,
            "transactionType": txn_type,
        }),
        tool_result_msg({
            "success": True,
            "transaction": {
                "id": 9001,
                "itemId": item["id"],
                "itemSku": item["sku"],
                "itemName": item["name"],
                "transactionType": txn_type,
                "quantityDelta": delta,
                "note": None,
                "createdUtc": UPDATED_UTC,
            },
            "item": {"id": item["id"], "sku": item["sku"], "name": item["name"], "quantity": new_quantity},
            "resolvedProduct": {"requestedName": word, "sku": item["sku"], "name": item["name"], "quantityDelta": delta},
        }),
        assistant_msg(
            f"Done. {item['name']} (SKU {item['sku']}) is now at {new_quantity} units "
            f"after {'adding' if delta > 0 else 'removing'} {abs(delta)}."
        ),
    ])

# --- clarifying questions that need no tool call ----------------------------

add([
    user_msg("Order some chicharron and tostadas."),
    assistant_msg(
        "I have a quantity for neither item yet. How many units of chicharron and how many of tostadas would you like?"
    ),
])

add([
    user_msg("Order 10 clamato and some jarritos."),
    assistant_msg("Got the 10 clamato. How many jarritos would you like to add?"),
])

add([
    user_msg("Hi, what can you help me with?"),
    assistant_msg(
        "I'm Viva, your inventory assistant. I can check stock levels, flag low-stock items, and help you "
        "create, update, or review purchase orders. What would you like to do?"
    ),
])

add([
    user_msg("What's the weather like today?"),
    assistant_msg(
        "I'm focused on inventory and order tasks for this store, so I can't help with weather. "
        "Is there something about your stock or orders I can help with?"
    ),
])

add([
    user_msg("Can you cancel my last order entirely?"),
    assistant_msg(
        "I can remove individual items from the latest order, but I don't have a tool to cancel an entire order outright. "
        "Would you like me to remove each item from it instead?"
    ),
])

with output_path.open("w", encoding="utf-8") as f:
    json.dump(conversations, f, indent=2, ensure_ascii=False)

print(f"Wrote {len(conversations)} conversations to {output_path}")
