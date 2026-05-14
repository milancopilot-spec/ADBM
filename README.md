# ABDM Integration — .NET MVC 5

Complete integration project for **Ayushman Bharat Digital Mission (ABDM)** covering:

- **M1** — ABHA Health ID (v3 API with RSA OAEP encryption)
- **M2** — Health Facility Registry (HFR)
- **M3** — Consent + FHIR data exchange

**Stack:** .NET Framework 4.8 / ASP.NET MVC 5 / C# / Bootstrap 5

---

## Quick Start

### Prerequisites

| Tool | Version |
|------|---------|
| Visual Studio | 2019 or 2022 (with ASP.NET workload) |
| .NET Framework | 4.8 |

### 1 — Open the solution

```
File → Open → Solution → ABDM.sln
```

### 2 — Restore NuGet packages

Right-click the solution → **Restore NuGet Packages**

Or via Package Manager Console:

```powershell
nuget restore ABDM.sln
```

### 3 — Set credentials in Web.config

```xml
<add key="ABDM_CLIENT_ID"     value="YOUR_CLIENT_ID"  />
<add key="ABDM_CLIENT_SECRET" value="YOUR_CLIENT_SECRET" />
```

Get credentials at **https://sandbox.abdm.gov.in** → Integrator Portal → Create Application.

### 4 — Run

Press **F5** in Visual Studio. IIS Express starts automatically and opens the browser at `/M1HealthId`.

---

## Project Structure

```
ABDM/
├── Controllers/
│   ├── M1HealthIdController.cs     GET /M1HealthId (UI) + JSON API endpoints
│   ├── M2FacilityController.cs     HFR CRUD
│   └── M3ConsentController.cs      Consent init/status/artefact + HIU callbacks
│
├── Services/
│   ├── AbdmAuthService.cs          Token fetch + cache (HttpRuntime.Cache)
│   ├── AbdmBaseService.cs          Shared HTTP client, headers, 401-retry
│   ├── AbdmEncryptionService.cs    RSA OAEP SHA-1 encryption (M1 v3 required)
│   ├── M1HealthIdService.cs        ABHA v3 enrollment flow
│   ├── M2FacilityService.cs        HFR CRUD
│   └── M3ConsentService.cs         Consent lifecycle + FHIR parsing
│
├── Models/
│   └── AbdmModels.cs               All request/response DTOs (v3 M1 + M2 + M3)
│
├── Views/
│   ├── M1HealthId/Index.cshtml     Bootstrap 5 dashboard (4 tabs)
│   ├── Shared/_Layout.cshtml       Site layout with navbar
│   ├── _ViewStart.cshtml
│   └── Web.config                  Razor engine config
│
├── Filters/
│   └── AbdmExceptionFilter.cs      Converts AbdmException → JSON error envelope
│
├── App_Start/
│   ├── FilterConfig.cs
│   └── RouteConfig.cs              Enables attribute routing
│
├── Global.asax / Global.asax.cs    Application startup
├── Web.config                      App settings + binding redirects
├── ABDM.csproj                     Project file (.NET Framework 4.8 / MVC 5)
├── ABDM.sln
└── ABDM_Postman_Collection.json    Ready-to-import Postman collection
```

---

## Architecture

```
Browser / Postman
      │
      ▼
MVC Controllers  (validate input → call service)
 M1HealthIdController
 M2FacilityController
 M3ConsentController + M3CallbackController
      │
      ▼
Service Layer
 AbdmEncryptionService  ←  fetches cert, RSA-encrypts Aadhaar/OTP/mobile
 M1HealthIdService      ←  ABHA v3 enrollment endpoints
 M2FacilityService      ←  HFR CRUD endpoints
 M3ConsentService       ←  Consent lifecycle + FHIR parsing
      │
      ▼
AbdmBaseService  (shared: Bearer token injection, headers, 401 retry)
      │
      ▼
AbdmAuthService  (POST /v0.5/sessions → token cached in HttpRuntime.Cache)
      │
      ▼
ABDM Gateway / ABHA v3 API / HFR API
```

---

## Configuration Reference (Web.config)

| Key | Sandbox value | Purpose |
|-----|---------------|---------|
| `ABDM_CLIENT_ID` | `SBXID_XXXXXX` | Sandbox client ID |
| `ABDM_CLIENT_SECRET` | `xxxxxxxx-xxxx-...` | Sandbox client secret |
| `ABDM_BASE_URL` | `https://dev.abdm.gov.in/gateway` | Gateway (auth, M3) |
| `ABDM_ABHA_BASE_URL` | `https://abhasbx.abdm.gov.in/abha/api` | ABHA v3 API (M1) |
| `ABDM_HFR_BASE_URL` | `https://facilitysbx.abdm.gov.in` | HFR API (M2) |
| `ABDM_HIU_CALLBACK_BASE` | `https://your-ngrok-url.io` | Where ABDM POSTs callbacks |
| `ABDM_CM_ID` | `sbx` | Consent Manager ID (sandbox) |

