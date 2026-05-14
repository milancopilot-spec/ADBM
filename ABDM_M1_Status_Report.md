# ABDM M1 Integration — Status Report

**Project:** ABDM Integration — .NET MVC 5
**Module:** M1 — ABHA Health ID
**Prepared:** May 2026
**Stack:** .NET Framework 4.8 / ASP.NET MVC 5 / C#
**Sandbox Client ID:** SBXID_034527

---

## 1. Executive Summary

This report documents the current implementation status of ABDM Milestone 1 (M1) — the ABHA Health ID module. M1 is the foundational identity layer of the Ayushman Bharat Digital Mission that allows healthcare facilities to create, verify, and link patient ABHA (Ayushman Bharat Health Account) numbers at the point of registration.

Of the **6 mandatory flows** required by NHA for M1 certification, **1 flow is substantially complete** (Aadhaar OTP creation), **1 is partially complete** (ABHA profile lookup), and **4 flows are not yet started**. Database persistence and linking token management — prerequisites for all verification flows — are also absent.

The project is at approximately **25% completion** for M1 certification readiness.

---

## 2. What Is M1?

M1 is the first ABDM integration milestone. It establishes digital patient identity at healthcare facilities and is a prerequisite for M2 (Health Facility Registry) and M3 (Consent + FHIR data exchange).

**Core objective:** Every patient visiting a facility must be able to create or verify their 14-digit ABHA number, which is then linked to their internal patient record.

**M1 is complete when:**
- All 3 ABHA creation methods are working
- All 3 ABHA verification methods are working
- Patient records are persistently linked to ABHA IDs
- Functional audit passed by NHA-certified agency
- Security audit (WASA certificate) obtained
- NHA team approves for production

---

## 3. M1 Mandatory Requirements (per NHA Guidelines)

### 3.1 ABHA Creation — 3 Methods (all mandatory)

| # | Method | Description |
|---|--------|-------------|
| 1 | **Aadhaar OTP** | Patient's Aadhaar-linked mobile receives OTP → verify → 14-digit ABHA generated |
| 2 | **Aadhaar Biometric** | Fingerprint/iris captured via RD device → encrypted PID sent → ABHA generated |
| 3 | **Aadhaar Demographic** | Name, DOB, gender matched from Aadhaar → mobile OTP confirmation → ABHA generated |

### 3.2 ABHA Verification — 3 Methods (all mandatory)

| # | Method | OTP Required | Description |
|---|--------|-------------|-------------|
| 1 | **QR Code Scan** | No | Patient scans facility QR code → HIE-CM returns profile instantly |
| 2 | **Manual ABHA Entry** | No | Patient types ABHA number → system lookups and confirms |
| 3 | **Verbal ABHA Sharing** | Yes | Staff enters spoken ABHA → OTP sent to patient mobile → verified |

### 3.3 Patient Linking (mandatory)

- Every ABHA must be linked to the facility's internal patient record
- Linking tokens (24-hour validity) must be generated and stored
- Duplicate ABHA linkages must be prevented
- All linking operations must be logged with transaction IDs

### 3.4 Certification Process (mandatory)

| Step | Requirement |
|------|-------------|
| Functional audit | Testing by FIME India, Suma Soft, or Tata Communications |
| Security audit | "Safe-to-Host" certificate from STQC or CERT-IN empaneled agency |
| NHA submission | Functional test reports + security certificate + implementation summary |
| Production access | Granted by NHA after approval |

### 3.5 Technical Requirements (mandatory)

| Requirement | Detail |
|-------------|--------|
| Encryption | RSA/ECB/OAEPWithSHA-1AndMGF1Padding for Aadhaar, OTP, mobile before transmission |
| Transport | TLS 1.2 or higher for all API calls |
| Headers | `Authorization`, `REQUEST-ID`, `TIMESTAMP`, `X-CM-ID` on every request |
| Token TTL | Session tokens expire in 1200 seconds; must be refreshed proactively |

---

## 4. ABDM v3 API Reference (M1)

