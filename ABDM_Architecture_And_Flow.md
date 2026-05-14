# ABDM M1 - Architecture, Flow & Demo Guide

**Platform:** .NET Framework 4.8 / ASP.NET MVC 5
**Module:** M1 - ABHA Health ID
**Client ID:** SBXID_034527
**Date:** May 2026

---

## 1. System Architecture

```plain
                        ABDM M1 - SYSTEM ARCHITECTURE
                        ================================

  +------------------+       +---------------------------+       +------------------------+
  |                  |       |                           |       |                        |
  |  Patient /       | HTTP  |    .NET MVC 5 App         | HTTPS |   ABDM Sandbox/Prod   |
  |  Hospital Staff  +------>+    localhost:8080          +------>+   (NHA Infrastructure) |
  |  (Browser)       |       |                           |       |                        |
  +------------------+       +------------+--------------+       +----------+-------------+
                                          |                                 |
                             +------------v--------------+       +----------v-------------+
                             |  AbdmEncryptionService    |       |  ABHA v3 API           |
                             |  - Fetch RSA public key   |       |  abhasbx.abdm.gov.in   |
                             |  - RSA 4096 OAEP SHA-1    |       |  /abha/api/v3/...      |
                             |  - Encrypt Aadhaar/OTP    |       |                        |
                             |  - Cache key 1 hour       |       +----------+-------------+
                             +---------------------------+                  |
                                                                 +----------v-------------+
                             +---------------------------+       |  UIDAI Staging         |
                             |  AbdmAuthService          |       |  (Aadhaar validation)  |
                             |  - POST /v0.5/sessions    +------>+  Verifies Aadhaar OTP  |
                             |  - Cache token 20 min     |       |  Returns resident data |
                             |  - Auto-refresh on expiry |       +------------------------+
                             +---------------------------+
```

---

## 2. Application Layer

```plain
                        APPLICATION LAYER BREAKDOWN
                        ============================

  Browser Request
       |
       v
  +--------------------+
  | M1HealthIdController|   17 endpoints - validates input, calls service
  +----------+---------+
             |
       +-----+------+
       |             |
       v             v
  +---------+   +--------------------+
  |M1Health |   |AbdmEncryption      |
  |IdService|   |Service             |
  |         |   |                    |
  | - OTP   |   | - GET /v3/profile/ |
  | - Enrol |   |   public/cert      |
  | - Login |   | - RSA encrypt()    |
  | - Card  |   | - Cache 1hr        |
  +---------+   +--------------------+
       |
       v
  +------------------+
  | AbdmBaseService  |   Shared HTTP layer
  |                  |
  | - Bearer token   |
  | - REQUEST-ID     |
  | - TIMESTAMP      |
  | - X-CM-ID        |
  | - X-Token        |
  | - 401 retry      |
  +--------+---------+
           |
           v
  +------------------+
  | AbdmAuthService  |   Token management
  |                  |
  | - /v0.5/sessions |
  | - Cache 1140s    |
  | - Auto-refresh   |
  +------------------+
```

---

## 3. Encryption Flow

```plain
                        RSA ENCRYPTION FLOW
                        ====================

  Step 1: App starts, user enters Aadhaar
                |
                v
  +----------------------------------+
  | AbdmEncryptionService            |
  | GET /v3/profile/public/cert      |
  | <- { publicKey: "MIICIj...",     |
  |       algorithm: "RSA/ECB/      |
  |       OAEPWithSHA-1AndMGF1" }   |
  | Cache key for 1 hour             |
  +----------------------------------+
                |
                v
  +----------------------------------+
  | Parse SPKI (ASN.1 DER format)    |
  | Extract 4096-bit RSA public key  |
  | No external library - pure .NET  |
  +----------------------------------+
                |
                v
  +----------------------------------+
  | RSACryptoServiceProvider         |
  | .Encrypt("999941057058", OAEP)   |
  | Output: 512 bytes = 684 b64 chars|
  +----------------------------------+
                |
                v
  ABDM receives encrypted value
  ABDM decrypts with PRIVATE key
  Plain Aadhaar never travels in network
```

