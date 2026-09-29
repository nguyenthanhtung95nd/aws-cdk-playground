import { useState } from 'react';
import type { NewOrder } from '../types/order';

type Field = 'customerName' | 'product' | 'amount' | 'notes';

type FormValues = Record<Field, string>;

type FieldErrors = Partial<Record<Field, string>>;

const emptyForm: FormValues = { customerName: '', product: '', amount: '', notes: '' };

const labels: Record<Field, string> = {
  customerName: 'Customer',
  product: 'Product',
  amount: 'Amount',
  notes: 'Notes',
};

export interface PlaceOrderFormProps {
  onPlace: (order: NewOrder) => Promise<void>;
}

function findMissing(values: FormValues): FieldErrors {
  const errors: FieldErrors = {};
  for (const field of ['customerName', 'product', 'amount'] as const) {
    if (values[field].trim().length === 0) {
      errors[field] = `${labels[field]} is required.`;
    }
  }
  return errors;
}

export function PlaceOrderForm({ onPlace }: PlaceOrderFormProps) {
  const [values, setValues] = useState<FormValues>(emptyForm);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [submitting, setSubmitting] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);

  const update = (field: Field) => (event: React.ChangeEvent<HTMLInputElement>) => {
    const next = event.target.value;
    setValues((current) => ({ ...current, [field]: next }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
  };

  async function place() {
    setFailure(null);

    const missing = findMissing(values);
    if (Object.keys(missing).length > 0) {
      setFieldErrors(missing);
      return;
    }

    setSubmitting(true);
    try {
      await onPlace({
        customerName: values.customerName,
        product: values.product,
        amount: Number(values.amount),
        notes: values.notes,
      });
      setValues(emptyForm);
    } catch (cause) {
      setFailure(cause instanceof Error ? cause.message : 'Could not place the order');
    } finally {
      setSubmitting(false);
    }
  }

  function renderField(field: Field, type = 'text') {
    const error = fieldErrors[field];
    const errorId = `${field}-error`;

    return (
      <>
        <label htmlFor={field}>{labels[field]}</label>
        <div className="field">
          <input
            id={field}
            type={type}
            value={values[field]}
            onChange={update(field)}
            required={field !== 'notes'}
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? errorId : undefined}
          />
          {error ? (
            <p className="field-error" id={errorId}>
              {error}
            </p>
          ) : null}
        </div>
      </>
    );
  }

  return (
    <form
      className="place-order"
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        void place();
      }}
    >
      {renderField('customerName')}
      {renderField('product')}
      {renderField('amount', 'number')}
      {renderField('notes')}

      <button type="submit" disabled={submitting}>
        {submitting ? 'Placing...' : 'Place order'}
      </button>

      {failure ? (
        <p className="failure" role="alert">
          {failure}
        </p>
      ) : null}
    </form>
  );
}
