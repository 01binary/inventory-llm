import { useEffect, useState } from "react";
import { NavLink, Route, Routes, matchPath, useLocation } from "react-router-dom";
import DashboardPage from "./pages/DashboardPage";
import InventoryPage from "./pages/InventoryPage";
import ItemFormPage from "./pages/ItemFormPage";
import TransactionsPage from "./pages/TransactionsPage";
import DiagnosticsPage from "./pages/DiagnosticsPage";
import OrdersPage from "./pages/OrdersPage";

const navItems = [
  { to: "/", label: "Dashboard" },
  { to: "/inventory", label: "Inventory" },
  { to: "/inventory/new", label: "Add Item" },
  { to: "/orders", label: "Orders" },
  { to: "/transactions", label: "Transactions" },
  { to: "/diagnostics", label: "Diagnostics" }
];

function getCurrentPageTitle(pathname) {
  if (pathname === "/") {
    return "AI Inventory Assistant";
  }

  if (matchPath("/inventory/:id/edit", pathname)) {
    return "Edit item";
  }

  if (pathname === "/inventory/new") {
    return "Add item";
  }

  if (pathname === "/inventory") {
    return "Inventory list";
  }

  if (pathname === "/orders") {
    return "Orders";
  }

  if (pathname === "/transactions") {
    return "Transactions";
  }

  if (pathname === "/diagnostics") {
    return "Diagnostics";
  }

  return "Inventory Demo";
}

export default function App() {
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);
  const location = useLocation();
  const currentPageTitle = getCurrentPageTitle(location.pathname);

  useEffect(() => {
    setIsMobileNavOpen(false);
  }, [location.pathname]);

  return (
    <div className="app-shell">
      <aside className={`sidebar ${isMobileNavOpen ? "mobile-open" : ""}`}>
        <div className="mobile-nav-header">
          <h1 className="mobile-nav-title">{currentPageTitle}</h1>
          <button
            type="button"
            className="mobile-nav-toggle"
            aria-expanded={isMobileNavOpen}
            aria-controls="main-navigation"
            aria-label="Toggle navigation"
            onClick={() => setIsMobileNavOpen((previous) => !previous)}
          >
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <line x1="4" y1="6.5" x2="20" y2="6.5" />
              <line x1="4" y1="12" x2="20" y2="12" />
              <line x1="4" y1="17.5" x2="20" y2="17.5" />
            </svg>
          </button>
        </div>
        <nav id="main-navigation" className="nav">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/" || item.to === "/inventory" || item.to === "/orders"}
              className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <main className="main-content">
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/inventory" element={<InventoryPage />} />
          <Route path="/inventory/new" element={<ItemFormPage />} />
          <Route path="/inventory/:id/edit" element={<ItemFormPage />} />
          <Route path="/orders" element={<OrdersPage />} />
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route path="/diagnostics" element={<DiagnosticsPage />} />
        </Routes>
      </main>
    </div>
  );
}
