# Học code Frontend React + TypeScript (qua Taskly)

## 1. React + Vite + TS 101

Frontend Taskly là một **SPA** (single-page app): React dựng UI, Vite lo dev server + build.

- Một **component** là một hàm trả về **JSX** (cú pháp giống HTML). Tên PascalCase, `export`.
- **State + side effect** không nằm trong biến thường mà qua **hooks**: `useState` (dữ liệu đổi
  theo thời gian), `useEffect` (chạy khi mount/deps đổi), `useCallback`/`useMemo` (giữ tham chiếu
  ổn định). Hook chỉ gọi ở top-level của component/hook khác.
- **Vite** đọc biến môi trường qua `import.meta.env.VITE_*`. Cờ `--mode <name>` chọn file
  `.env.<name>` -> đây là cách Taskly bật 3 chế độ chạy mà **không đổi code**.
- `main.tsx` bọc app trong `<StrictMode>` (React gọi effect 2 lần ở dev để lộ side effect ẩn -
  bình thường, không phải bug).

Điểm khác backend: ở đây **không có server**. Mọi thứ chạy trong browser; "backend" chỉ là một
`fetch` tới HTTP API (hoặc một mock trong bộ nhớ).

---

## 2. `frontend/` là một dự án Node TS riêng (src/ + tests/)

Giống `lambdas/`, frontend là project độc lập (`package.json` + deps riêng: React, Vite, amplify):

```
frontend/
├── src/
│   ├── main.tsx                # entry: mount <App/> vào #root
│   ├── App.tsx                 # compose: dựng services, gate signed-in
│   ├── config.ts               # đọc VITE_* -> chọn mock / local / live
│   ├── types/task.ts           # kiểu domain (Task, TaskStatus)
│   ├── services/               # tầng dữ liệu (gateway + impl thật/mock)
│   ├── hooks/                  # logic tái dùng (useAuth, useTasks)
│   └── components/             # UI (App, Workspace, SignIn, tasks/...)
├── tests/                      # Vitest unit, mirror src/
├── e2e/                        # Playwright (xem README "Testing")
└── package.json  vite.config.ts  vitest.config.ts
```

Tổ chức **theo domain/vai trò** (services / hooks / components), không theo "loại kỹ thuật".
Test đặt cạnh loại code nó kiểm (`tests/services`, `tests/components`).

---

## 3. Bức tranh lớn: một UI, ba nguồn dữ liệu

Bí quyết để app chạy được **local (mock), local-backend, hay AWS thật** mà UI **không đổi một
dòng**: component không gọi thẳng `fetch` hay amplify. Chúng phụ thuộc vào **interface** (gateway),
còn việc chọn implementation nào là của một **factory**.

```
main.tsx -> App.tsx
              |  createServices()  (đọc config)
              v
   ┌─────────────────────────────┐
   │  { auth: AuthGateway,        │
   │    tasks: TaskGateway }      │
   └─────────────────────────────┘
        UI dùng interface, không biết bên dưới là gì
   mock:  MockAuthService     + MockTaskDataService   (in-memory, 0 network)
   local: MockAuthService     + TaskDataService(:4000) (fake token, backend thật local)
   live:  AuthService(Cognito) + TaskDataService(API)   (JWT thật)
```

| Mode | Cờ (`.env`) | auth | tasks | Dùng khi |
|------|-------------|------|-------|----------|
| **mock** | `VITE_USE_MOCKS=true` | Mock | Mock | Code/style UI nhanh, 0 backend |
| **local** | `VITE_DEV_AUTH=true` + `VITE_API_URL` | Mock (token giả) | HTTP thật -> `:4000` | Test luồng thật với DynamoDB Local |
| **live** | `VITE_API_URL` + Cognito ids | Cognito (amplify) | HTTP thật -> API AWS | Verify tích hợp thật |

Đây là **Dependency Inversion**: UI phụ thuộc abstraction (`gateways.ts`), không phụ thuộc
concrete. Đổi nguồn dữ liệu = đổi factory, không đụng component.

---

## 4. Walkthrough từng file

### `types/task.ts` - kiểu domain (dùng chung ngôn ngữ với backend)

```ts
export const TASK_STATUSES = ['todo', 'doing', 'done'] as const;
export type TaskStatus = (typeof TASK_STATUSES)[number];   // 'todo' | 'doing' | 'done'

export const STATUS_LABEL: Record<TaskStatus, string> = { todo: 'To do', doing: 'Doing', done: 'Done' };

export function nextStatus(current: TaskStatus): TaskStatus {
  const order = TASK_STATUSES;
  return order[(order.indexOf(current) + 1) % order.length];   // todo -> doing -> done -> todo
}
```

