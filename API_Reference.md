# ABDM M1 - API Reference

**Sandbox Client ID:** `SBXID_034527`
**App base URL:** `http://localhost:8080`

---

## 1. Auth

### Get Bearer Token

**POST** `https://dev.abdm.gov.in/gateway/v0.5/sessions`

```bash
curl -X POST https://dev.abdm.gov.in/gateway/v0.5/sessions \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "SBXID_034527",
    "clientSecret": "979b8db4-5846-45a6-85ec-409ee520ce18"
  }'
```

**Response**
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI...",
  "tokenType": "bearer",
  "expiresIn": 1200
}
```

> Save the `accessToken`. Replace `<TOKEN>` in all requests below with this value.

---

## 2. Encryption - Fetch RSA Public Key

### Get Certificate

**GET** `https://abhasbx.abdm.gov.in/abha/api/v3/profile/public/certificate`

```bash
curl -X GET https://abhasbx.abdm.gov.in/abha/api/v3/profile/public/certificate \
  -H "Authorization: Bearer <TOKEN>" \
  -H "REQUEST-ID: $(uuidgen)" \
  -H "TIMESTAMP: $(date -u +%Y-%m-%dT%H:%M:%S.000Z)" \
  -H "X-CM-ID: sbx"
```

**Response**
```json
{
  "publicKey": "MIICIjANBgkqhkiG9w0BAQEFAAOCA...",
  "encryptionAlgorithm": "RSA/ECB/OAEPWithSHA-1AndMGF1Padding"
}
```

> The `publicKey` is used to RSA-encrypt Aadhaar, OTP, and mobile before sending to the API.

---

## 3. ABHA Enrollment (v3)

> **Note:** `loginId`, `otpValue`, and `mobile` must be RSA-encrypted using the public key from step 2.
> The app handles encryption internally. Use the app endpoints below for testing.

### Step 1 - Request OTP

**POST** `https://abhasbx.abdm.gov.in/abha/api/v3/enrollment/request/otp`

```bash
curl -X POST https://abhasbx.abdm.gov.in/abha/api/v3/enrollment/request/otp \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -H "REQUEST-ID: $(uuidgen)" \
  -H "TIMESTAMP: $(date -u +%Y-%m-%dT%H:%M:%S.000Z)" \
  -H "X-CM-ID: sbx" \
  -d '{
    "txnId": "",
    "scope": ["abha-enrol"],
    "loginHint": "aadhaar",
    "loginId": "<RSA_ENCRYPTED_AADHAAR>",
    "otpSystem": "aadhaar"
  }'
```

**Response**
```json
{
  "txnId": "a825f76b-0696-40f3-864c-5a3a5b389a84",
  "message": "OTP sent to Aadhaar registered mobile ending with XXXX"
}
```

---

### Step 2 - Verify OTP and Create ABHA

**POST** `https://abhasbx.abdm.gov.in/abha/api/v3/enrollment/enrol/byAadhaar`

```bash
curl -X POST https://abhasbx.abdm.gov.in/abha/api/v3/enrollment/enrol/byAadhaar \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -H "REQUEST-ID: $(uuidgen)" \
  -H "TIMESTAMP: $(date -u +%Y-%m-%dT%H:%M:%S.000Z)" \
  -H "X-CM-ID: sbx" \
  -d '{
    "txnId": "<TXNID_FROM_STEP1>",
    "scope": ["abha-enrol"],
    "authData": {
      "authMethods": ["otp"],
      "otp": {
        "txnId": "<TXNID_FROM_STEP1>",
        "otpValue": "<RSA_ENCRYPTED_OTP>",
        "mobile": "<RSA_ENCRYPTED_MOBILE>"
      }
    },
    "consent": {
      "code": "abha-enrollment",
      "version": "1.4"
    }
  }'
```