**Base URL (Sandbox):** `https://abhasbx.abdm.gov.in/abha/api`

| # | Method | Endpoint | Purpose | Status |
|---|--------|----------|---------|--------|
| 1 | GET | `/v3/profile/public/certificate` | Fetch RSA public key for encryption | ✅ Implemented |
| 2 | POST | `/v3/enrollment/request/otp` | Send OTP to Aadhaar-linked mobile | ✅ Implemented |
| 3 | POST | `/v3/enrollment/enrol/byAadhaar` | Verify OTP + create ABHA | ✅ Implemented |
| 4 | POST | `/v3/enrollment/auth/byAbdm` | Verify mobile OTP during enrollment | ❌ Not started |
| 5 | POST | `/v1/registration/aadhaar/verifyBio` | Biometric ABHA creation | ❌ Not started |
| 6 | POST | `/v1/registration/aadhaar/generateMobileOTP` | Demographic flow mobile OTP | ✅ Implemented |
| 7 | POST | `/v1/registration/aadhaar/verifyMobileOTP` | Demographic flow OTP verify | ✅ Implemented |
| 8 | GET | `/v1/search/existsByHealthId` | Profile lookup by ABHA | ✅ Implemented |
| 9 | POST | `/v3/profile/login/request/otp` | Verbal sharing — send OTP | ❌ Not started |
| 10 | POST | `/v3/profile/login/verify` | Verbal sharing — verify OTP | ❌ Not started |
| 11 | POST | `/v1/profile/deactivate` | Soft-delete ABHA | ✅ Implemented |

---

## 5. Current Implementation Status

### 5.1 Architecture Implemented

```
Browser (Bootstrap 5 UI)
        │
        ▼
M1HealthIdController.cs     — 7 API endpoints + Bootstrap 5 dashboard view
        │
        ▼
AbdmEncryptionService.cs    — RSA OAEP SHA-1 encryption (no external library)
M1HealthIdService.cs        — ABHA v3 API calls
        │
        ▼
AbdmBaseService.cs          — Shared HTTP, headers, 401-retry
AbdmAuthService.cs          — Token fetch + cache (HttpRuntime.Cache)
        │
        ▼
ABDM ABHA v3 API (abhasbx.abdm.gov.in)
```

### 5.2 Flow Status

#### Flow 1 — Aadhaar OTP Creation
**Status: Substantially Complete**

| Step | Code | Tested | Notes |
|------|------|--------|-------|
| Fetch RSA public key | ✅ | ✅ Live | 4096-bit key, cached 1 hour |
| Encrypt Aadhaar (RSA OAEP SHA-1) | ✅ | ✅ Live | 684-char base64 output confirmed |
| POST /v3/enrollment/request/otp | ✅ | ✅ Live | API responds; blocked by sandbox Aadhaar |
| POST /v3/enrollment/enrol/byAadhaar | ✅ | ⚠ Partial | Code written; requires step 1 success |
| Mobile field in enrollment request | ✅ | — | Added per v3 spec |
| Bootstrap UI (tab + forms) | ✅ | — | txnId auto-populated step 1→2 |

**Blocker:** v3 sandbox requires Aadhaar numbers specific to each client ID. These must be obtained from the ABDM sandbox portal under application test data. The old `999900000001–3` series is v1-only.

---

#### Flow 2 — Aadhaar Biometric Creation
**Status: Not Started**

Requires:
- Physical RD (Registered Device) hardware — fingerprint/iris scanner
- Device SDK integration for encrypted PID block generation
- `/v1/registration/aadhaar/verifyBio` API implementation

This flow cannot be fully tested without hardware. It is typically implemented by healthcare facility IT teams with physical device access.

---

#### Flow 3 — Aadhaar Demographic Creation
**Status: Partially implemented (mobile OTP endpoints exist, demographic flow not wired)**

The mobile OTP endpoints (`generateMobileOTP`, `verifyMobileOTP`) are implemented in `M1HealthIdService.cs` but the full demographic verification flow (name + DOB + gender matching) is not connected to a UI or dedicated controller flow.

