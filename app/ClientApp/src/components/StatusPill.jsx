export default function StatusPill({ ok, label }) {
  return <span className={ok ? "status-pill ok" : "status-pill bad"}>{label}</span>;
}
