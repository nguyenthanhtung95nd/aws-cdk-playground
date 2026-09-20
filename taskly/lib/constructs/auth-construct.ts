import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import * as cognito from 'aws-cdk-lib/aws-cognito';
import { NagSuppressions } from 'cdk-nag';
import { resourceName, type TasklyNaming } from '../utils';

export interface AuthConstructProps {
  /** Naming context used for every resource name in this construct. */
  readonly naming: TasklyNaming;
  /**
   * Apply production hardening: RETAIN the user pool on stack removal.
   * @default false
   */
  readonly isProd?: boolean;
}

/**
 * Authentication layer: a Cognito user pool plus an app client. The SPA signs in against the
 * client and the HTTP API's JWT authorizer trusts it.
 */
export class AuthConstruct extends Construct {
  /** The user pool that issues the JWTs. */
  public readonly userPool: cognito.UserPool;
  /** The app client the SPA authenticates with and the API authorizer accepts. */
  public readonly userPoolClient: cognito.UserPoolClient;

  constructor(scope: Construct, id: string, props: AuthConstructProps) {
    super(scope, id);
    const isProd = props.isProd ?? false;

    this.userPool = new cognito.UserPool(this, 'user-pool', {
      userPoolName: resourceName(props.naming, 'users'),
      selfSignUpEnabled: false,
      signInAliases: { email: true, username: true },
      passwordPolicy: {
        minLength: 12,
        requireLowercase: true,
        requireUppercase: true,
        requireDigits: true,
        requireSymbols: true,
      },
      removalPolicy: isProd ? cdk.RemovalPolicy.RETAIN : cdk.RemovalPolicy.DESTROY,
    });

    this.userPoolClient = this.userPool.addClient('user-pool-client', {
      userPoolClientName: resourceName(props.naming, 'web'),
      // USER_PASSWORD_AUTH lets a token be fetched via CLI/Postman for smoke tests.
      authFlows: { userPassword: true, userSrp: true },
    });

    NagSuppressions.addResourceSuppressions(this.userPool, [
      {
        id: 'AwsSolutions-COG8',
        reason: 'Plus feature plan (advanced security) is not used on the demo user pool to avoid per-MAU cost.',
      },
      {
        id: 'AwsSolutions-COG2',
        reason: 'MFA is intentionally not required for this internal demo template.',
      },
    ]);
  }
}
