export interface Product {
  id: string;
  name: string;
  description: string;
  price: number;
  imageUrl: string;
  createdAt: string;
  updatedAt: string;
}

export interface NewProduct {
  name: string;
  description: string;
  price: number;
  imageData: string; // base64 data URI
}
