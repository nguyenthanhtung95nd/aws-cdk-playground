#!/usr/bin/env node
import * as cdk from 'aws-cdk-lib';
import { Aspects } from 'aws-cdk-lib';
import { AwsSolutionsChecks } from 'cdk-nag';
import { TasklyStack } from '../lib/taskly-stack';

const app = new cdk.App();

// cdk-nag: enforce AWS Solutions best practices at synth time. This is the compliance
// gate for the whole template - every suppression elsewhere must carry an explicit reason.
Aspects.of(app).add(new AwsSolutionsChecks({ verbose: true }));

// Config-driven stages: all per-environment values live in cdk.json (context.stages.<env>).
// Pick a stage with `cdk deploy -c stage=dev`.
const stageName = app.node.tryGetContext('stage') ?? 'dev';
const stageConfig = app.node.tryGetContext('stages')?.[stageName];
if (!stageConfig) {
  throw new Error(`Unknown stage "${stageName}". Add it under context.stages in cdk.json.`);
}

new TasklyStack(app, `${stageConfig.tagSystem}-${stageConfig.tagEnvironment}-${stageConfig.tagCustomerCode}`, {
  // Account comes from the CLI; region is pinned per stage so the same app is portable.
  env: { account: process.env.CDK_DEFAULT_ACCOUNT, region: stageConfig.region },
  description: 'Taskly - serverless ToDo',
  tagSystem: stageConfig.tagSystem,
  tagEnvironment: stageConfig.tagEnvironment,
  tagSystemApp: stageConfig.tagSystemApp,
  tagCustomerCode: stageConfig.tagCustomerCode,
});

app.synth();
