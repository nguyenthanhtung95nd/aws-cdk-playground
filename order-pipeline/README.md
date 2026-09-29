# Order Pipeline

Hệ xử lý đơn hàng event-driven trên AWS: API Gateway → Lambda (.NET 10) → DynamoDB Streams →
SQS + DLQ → EventBridge Pipes → CloudWatch + SNS. Frontend React + Vite. Hạ tầng bằng CDK C#.
Local chạy dưới Aspire, **không chạm AWS thật**.

- Tại sao thiết kế như vậy: [docs/system-design.md](docs/system-design.md)
- File này: **chạy local và test như thế nào**.

---

## 1. Yêu cầu máy

| Công cụ | Phiên bản đã dùng | Cần cho |
|---|---|---|
| .NET SDK | 10.0.x | mọi thứ |
| Node.js + npm | 22.x / 11.x | frontend |
| Docker Desktop (daemon Linux) | 28.x | DynamoDB Local, dynamodb-admin, E2E |
| AWS CDK CLI | 2.1113.x | chỉ khi `cdk synth` / deploy |

Không cần AWS credential, không cần đặt biến môi trường AWS nào. Mọi hàng rào local nằm trong
code của AppHost.

---

## 2. Chạy local

Lần đầu cài dependency frontend:

```powershell
cd order-pipeline\frontend
npm ci
```

Khởi động toàn bộ stack local:

```powershell
cd order-pipeline
dotnet run --project src\OrderPipeline.AppHost --launch-profile http
```

Trình duyệt tự mở Aspire dashboard tại `http://localhost:15195`. Chờ tới khi các resource
sau đều **Healthy / Running**:

| Resource | Là gì | Địa chỉ |
|---|---|---|
| `dynamodb` | DynamoDB Local, bảng `orderpipeline-local-demo-orders` được tạo lại mỗi lần chạy | endpoint API, không mở bằng trình duyệt |
| `dynamodb-admin` | UI xem dữ liệu bảng | bấm link trên dashboard |
| `GetHealth`, `PlaceOrder`, `ListOrders` | 3 Lambda HTTP chạy qua Lambda Test Tool | nội bộ |
| `ApiGateway` | API Gateway emulator (HTTP v2) | `http://localhost:5300` |
| `web` | Vite dev server, proxy `/api` sang ApiGateway | `http://localhost:5173` |

Lần chạy đầu tiên chậm vì Docker phải pull image DynamoDB Local và dynamodb-admin.

### Điều gì **không** chạy local

Dispatcher, Worker, Settler, Reviewer, Pipe, alarm và CloudFront không có emulator chính chủ.
Vì vậy **mọi đơn ở local dừng ở `PENDING`**. Đó là đúng thiết kế. Các nhánh này chỉ được phủ
bởi unit test và nghiệm thu sau khi deploy.

---

## 3. Test tay trên local

### 3.1 Qua giao diện

1. Mở `http://localhost:5173`.
2. Điền Customer, Product, Amount, bấm **Place order**. Đơn xuất hiện trong bảng ngay, không
   cần reload, trạng thái `PENDING`.
3. Nhập Amount `25000` để thấy nhãn **high value** (ngưỡng 10 000 đọc từ `cdk.json`).
4. Bỏ trống Amount để thấy validation phía client (`Amount is required.`).
5. Mở `dynamodb-admin` từ dashboard để xem item thật, kể cả `valueTier` và `recencyCursor`.

### 3.2 Lệnh trên dashboard

| Resource | Lệnh | Tác dụng |
|---|---|---|
| `ApiGateway` | **Seed sample orders** | Đặt 3 đơn mẫu qua API (1 499, 12 000 high value, 250) |
| `dynamodb` | **Reset orders table** | Xoá và tạo lại bảng rỗng, có hỏi xác nhận |

### 3.3 Gọi API trực tiếp

Đặt một đơn:

```powershell
curl.exe -i -X POST http://localhost:5300/orders `
  -H "Content-Type: application/json" `
  -d '{"customerName":"Alice","product":"Headphones","amount":1499,"notes":""}'
```

Kết quả mong đợi: `HTTP/1.1 201`, header `Location: /orders/<id>` và `x-correlation-id`,
body:

```json
{"orderId":"<uuid>","status":"PENDING"}
```

Liệt kê đơn mới nhất:

```powershell
curl.exe "http://localhost:5300/orders?limit=5"
```

Body có mảng `orders` giảm dần theo thời gian đặt và, nếu còn trang sau, một `nextCursor` mờ.
Truyền lại nó bằng `?cursor=<nextCursor>`. Cursor tự chế sẽ bị trả `400`.

Các lỗi validation nên thử: thiếu `customerName` (`400`), `amount` là chuỗi (`400 "amount must
be a number."`), `limit=abc` (`400 "limit must be a whole number."`), `limit=101` (`400`).

Health:

```powershell
curl.exe http://localhost:5300/health
```

---

## 4. Test tự động

Chạy từ thư mục `order-pipeline`.

