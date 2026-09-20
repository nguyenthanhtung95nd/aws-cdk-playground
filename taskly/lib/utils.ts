export interface TasklyNaming {
  tagSystem: string;
  tagEnvironment: string;
  tagCustomerCode: string;
}

/**
 * Builds a resource name with the shared `${system}-${environment}-${customer}` prefix.
 * The prefix keeps names globally unique and self-describing, so multiple stages or
 * customers can coexist in one AWS account without collisions.
 */
export function resourceName(naming: TasklyNaming, suffix: string): string {
  return `${naming.tagSystem}-${naming.tagEnvironment}-${naming.tagCustomerCode}-${suffix}`;
}
