# Secure Custom JWT Authentication & Refresh Token System

A production-grade, highly secure implementation of Custom JWT Authentication and Refresh Token Rotation in ASP.NET Core (Backend) and React + TypeScript (Frontend), built from scratch without relying on external identity providers like IdentityServer or OpenIddict.

## 🛡️ Security Standards & Architecture
This project is built with strict adherence to security best practices and OWASP guidelines:

- **Short-lived Access Tokens**: JWTs expire in 15 minutes to minimize the attack surface if intercepted.
- **Stateless JWT Validation**: No database lookup is required for access token validation, relying entirely on HMAC-SHA256 signature verification.
- **HttpOnly Refresh Tokens (Planned)**: Refresh tokens will be stored in `HttpOnly`, `Secure`, and `SameSite=Strict` cookies to prevent XSS attacks. The frontend JS will never access them.
- **Hashed Refresh Tokens (Planned)**: Refresh tokens will be hashed (e.g., SHA256) before being saved to the database. If the database is compromised, the tokens cannot be used by the attacker.
- **Refresh Token Rotation & Reuse Detection (Planned)**: A new refresh token is issued upon every refresh request. If an old, already-used refresh token is presented, the system detects a potential theft and revokes all active sessions for that user.
- **Secure Password Hashing**: Passwords are mathematically secured using **BCrypt** to make offline brute-force attacks economically unfeasible.
- **Secret Management**: Sensitive data such as connection strings and JWT secrets are kept out of source control using `dotnet user-secrets`.

## 🚀 Technology Stack
- **Backend**: ASP.NET Core Web API (.NET 8/9), Entity Framework Core, PostgreSQL, BCrypt.Net-Next.
- **Frontend**: React, TypeScript, Vite, Axios.

## 📋 Implementation Steps
The project is being developed in 11 structured steps:
1. [x] **Project Skeleton**: Setup ASP.NET Core API and Vite React TS projects.
2. [x] **User Entity & DB Setup**: Configure Entity Framework Core with PostgreSQL and BCrypt for password hashing.
3. [x] **JWT Generation Service**: Implement secure token generation with specific claims and zero clock skew.
4. [ ] **Refresh Token Entity**: DB model for 1-to-N User-RefreshToken relationship (hashed storage).
5. [ ] **Login Endpoint**: Issue Access Token in body and Refresh Token in HttpOnly cookie.
6. [ ] **Refresh Endpoint**: Implement rotation logic (invalidate old, issue new).
7. [ ] **Reuse Detection**: Detect stolen tokens and invalidate the entire user session family.
8. [ ] **Logout Endpoint**: Revoke current token and clear cookies.
9. [ ] **Frontend Axios Interceptor**: Implement silent refresh flow on `401 Unauthorized`.
10. [ ] **Security Hardening**: Rate limiting, CORS policies, and security headers.
11. [ ] **OWASP Security Review**: Final self-review against best practices.

## 🛠️ Getting Started (Local Development)

### Prerequisites
- .NET 8 or 9 SDK
- Node.js & npm
- PostgreSQL

### Backend Setup
1. Navigate to the `backend` folder.
2. Set up your user secrets for the database connection and JWT secret:
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=SecureJwtDb;Username=postgres;Password=your_password"
   dotnet user-secrets set "Jwt:Secret" "your_super_secret_key_that_is_at_least_32_bytes_long"
   ```
3. Apply database migrations:
   ```bash
   dotnet ef database update
   ```
4. Run the API:
   ```bash
   dotnet run
   ```

### Frontend Setup
1. Navigate to the `frontend` folder.
2. Install dependencies:
   ```bash
   npm install
   ```
3. Run the development server:
   ```bash
   npm run dev
   ```