| Lệnh | Gồm | Cần Docker | Thời gian |
|---|---|---|---|
| `dotnet test --nologo` | 296 test .NET (Domain 75, Functions 148, Infrastructure 73) + 1 test Aspire bị skip | Không | ~2 phút (Infrastructure synth CDK bằng Node) |
| `npm --prefix frontend test` | 75 test Vitest + typecheck | Không | ~1 phút |
| `npm --prefix frontend run lint` / `run typecheck` | ESLint strict, `tsc --noEmit` | Không | nhanh |
| `npm --prefix frontend run test:e2e` | 7 test Playwright, **tự khởi động AppHost** profile `e2e` | **Có** | vài phút |
| `npm run verify:aspire` | 1 test đi qua AppHost thật: POST rồi GET, đơn là PENDING | **Có** | vài phút |
| `npm run verify` | frontend verify (typecheck + lint + Vitest + E2E) rồi build + test .NET | **Có** | tổng cộng |

Ghi chú:

- E2E **không mock** API. `frontend/e2e/host.ts` tự chạy `dotnet run --launch-profile e2e`,
  chờ `http://localhost:5173` trả lời, và dọn container Aspire mồ côi khi xong. Nếu AppHost
  đang chạy sẵn ở `5173`, E2E dùng luôn instance đó.
- Unit test mock ở đúng một seam: `.NET` mock `IAmazonDynamoDB`/`IAmazonSQS` bằng NSubstitute;
  frontend `vi.mock` hàm `createOrderGateway`.
- Test Aspire mặc định skip; chỉ chạy khi có `ORDER_PIPELINE_ASPIRE_TESTS=1` (script
  `verify:aspire` tự đặt).

Kiểm tra cdk-nag (thước đo thật, test `NagComplianceTests` bỏ sót cảnh báo):

```powershell
npx cdk synth -c stage=dev
```

Mong đợi: synth xong, không dòng `[Error at ...] AwsSolutions-...`.

---

## 5. Debug khi chạy local

Aspire dashboard (`http://localhost:15195`) là điểm vào duy nhất:

| Cần xem | Ở đâu |
|---|---|
| Resource nào chưa lên, vì sao | Trang Resources → cột State / Health → Logs của resource đó |
| Log có cấu trúc của một Lambda | Structured logs, lọc theo `CorrelationId` (là `orderId` sau khi đặt) hoặc `InvocationId` |
| Một request đi qua đâu, mất bao lâu | Traces, span `order` có tag `order.id` |
| Item thật trong bảng | Link `dynamodb-admin` trên resource `dynamodb` |
| Response thô của một Lambda | Lambda Test Tool mà AppHost khởi động cùng mỗi function |

Vòng lặp thường dùng: **Reset orders table** → **Seed sample orders** → mở Structured logs →
lọc theo `orderId` vừa tạo. Header `x-correlation-id` trong response `POST /orders` là id để
tìm dòng log "accepted" của đơn đó.

Có trợ lý AI thì `aspire mcp init` trong `src/OrderPipeline.AppHost` (cần `aspire` CLI) cho
phép hỏi thẳng "resource nào unhealthy?" hay "trace của order X".

Không xem được local: alarm, SNS, message trong bãi đỗ, vì các làn đó không chạy local. Sau
deploy, xem chúng trên CloudWatch (log group `/aws/lambda/<function>` và
`/aws/vendedlogs/pipes/<pipe>`), dashboard CloudWatch tên theo stack id, và console SQS.

---

## 6. Sự cố thường gặp

| Triệu chứng | Nguyên nhân | Cách xử lý |
|---|---|---|
| `dynamodb` mãi không Healthy | Docker chưa chạy hoặc đang pull image | Mở Docker Desktop, chờ, xem log resource trên dashboard |
| `web` báo lỗi proxy `ECONNREFUSED 5300` | ApiGateway chưa lên | Chờ `ApiGateway` Healthy; `web` có `WaitFor` nên thường tự hết |
| Port `5173` hoặc `5300` bận | Instance cũ chưa tắt | Tắt process `dotnet` cũ; E2E teardown dùng `taskkill /T /F` |
| Container `dynamodb-local` còn sót sau khi Ctrl+C | Aspire bị kill trước khi dọn | `docker ps` và `docker rm -f <id>`; E2E tự làm việc này |
| Đơn không bao giờ rời `PENDING` | Đúng thiết kế local, không có SQS | Xem mục 2 |
| `Infrastructure.Tests` chạy lâu | Mỗi test class synth stack bằng Node | Bình thường, ~2 phút |
| AppHost ném `The AppHost is a local run model only` | Đang gọi `aspire publish` hoặc publish mode | Không publish AppHost; deploy bằng CDK |

---

## 7. Deploy (tóm tắt, làm tay)

CDK là nguồn sự thật duy nhất. AppHost không bao giờ được publish.

1. Đặt `context.stages.dev.alertEmail` trong `cdk.json`, nếu không sẽ không có ai nhận alarm.
2. `npx cdk synth -c stage=dev` phải sạch cdk-nag.
3. `npx cdk deploy -c stage=dev` với credential có quyền deploy. Docker cần chạy vì Lambda và
   frontend được bundle trong container.
4. Xác nhận email đăng ký SNS.
5. Nghiệm thu bốn kịch bản trong [system-design.md mục 4](docs/system-design.md#4-vòng-đời-một-đơn-hàng)
   qua URL CloudFront (`output-site-url`). Đây là lúc Dispatcher, Worker, Settler, Reviewer,
   Pipe và alarm chạy thật lần đầu.

Muốn hai stack song song trên cùng account: `-c customerCode=<tên khác>`.