> **Local callback tip:** use [ngrok](https://ngrok.com):
> `ngrok http 8080` → copy the HTTPS URL → set as `ABDM_HIU_CALLBACK_BASE`

---

## M1 — ABHA Health ID (v3)

**Base URL:** `https://abhasbx.abdm.gov.in/abha/api`

### Encryption requirement

The v3 API requires **all sensitive fields** (Aadhaar, OTP, mobile) to be encrypted before transmission:

- **Algorithm:** `RSA/ECB/OAEPWithSHA-1AndMGF1Padding`
- **Key source:** `GET /v3/profile/public/certificate` (4096-bit key, cached 1 hour)
- **Implemented in:** `AbdmEncryptionService.cs` — no external library needed (uses `RSACryptoServiceProvider`)

### Enrollment flow

```
Step 1  POST /M1HealthId/GenerateOtp
        Body: { "aadhaar": "999900000001" }
        → encrypts Aadhaar internally → calls /v3/enrollment/request/otp
        → returns { txnId, message }

Step 2  POST /M1HealthId/VerifyOtpAndCreate
        Body: { "txnId": "...", "otp": "123456", "mobile": "9XXXXXXXXX" }
        → encrypts OTP + mobile → calls /v3/enrollment/enrol/byAadhaar
        → returns ABHAProfile + tokens
```

### App endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/M1HealthId` | Bootstrap 5 dashboard UI |
| `POST` | `/M1HealthId/GenerateOtp` | Send Aadhaar OTP |
| `POST` | `/M1HealthId/VerifyOtpAndCreate` | Verify OTP → create ABHA |
| `GET` | `/M1HealthId/GetProfile/{healthId}` | Fetch existing ABHA profile |
| `POST` | `/M1HealthId/GenerateMobileOtp` | Send mobile update OTP |
| `POST` | `/M1HealthId/VerifyMobileOtp` | Confirm mobile update |
| `POST` | `/M1HealthId/Deactivate` | Soft-delete ABHA |

### ABDM API endpoints called internally

```
GET  /v3/profile/public/certificate          ← cert for RSA encryption
POST /v3/enrollment/request/otp             ← step 1
POST /v3/enrollment/enrol/byAadhaar         ← step 2
GET  /v1/search/existsByHealthId            ← profile lookup
POST /v1/registration/aadhaar/generateMobileOTP
POST /v1/registration/aadhaar/verifyMobileOTP
POST /v1/profile/deactivate
```

### Sample requests

**Step 1 — Generate OTP**

```json
POST /M1HealthId/GenerateOtp
{ "aadhaar": "999900000001" }

// Success
{
  "success": true,
  "message": "OTP sent successfully.",
  "data": { "txnId": "a825f76b-0696-40f3-864c-5a3a5b389a84", "message": "OTP sent to Aadhaar-linked mobile" }
}
```

**Step 2 — Verify OTP & Create ABHA**

```json
POST /M1HealthId/VerifyOtpAndCreate
{ "txnId": "a825f76b-...", "otp": "123456", "mobile": "9876543210" }

// Success
{
  "success": true,
  "message": "ABHA created successfully.",
  "data": {
    "txnId": "a825f76b-...",
    "ABHAProfile": {
      "ABHANumber": "91-1234-5678-9012",
      "firstName": "Ravi",
      "lastName": "Kumar",
      "dateOfBirth": "1990-05-15",
      "gender": "M",
      "mobile": "9876543210",
      "abhaStatus": "ACTIVE"
    },
    "isNew": true
  }
}
```

### Sandbox test Aadhaar numbers

> **Note:** The v3 sandbox uses Aadhaar numbers specific to your sandbox account.
> Log in to [https://sandbox.abdm.gov.in](https://sandbox.abdm.gov.in) with your client credentials
> and look for **Test Aadhaar Numbers** under your application settings.

The old v1 test numbers (`999900000001–3`) are **not valid for the v3 API**.

The OTP in sandbox is typically `123456`.

---

## M2 — Health Facility Registry (HFR)

**Base URL:** `https://facilitysbx.abdm.gov.in`

| Method | Path | Purpose | ABDM endpoint |
|--------|------|---------|---------------|
| `POST` | `/M2Facility/Register` | Create facility | `POST /v2/facility/add` |
| `GET` | `/M2Facility/Get/{id}` | Fetch by ID | `GET /v2/facility/{id}` |
| `GET` | `/M2Facility/Search?state=&district=&name=` | Search | `GET /v2/facility/search` |
| `PUT` | `/M2Facility/Update/{id}` | Update details | `PUT /v2/facility/{id}` |
| `DELETE` | `/M2Facility/Delete/{id}` | Deactivate | `DELETE /v2/facility/{id}` |

**Sample — Register Facility**

```json
POST /M2Facility/Register
{
  "facilityName": "City Health Clinic",
  "facilityType": "Hospital",
  "ownership": "Private",
  "address": {
    "buildingNo": "12A", "locality": "MG Road",
    "district": "Bengaluru Urban", "state": "Karnataka", "pincode": "560001"
  },
  "contact": { "mobile": "9876543210", "email": "admin@cityclinic.in" },
  "services": ["GeneralMedicine", "Radiology"]
}
```

---

## M3 — Consent & FHIR Data Exchange

**Base URL:** `https://dev.abdm.gov.in/gateway`

### Outbound (your app → ABDM gateway)

| Method | Path | ABDM endpoint | Purpose |
|--------|------|---------------|---------|
| `POST` | `/M3Consent/Init` | `POST /v0.5/consent-requests/init` | Raise consent request |
| `GET` | `/M3Consent/Status/{requestId}` | `GET /v0.5/consent-requests/{id}/status` | Poll status |
| `GET` | `/M3Consent/Artefact/{consentId}` | `GET /v0.5/consents/{id}` | Fetch granted artefact |
| `POST` | `/M3Consent/RequestHealthInfo` | `POST /v0.5/health-information/cm/request` | Trigger FHIR push |
| `POST` | `/M3Consent/Revoke/{consentId}` | `POST /v0.5/consents/revoke` | Revoke consent |

### Inbound callbacks (ABDM → your app)

Register these URLs in the ABDM sandbox portal as your HIU callback base:

| Path | Event |
|------|-------|
| `POST /M3Callback/ConsentNotify` | Patient approved or denied consent |
| `POST /M3Callback/HealthDataReceive` | HIP pushed encrypted FHIR bundles |
| `POST /M3Callback/ConsentRevoke` | Consent revoked by patient |

All callbacks return `202 Accepted` to ABDM.

### M3 FHIR encryption (pending — BouncyCastle)

ABDM encrypts health data with **ECDH + AES-256-GCM**. The `HealthDataReceive` callback currently logs raw ciphertext. To decrypt, install BouncyCastle and follow this pattern:

```powershell
Install-Package Portable.BouncyCastle
```

```csharp
var agreement = new ECDHBasicAgreement();
agreement.Init(yourPrivateKey);
var sharedKey = agreement.CalculateAgreement(senderPublicKey).ToByteArrayUnsigned();
var aesKey    = SHA256.HashData(sharedKey)[..32];

var cipher    = new GcmBlockCipher(new AesEngine());
cipher.Init(false, new AeadParameters(new KeyParameter(aesKey), 128, nonce));
var plain     = new byte[cipher.GetOutputSize(ciphertext.Length)];
cipher.DoFinal(plain, cipher.ProcessBytes(ciphertext, 0, ciphertext.Length, plain, 0));

var fhirJson  = Encoding.UTF8.GetString(plain);
var bundle    = _service.ParseFhirBundle(fhirJson);
```

---

## Headers sent on every ABDM request

| Header | Value |
|--------|-------|
| `Authorization` | `Bearer <token>` |
| `REQUEST-ID` | New UUID per request |
| `TIMESTAMP` | ISO-8601 UTC (`yyyy-MM-ddTHH:mm:ss.fffZ`) |
| `X-CM-ID` | `sbx` (sandbox) / your CM-ID (production) |
| `Content-Type` | `application/json` |

---

## Auth & token lifecycle

| Item | Detail |
|------|--------|
| Endpoint | `POST https://dev.abdm.gov.in/gateway/v0.5/sessions` |
| Body | `{ clientId, clientSecret }` |
| Token TTL | **1200 seconds** (20 min) in sandbox |
| Cache | Stored in `HttpRuntime.Cache`, evicted 60 s before expiry |
| 401 recovery | `AbdmBaseService` refreshes token once and retries automatically |

---

## Error response envelope

All endpoints return a consistent JSON shape:

```json
// Success
{ "success": true, "message": "...", "data": { ... } }

// Error
{ "success": false, "message": "ABDM API error 400 – Invalid Aadhaar", "detail": { ... } }
```

| HTTP status | Cause |
|-------------|-------|
| 400 | Bad request or ABDM validation error |
| 401 | Token expired (auto-retried once); if persists, check credentials |
| 500 | Unexpected server error (stack trace suppressed in response) |
| 503 | ABDM gateway unreachable |

Logs go to `System.Diagnostics.Trace`. Replace `Trace.TraceError(...)` calls with your `ILogger` instance to forward to Serilog or NLog.

---

## Known limitations & next steps

| # | Item | Status |
|---|------|--------|
| 1 | M3 FHIR decryption | Stub in `M3CallbackController` — wire in BouncyCastle (see above) |
| 2 | Database persistence | Callback controllers log to Trace only — add EF/Dapper context |
| 3 | Refresh token | Auth re-fetches full token on expiry — extend `AbdmAuthService.FetchTokenAsync` to use the `refreshToken` field |
| 4 | M3 payload structure | Uses v0.5 gateway format — validate against NHA v3 spec before Milestone 3 certification |
| 5 | HPR (Health Professional Registry) | Not implemented — add `M3HprService` targeting `https://hprsbx.abdm.gov.in` |
| 6 | Rate limiting | ABDM sandbox: ~10 req/s — add Polly retry/backoff for production |
| 7 | M1 v3 sandbox Aadhaar | Obtain test Aadhaar numbers from the ABDM sandbox portal (v3-specific, not the old `9999000000XX` series) |
