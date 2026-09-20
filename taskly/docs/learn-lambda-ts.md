# Học code Lambda với TypeScript (qua Taskly)

## 1. Lambda TypeScript 101

Một **Lambda function** là một hàm chạy theo sự kiện. Với HTTP API Gateway v2, AWS gọi hàm
`handler` của bạn kèm một **event** mô tả request, và mong nhận về một **result** mô tả response.

```ts
export async function handler(event: APIGatewayProxyEventV2WithJWTAuthorizer)
  : Promise<APIGatewayProxyStructuredResultV2> { ... }
```

Vài điều cần nhớ:
- **`export`** tên hàm phải khớp `handler` khai báo ở CDK (`handler: 'handler'`).
- Event của HTTP API v2 + JWT authorizer có sẵn **claims** đã verify tại
  `event.requestContext.authorizer.jwt.claims` - Lambda **không** tự verify token (API Gateway lo).
- Trả về object `{ statusCode, headers, body }`. `body` là **string** (JSON.stringify).
- Kiểu event/result lấy từ package `@types/aws-lambda`.
- **`@aws-sdk/*` v3** có sẵn trong Lambda runtime Node -> không cần bundle, chỉ import.

---

## 2. `lambdas/` là một dự án Node TS riêng

Code Lambda tách khỏi code hạ tầng CDK, thành một **project Node TS độc lập**:

```
lambdas/
├── package.json        # chỉ deps runtime: @aws-sdk/*  (+ devDeps: vitest, @types/aws-lambda...)
├── tsconfig.json       # config TS cho runtime
├── vitest.config.ts    # test runner
├── src/tasks/          # source
└── tests/tasks/        # test, mirror src/
```

Vì sao tách: deps runtime (aws-sdk...) **không lẫn** với deps hạ tầng (aws-cdk-lib...), cài npm
package cho Lambda gọn gàng (`cd lambdas && npm install <pkg>`), và test đi cùng code.

---

## 3. Bức tranh CRUD

Một Lambda duy nhất xử lý cả 4 method qua **internal router**. Mọi thao tác **scope theo `userId`**
(lấy từ JWT), nên user chỉ đụng data của mình.

```
GET    /tasks       -> listTasks(userId)        -> Query(PK=userId)
POST   /tasks       -> createTask(userId, body) -> PutItem
PUT    /tasks/{id}  -> updateTask(userId, id, body) -> UpdateItem (guard 404)
DELETE /tasks/{id}  -> deleteTask(userId, id)   -> DeleteItem (guard 404)
```

Kiến trúc "deep module": `handler` mỏng (chỉ định tuyến + map lỗi), logic nằm ở các module tập trung.

---

## 4. Walkthrough từng file

### `model/task.ts` - kiểu dữ liệu

```ts
export const TASK_STATUSES = ['todo', 'doing', 'done'] as const;
export type TaskStatus = (typeof TASK_STATUSES)[number];   // 'todo' | 'doing' | 'done'

export interface Task {
  userId: string;   // PK
  id: string;       // SK (uuid)
  title: string;
  status: TaskStatus;
  createdAt: string;
  updatedAt: string;
}
export interface TaskUpdate { title?: string; status?: TaskStatus; }
```

Mẹo hay: khai báo `TASK_STATUSES` là mảng `as const`, rồi **suy ra** union type từ nó. Một nguồn
sự thật - thêm status chỉ sửa một chỗ, và runtime có sẵn danh sách để validate.

### `shared/errors.ts` - lỗi domain

```ts
export class ValidationError extends Error {}
export class NotFoundError extends Error {}
export class UnauthorizedError extends Error {}
```

Code nghiệp vụ **ném** các lỗi này để báo *ý định* (input sai, không tìm thấy, chưa auth). Việc
map sang HTTP status nằm ở boundary (`responses.ts`) - giữ handler mỏng.

### `shared/auth.ts` - lấy userId từ JWT

```ts
export function getUserId(event: APIGatewayProxyEventV2WithJWTAuthorizer): string {
  const sub = event.requestContext.authorizer?.jwt?.claims?.sub;
  if (typeof sub !== 'string' || sub.length === 0) {
    throw new UnauthorizedError('Missing user identity in token.');
  }
  return sub;
}
```