Cùng mẹo với Lambda: `as const` + suy ra union type -> một nguồn sự thật. `nextStatus` giữ luật
"bấm chip để tiến trạng thái" ở **một chỗ**, cả UI lẫn optimistic update đều dùng.

### `services/gateways.ts` - hợp đồng UI phụ thuộc

```ts
export interface AuthGateway {
  currentEmail(): Promise<string | null>;
  signIn(email: string, password: string): Promise<void>;
  signOut(): Promise<void>;
  getIdToken(): Promise<string>;
}

export interface TaskGateway {
  list(): Promise<Task[]>;
  create(title: string): Promise<Task>;
  update(id: string, fields: { title?: string; status?: TaskStatus }): Promise<Task>;
  remove(id: string): Promise<void>;
}
```

Đây là **deep module boundary**: interface nhỏ, ổn định; mọi implementation (thật/mock) đều thoả.
Component và hook chỉ biết hai interface này.

### `services/errors.ts` - hai loại lỗi có ý nghĩa khác nhau

```ts
export class SessionExpiredError extends Error {}   // JWT thiếu / API trả 401
export class ApiError extends Error {}              // lỗi API khác, mang message cho user
```

Tách 2 loại vì UI phản ứng **khác nhau**: `SessionExpiredError` -> hiện banner "đăng nhập lại";
`ApiError` -> hiện message lỗi ở đúng chỗ (vd form create).

### `config.ts` - đọc env, chọn một trong ba "hình dạng"

```ts
const useMocks = import.meta.env.VITE_USE_MOCKS === 'true';
const devAuth = import.meta.env.VITE_DEV_AUTH === 'true';

function required(name: string, value: string | undefined): string {
  if (!value) throw new Error(`Missing ${name}. Set it in .env, or run "npm run dev:mock" / "npm run dev:local".`);
  return value;
}

export const config: AppConfig = useMocks
  ? { useMocks: true,  devAuth: false, apiUrl: '', userPoolId: '', userPoolClientId: '', region: '' }
  : devAuth
    ? { useMocks: false, devAuth: true,  apiUrl: required('VITE_API_URL', import.meta.env.VITE_API_URL), /* ... */ }
    : { useMocks: false, devAuth: false, apiUrl: required('VITE_API_URL', ...), userPoolId: required(...), /* ... */ };
```

`required()` chỉ ép biến AWS khi **thực sự cần** (mode live). Mock/local không đòi Cognito ids ->
chạy được ngay, không cấu hình. Fail-fast: thiếu biến ở live -> báo lỗi rõ, không lỗi mơ hồ lúc gọi API.

### `services/AuthService.ts` - auth thật qua Cognito (amplify)

```ts
export class AuthService implements AuthGateway {
  constructor() {
    Amplify.configure({ Auth: { Cognito: { userPoolId: config.userPoolId, userPoolClientId: config.userPoolClientId } } });
  }
  async signIn(email: string, password: string) {
    await signIn({ username: email, password, options: { authFlowType: 'USER_PASSWORD_AUTH' } });
  }
  async getIdToken(): Promise<string> {
    const session = await fetchAuthSession();
    const token = session.tokens?.idToken?.toString();
    if (!token) throw new SessionExpiredError('No active session');   // hết phiên -> báo đúng loại
    return token;
  }
}
```

Amplify giấu toàn bộ chi tiết Cognito. Điểm cần nhớ: **không token = phiên hết** -> ném
`SessionExpiredError` để tầng trên xử lý thống nhất.

### `services/TaskDataService.ts` - client HTTP thật (map lỗi tại boundary)

