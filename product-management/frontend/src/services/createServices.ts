import { config } from '../config';
import type { ProductGateway } from './gateways';
import { MockProductDataService } from './mocks/MockProductDataService';
import { ProductDataService } from './ProductDataService';

/** Picks the mock or HTTP gateway based on config, so components stay implementation-agnostic. */
export function createProductGateway(): ProductGateway {
  return config.useMocks ? new MockProductDataService() : new ProductDataService();
}
