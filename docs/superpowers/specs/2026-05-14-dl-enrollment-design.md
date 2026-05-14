# Design: ABHA Enrollment via Driving License (+ Dedicated Aadhaar Page)

**Date:** 2026-05-14  
**Project:** ABDM M1 Integration (.NET MVC 5)  
**Scope:** Add DL-based ABHA enrollment as a dedicated page; extract Aadhaar enrollment into its own dedicated page; restructure M1 page as a management hub.

---

## 1. Goals

1. Add ABHA creation via Driving License (3-step flow: mobile OTP → verify OTP → submit DL details + photos).
2. Give Aadhaar enrollment its own focused page (extracted from the existing Create ABHA tab).
3. Restructure the M1 page to be a management hub (Login, Update Mobile, Demographic, Deactivate) with prominent navigation buttons to both enrollment pages.

---

## 2. Architecture

### Pages

| URL | Controller Action | Purpose |
|-----|------------------|---------|
| `/M1HealthId/` | `Index` (GET) | Management hub — Login, Update Mobile, Demographic, Deactivate tabs + enrollment nav buttons |
| `/M1HealthId/AadhaarEnroll` | `AadhaarEnroll` (GET) | Dedicated Aadhaar enrollment page (3-step) |
| `/M1HealthId/DrivingLicense` | `DrivingLicense` (GET) | Dedicated DL enrollment page (3-step) |

### API Endpoints (new)

| Route | Method | Purpose |
|-------|--------|---------|
| `/M1HealthId/DlGenerateMobileOtp` | POST | Step 1 — send OTP to mobile |
| `/M1HealthId/DlVerifyMobileOtp` | POST | Step 2 — verify mobile OTP |
| `/M1HealthId/DlEnrol` | POST | Step 3 — submit DL details and create ABHA |

Existing endpoints (GenerateOtp, ResendOtp, VerifyOtpAndCreate, Suggestions, CreateAbhaAddress) remain unchanged and are reused by the Aadhaar enrollment page.

---

## 3. UI Design

### Navbar (`_Layout.cshtml`)

Add two new links alongside "M1 - Health ID":
- **Aadhaar Enroll** → `/M1HealthId/AadhaarEnroll`
- **DL Enroll** → `/M1HealthId/DrivingLicense`

Active state set via `ViewBag.Active` ("AadhaarEnroll", "DL").

### M1 Hub Page (`Index.cshtml`)

- **Remove** the "Create ABHA" (Aadhaar enrollment) tab.
- **Add** a card at the top with two prominent buttons: "Create ABHA via Aadhaar" and "Create ABHA via Driving License", linking to the respective pages.
- Keep remaining tabs: Login, Update Mobile, Demographic, Deactivate.

### Aadhaar Enroll Page (`AadhaarEnroll.cshtml`)

Extract the existing Create ABHA tab content verbatim into a new view. No functional changes — same 3-step flow (Send OTP → Verify OTP + Create → Choose ABHA Address). All existing JS logic moves with it.

### DL Enroll Page (`DrivingLicense.cshtml`)

Three-step layout:

**Step 1 — Send Mobile OTP**
- Mobile number input (10 digits)
- Send OTP button (shows spinner, enables Resend)
- Countdown timer (same pattern as Aadhaar page)
- Result area shows txnId on success

**Step 2 — Verify Mobile OTP** (revealed after Step 1 success)
- Read-only txnId field (auto-filled)
- OTP input
- Verify button
- On success, reveals Step 3

**Step 3 — DL Details + Create ABHA** (revealed after Step 2 success)
- DL Number (text input)
- First Name, Last Name (text inputs)
- Date of Birth (`<input type="date">`)
- Gender (select: Male/Female/Other)
- Address (text)
- State, District, Pincode (text inputs)
- Front Photo (`<input type="file" accept="image/*">`) — previews thumbnail
- Back Photo (`<input type="file" accept="image/*">`) — previews thumbnail
- Create ABHA button
- Result area shows ABHA enrollment number and profile on success

**Photo handling (client-side JS):**  
`FileReader.readAsDataURL()` → strip `data:image/...;base64,` prefix → send clean Base64 string.

---

## 4. Backend Design

### New Models (`AbdmModels.cs`)