**Response**
```json
{
  "txnId": "a825f76b-0696-40f3-864c-5a3a5b389a84",
  "ABHAProfile": {
    "ABHANumber": "91-1234-5678-9012",
    "firstName": "Ravi",
    "lastName": "Kumar",
    "dateOfBirth": "1990-05-15",
    "gender": "M",
    "mobile": "9876543210",
    "abhaStatus": "ACTIVE"
  },
  "tokens": {
    "token": "eyJhbGci...",
    "expiresIn": 1800
  },
  "isNew": true
}
```

---

### Look Up Existing ABHA Profile

**GET** `https://abhasbx.abdm.gov.in/abha/api/v1/search/existsByHealthId?healthId=<ABHA>`

```bash
curl -X GET "https://abhasbx.abdm.gov.in/abha/api/v1/search/existsByHealthId?healthId=91-1234-5678-9012" \
  -H "Authorization: Bearer <TOKEN>" \
  -H "REQUEST-ID: $(uuidgen)" \
  -H "TIMESTAMP: $(date -u +%Y-%m-%dT%H:%M:%S.000Z)" \
  -H "X-CM-ID: sbx"
```

---

### Update Mobile - Send OTP

**POST** `https://abhasbx.abdm.gov.in/abha/api/v1/registration/aadhaar/generateMobileOTP`

```bash
curl -X POST https://abhasbx.abdm.gov.in/abha/api/v1/registration/aadhaar/generateMobileOTP \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -H "REQUEST-ID: $(uuidgen)" \
  -H "TIMESTAMP: $(date -u +%Y-%m-%dT%H:%M:%S.000Z)" \
  -H "X-CM-ID: sbx" \
  -d '{
    "txnId": "<TXNID>",
    "mobile": "9876543210"
  }'
```

---

### Update Mobile - Verify OTP

**POST** `https://abhasbx.abdm.gov.in/abha/api/v1/registration/aadhaar/verifyMobileOTP`

```bash
curl -X POST https://abhasbx.abdm.gov.in/abha/api/v1/registration/aadhaar/verifyMobileOTP \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -H "REQUEST-ID: $(uuidgen)" \
  -H "TIMESTAMP: $(date -u +%Y-%m-%dT%H:%M:%S.000Z)" \
  -H "X-CM-ID: sbx" \
  -d '{
    "txnId": "<TXNID>",
    "otp": "123456"
  }'
```

---

### Deactivate ABHA

**POST** `https://abhasbx.abdm.gov.in/abha/api/v1/profile/deactivate`

```bash
curl -X POST https://abhasbx.abdm.gov.in/abha/api/v1/profile/deactivate \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -H "REQUEST-ID: $(uuidgen)" \
  -H "TIMESTAMP: $(date -u +%Y-%m-%dT%H:%M:%S.000Z)" \
  -H "X-CM-ID: sbx" \
  -d '{
    "healthId": "91-1234-5678-9012"
  }'
```

---

## 4. App Endpoints (localhost)

These are the endpoints exposed by the .NET app. The app handles RSA encryption internally so you can pass plain values.

### Open Dashboard

```bash
curl http://localhost:8080/
```

---

### Generate OTP

**POST** `http://localhost:8080/M1HealthId/GenerateOtp`

```bash
curl -X POST http://localhost:8080/M1HealthId/GenerateOtp \
  -H "Content-Type: application/json" \
  -d '{"aadhaar": "999941057058"}'
```

**Response**
```json
{
  "success": true,
  "message": "OTP sent successfully.",
  "data": {
    "txnId": "a825f76b-0696-40f3-864c-5a3a5b389a84",
    "message": "OTP sent to Aadhaar registered mobile"
  }
}
```

---

### Resend OTP

**POST** `http://localhost:8080/M1HealthId/ResendOtp?txnId=<TXNID>`

```bash
curl -X POST "http://localhost:8080/M1HealthId/ResendOtp?txnId=a825f76b-0696-40f3-864c-5a3a5b389a84" \
  -H "Content-Type: application/json" \
  -d '{"aadhaar": "999941057058"}'
```

---

### Verify OTP and Create ABHA

**POST** `http://localhost:8080/M1HealthId/VerifyOtpAndCreate`