```ts
private async request<T>(path: string, init?: RequestInit): Promise<T> {
  let token: string;
  try { token = await this.auth.getIdToken(); }
  catch { throw new SessionExpiredError('Session expired'); }

  const res = await fetch(`${this.apiUrl}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', Authorization: token, ...init?.headers },
  });

  if (res.status === 401) throw new SessionExpiredError('Session expired');   // -> banner re-auth
  if (!res.ok) {
    const message = await res.json().then((b: { message?: string }) => b.message ?? 'Request failed').catch(() => 'Request failed');
    throw new ApiError(message);                                              // -> message cho user
  }
  if (res.status === 204) return undefined as T;                             // DELETE không body
  return (await res.json()) as T;
}
```

Đây là **đối xứng với `responses.ts` của Lambda**: backend map lỗi domain -> HTTP status; frontend
map HTTP status -> lỗi domain (`SessionExpiredError`/`ApiError`). `list/create/update/remove` chỉ
là các dòng mỏng gọi `request` với method + path.

### `services/mocks/*` - implementation in-memory (bản sao hành vi)

```ts
export class MockTaskDataService implements TaskGateway {
  private tasks: Task[] = seed();          // 3 task cố định: 1 todo / 1 doing / 1 done
  async list()  { await delay(); return [...this.tasks]; }
  async create(title: string) { await delay(); const t = { /* ... */ status: 'todo' }; this.tasks = [t, ...this.tasks]; return t; }
  // update/remove tương tự
}
```

Mock **giả lập cả độ trễ** (`delay(250ms)`) để UI lộ đúng loading state như thật. `MockAuthService`
cho **bất kỳ** email/password đăng nhập. Vì thoả cùng interface, UI không phân biệt được -> đây là
nền tảng cho **E2E deterministic** (seed cố định).

### `services/createServices.ts` - factory chọn implementation

```ts
export function createServices(): Services {
  if (config.useMocks) return { auth: new MockAuthService(), tasks: new MockTaskDataService() };
  // devAuth = local backend (không Cognito): token giả, nhưng client HTTP thật gọi API URL.
  const auth = config.devAuth ? new MockAuthService() : new AuthService();
  return { auth, tasks: new TaskDataService(config.apiUrl, auth) };
}
```

**Một chỗ duy nhất** quyết định thật/mock. `TaskDataService` nhận `auth` qua constructor (nó chỉ
cần `getIdToken` - xem `TokenProvider`) -> ở local dùng token giả, ở live dùng token Cognito, cùng
một client.

### `hooks/useAuth.ts` - vòng đời phiên đăng nhập

```ts
export function useAuth(auth: AuthGateway) {
  const [email, setEmail] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    auth.currentEmail().then(setEmail).finally(() => setReady(true));   // khôi phục phiên khi mở app
  }, [auth]);

  const signIn = useCallback(async (e: string, p: string) => { await auth.signIn(e, p); setEmail(await auth.currentEmail()); }, [auth]);
  const signOut = useCallback(async () => { await auth.signOut(); setEmail(null); }, [auth]);

  return { email, ready, signedIn: email !== null, signIn, signOut };
}
```

`ready` phân biệt "đang kiểm tra phiên" với "chưa đăng nhập" -> tránh nháy màn hình sign-in khi
F5. `signIn/signOut` được `useCallback` để tham chiếu ổn định (quan trọng cho effect ở dưới).

### `hooks/useTasks.ts` - CRUD + optimistic update (trái tim UI)

Hook này giữ danh sách task, trạng thái load, và các thao tác. Ba ý quan trọng:

**(1) Xử lý hết phiên thống nhất** - mọi thao tác gọi `handledExpiry`; nếu là `SessionExpiredError`
thì báo lên app (banner), không coi là lỗi thường:

```ts
const handledExpiry = useCallback((error: unknown): boolean => {
  if (error instanceof SessionExpiredError) { onExpired(); return true; }
  return false;
}, [onExpired]);   // onExpired PHẢI memoized ở caller, nếu không effect dưới re-run mỗi render
```

**(2) Load ban đầu** - chạy một lần vì `reload` đã memoized:

```ts
useEffect(() => {
  // reload memoized -> chạy một lần cho mỗi service
  reload();
}, [reload]);
```

**(3) Optimistic update** - đổi UI **ngay**, chỉ rollback đúng task đó nếu API fail. Dùng
functional updater (`prev =>`) để không đóng gói (closure) lên snapshot cũ:

```ts
const mutate = useCallback(async (task, optimistic, apply) => {
  setTasks((prev) => prev.map((t) => (t.id === task.id ? optimistic(t) : t)));  // áp ngay
  try { await apply(); }
  catch (error) { setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t))); handledExpiry(error); }  // rollback đúng 1 task
}, [handledExpiry]);

