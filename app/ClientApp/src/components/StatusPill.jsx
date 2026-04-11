import healthyIcon from "../assets/status/healthy.svg";
import unhealthyIcon from "../assets/status/unhealthy.svg";

export default function StatusPill({ ok, label }) {
  const icon = ok ? healthyIcon : unhealthyIcon;

  return (
    <span className="status-pill" title={label}>
      <img className="status-pill-icon" src={icon} alt={label} />
    </span>
  );
}
