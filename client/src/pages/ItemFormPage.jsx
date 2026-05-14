import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import PageHeader from "../components/PageHeader";
import { api } from "../services/api";

const emptyForm = {
  sku: "",
  name: "",
  quantity: 0,
  transactionNote: ""
};

export default function ItemFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const isEditing = Boolean(id);
  const [form, setForm] = useState(emptyForm);
  const [skuValidation, setSkuValidation] = useState(null);
  const [skuValidationBusy, setSkuValidationBusy] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(isEditing);
  const skuValidationRequestRef = useRef(0);

  useEffect(() => {
    if (!isEditing) {
      return;
    }

    api.getItem(id)
      .then((item) => {
        setForm({
          sku: item.sku,
          name: item.name,
          quantity: item.quantity,
          transactionNote: ""
        });
      })
      .catch((err) => setError(err.message))
      .finally(() => setBusy(false));
  }, [id, isEditing]);

  useEffect(() => {
    if (isEditing) {
      return;
    }

    const sku = form.sku.trim();
    if (!sku) {
      setSkuValidation(null);
      setSkuValidationBusy(false);
      return;
    }

    const requestId = skuValidationRequestRef.current + 1;
    skuValidationRequestRef.current = requestId;

    const timer = window.setTimeout(async () => {
      setSkuValidationBusy(true);
      try {
        const result = await api.validateSku(sku);
        if (skuValidationRequestRef.current !== requestId) {
          return;
        }
        setSkuValidation(result);
      } catch (err) {
        if (skuValidationRequestRef.current !== requestId) {
          return;
        }
        setSkuValidation({
          valid: false,
          exists: false,
          message: err.message || "Could not validate SKU."
        });
      } finally {
        if (skuValidationRequestRef.current === requestId) {
          setSkuValidationBusy(false);
        }
      }
    }, 350);

    return () => window.clearTimeout(timer);
  }, [form.sku, isEditing]);

  function updateField(event) {
    const { name, value } = event.target;
    setForm((current) => ({
      ...current,
      [name]: name === "quantity" ? Number(value) : value
    }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setBusy(true);
    setError("");

    try {
      if (isEditing) {
        await api.updateItem(id, form);
      } else {
        await api.createItem(form);
      }
      navigate("/inventory");
    } catch (err) {
      setError(err.message);
      setBusy(false);
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title={isEditing ? "Edit item" : "Add item"}
        description="Simple item editor backed by parameterized SQLite queries."
        actions={<Link className="text-button" to="/inventory">Back to list</Link>}
      />

      <form className="card form-grid" onSubmit={handleSubmit}>
        <label className="form-field">
          <span>SKU</span>
          <input name="sku" value={form.sku} onChange={updateField} required maxLength={64} />
          {!isEditing && form.sku.trim() ? (
            <span className={`sku-validation ${skuValidation?.exists ? "exists" : "new"}`}>
              {skuValidationBusy ? "..." : skuValidation?.exists ? "✓ Existing SKU" : "+ New SKU"}
            </span>
          ) : null}
        </label>

        <label className="form-field">
          <span>Name</span>
          <input name="name" value={form.name} onChange={updateField} required maxLength={200} />
        </label>

        <label className="form-field">
          <span>Quantity</span>
          <input name="quantity" type="number" min="0" value={form.quantity} onChange={updateField} required />
        </label>

        <label className="form-field full-width">
          <span>Transaction note</span>
          <input name="transactionNote" value={form.transactionNote} onChange={updateField} maxLength={400} />
        </label>

        <div className="button-row full-width">
          <button className="primary-button" type="submit" disabled={busy}>
            {busy ? "Saving..." : isEditing ? "Save changes" : "Create item"}
          </button>
        </div>

        {error ? <p className="error-text full-width">{error}</p> : null}
      </form>
    </div>
  );
}