const changeStatus = (task) => mutate(task, (t) => ({ ...t, status: nextStatus(task.status) }), () => data.update(task.id, { status: nextStatus(task.status) }));
```

`create` **rethrow** lỗi non-expiry để `CreateTaskForm` hiện message (giữ nguyên text đã gõ).
`remove` chụp snapshot **bên trong** updater rồi khôi phục cả list nếu xoá thất bại.

> Bài học React quan trọng: **stale closure**. Luôn dùng `setState(prev => ...)` khi state mới phụ
> thuộc state cũ, và memoize callback truyền vào effect/child để tránh re-run/re-render thừa.

### `App.tsx` + `main.tsx` - lắp ráp

```ts
export default function App() {
  const services = useMemo(() => createServices(), []);          // dựng MỘT lần
  const { email, ready, signedIn, signIn, signOut } = useAuth(services.auth);

  if (!ready) return <div className="shell"><p className="footer app-splash">Loading…</p></div>;
  if (!signedIn || email === null) return <SignIn onSignIn={signIn} />;
  return <Workspace taskService={services.tasks} email={email} onSignOut={signOut} />;
}
```

`useMemo(..., [])` giữ services ổn định suốt vòng đời app (nếu tạo mới mỗi render, `useTasks` effect
sẽ reload liên tục). `App` là **humble object** ở tầng UI: chỉ quyết định "splash / sign-in /
workspace", không chứa logic nghiệp vụ.

### `components/` - UI anchor vào accessibility

Component chia theo domain (`tasks/`) + layout (`AppHeader`, `SignIn`, `Workspace`). Vài điểm chuẩn:

- **A11y-first:** nút có `aria-label` rõ (`Edit task`, `Delete task`, `Status: To do. Click to
  advance.`), input có `<label htmlFor>`/`aria-label`. Vừa tốt cho screen reader, vừa cho E2E anchor
  (Playwright `getByRole`/`getByLabel`).
- **`TaskItem` bọc `React.memo`:** mỗi row chỉ re-render khi task/handler của **chính nó** đổi.
- **Đủ trạng thái** trong `states.tsx`: `LoadingState` (skeleton), `EmptyState`, `ErrorState`
  (có retry), `ExpiredBanner` (re-auth). `Workspace` chọn hiển thị theo `status` từ `useTasks`.
- **Optimistic** thể hiện ở `StatusControl`: bấm chip -> UI đổi màu ngay (không chờ round-trip).

---

## 5. Test với Vitest (+ jsdom)

Unit test đặt ở `tests/`, mirror `src/`, chạy trong **jsdom** (giả lập DOM, không cần browser).
Mock ranh giới (fetch / gateway) để test **logic** nhanh và deterministic.

```ts
// tests/services/TaskDataService.test.ts - test map lỗi HTTP -> lỗi domain
it('maps a 401 response to SessionExpiredError', async () => {
  mockFetch(401, {});
  await expect(svc.list()).rejects.toBeInstanceOf(SessionExpiredError);
});

it('maps other errors to ApiError carrying the server message', async () => {
  mockFetch(400, { message: 'Title is required' });
  await expect(svc.create('')).rejects.toThrow('Title is required');
});
```

Component test dùng Testing Library (`tests/components/tasks/TaskItem.test.tsx`): render component,
tương tác qua **role/label** (giống người dùng), assert kết quả. Chạy: `npm test` (ở `frontend/`).

> Ranh giới runner: Vitest chỉ nhặt `tests/**/*.test.{ts,tsx}`; Playwright nhặt `e2e/**/*.spec.ts`.
> Khác glob -> hai loại **không đè nhau**.

---

## 6. E2E, và từ code tới hosting

- **E2E (Playwright)** lái browser thật trên **mock mode** (deterministic) + một suite riêng trên
  **local backend thật**. Chi tiết + lệnh ở [README - Testing](../README.md). Một cổng
  `npm run verify` gom typecheck + lint + unit + e2e.
- **Build:** `npm run build` = `tsc -b` (typecheck) + `vite build` -> `dist/` (HTML/CSS/JS tĩnh,
  đã tree-shake + minify).
- **Hosting:** `WebHostingConstruct` (CDK) upload `frontend/dist` lên **S3** (private,
  `BlockPublicAccess.BLOCK_ALL`) và phục vụ qua **CloudFront** (OAC). Xem
  [Học AWS CDK](learn-cdk.md). Bề mặt public duy nhất là CloudFront + HTTP API - đều sau Cognito.

---

## Bản đồ nhanh

| Chủ đề | File |
|--------|------|
| Kiểu domain + luật status | `types/task.ts` |
| Hợp đồng UI phụ thuộc | `services/gateways.ts` |
| Hai loại lỗi | `services/errors.ts` |
| Chọn mock/local/live | `config.ts` + `services/createServices.ts` |
| Auth thật (Cognito) | `services/AuthService.ts` |
| Client HTTP + map lỗi | `services/TaskDataService.ts` |
| Nguồn dữ liệu giả lập | `services/mocks/*` |
| Vòng đời phiên | `hooks/useAuth.ts` |
| CRUD + optimistic update | `hooks/useTasks.ts` |
| Lắp ráp + gate đăng nhập | `App.tsx` / `main.tsx` |
| UI + a11y + trạng thái | `components/**`, `components/states.tsx` |
| Unit test | `tests/**/*.test.tsx` |
| E2E | `e2e/**/*.spec.ts` (xem README) |
