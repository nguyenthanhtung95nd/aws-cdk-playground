import { describe, it, expect } from 'vitest';
import { MockProductDataService } from '../../src/services/mocks/MockProductDataService';

describe('MockProductDataService', () => {
  it('lists the seeded product', async () => {
    const service = new MockProductDataService();
    const products = await service.list();
    expect(products).toHaveLength(1);
  });

  it('creates a product and returns it at the top of the list', async () => {
    const service = new MockProductDataService();
    const created = await service.create({
      name: 'Hat',
      description: 'A warm hat',
      price: 15,
      imageData: 'data:image/png;base64,aGVsbG8=',
    });
    expect(created.id).toBeTruthy();
    const products = await service.list();
    expect(products).toHaveLength(2);
    expect(products[0].name).toBe('Hat');
  });

  it('removes a product by id', async () => {
    const service = new MockProductDataService();
    await service.remove('seed-1');
    expect(await service.list()).toHaveLength(0);
  });
});