`sub` là định danh user ổn định của Cognito. Ta dùng nó làm **partition key** -> mọi query tự động
giới hạn theo người gọi. Token đã được API Gateway verify, nên ở đây chỉ *đọc* claim.

### `shared/validator.ts` - validate + convert enum tại boundary

```ts
export function parseTitle(raw: unknown): string {
  if (typeof raw !== 'string' || raw.trim().length === 0) throw new ValidationError('Title is required');
  const title = raw.trim();
  if (title.length > 200) throw new ValidationError('Title must be 1-200 characters');
  return title;
}

export function parseStatus(raw: unknown): TaskStatus {
  if (typeof raw !== 'string' || !TASK_STATUSES.includes(raw as TaskStatus)) {
    throw new ValidationError(`Invalid status; expected one of: ${TASK_STATUSES.join(', ')}`);
  }
  return raw as TaskStatus;
}
```

Nguyên tắc: **không tin input**. Body vào là `unknown`, validate rồi mới thành kiểu chặt. Chuỗi
status được convert thành `TaskStatus` **tại API boundary** - bên trong luôn làm việc với enum.

### `shared/responses.ts` - dựng response + map lỗi

```ts
export function json(statusCode: number, body: unknown): APIGatewayProxyStructuredResultV2 {
  return { statusCode, headers: { 'Content-Type': 'application/json', ...CORS_HEADERS }, body: JSON.stringify(body) };
}
export function noContent() { return { statusCode: 204, headers: { ...CORS_HEADERS } }; }

export function toErrorResponse(error: unknown): APIGatewayProxyStructuredResultV2 {
  if (error instanceof ValidationError) return json(400, { message: error.message });
  if (error instanceof NotFoundError)   return json(404, { message: error.message });
  if (error instanceof UnauthorizedError) return json(401, { message: error.message });
  console.error('Unhandled error', error);          // log server-side
  return json(500, { message: 'Internal server error' });  // không lộ chi tiết ra client
}
```

Một chỗ duy nhất biến lỗi domain -> HTTP status. Lỗi lạ -> log + trả 500 chung chung (không rò rỉ nội bộ).

### `taskRepository.ts` - truy cập DynamoDB (SDK v3)

Client tạo **một lần** ngoài handler (tái dùng qua warm invocation):

```ts
const client = new DynamoDBClient({});
const TABLE_NAME = process.env.TABLE_NAME as string;   // CDK bơm vào qua environment
```

DynamoDB nhận/trả dữ liệu ở dạng "attribute value" đặc biệt -> dùng `marshall`/`unmarshall` để
chuyển qua lại với object JS thường:

```ts
export async function queryTasksByUser(userId: string): Promise<Task[]> {
  const result = await client.send(new QueryCommand({
    TableName: TABLE_NAME,
    KeyConditionExpression: 'userId = :userId',
    ExpressionAttributeValues: marshall({ ':userId': userId }),
  }));
  return (result.Items ?? []).map((item) => unmarshall(item) as Task);
}
```

`Query` (không phải `Scan`) theo PK = userId -> chỉ đọc task của người đó, rẻ và scoped.

**Update động + guard 404** - chỉ set field client gửi, `updatedAt` luôn đổi; `ConditionExpression`
biến "id không tồn tại / của user khác" thành lỗi sạch:

```ts
const sets = ['#updatedAt = :updatedAt'];
if (fields.title  !== undefined) { names['#title']  = 'title';  values[':title']  = fields.title;  sets.push('#title = :title'); }
if (fields.status !== undefined) { names['#status'] = 'status'; values[':status'] = fields.status; sets.push('#status = :status'); }

await client.send(new UpdateItemCommand({
  Key: marshall({ userId, id }),
  UpdateExpression: `SET ${sets.join(', ')}`,
  ExpressionAttributeNames: names,             // dùng #alias cho mọi field (tránh reserved word)
  ExpressionAttributeValues: marshall(values),
  ConditionExpression: 'attribute_exists(id)', // không có -> ConditionalCheckFailedException
  ReturnValues: 'ALL_NEW',
}));
```