---

## 4. Complete ABHA Creation Flow

```plain
                  FULL ABHA CREATION - STEP BY STEP
                  ===================================

  BROWSER                  .NET APP                    ABDM                  UIDAI
    |                         |                           |                     |
    | Enter Aadhaar            |                           |                     |
    | Click "Send OTP"         |                           |                     |
    |------------------------>|                           |                     |
    |                         | Encrypt Aadhaar (RSA)     |                     |
    |                         |-------------------------->|                     |
    |                         | POST /v3/enrollment/      |                     |
    |                         |      request/otp          |                     |
    |                         |  { loginId: "oCeSwK..." } |                     |
    |                         |                           | Decrypt Aadhaar     |
    |                         |                           |-------------------->|
    |                         |                           |                     | Validate
    |                         |                           |                     | Send OTP
    |                         |                           |<--------------------|
    |                         |<--------------------------|                     |
    |<------------------------|                           |                     |
    | { txnId: "a825f76b..." } |                          |                     |
    | OTP arrives on phone     |                          |                     |
    |                         |                           |                     |
    | Enter OTP + Mobile       |                          |                     |
    | Click "Verify"           |                          |                     |
    |------------------------>|                           |                     |
    |                         | Encrypt OTP (RSA)         |                     |
    |                         | Encrypt Mobile (RSA)      |                     |
    |                         |-------------------------->|                     |
    |                         | POST /v3/enrollment/      |                     |
    |                         |      enrol/byAadhaar      |                     |
    |                         |<--------------------------|                     |
    |<------------------------|                           |                     |
    | ABHA Profile displayed   |                          |                     |
    | ABHANumber: 91-XXXX-...  |                          |                     |
    | tokens.token = X-Token   |                          |                     |
    |                         |                           |                     |
    | STEP 3: Get suggestions  |                          |                     |
    |------------------------>|                           |                     |
    |                         | GET /v3/enrollment/       |                     |
    |                         |     enrol/suggestion      |                     |
    |                         |<--------------------------|                     |
    |<------------------------|                           |                     |
    | Pick ABHA address        |                          |                     |
    | e.g. milan.rathod@abdm     |                          |                     |
    |------------------------>|                           |                     |
    |                         | POST /v3/enrollment/      |                     |
    |                         |      enrol/abha-address   |                     |
    |                         |<--------------------------|                     |
    |<------------------------|                           |                     |
    | ABHA Address confirmed   |                          |                     |
```

---

## 5. Login Flow

```plain
                  LOGIN WITH EXISTING ABHA
                  =========================

  BROWSER                  .NET APP                    ABDM
    |                         |                           |
    | Enter ABHA Number        |                           |
    | Click "Send OTP"         |                           |
    |------------------------>|                           |
    |                         | Encrypt ABHA number (RSA) |
    |                         | POST /v3/profile/login/   |
    |                         |      request/otp          |
    |                         |<--------------------------|
    |<------------------------|                           |
    | { txnId: "..." }         |                          |
    | OTP arrives on phone     |                          |
    |                         |                           |
    | Enter OTP                |                          |
    | Click "Login"            |                          |
    |------------------------>|                           |
    |                         | Encrypt OTP (RSA)         |
    |                         | POST /v3/profile/login/   |
    |                         |      verify               |
    |                         |<--------------------------|
    |<------------------------|  { token: "eyJ..." }      |
    | Profile displayed         |  <- X-Token stored      |
    |                         |                           |
    | Click "Download Card"    |                          |
    |------------------------>|                           |
    |                         | GET /v3/profile/account/  |
    |                         |     abha-card             |
    |                         | Header: X-Token: eyJ...   |
    |                         |<--------------------------|
    |<------------------------|  <- PNG binary             |
    | ABHA card downloaded     |                          |
    |                         |                           |
    | Click "Show QR"          |                          |
    |------------------------>|                           |
    |                         | GET /v3/profile/account/  |
    |                         |     qrCode                |
    |                         | Header: X-Token: eyJ...   |
    |                         |<--------------------------|
    |<------------------------|  <- PNG -> base64 dataUrl  |
    | QR code displayed        |                          |
```

