import { useState, type ChangeEvent, type FormEvent } from 'react';
import type { NewProduct } from '../types/product';

export function CreateProductForm({ onCreate }: { onCreate: (input: NewProduct) => Promise<void> }) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [price, setPrice] = useState('');
  const [imageData, setImageData] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    setImageData(await readAsDataUrl(file));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (!name.trim() || !description.trim() || !price || !imageData) {
      setError('All fields, including an image, are required.');
      return;
    }

    setSubmitting(true);
    try {
      await onCreate({
        name: name.trim(),
        description: description.trim(),
        price: Number(price),
        imageData,
      });
      setName('');
      setDescription('');
      setPrice('');
      setImageData('');
      event.currentTarget.reset();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create product.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} aria-label="Add product">
      <div className="field">
        <label htmlFor="name">Name</label>
        <input id="name" name="name" value={name} onChange={(e) => setName(e.target.value)} />
      </div>

      <div className="field">
        <label htmlFor="description">Description</label>
        <textarea
          id="description"
          name="description"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </div>

      <div className="field">
        <label htmlFor="price">Price</label>
        <input
          id="price"
          name="price"
          type="number"
          min="0"
          step="0.01"
          value={price}
          onChange={(e) => setPrice(e.target.value)}
        />
      </div>

      <div className="field">
        <label htmlFor="image">Image</label>
        <input id="image" name="image" type="file" accept="image/*" onChange={handleFile} />
      </div>

      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}

      <button type="submit" disabled={submitting}>
        {submitting ? 'Adding...' : 'Add product'}
      </button>
    </form>
  );
}

function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(new Error('Failed to read the selected file.'));
    reader.readAsDataURL(file);
  });
}