---

#### Flow 4 — QR Code Scan Verification
**Status: Not Started**

Requires:
- Facility QR code generation: `https://phrsbx.abdm.gov.in/share-profile?hip-id=[ID]&counter-id=[ID]`
- QR display at registration counters (physical or on-screen)
- Callback endpoint to receive patient profile after QR scan
- No OTP required — fastest verification method

---

#### Flow 5 — Manual ABHA Entry
**Status: Partial**

Profile lookup (`GET /v1/search/existsByHealthId`) is implemented and returns the ABHA profile. However:
- No UI form for staff to enter an ABHA number
- No linking token generation after successful lookup
- No storage of the patient↔ABHA mapping

---

#### Flow 6 — Verbal ABHA + OTP Verification
**Status: Not Started**

Requires:
- `POST /v3/profile/login/request/otp` — trigger OTP to patient's mobile
- `POST /v3/profile/login/verify` — verify OTP
- UI for staff to enter the spoken ABHA number and OTP
- Linking token storage after successful verification

---

### 5.3 Supporting Infrastructure Status

| Component | Status | Notes |
|-----------|--------|-------|
| Session auth (token) | ✅ Working | Confirmed live — 1200s TTL |
| RSA OAEP encryption | ✅ Working | Confirmed live — correct 684-char output |
| Request headers | ✅ Correct | REQUEST-ID, TIMESTAMP, X-CM-ID, Authorization |
| Bootstrap 5 UI | ✅ Built | 4-tab dashboard at /M1HealthId |
| Error handling | ✅ Built | AbdmExceptionFilter → JSON envelope |
| Database persistence | ❌ Missing | No DB — linking tokens, patient mapping not stored |
| Linking token management | ❌ Missing | 24-hour tokens not generated or stored |
| Duplicate ABHA check | ❌ Missing | Requires DB |
| Transaction logging | ❌ Missing | Only Trace logs; no structured audit trail |
| M2 reflection hack fix | ❌ Pending | M2FacilityService still uses reflection (same bug fixed in M1) |

---

## 6. Gap Analysis

### 6.1 Completion by Flow

```
Flow 1  Aadhaar OTP Creation        ████████░░  80%  (blocked on sandbox Aadhaar)
Flow 2  Biometric Creation          ░░░░░░░░░░   0%  (needs RD hardware)
Flow 3  Demographic Creation        ██░░░░░░░░  20%  (mobile OTP endpoints exist)
Flow 4  QR Code Scan                ░░░░░░░░░░   0%
Flow 5  Manual ABHA Entry           ████░░░░░░  40%  (lookup exists, no linking)
Flow 6  Verbal ABHA + OTP           ░░░░░░░░░░   0%
Database / Linking                  ░░░░░░░░░░   0%
─────────────────────────────────────────────────
Overall M1 readiness                ███░░░░░░░  ~25%
```

### 6.2 Priority Gap List

| Priority | Gap | Effort | Impact |
|----------|-----|--------|--------|
| 🔴 High | Sandbox test Aadhaar numbers (from portal) | Low — data only | Unblocks Flow 1 end-to-end |
| 🔴 High | Database integration (EF/Dapper) | Medium | Required for all linking flows |
| 🔴 High | Linking token generation + storage | Medium | Required for certification |
| 🔴 High | QR code scan verification (Flow 4) | Medium | Fastest patient flow — high usage |
| 🔴 High | Verbal ABHA + OTP login (Flow 6) | Medium | Mandatory for certification |
| 🟡 Medium | Full demographic creation flow (Flow 3) | Medium | Mandatory but less common |
| 🟡 Medium | Manual ABHA entry UI + linking (Flow 5) | Low-Medium | Partial code exists |
| 🟡 Medium | Fix M2FacilityService reflection hack | Low | Code quality |
| 🟢 Low | Biometric flow (Flow 2) | High — needs hardware | Can defer until hardware available |
| 🟢 Low | Proactive token refresh (every 15 min) | Low | Reliability improvement |

---

