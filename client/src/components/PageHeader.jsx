export default function PageHeader({ title, description, actions }) {
  return (
    <header className="page-header">
      <div>
        <h2>{title}</h2>
        {description ? <p className="page-description">{description}</p> : null}
      </div>
      {actions ? <div className="page-actions">{actions}</div> : null}
    </header>
  );
}
