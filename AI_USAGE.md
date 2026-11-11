# AI_USAGE.md

## 1. AI Tools Used
- **Tools**: Antigravity, ChatGPT

## 2. Areas Where AI Was Utilized
1. **Concurrency & Locking Design**: Determining the database-level `SELECT ... FOR UPDATE` transaction pattern to guarantee that even when thousands of parents apply simultaneously, seats are never overbooked (`held + confirmed <= totalSeats`).
2. **Testing Suite**: Formulating the xUnit automated test suite, including edge-case tests and concurrency tests (simulating 50 and 100 simultaneous parallel requests).

## 3. Where AI Made Mistakes and How They Were Identified & Resolved
- **Socket Connection Block in xUnit Test Runner**:
  - *Mistake/Issue*: In the sandboxed shell environment, `vstest.console` timed out after 90 seconds attempting to connect to `testhost.exe` due to local loopback port binding restrictions.
  - *Resolution*: Identified that the test runner communicates with its test host via a local TCP socket. Executed the test runner with appropriate network/loopback port binding permissions, successfully running and passing 100% of all 13 test cases.
