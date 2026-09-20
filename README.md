# aws-cdk-playground

A personal workspace of hands-on **AWS CDK (TypeScript)** practice projects. Each project is
**self-contained** - it has its own dependencies, tests, local dev setup, and can be deployed on
its own. This repository just groups them together and shares one set of engineering conventions.

---

## Projects

| Project | What it is | Key AWS services | Docs |
|---------|------------|------------------|------|
| [product-management](./product-management/) | Serverless product catalog (create/view/delete products with images + async processing) | S3, Lambda, HTTP API v2, DynamoDB, SQS/DLQ, Secrets Manager, CloudFront | [README](./product-management/README.md) |
| [taskly](./taskly/) | Serverless ToDo app (per-user tasks behind auth) | Cognito, Lambda, HTTP API v2, DynamoDB, S3/CloudFront, KMS | [README](./taskly/README.md) |

Start with a project's own README - it contains that project's quickstart, local-run modes,
testing, and deploy instructions.

---

## Repository layout

```
aws-cdk-playground/
  product-management/   # Practice project - self-contained
  taskly/               # Practice project - self-contained
```

---

## Shared conventions

Every project in this workspace follows the same engineering standards:

- **AWS CDK v2 (TypeScript)**, **Node 22**, region **us-west-1**.
- **One stack composed from L3 constructs** - single responsibility + reuse, atomic
  deploy/destroy (no cross-stack exports).
- **Config-driven stages** - environment differences live in `cdk.json` (`context.stages.<env>`),
  selected with `-c stage=<env>`.
- **cdk-nag** runs at synth time; every suppression carries a reason.
- **Least-privilege IAM** (`grant*`), **KMS** for sensitive resources, `enforceSSL`.
- **Testing** - Vitest for unit tests, Playwright for E2E.
- **Local-first** - each project runs offline (LocalStack or DynamoDB Local) so you rarely need a
  real AWS account to iterate.

---

## Getting started

You need a project-level toolchain, then work inside whichever project you want.

### Prerequisites

- **Node.js 22.x** (`node --version`)
- **Docker** - for local AWS emulation (LocalStack / DynamoDB Local)
- **AWS CLI** - only for real deploys (`aws sts get-caller-identity`)
- **AWS CDK CLI** - matching each project's `aws-cdk-lib`

### Run a project

Each project is independent, so `cd` into it and follow its README. Example:

```bash
cd product-management
npm install                      # infrastructure deps
npm --prefix lambdas install     # Lambda runtime deps
npm --prefix frontend install    # frontend deps
npm test                         # unit tests (no AWS)
npx cdk synth -c stage=dev       # synthesize CloudFormation + run cdk-nag
```

For local development, running tests, and deploying, see the project's README:

- [product-management/README.md](./product-management/README.md)
- [taskly/README.md](./taskly/README.md)

---

## Adding a new project

1. Create a new top-level folder (e.g. `my-new-project/`).
2. Follow the shared conventions above (single stack + L3 constructs, config-driven stages,
   cdk-nag, Vitest, local-first).
3. Give it its own `README.md`, dependencies, and tests so it stays self-contained.
4. Add a row to the [Projects](#projects) table.
```