Bắt `ConditionalCheckFailedException` và ném `NotFoundError` (gom vào `mapMissingItemToNotFound`).

### `createTask.ts` / `listTasks.ts` / `updateTask.ts` / `deleteTask.ts` - thao tác

Mỗi file một thao tác, mỏng và dễ test. Ví dụ create:

```ts
export async function createTask(userId: string, body: string | undefined): Promise<Task> {
  const input = parseJsonBody(body);
  const title = parseTitle(input.title);
  const now = new Date().toISOString();
  const task: Task = { userId, id: randomUUID(), title, status: 'todo', createdAt: now, updatedAt: now };
  await putTask(task);
  return task;
}
```

`randomUUID` lấy từ `node:crypto` (có sẵn trong Node runtime - không cần package `uuid`).

### `handler.ts` - router mỏng

```ts
export async function handler(event: APIGatewayProxyEventV2WithJWTAuthorizer): Promise<APIGatewayProxyStructuredResultV2> {
  try {
    const userId = getUserId(event);
    switch (event.requestContext.http.method) {
      case 'GET':    return json(200, await listTasks(userId));
      case 'POST':   return json(201, await createTask(userId, event.body));
      case 'PUT':    return json(200, await updateTask(userId, requireId(event), event.body));
      case 'DELETE': await deleteTask(userId, requireId(event)); return noContent();
      default:       return json(405, { message: `Method ${event.requestContext.http.method} not allowed` });
    }
  } catch (error) {
    return toErrorResponse(error);   // mọi lỗi domain -> HTTP status tương ứng
  }
}
```

Handler chỉ làm 3 việc: **lấy userId**, **định tuyến theo method**, **map lỗi**. Không có logic
nghiệp vụ - đó là "humble object".

---

## 5. Viết test với Vitest

Test nằm cạnh code trong `lambdas/tests/tasks`, mirror `src/`. Mock DynamoDB để test **routing +
validation** mà không đụng AWS:

```ts
vi.mock('../../src/tasks/taskRepository', () => ({
  putTask: vi.fn(async () => {}),
  queryTasksByUser: vi.fn(async () => []),
  updateTask: vi.fn(async (u, id, f) => ({ userId: u, id, title: f.title ?? 'old', status: f.status ?? 'todo', createdAt: '', updatedAt: 'now' })),
  deleteTask: vi.fn(async () => {}),
}));

it('POST with a blank title returns 400', async () => {
  const res = await handler(event('POST', { sub: 'u1', body: JSON.stringify({ title: '   ' }) }));
  expect(res.statusCode).toBe(400);
});
```

Test thuần logic (validator) không cần mock. Chạy: `npm --prefix lambdas test` (hoặc `npm test` ở
root chạy cả infra + lambdas).

---

## 6. Từ code tới Lambda thật (bundling)

Ta **không** build Lambda tay. CDK (`NodejsFunction`) dùng **esbuild** bundle
`lambdas/src/tasks/handler.ts` thành 1 file JS lúc `cdk deploy`:

- esbuild resolve import từ `lambdas/node_modules` -> cần `cd lambdas && npm install`.
- `@aws-sdk/*` được externalize (runtime có sẵn) -> bundle nhẹ.
- CDK bơm `TABLE_NAME` vào `environment` để repository đọc `process.env.TABLE_NAME`.

Xem cách wiring ở `lib/constructs/tasks-api-construct.ts` và mục 7 (Assets & bundling) của
[Học AWS CDK](learn-cdk.md).

---

## Bản đồ nhanh

| Chủ đề | File |
|--------|------|
| Kiểu dữ liệu | `model/task.ts` |
| Router mỏng + map lỗi | `handler.ts` |
| Validate + enum boundary | `shared/validator.ts` |
| Đọc userId từ JWT | `shared/auth.ts` |
| Response + error->status | `shared/responses.ts` |
| DynamoDB SDK v3 | `taskRepository.ts` |
| Thao tác CRUD | `createTask/listTasks/updateTask/deleteTask.ts` |
| Test | `tests/tasks/*.test.ts` |
