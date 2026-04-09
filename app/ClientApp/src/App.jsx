import { NavLink, Route, Routes } from "react-router-dom";
import DashboardPage from "./pages/DashboardPage";
import InventoryPage from "./pages/InventoryPage";
import ItemFormPage from "./pages/ItemFormPage";
import TransactionsPage from "./pages/TransactionsPage";
import DiagnosticsPage from "./pages/DiagnosticsPage";

const navItems = [
  { to: "/", label: "Dashboard" },
  { to: "/inventory", label: "Inventory" },
  { to: "/inventory/new", label: "Add Item" },
  { to: "/transactions", label: "Transactions" },
  { to: "/diagnostics", label: "Diagnostics" }
];

export default function App() {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <nav className="nav">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/"}
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
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route path="/diagnostics" element={<DiagnosticsPage />} />
        </Routes>
      </main>
    </div>
  );
}
