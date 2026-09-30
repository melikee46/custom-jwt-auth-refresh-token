# Secure Custom JWT Authentication & Refresh Token System

A production-grade, highly secure implementation of Custom JWT Authentication and Refresh Token Rotation in ASP.NET Core (Backend) and React + TypeScript (Frontend). Built from scratch without relying on external identity providers.

## 🛡️ OWASP Security Standards & Architecture (Step 11)
This project is built with strict adherence to security best practices and OWASP guidelines:

1. **Short-lived Access Tokens**: JWTs expire in 15 minutes to minimize the attack surface if intercepted.
2. **Stateless JWT Validation**: No database lookup is required for access token validation, relying entirely on HMAC-SHA256 signature verification. `ClockSkew` is set to `Zero`.
3. **HttpOnly Refresh Tokens**: Refresh tokens are stored in `HttpOnly`, `Secure`, and `SameSite=Strict` cookies to completely prevent XSS attacks. The frontend JS never accesses them.
4. **Hashed Refresh Tokens**: Refresh tokens are hashed (SHA256) before being saved to the database. If the database is compromised, the tokens cannot be used by the attacker.
5. **Refresh Token Rotation**: A new refresh token is issued upon every refresh request.
6. **Reuse Detection**: If an old, already-used refresh token is presented, the system detects a potential theft and revokes **ALL** active sessions for that user.
7. **Secure Password Hashing**: Passwords are mathematically secured using **BCrypt** to make offline brute-force attacks economically unfeasible.
8. **Rate Limiting**: Brute-force protection on the API (Max 10 requests per minute per IP).
9. **Strict CORS**: `AllowAnyOrigin` is strictly prohibited. Only the specific frontend URL is allowed.
10. **Silent Refresh Interceptor**: React frontend uses an Axios Interceptor to transparently handle `401 Unauthorized` responses and retry requests without disrupting the user experience, while protecting against race conditions.

## 🚀 Technology Stack
- **Backend**: ASP.NET Core Web API (.NET 8/9), Entity Framework Core, PostgreSQL, BCrypt.Net-Next.
- **Frontend**: React, TypeScript, Vite, Axios.

---

## 🛠️ Getting Started & How to Test

### Prerequisites
- .NET 8 or 9 SDK
- Node.js & npm
- PostgreSQL running locally

### 1. Backend Setup
1. Open a terminal in the `backend` folder:
   ```bash
   cd backend
   ```
2. Set up your user secrets for the database connection and JWT secret. **Make sure your PostgreSQL is running and the credentials match**:
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=SecureJwtDb;Username=postgres;Password=your_password"
   dotnet user-secrets set "Jwt:Secret" "your_super_secret_key_that_is_at_least_32_bytes_long"
   ```
3. Apply database migrations to create the tables (`Users` and `RefreshTokens`):
   ```bash
   dotnet ef database update
   ```
4. Run the API:
   ```bash
   dotnet run
   ```
   *The backend will typically start on `http://localhost:5000` or `https://localhost:5001`.*

### 2. Frontend Setup
1. Open a new terminal in the `frontend` folder:
   ```bash
   cd frontend
   ```
2. Install dependencies:
   ```bash
   npm install
   ```
3. Run the development server:
   ```bash
   npm run dev
   ```
   *The frontend will typically start on `http://localhost:5173`.*

---

## 🧪 Step-by-Step Testing Guide

Here is how you can test the security features of this system:

### Test 1: Secure Login & HttpOnly Cookie
1. Using Postman or Swagger, register a user (you can temporarily create a Register endpoint or manually insert a BCrypt hashed password into the DB for testing).
2. Send a `POST` request to `https://localhost:5001/api/auth/login` with your email and password.
3. **Verify**: The response JSON will *only* contain `accessToken`. Check your browser/Postman Cookies tab — you will see the `refreshToken` stored there securely as `HttpOnly`.

### Test 2: Token Rotation
1. Wait 15 minutes (or temporarily change `ExpiryMinutes` to 1 in `appsettings.json` for quick testing).
2. Make an authenticated request using the expired Access Token -> You will get `401 Unauthorized`.
3. The frontend Axios Interceptor will automatically catch this 401, send a request to `/api/auth/refresh`, get a NEW access token, overwrite the old Refresh Token cookie with a new one, and retry your request.
4. **Verify**: Check the database `RefreshTokens` table. The old token will have a `RevokedAt` timestamp, and a new token will be inserted.

### Test 3: Reuse Detection (The Ultimate Security Test)
1. Log in to get a refresh token.
2. Copy the refresh token value from your browser's dev tools (Application -> Cookies).
3. Trigger a normal refresh (let the frontend do it or call `/refresh` manually). The token in your hand is now "used" (RevokedAt is set).
4. **Act like an attacker**: Open Postman and manually send a `/refresh` request using the OLD (copied) refresh token.
5. **Verify**: The API will return a strict 401 error: `"Güvenlik ihlali tespit edildi!..."`. 
6. Look at the database: **ALL** refresh tokens for that user will now have `RevokedAt` set. The attacker is kicked out, and the real user is also kicked out (forcing a safe re-login).

### Test 4: Rate Limiting
1. Rapidly send 15 requests to the `/login` endpoint within a few seconds.
2. **Verify**: The API will return `429 Too Many Requests` after the 10th request, protecting your system from brute-force password attacks.
