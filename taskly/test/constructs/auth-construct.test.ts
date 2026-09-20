import { describe, it } from 'vitest';
import * as cdk from 'aws-cdk-lib';
import { Match, Template } from 'aws-cdk-lib/assertions';
import { AuthConstruct } from '../../lib/constructs/auth-construct';

const naming = { tagSystem: 'taskly', tagEnvironment: 'dev', tagCustomerCode: 'demo' };

function synth(): Template {
  const stack = new cdk.Stack(new cdk.App(), 'test');
  new AuthConstruct(stack, 'auth', { naming });
  return Template.fromStack(stack);
}

describe('AuthConstruct', () => {
  const template = synth();

  it('creates one user pool with a strong password policy and admin-only sign-up', () => {
    template.resourceCountIs('AWS::Cognito::UserPool', 1);
    template.hasResourceProperties('AWS::Cognito::UserPool', {
      AdminCreateUserConfig: { AllowAdminCreateUserOnly: true },
      Policies: { PasswordPolicy: Match.objectLike({ MinimumLength: 12 }) },
    });
  });

  it('creates a client that allows USER_PASSWORD_AUTH', () => {
    template.hasResourceProperties('AWS::Cognito::UserPoolClient', {
      ExplicitAuthFlows: Match.arrayWith(['ALLOW_USER_PASSWORD_AUTH']),
    });
  });
});
