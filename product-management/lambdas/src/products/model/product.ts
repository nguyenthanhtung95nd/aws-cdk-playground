/** Incoming create payload. `imageData` is a base64 data URI. */
export interface ProductInput {
  name: string;
  description: string;
  price: number;
  imageData: string;
}

/** Stored product record. Partition key is `id`; the catalog is shared (no per-user scoping). */
export interface Product {
  id: string;
  name: string;
  description: string;
  price: number;
  imageUrl: string;
  createdAt: string;
  updatedAt: string;
}