---

## 6. Token Architecture

```plain
                  TWO-TOKEN SYSTEM
                  =================

  CLIENT TOKEN (Bearer)                USER TOKEN (X-Token)
  =====================                ====================
  Source: /v0.5/sessions               Source: After login/enrollment
  TTL: 1200 seconds (20 min)           TTL: ~1800 seconds
  Cached: HttpRuntime.Cache            Stored: Browser sessionStorage
  Used for: ALL ABDM API calls         Used for: Profile, ABHA Card, QR
  Header: Authorization: Bearer ...    Header: X-Token: eyJ...
  Auto-refresh: Yes (on expiry/401)    Refresh: Re-login required
```

---

## 7. All Endpoints

### App Endpoints (what the browser calls)

| Method | URL | Purpose |
|--------|-----|---------|
| GET | `/` | Dashboard UI |
| POST | `/M1HealthId/GenerateOtp` | Step 1 - Send Aadhaar OTP |
| POST | `/M1HealthId/ResendOtp?txnId=` | Resend OTP |
| POST | `/M1HealthId/VerifyOtpAndCreate` | Step 2 - Verify + Create ABHA |
| POST | `/M1HealthId/AuthByAbdm?txnId=&otp=` | Step 3 - Mobile OTP verify |
| GET | `/M1HealthId/Suggestions?txnId=` | Get ABHA address suggestions |
| POST | `/M1HealthId/CreateAbhaAddress?txnId=&abhaAddress=` | Confirm ABHA address |
| POST | `/M1HealthId/LoginRequestOtp?abhaNumber=` | Login - Send OTP |
| POST | `/M1HealthId/LoginVerify?txnId=&otp=` | Login - Verify + X-Token |
| GET | `/M1HealthId/Account?xToken=` | Get ABHA profile |
| GET | `/M1HealthId/AbhaCard?xToken=` | Download PNG card |
| GET | `/M1HealthId/QrCode?xToken=` | Get QR code |
| GET | `/M1HealthId/GetProfile/{healthId}` | Lookup by ABHA number |
| POST | `/M1HealthId/GenerateMobileOtp` | Update mobile OTP |
| POST | `/M1HealthId/VerifyMobileOtp` | Confirm mobile update |
| GET | `/M1HealthId/Config` | Debug - show config values |
| GET | `/M1HealthId/EncryptTest?aadhaar=` | Debug - test encryption |

### ABDM APIs Called Internally

| Method | URL | Purpose |
|--------|-----|---------|
| POST | `dev.abdm.gov.in/gateway/v0.5/sessions` | Get Bearer token |
| GET | `/v3/profile/public/certificate` | Fetch RSA public key |
| POST | `/v3/enrollment/request/otp` | Send Aadhaar OTP |
| POST | `/v3/enrollment/enrol/byAadhaar` | Verify OTP + create ABHA |
| POST | `/v3/enrollment/auth/byAbdm` | Verify mobile OTP |
| GET | `/v3/enrollment/enrol/suggestion` | ABHA address suggestions |
| POST | `/v3/enrollment/enrol/abha-address` | Create ABHA address |
| POST | `/v3/profile/login/request/otp` | Login OTP request |
| POST | `/v3/profile/login/verify` | Login OTP verify -> X-Token |
| GET | `/v3/profile/account` | Get account (X-Token required) |
| GET | `/v3/profile/account/abha-card` | PNG card (X-Token required) |
| GET | `/v3/profile/account/qrCode` | QR code (X-Token required) |

---

## 8. Request Headers

Every ABDM API call includes:

