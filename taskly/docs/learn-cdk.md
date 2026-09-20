# Học AWS CDK (qua Taskly)

## 1. CDK là gì

**AWS CDK (Cloud Development Kit)** cho phép định nghĩa hạ tầng AWS bằng **ngôn ngữ lập trình
thật** (TypeScript ở đây) thay vì viết YAML/JSON CloudFormation tay. Khi chạy, CDK **synthesize**
(tổng hợp) code thành **CloudFormation template**, rồi CloudFormation dựng tài nguyên.

Vì sao đáng dùng:
- **Trừu tượng hoá cao (constructs):** đóng gói pattern + best practice, ít code hơn nhiều so với CloudFormation thô.
- **Type-safety:** TypeScript bắt lỗi lúc compile thay vì lúc deploy.
- **Tái sử dụng:** đóng gói thành construct/library dùng lại giữa các project (giá trị lớn cho team).

Luồng: `code TS` -> `cdk synth` -> `CloudFormation template (cdk.out/)` -> `cdk deploy` -> tài nguyên AWS.

---

## 2. Mô hình 3 tầng: App / Stack / Construct

CDK tổ chức theo 3 khái niệm lồng nhau:

| Khái niệm | Là gì | Trong Taskly |
|-----------|-------|--------------|
| **App** | Gốc của cây; chứa một hoặc nhiều Stack | `new cdk.App()` trong `bin/taskly.ts` |
| **Stack** | Đơn vị **triển khai** = 1 CloudFormation stack | `TasklyStack` |
| **Construct** | Thành phần tái dùng, đại diện 1+ tài nguyên | `DataConstruct`, `AuthConstruct`... |

Entry point `bin/taskly.ts` tạo App rồi Stack:

```ts
const app = new cdk.App();
// Stack id suy ra từ stage config: `${tagSystem}-${tagEnvironment}-${tagCustomerCode}` -> "taskly-dev-demo".
new TasklyStack(app, `${stageConfig.tagSystem}-${stageConfig.tagEnvironment}-${stageConfig.tagCustomerCode}`, {
  env: { account: process.env.CDK_DEFAULT_ACCOUNT, region: stageConfig.region }, // account từ CLI, region pin theo stage
  /* tag props */
});
app.synth();
```

**Điểm mấu chốt:** *Stack* là đơn vị deploy (deploy/destroy theo stack), còn *Construct* là đơn
vị tổ chức code. Đừng nhầm: chia nhỏ code cho gọn (SRP) là việc của **Construct**, không nhất
thiết phải tách **Stack** (xem mục 8 - Cross-stack).

---

## 3. Construct L1 / L2 / L3

Sách chia construct thành 3 cấp trừu tượng:

| Cấp | Tên | Đặc điểm | Ví dụ |
|-----|-----|----------|-------|
| **L1** | Cfn* (raw) | Ánh xạ 1-1 với CloudFormation; tiền tố `Cfn`; kiểm soát tối đa, verbose | `new cognito.CfnUserPoolGroup(...)` |
| **L2** | Construct chuẩn | AWS/community viết sẵn; có default hợp lý (mã hoá, versioning...) | `new dynamodb.Table(...)`, `new s3.Bucket(...)` |
| **L3** | Custom/pattern | **Bạn tự viết**, ghép nhiều L1/L2 thành một đơn vị domain | `DataConstruct`, `TasksApiConstruct` |

**L2 trong Taskly** - `data-construct.ts` dùng L2 `Table` với default an toàn:
```ts
this.table = new dynamodb.Table(this, 'table', {
  partitionKey: { name: 'userId', type: dynamodb.AttributeType.STRING },
  sortKey: { name: 'id', type: dynamodb.AttributeType.STRING },
  encryption: dynamodb.TableEncryption.CUSTOMER_MANAGED,
  encryptionKey: this.key,
});
```

**L3 trong Taskly** - ta gói KMS + DynamoDB thành một construct domain (`DataConstruct`), phơi ra
interface tối thiểu (`public readonly table`, `key`). Sách nhấn mạnh L3 vì: **đóng gói best
practice**, **sửa một chỗ áp cho toàn dự án/tổ chức**, **enforce kiến trúc** (vd DataConstruct
luôn kèm KMS), **tái dùng** sang project khác.

Vì sao Taskly dùng L3 khắp nơi: mỗi concern (data/auth/api/web) là một construct isolated, có
`Props` interface + JSDoc + `public readonly` - đọc `lib/constructs/*.ts` để thấy pattern.

---

## 4. Cây construct, scope, logical ID

