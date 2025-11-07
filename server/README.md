# Backend Service - Key Generator

This small Node + TypeScript service exposes endpoints to generate a 280-character encrypted key that encodes user data (email, device serial, level, user id) plus an issuance timestamp, and to decode it.

Quick start

1. Copy `.env.example` to `.env` and set `ENCRYPTION_KEY` to a strong secret.
2. Install dependencies:

```powershell
npm install
```

3. Run in development mode:

```powershell
npm run dev
```

Endpoints

- POST /generate

  - body: { email: string, deviceSerial?: string, level?: number, userId?: string }
  - returns: { token: string } where `token.length === 280`. The token payload will include `timestamp` (epoch ms) and `issuedAt` (ISO string).

- POST /decode
  - body: { token: string }
  - returns: { payload: object }

Notes

- The implementation uses AES-256-GCM with a key derived from `ENCRYPTION_KEY` (sha256). The token format carries a 1-byte padding length, iv(12), authTag(16), ciphertext, and padding so that base64url encoding yields exactly 280 characters.