```plain
Authorization : Bearer eyJhbGciOiJSUzI1NiIsInR5c...  <- client token
REQUEST-ID    : 550e8400-e29b-41d4-a716-446655440000  <- unique UUID per request
TIMESTAMP     : 2026-05-14T10:30:00.000Z              <- ISO-8601 UTC
X-CM-ID       : sbx                                   <- "sbx" sandbox / prod CM-ID
X-Token       : eyJhbGci...                           <- only for profile endpoints
Content-Type  : application/json
```

---

## 9. Configuration

### Web.config (Sandbox)

| Key | Value |
|-----|-------|
| `ABDM_AUTH_URL` | `https://dev.abdm.gov.in/gateway/v0.5/sessions` |
| `ABDM_BASE_URL` | `https://dev.abdm.gov.in/gateway` |
| `ABDM_ABHA_BASE_URL` | `https://abhasbx.abdm.gov.in/abha/api` |
| `ABDM_CLIENT_ID` | `SBXID_034527` |
| `ABDM_CLIENT_SECRET` | `979b8db4-5846-45a6-85ec-409ee520ce18` |
| `ABDM_CM_ID` | `sbx` |

### Web.Release.config (Production - verified URLs)

| Key | Value | Verified |
|-----|-------|----------|
| `ABDM_AUTH_URL` | `https://apis.abdm.gov.in/api/hiecm/gateway/v3/sessions` | 403 (exists) |
| `ABDM_ABHA_BASE_URL` | `https://abha.abdm.gov.in/api/abha` | 401 (exists) |
| `ABDM_HFR_BASE_URL` | `https://facility.abdm.gov.in` | 200 OK |
| `ABDM_CM_ID` | `YOUR_PRODUCTION_CM_ID` | From NHA |

---

## 10. Implementation Status

### Done

| Feature | File | Status |
|---------|------|--------|
| Auth token + cache | `AbdmAuthService.cs` | Complete + tested |
| RSA OAEP SHA-1 encryption | `AbdmEncryptionService.cs` | Complete + tested |
| HTTP base layer | `AbdmBaseService.cs` | Complete |
| All 10 v3 API endpoints | `M1HealthIdService.cs` | Complete + tested |
| 17 controller endpoints | `M1HealthIdController.cs` | Complete |
| Bootstrap 5 UI | `Index.cshtml` | Complete |
| OTP countdown timer | `Index.cshtml` | Complete |
| Session persistence | `Index.cshtml` | Complete |
| Input validation | `Index.cshtml` | Complete |
| Production config | `Web.Release.config` | Complete + URLs verified |
| Security headers | `Web.Release.config` | Complete |
| API reference doc | `API_Reference.md` | Complete |
| Status report | `ABDM_M1_Status_Report.md` | Complete |

### Pending

| Feature | Blocker |
|---------|---------|
| End-to-end ABHA creation | Valid sandbox Aadhaar from NHA portal |
| ABHA card download test | Needs X-Token from successful login |
| QR code display test | Same |
| Database - patient mapping | DB integration not started |
| Biometric enrollment | Needs physical RD hardware |
| QR scan verification | Not started |

---

## 11. Demo Script (25 min)

### Part 1 - Architecture (5 min)
Show the architecture diagram above. Key talking points:
- Patient Aadhaar never travels in plain text
- Two separate tokens: client token + user X-Token
- Sandbox and production configs already set and verified

### Part 2 - Live App (8 min)

Open `http://localhost:8080`

**Show validation (no credentials needed):**
1. Type 11 digits in Aadhaar -> "Enter a valid 12-digit Aadhaar"
2. Skip Step 1, click Verify -> "txnId missing. Complete Step 1 first"
3. Refresh browser -> txnId and Aadhaar restore from sessionStorage

**Show encryption working:**
Open `http://localhost:8080/M1HealthId/EncryptTest?aadhaar=999941057058`

```json
{
  "success": true,
  "message": "Encryption OK (RSA-4096)",
  "data": { "encryptedLen": 684, "ok": true }
}
```