Mọi construct nhận `(scope, id, props)`:
- `scope`: cha trong cây (thường là `this`).
- `id`: định danh **trong scope đó** (phải duy nhất giữa các anh em cùng cha).

CDK ghép `id` dọc cây thành **logical ID** trong CloudFormation. Vì Taskly bọc tài nguyên trong
construct (`data`, `auth`, `api`, `web`), logical ID có tiền tố scope -> **cô lập rõ ràng** giữa
các khối. Ví dụ table nằm trong `DataConstruct` id `data` -> path `taskly-dev-demo/data/table`.

```ts
const data = new DataConstruct(this, 'data', { naming, isProd }); // scope=stack, id='data'
// -> table.node.path = "taskly-dev-demo/data/table"
```

Đây là lý do refactor "gộp một constructor to -> nhiều construct" **đổi logical ID** (thêm tiền tố)
- xem ADR-0002 về hệ quả khi đã deploy.

---

## 5. Aspects & cdk-nag

**Aspect** là cơ chế "duyệt toàn cây và áp một hành động lên mọi node" - dùng cho việc cross-cutting
(gắn tag, kiểm tra, chỉnh sửa hàng loạt).

**cdk-nag** là một Aspect kiểm tra best practice/bảo mật lúc synth. Taskly bật ở entry point:

```ts
// bin/taskly.ts
Aspects.of(app).add(new AwsSolutionsChecks({ verbose: true }));
```

Khi `cdk synth`, cdk-nag quét mọi resource; vi phạm -> **ERROR** làm synth fail (cổng compliance).
Khi một cảnh báo là **có chủ đích**, ta suppress **kèm lý do** (không tắt mù):

```ts
NagSuppressions.addResourceSuppressions(this.userPool, [
  { id: 'AwsSolutions-COG8', reason: 'Plus feature plan not used on demo to avoid per-MAU cost.' },
]);
```

Quy tắc vàng: **mọi suppression phải có `reason`** - để người sau biết vì sao, không tưởng là quên.

---

## 6. Context & config-driven stages

**Context** là dữ liệu cấu hình CDK đọc lúc synth (từ `cdk.json`, `cdk.context.json`, hoặc cờ `-c`).
Taskly dùng context để **config-driven stages**: mọi khác biệt môi trường nằm ở `cdk.json`, không hardcode.

```jsonc
// cdk.json
"context": {
  "stages": {
    "dev": { "tagSystem": "taskly", "tagEnvironment": "dev", "tagSystemApp": "todo", "tagCustomerCode": "demo", "region": "us-west-1" }
  }
}
```

```ts
// bin/taskly.ts - đọc stage + chọn config
const stageName = app.node.tryGetContext('stage') ?? 'dev';
const stageConfig = app.node.tryGetContext('stages')?.[stageName];
```

Deploy môi trường khác chỉ bằng cờ: `cdk deploy -c stage=dev`. Muốn thêm `staging`/`prod` -> thêm
một khối vào `context.stages`, không sửa code. Nhánh hardening prod (`isProd`) rẽ theo
`tagEnvironment.startsWith('prod')`.

---

## 7. Assets & bundling

**Asset** là file/thư mục cục bộ mà CDK đóng gói và upload (lên S3 asset bucket) để CloudFormation
dùng. Hai loại Taskly gặp:

**(a) Lambda code - `NodejsFunction` + esbuild.** CDK tự **bundle** TypeScript của Lambda thành 1
file JS lúc synth/deploy - **không cần build tay**:
```ts
new NodejsFunction(this, 'fn', {
  runtime: Runtime.NODEJS_22_X,
  entry: join(__dirname, '..', '..', 'lambdas', 'src', 'tasks', 'handler.ts'),
  handler: 'handler',
});
```
esbuild resolve module từ `lambdas/node_modules`; `@aws-sdk/*` được **externalize** (đã có sẵn
trong Lambda runtime) nên không bị bundle. Đây là lý do `lambdas/` cần `npm install` riêng.

**(b) Frontend build - `BucketDeployment`.** Upload nội dung `frontend/dist` lên S3 rồi invalidate
CloudFront. Taskly guard `existsSync` để synth vẫn chạy khi chưa build FE.

---

## 8. Cross-stack & vì sao Taskly một stack

Có thể tham chiếu resource giữa các Stack - CDK sinh **CloudFormation Export/Import** tự động. Nhưng
sách cảnh báo: *"be cautious not to create too many dependencies between stacks. This can lead to
tight coupling and make it harder to update or deploy stacks independently."*

