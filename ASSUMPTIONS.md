# ASSUMPTIONS.md

## 1. Assumptions & Rationale

1. **ID Format**:
   - The specification states: *The IDs in the examples (`c_123`, `h_789`) are only illustrative; the format of IDs is up to your preference.*
   - We adopted a prefixed unique alphanumeric identifier format: `c_` prefix for Class IDs and `h_` prefix for Hold IDs (e.g., `c_a1b2c3d4`, `h_e5f6g7h8`). This format prevents ID collisions across distributed and multi-instance deployments.

2. **Student Identity Verification**:
   - According to the specification, the student identity is provided via the `X-Student-Id` HTTP request header, which corresponds to the student's 17-digit birth registration number.
   - The backend validates that this header is present and consists of exactly 17 numeric digits. Otherwise, a 4xx error (`400 Bad Request`) with a clear JSON error message is returned.

3. **Automatic Seat Release (Zero-API Call Guarantee)**:
   - The specification states: *When a hold expires, the seat automatically becomes available again without requiring anyone to make an API call.*
   - We implemented this directly in the calculation queries:
     $$\text{availableSeats} = \text{totalSeats} - (\text{confirmedSeats} + \text{heldSeats})$$
     where `heldSeats` strictly counts only holds where `Status == 'HELD'` **and** `ExpiresAt > DateTime.UtcNow`.
   - Consequently, the exact millisecond a hold expires, it is automatically omitted from the held seat count. The seat is instantly restored to available capacity for other applicants without requiring any external cron trigger, API endpoint invocation, or manual intervention.
   - Additionally, a lightweight hosted background service (`HoldExpirationService`) runs periodically to update expired status records in the database for clean auditing.

4. **Single Active Application Constraint**:
   - The specification states: *A student cannot have more than one active application or confirmed admission in the same class at the same time.*
   - If a student attempts to create a hold for a class where they already hold an active reservation (`HELD` and unexpired) or already have a confirmed admission (`CONFIRMED`), the request is rejected with `409 Conflict`. However, applications to different classes/shifts remain permissible.

5. **Idempotent Admission Confirmation**:
   - The specification states: *Even if confirm is requested multiple times for the same application, confirmedSeats will increase only once.*
   - Once a hold is confirmed (`CONFIRMED`), subsequent requests to `POST /holds/{holdId}/confirm` return `200 OK` with `{ "holdId": "...", "status": "CONFIRMED" }` without double-incrementing confirmed seats.

---

## 2. Bonus: Multi-Process Concurrency Guarantee

### Question:
> *"Even when two backend processes run simultaneously on different ports connected to the same database, Rule 1 (held + confirmed <= totalSeats) must hold true. How did you ensure this?"*

### Solution & Technical Strategy:

1. **PostgreSQL Row-Level Pessimistic Locking (`SELECT ... FOR UPDATE`)**:
   - Whenever an application hold request arrives (`POST /classes/{classId}/holds`), the backend initiates an atomic database transaction at the `ReadCommitted` isolation level and acquires an exclusive row lock on the target class record:
     ```sql
     SELECT "Id" FROM "Classes" WHERE "Id" = @classId FOR UPDATE;
     ```
   - PostgreSQL's `FOR UPDATE` clause places an exclusive row-level lock on that specific class.
   - If two separate backend processes (e.g., running on Port `8080` and Port `8081`, or inside separate container replicas) concurrently attempt to hold a seat for the same class, the database engine forces the second transaction to wait until the first transaction either commits or rolls back.
   - This eliminates race conditions natively at the database engine level via ACID transactions, eliminating any dependency on in-memory synchronization across instances.

2. **Atomic Verification and Seat Counting Inside the Lock**:
   - Within the locked transaction boundary, fresh committed data is queried directly from the database:
     $$\text{confirmedCount} + \text{activeHeldCount} < \text{totalSeats}$$
   - Only when seats are verified to be available is the new hold record inserted and the transaction committed (`COMMIT`).
   - If seats are exhausted or the student already has an active application, the transaction is immediately rolled back (`ROLLBACK`) and `409 Conflict` is returned.
   - As a result, $\text{held} + \text{confirmed} \le \text{totalSeats}$ is mathematically and strictly guaranteed at all times under any concurrent workload.