## 7. What Needs to Be Built Next

### Phase 1 — Unblock Flow 1 (1–2 days)
1. Log into ABDM sandbox portal → get test Aadhaar numbers for SBXID_034527
2. Run end-to-end: OTP → verify → ABHA created → profile displayed

### Phase 2 — Database Layer (3–5 days)
1. Choose: Entity Framework 6 or Dapper (both compatible with .NET Framework 4.8)
2. Tables needed:
   - `AbhaPatients` (PatientId, AbhaNumber, AbhaAddress, LinkingToken, TokenExpiry)
   - `AbdmTransactions` (TxnId, PatientId, Flow, Status, CreatedAt)
3. Wire DB into all callback controllers and linking flows

### Phase 3 — Missing Flows (5–7 days)
1. **QR Code Scan** — generate facility QR, add callback endpoint, wire to patient record
2. **Verbal ABHA + OTP** — add `/v3/profile/login/request/otp` + `/verify` + UI
3. **Manual ABHA linking** — extend existing lookup to generate + store linking token
4. **Demographic flow** — complete the enrollment form with name/DOB/gender inputs

### Phase 4 — Certification Readiness (2–3 weeks)
1. Contact NHA-approved testing agency (FIME India / Suma Soft / Tata Communications)
2. Run all test cases against sandbox
3. Obtain WASA security certificate (STQC or CERT-IN empaneled agency)
4. Submit to NHA for production approval

---

## 8. Verified Live Results

The following were confirmed working against the ABDM sandbox during development:

| Test | Result |
|------|--------|
| Auth `POST /v0.5/sessions` with SBXID_034527 | ✅ Token issued (1200s TTL) |
| Cert `GET /v3/profile/public/certificate` | ✅ RSA-4096 SPKI key returned |
| RSA OAEP SHA-1 encryption of Aadhaar | ✅ 684-char base64 — correct for RSA-4096 |
| `POST /v3/enrollment/request/otp` — request sent | ✅ API responds |
| Same endpoint with sandbox Aadhaar | ❌ `"Invalid LoginId"` — v3 sandbox Aadhaar required |

---

## 9. Recommendations

1. **Immediate:** Log into the ABDM sandbox portal and retrieve the v3-specific test Aadhaar numbers assigned to SBXID_034527. This unblocks end-to-end testing of Flow 1 with no code changes.

2. **Short term:** Add a database before building any more flows. Without persistence, the linking token and patient mapping cannot be implemented, and NHA will not certify.

3. **Biometric flow:** Defer until physical RD device hardware is available. This flow requires a government-approved fingerprint/iris scanner and its SDK — it cannot be simulated in software.

4. **Target timeline:** With a focused team, M1 sandbox completion (all 6 flows passing) is achievable in **3–4 weeks**. Certification (audit + NHA approval) adds another **4–6 weeks**.

5. **Consider scope:** If the facility does not have biometric scanners at registration counters, apply for a waiver with NHA for the biometric flow. Many facilities are certified with only OTP and QR flows operational.

---

## 10. Appendix — File Reference

| File | Purpose |
|------|---------|
| `Services/AbdmAuthService.cs` | Token fetch + cache |
| `Services/AbdmBaseService.cs` | Shared HTTP client, headers, retry |
| `Services/AbdmEncryptionService.cs` | RSA OAEP SHA-1 (no external library) |
| `Services/M1HealthIdService.cs` | All ABHA v3 API calls |
| `Controllers/M1HealthIdController.cs` | 7 JSON API endpoints + UI action |
| `Models/AbdmModels.cs` | All DTOs including v3 M1 models |
| `Views/M1HealthId/Index.cshtml` | Bootstrap 5 dashboard (4 tabs) |
| `Web.config` | Base URLs, credentials, binding redirects |
| `ABDM.csproj` | .NET 4.8 MVC 5 project file |
| `ABDM.sln` | Visual Studio 2022 solution |

---

*Report generated from live sandbox testing and NHA/ABDM official documentation.*
*For certification queries contact: abdm.support@nha.gov.in*