Cái đau thật: khi StackA export một giá trị mà StackB import, **không xoá/sửa được** resource đó
khi còn bị import (`Export ... cannot be deleted as it is in use`) -> destroy phải đúng thứ tự, dễ
sót resource/orphan.

**Taskly chọn một stack** vì yêu cầu **deploy/destroy nguyên tử** (một CloudFormation stack = tất
cả-hoặc-không, không export kẹt). SRP + tái dùng lấy từ **L3 constructs**, không phải từ việc tách
stack. Data-protection giải ở **tầng resource** (`RemovalPolicy.RETAIN` + `deletionProtection` cho
prod), không cần tách "stateful stack".

---

## 9. Naming, Tags, Removal policy

**Naming convention** - mọi resource dùng prefix `${tagSystem}-${tagEnvironment}-${tagCustomerCode}`
qua helper `resourceName()`. Tên tự mô tả + globally-unique -> nhiều stage/customer chung account
không đụng nhau.

**Tags** - gắn ở cấp Stack, propagate xuống mọi resource (cho cost allocation):
```ts
cdk.Tags.of(this).add('System', props.tagSystem);
cdk.Tags.of(this).add('Environment', props.tagEnvironment);
```

**Removal policy** - quyết định điều gì xảy ra khi resource bị xoá khỏi stack:
- `DESTROY` (dev): xoá sạch -> destroy gọn.
- `RETAIN` (prod): giữ lại -> bảo vệ data. Taskly rẽ theo `isProd`:
```ts
removalPolicy: isProd ? cdk.RemovalPolicy.RETAIN : cdk.RemovalPolicy.DESTROY,
deletionProtection: isProd,  // DynamoDB: chặn xoá nhầm ở prod
```

---

## 10. Security mặc định

Taskly áp các default an toàn ngay từ construct (đọc `lib/constructs/*.ts`):
- **KMS CMK** mã hoá DynamoDB, log group, (S3 dùng SSE-S3).
- **IAM least-privilege**: `table.grantReadWriteData(fn)` - chỉ cấp đúng quyền cần.
- **S3**: `BlockPublicAccess.BLOCK_ALL` + `enforceSSL`; CloudFront đọc bucket qua **Origin Access
  Control (OAC)**, không mở public.
- **Cognito**: password policy mạnh, `selfSignUpEnabled: false`.
- **Không secret hardcode** - dùng Secrets Manager / SSM.

---

## 11. Testing hạ tầng CDK

CDK có module `assertions`: synth stack/construct thành `Template` rồi assert tài nguyên. Taskly
tách **construct test** (từng L3 isolated) và **stack test** (composition).

```ts
import { Template } from 'aws-cdk-lib/assertions';

const stack = new cdk.Stack(new cdk.App(), 'test');
new DataConstruct(stack, 'data', { naming });
const template = Template.fromStack(stack);

template.hasResourceProperties('AWS::DynamoDB::Table', {
  KeySchema: [
    { AttributeName: 'userId', KeyType: 'HASH' },
    { AttributeName: 'id', KeyType: 'RANGE' },
  ],
});
```

Lưu ý: synth (nhất là bundling Lambda) nặng -> đặt `testTimeout` cao và synth một lần ở scope
`describe`. Xem `test/constructs/*` và `test/stack/*`.

---

## 12. Vòng đời lệnh CDK

| Lệnh | Làm gì |
|------|--------|
| `cdk init app --language typescript` | Tạo scaffold project (folder rỗng) |
| `cdk bootstrap aws://ACC/us-west-1` | Dựng tài nguyên CDK cần (asset bucket, roles) - một lần/account+region |
| `cdk ls` | Liệt kê stacks |
| `cdk synth -c stage=dev` | Tổng hợp ra CloudFormation (chạy cdk-nag) |
| `cdk diff -c stage=dev` | So sánh với trạng thái đã deploy |
| `cdk deploy -c stage=dev` | Deploy stack |
| `cdk destroy -c stage=dev` | Xoá stack (RETAIN sẽ giữ lại resource được đánh dấu) |

---

## Liên hệ với Taskly (bản đồ nhanh)

| Khái niệm | File Taskly |
|-----------|-------------|
| App + entry + cdk-nag Aspect + context stages | `bin/taskly.ts` |
| Stack compose constructs | `lib/taskly-stack.ts` |
| L3 constructs (L2 bên trong) | `lib/constructs/*.ts` |
| Naming helper | `lib/utils.ts` |
| Config-driven stages | `cdk.json` |
| Testing | `test/constructs/*`, `test/stack/*` |

Tiếp theo: [Học code Lambda TypeScript](learn-lambda-ts.md) - backend runtime · [Học code Frontend React](learn-react.md) - SPA.