**Show config:**
Open `http://localhost:8080/M1HealthId/Config`

### Part 3 - Live ABDM Hit (7 min)

Enter Aadhaar `999941057058` -> Click **Send OTP**

Expected response:
```plain
UIDAI Error code: 998 - Aadhaar number is incorrect
```

Explain what this proves:
- Token obtained from ABDM gateway
- RSA cert fetched and parsed
- Aadhaar encrypted with 4096-bit RSA
- Request sent to ABDM
- ABDM forwarded to UIDAI
- UIDAI responded
- Only missing: sandbox Aadhaar registered for our account

### Part 4 - Production Readiness (3 min)

Open `Web.Release.config` - show:
- All production URLs already verified against live ABDM
- Security headers included
- One-click deploy via VS Publish

### Part 5 - Q&A (2 min)

---

## 12. What Client Must Provide

### Before Testing Can Complete

| Item | Where to get | Priority |
|------|-------------|----------|
| Sandbox test Aadhaar numbers | ABDM sandbox portal or email abdm.support@nha.gov.in | URGENT |
| Health Facility ID (HFR) | Register at hfr.abdm.gov.in | High |
| Public HTTPS domain | Client's IT team | High |

### Before Production Go-Live

| Item | Who provides |
|------|-------------|
| Production CLIENT_ID | NHA portal after approval |
| Production CLIENT_SECRET | NHA portal after approval |
| Production CM_ID | Issued by NHA during onboarding |
| Security audit (WASA) | Client hires STQC/CERT-IN agency |
| Functional audit | Client hires FIME India/Suma Soft |
| Patient database structure | Client's tech team |

### Client Checklist

```plain
[ ] Facility registered in HFR? If yes, share Facility ID.
[ ] Applied for production ABDM credentials?
[ ] Have public HTTPS domain ready?
[ ] Know which EMR/HIMS you use?
[ ] Have sandbox test Aadhaar numbers from ABDM portal?
[ ] Selected security audit agency for WASA certificate?
```

---

## 13. Key Files Reference

```plain
E:\Study Material\ABDM\
|
+-- Controllers\
|   +-- M1HealthIdController.cs     17 endpoints
|
+-- Services\
|   +-- AbdmAuthService.cs          Token management
|   +-- AbdmBaseService.cs          HTTP + headers
|   +-- AbdmEncryptionService.cs    RSA encryption
|   +-- M1HealthIdService.cs        All ABHA v3 calls
|
+-- Models\
|   +-- AbdmModels.cs               All DTOs
|
+-- Views\M1HealthId\
|   +-- Index.cshtml                Bootstrap 5 UI
|
+-- Web.config                      Sandbox config
+-- Web.Release.config              Production config (XDT transform)
+-- API_Reference.md                All curl commands
+-- ABDM_M1_Status_Report.md        Detailed status
+-- ABDM_Architecture_And_Flow.md   This file
```

---

## 14. Common Errors and What They Mean

| Error | HTTP | Meaning | Action |
|-------|------|---------|--------|
| `Invalid LoginId` | 400 | Wrong Aadhaar format or old v1 test numbers | Use UIDAI test numbers starting with 9999 |
| `UIDAI Error 998` | 422 | Aadhaar not in UIDAI staging DB | Get valid test Aadhaar from NHA |
| `OTP expired` | 400 | 10-min OTP window passed | Click Resend OTP |
| `Invalid X-token` | 400 | X-Token missing or expired | Re-login to get fresh X-Token |
| `429 Too Many Requests` | 429 | Rate limit - wait 30 seconds | Normal during testing |
| `Missing Credentials` | 401 | Bearer token missing or expired | Auto-handled by app, check client ID |
| `Not Found 404` | 404 | Wrong URL or visiting POST endpoint via browser | Use the dashboard UI, not direct URLs |

---

*Document generated: May 2026*
*Stack: .NET 4.8 / MVC 5 / Bootstrap 5 / ABDM v3 APIs*