```
M1DlEnrolRequest          — MVC model binding (txnId, all DL fields as strings, frontPhoto, backPhoto)
M1V3DlEnrolRequest        — ABDM API payload (txnId, scope, authData, consent)
M1V3AuthDataDl            — authData wrapper (authMethods: ["dl"], document: M1V3DlDocumentData)
M1V3DlDocumentData        — all DL fields (documentType, documentId, firstName, lastName,
                             dob, gender, address, state, district, pinCode,
                             frontSidePhoto, backSidePhoto)
M1V3DlEnrolResponse       — ABDM response (txnId, enrolmentNumber, ABHAProfile?, tokens?)
```

### New Service Methods (`M1HealthIdService`)

```csharp
DlGenerateMobileOtpAsync(string mobile)
// POST /v3/enrollment/request/otp
// scope: ["dl-flow"], loginHint: "mobile", loginId: RSA(mobile), otpSystem: "abdm"
// Returns M1V3GenerateOtpResponse (reuses existing model)

DlVerifyMobileOtpAsync(string txnId, string otp)
// POST /v3/enrollment/auth/byAbdm
// scope: ["dl-flow"], authMethods: ["otp"], otpValue: RSA(otp)
// Returns M1V3GenerateOtpResponse (txnId for next step)

DlEnrolAsync(string txnId, M1V3DlDocumentData doc)
// POST /v3/enrollment/enrol/byDocument
// DOB conversion: YYYY-MM-DD (HTML) → DD-MM-YYYY (ABDM API)
// Photos: passed as-is (already stripped Base64 from frontend)
// Returns M1V3DlEnrolResponse
```

### New Controller Actions (`M1HealthIdController`)

```csharp
[HttpGet]  AadhaarEnroll()   → View()
[HttpGet]  DrivingLicense()  → View()

[HttpPost] DlGenerateMobileOtp(string mobile)
[HttpPost] DlVerifyMobileOtp(string txnId, string otp)
[HttpPost] DlEnrol(M1DlEnrolRequest model)
```

All use the same `JsonSuccess`/`JsonError` helpers (Newtonsoft camelCase serialization, already fixed).

---

## 5. Data Flow — DL Enrollment

```
User fills mobile → POST DlGenerateMobileOtp
  → service RSA-encrypts mobile → POST /v3/enrollment/request/otp (scope: dl-flow)
  → returns txnId → JS stores in sessionStorage

User enters OTP → POST DlVerifyMobileOtp
  → service RSA-encrypts OTP → POST /v3/enrollment/auth/byAbdm
  → returns new txnId → JS stores, Step 3 revealed

User fills DL form + uploads photos → JS Base64-encodes photos → POST DlEnrol
  → service converts DOB format, builds payload → POST /v3/enrollment/enrol/byDocument
  → returns enrollment number + profile → displayed in result area
```

---

## 6. Error Handling

- Validation in JS before each API call (required fields, 10-digit mobile, file size check < 1 MB per photo)
- Controller validates required fields server-side (model properties)
- ABDM errors surfaced via `friendlyError()` pattern (reuse existing JS function)
- If DL photo is too large to send, JS shows an inline error before even submitting

---

## 7. Out of Scope

- DL enrollment does not include ABHA address suggestion/selection step (ABDM may not support it for DL flow — add if confirmed working in sandbox)
- No mobile verification secondary step (only needed if mobile entered in Step 1 differs from Aadhaar-linked mobile, not applicable for DL flow)
- No file compression — user must supply reasonably sized images

---

## 8. Files Changed

| File | Change |
|------|--------|
| `Views/Shared/_Layout.cshtml` | Add two nav links |
| `Views/M1HealthId/Index.cshtml` | Remove Create ABHA tab; add enrollment nav card |
| `Views/M1HealthId/AadhaarEnroll.cshtml` | New view — extracted Aadhaar enrollment content |
| `Views/M1HealthId/DrivingLicense.cshtml` | New view — full DL enrollment UI |
| `Models/AbdmModels.cs` | Add DL models |
| `Services/M1HealthIdService.cs` | Add 3 DL service methods |
| `Controllers/M1HealthIdController.cs` | Add 2 GET + 3 POST actions |
