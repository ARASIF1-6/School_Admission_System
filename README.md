# School Admission System (বিদ্যালয় ভর্তি ব্যবস্থা)

An automated online school admission system built with **.NET 8 (C#)**, **Entity Framework Core**, **PostgreSQL**, **React 18+ (TypeScript + Vite)**, and **Docker**.

The system operates strictly on a **"first-come, first-served" (আগে আসলে আগে পাবেন)** basis with a dynamic, time-limited seat hold and payment confirmation mechanism.

---

## 🚀 Quick Start with Docker (এক ক্লিকে চালু)

No need to install .NET, Node.js, or PostgreSQL on your host machine. Simply run:

```bash
docker compose up
```

This will automatically start:
1. **PostgreSQL 16**: Port `5432` with a persistent Docker volume (`postgres_data`).
2. **Backend (.NET 8 Web API)**: Running on `http://localhost:8080`.
3. **Frontend (React 18+)**: Running on `http://localhost:3000` (also mapped to `http://localhost:5173`).

### 🌐 Frontend URLs:
- **Primary URL**: [http://localhost:3000](http://localhost:3000)
- **Alternative URL**: [http://localhost:5173](http://localhost:5173)

### 📚 Backend Swagger API Documentation:
- **Swagger UI**: [http://localhost:8080/swagger](http://localhost:8080/swagger)

---

## 🧪 Running Automated Tests (টেস্ট চালানোর নিয়ম)

### 1. Backend Automated Tests (xUnit)
Includes unit tests and the mandatory **Concurrency Test** (simulating 50-100 simultaneous requests competing for limited seats):

```bash
cd backend
dotnet test SchoolAdmission.Tests/SchoolAdmission.Tests.csproj
```

Output highlights:
- `ConcurrentSeatHold_50StudentsCompetingFor5Seats_Exactly5SucceedAnd45ReceiveConflict`: **PASSED**
- `ConcurrentSeatHold_100StudentsCompetingFor20Seats_Exactly20Succeed`: **PASSED**
- `ConfirmHold_MultipleTimes_IsIdempotent`: **PASSED**
- `ExpiredHold_ReturnsSeatToAvailable_WithoutApiCall`: **PASSED**
- Total: 13 tests, 100% passing.

### 2. Frontend Automated Tests
Runs unit tests for student ID validation, seat calculation, countdown timer, and dynamic expiration:

```bash
cd frontend
npm test
```

---

## 🛠️ Environment Variables Configuration

| Variable | Location | Default | Description |
|---|---|---|---|
| `DATABASE_CONNECTION_STRING` | Backend | `Host=postgres;...` | PostgreSQL connection string |
| `HOLD_DURATION_SECONDS` | Backend | `600` (10 mins) | Duration in seconds a seat remains on hold |
| `PORT` | Backend | `8080` | Port for Kestrel web server |
| `VITE_API_URL` | Frontend | `http://localhost:8080` | Base URL of the backend API |

---

## 📋 API Contract Summary

All endpoints strictly follow the JSON contract specified in the brief:

| # | Method | Path | Header | Description |
|---|---|---|---|---|
| 1 | `POST` | `/classes` | - | Create new class shift (`{ name, totalSeats }`) |
| 2 | `GET` | `/classes` | - | List all classes with seat stats (`heldSeats`, `confirmedSeats`, `availableSeats`) |
| 3 | `GET` | `/classes/{classId}` | - | Get specific class seat status |
| 4 | `POST` | `/classes/{classId}/holds` | `X-Student-Id` | Apply/Hold a seat. Returns 409 if no seats available |
| 5 | `GET` | `/holds/{holdId}` | - | Get hold status (`HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`) |
| 6 | `POST` | `/holds/{holdId}/confirm` | `X-Student-Id` | Confirm admission / pay fee (Idempotent) |
| 7 | `DELETE`| `/holds/{holdId}` | `X-Student-Id` | Cancel hold (204 No Content) |
| 8 | `GET` | `/me/holds` | `X-Student-Id` | Get all applications of the student |

---

## 🖥️ UI Features

1. **No Login Required**: The top bar provides an input field for the student's 17-digit birth registration number (`X-Student-Id`). Persisted in `localStorage`.
2. **Class Creation (Admin)**: Create any class and shift with customized seat numbers.
3. **Live Class List**: Shows Total, Held, Confirmed, and Available seats with color progress bars. Auto-refreshes every 4 seconds.
4. **Live Countdown & Expiry**: Visual countdown timer showing remaining hold time. When it reaches 00:00, status immediately switches to `EXPIRED` in real-time, disabling payment button without requiring page reload.
5. **My Applications**: View application history and confirm or cancel pending holds.
