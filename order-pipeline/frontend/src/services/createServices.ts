import type { OrderGateway } from './gateways';
import { OrderDataService } from './OrderDataService';

// The one seam unit tests replace with a fake gateway.
export function createOrderGateway(): OrderGateway {
  return new OrderDataService();
}