```bash
curl -X POST http://localhost:8080/M1HealthId/VerifyOtpAndCreate \
  -H "Content-Type: application/json" \
  -d '{
    "txnId": "a825f76b-0696-40f3-864c-5a3a5b389a84",
    "otp": "123456",
    "mobile": "9876543210"
  }'
```

**Response**
```json
{
  "success": true,
  "message": "ABHA created successfully.",
  "data": {
    "ABHAProfile": {
      "ABHANumber": "91-1234-5678-9012",
      "firstName": "Ravi",
      "lastName": "Kumar",
      "gender": "M",
      "mobile": "9876543210",
      "abhaStatus": "ACTIVE"
    },
    "isNew": true
  }
}
```

---

### Look Up Profile

**GET** `http://localhost:8080/M1HealthId/GetProfile/{healthId}`

```bash
curl http://localhost:8080/M1HealthId/GetProfile/91-1234-5678-9012
```

---

### Update Mobile - Send OTP

**POST** `http://localhost:8080/M1HealthId/GenerateMobileOtp`

```bash
curl -X POST http://localhost:8080/M1HealthId/GenerateMobileOtp \
  -H "Content-Type: application/json" \
  -d '{
    "txnId": "a825f76b-0696-40f3-864c-5a3a5b389a84",
    "mobile": "9876543210"
  }'
```

---

### Update Mobile - Verify OTP

**POST** `http://localhost:8080/M1HealthId/VerifyMobileOtp`

```bash
curl -X POST http://localhost:8080/M1HealthId/VerifyMobileOtp \
  -H "Content-Type: application/json" \
  -d '{
    "txnId": "a825f76b-0696-40f3-864c-5a3a5b389a84",
    "otp": "123456"
  }'
```

---

### Deactivate ABHA

**POST** `http://localhost:8080/M1HealthId/Deactivate`

```bash
curl -X POST http://localhost:8080/M1HealthId/Deactivate \
  -H "Content-Type: application/json" \
  -d '{"healthId": "91-1234-5678-9012"}'
```

---

## 5. Debug Endpoints

### Check Config

```bash
curl http://localhost:8080/M1HealthId/Config
```

### Test Encryption

```bash
curl "http://localhost:8080/M1HealthId/EncryptTest?aadhaar=999941057058"
```

Expected: `"encryptedLen": 684` confirms RSA-4096 encryption is working.

---

## 6. Required Headers (all ABDM calls)

| Header | Value | Notes |
|--------|-------|-------|
| `Authorization` | `Bearer <TOKEN>` | From auth step |
| `REQUEST-ID` | UUID v4 | Fresh UUID per request |
| `TIMESTAMP` | `2026-05-14T10:30:00.000Z` | ISO-8601 UTC |
| `X-CM-ID` | `sbx` | Sandbox only |
| `Content-Type` | `application/json` | POST requests |

---

## 7. Sandbox Test Aadhaar Numbers

| Aadhaar | Name |
|---------|------|
| `999941057058` | Shivshankar Choudhury |
| `999971658847` | Kumar Agarwal |
| `999933119405` | Fatima Bedi |
| `999955183433` | Rohit Pandey |
| `999990501894` | Anisha Jay Kapoor |

> These numbers reach UIDAI's staging environment. If you get **UIDAI Error 998**, the numbers are not registered in your specific sandbox account. Email `abdm.support@nha.gov.in` with Client ID `SBXID_034527` to request valid test Aadhaar numbers.

---

## 8. Common Error Responses

| Code | Message | Cause |
|------|---------|-------|
| `ABDM-1204` | UIDAI Error 998 | Aadhaar not in UIDAI staging DB |
| `400` | Invalid LoginId | Wrong Aadhaar format or old v1 test numbers |
| `401` | Unauthorized | Token expired - re-run auth step |
| `404` | Not Found | Wrong endpoint URL or GET on POST-only route |
| `422` | Unprocessable | Data validation failed at UIDAI level |
