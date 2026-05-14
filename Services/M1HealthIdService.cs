using System;
using System.Threading.Tasks;
using ABDM.Models;

namespace ABDM.Services
{
    public class M1HealthIdService : AbdmBaseService
    {
        private readonly AbdmEncryptionService _enc;

        public M1HealthIdService(AbdmAuthService auth) : base(auth)
        {
            _baseUrl = System.Configuration.ConfigurationManager.AppSettings["ABDM_ABHA_BASE_URL"]
                       ?? "https://abhasbx.abdm.gov.in/abha/api";
            _enc = new AbdmEncryptionService(_baseUrl, auth);
        }

        // ── Enrollment: Step 1 - Send Aadhaar OTP ────────────────────────────

        public async Task<M1V3GenerateOtpResponse> GenerateAadhaarOtpAsync(string aadhaar)
        {
            var payload = new M1V3GenerateOtpRequest { LoginId = await _enc.EncryptAsync(aadhaar) };
            return await PostAsync<M1V3GenerateOtpResponse>("/v3/enrollment/request/otp", payload);
        }

        // ── Enrollment: Step 1b - Resend OTP ─────────────────────────────────

        public async Task<M1V3GenerateOtpResponse> ResendOtpAsync(string txnId, string aadhaar)
        {
            var payload = new M1V3GenerateOtpRequest { TxnId = txnId, LoginId = await _enc.EncryptAsync(aadhaar) };
            return await PostAsync<M1V3GenerateOtpResponse>("/v3/enrollment/request/otp", payload);
        }

        // ── Enrollment: Step 2 - Verify OTP + Create ABHA ────────────────────

        public async Task<M1V3EnrolResponse> VerifyOtpAndCreateAsync(string txnId, string otp, string mobile)
        {
            var payload = new M1V3EnrolByAadhaarRequest
            {
                TxnId    = txnId,
                AuthData = new M1V3AuthData
                {
                    Otp = new M1V3OtpData
                    {
                        TxnId    = txnId,
                        OtpValue = await _enc.EncryptAsync(otp),
                        Mobile   = mobile   // ABDM v3 validates mobile in plain text; encrypting causes "Invalid Mobile Number"
                    }
                },
                Consent = new M1V3Consent()
            };
            return await PostAsync<M1V3EnrolResponse>("/v3/enrollment/enrol/byAadhaar", payload);
        }

        // ── Enrollment: Step 3 - Auth by ABDM (mobile OTP if different mobile)

        public async Task<M1V3EnrolResponse> AuthByAbdmAsync(string txnId, string otp)
        {
            var payload = new M1V3AuthByAbdmRequest
            {
                TxnId    = txnId,
                AuthData = new M1V3AuthData
                {
                    Otp = new M1V3OtpData
                    {
                        TxnId    = txnId,
                        OtpValue = await _enc.EncryptAsync(otp)
                    }
                }
            };
            return await PostAsync<M1V3EnrolResponse>("/v3/enrollment/auth/byAbdm", payload);
        }

        // ── Enrollment: Step 4 - Get ABHA Address Suggestions ────────────────

        public async Task<M1V3SuggestionResponse> GetAbhaAddressSuggestionsAsync(string txnId)
            => await GetAsync<M1V3SuggestionResponse>($"/v3/enrollment/enrol/suggestion?txnId={txnId}");

        // ── Enrollment: Step 5 - Create ABHA Address ─────────────────────────

        public async Task<M1V3CreateAbhaAddressResponse> CreateAbhaAddressAsync(string txnId, string abhaAddress)
        {
            var payload = new M1V3CreateAbhaAddressRequest
            {
                TxnId       = txnId,
                AbhaAddress = abhaAddress,
                Preferred   = true
            };
            return await PostAsync<M1V3CreateAbhaAddressResponse>("/v3/enrollment/enrol/abha-address", payload);
        }

        // ── Login: Step 1 - Request OTP for existing ABHA ────────────────────

        public async Task<M1V3GenerateOtpResponse> LoginRequestOtpAsync(string abhaNumber)
        {
            var payload = new M1V3LoginRequestOtpRequest
            {
                LoginId   = await _enc.EncryptAsync(abhaNumber),
                LoginHint = abhaNumber.Contains("@") ? "abha-address" : "abha-number"
            };
            return await PostAsync<M1V3GenerateOtpResponse>("/v3/profile/login/request/otp", payload);
        }

        // ── Login: Step 2 - Verify OTP -> X-Token ────────────────────────────

        public async Task<M1V3LoginVerifyResponse> LoginVerifyAsync(string txnId, string otp)
        {
            var payload = new M1V3LoginVerifyRequest
            {
                TxnId    = txnId,
                AuthData = new M1V3AuthData
                {
                    Otp = new M1V3OtpData
                    {
                        TxnId    = txnId,
                        OtpValue = await _enc.EncryptAsync(otp)
                    }
                }
            };
            return await PostAsync<M1V3LoginVerifyResponse>("/v3/profile/login/verify", payload);
        }

        // ── Profile: Get Account (needs X-Token) ─────────────────────────────

        public async Task<M1V3AbhaProfile> GetAccountAsync(string xToken)
            => await GetAsync<M1V3AbhaProfile>("/v3/profile/account", xToken);

        // ── Profile: Download ABHA Card PNG (needs X-Token) ──────────────────

        public async Task<byte[]> GetAbhaCardBytesAsync(string xToken)
            => await GetBytesAsync("/v3/profile/account/abha-card", xToken);

        // ── Profile: Get QR Code (needs X-Token) ─────────────────────────────

        public async Task<byte[]> GetQrCodeBytesAsync(string xToken)
            => await GetBytesAsync("/v3/profile/account/qrCode", xToken);

        // ── Profile: Look up by Health ID ────────────────────────────────────

        public async Task<M1HealthIdProfile> GetHealthIdProfileAsync(string healthId)
            => await GetAsync<M1HealthIdProfile>($"/v1/search/existsByHealthId?healthId={healthId}");

        // ── Profile: Check if ABHA exists (null if not found) ────────────────

        public async Task<M1HealthIdProfile> CheckAbhaExistsAsync(string healthId)
        {
            try { return await GetHealthIdProfileAsync(healthId); }
            catch (AbdmException ex) when (ex.ApiError?.HttpStatus == 404) { return null; }
        }

        // ── Enrollment: Demographic - Verify demographics + create ABHA ─────────

        public async Task<M1V3EnrolResponse> DemographicEnrolAsync(
            string txnId, string name, string gender, string dateOfBirth, string districtCode = null)
        {
            var payload = new M1V3EnrolByDemographicRequest
            {
                TxnId = txnId,
                AuthData = new M1V3AuthDataDemographic
                {
                    Demographic = new M1V3DemographicData
                    {
                        TxnId        = txnId,
                        Name         = name,
                        Gender       = gender,
                        DateOfBirth  = dateOfBirth,
                        DistrictCode = districtCode
                    }
                },
                Consent = new M1V3Consent()
            };
            return await PostAsync<M1V3EnrolResponse>("/v3/enrollment/enrol/byAadhaar", payload);
        }

        // ── Mobile Update ─────────────────────────────────────────────────────

        public async Task<M1V3GenerateOtpResponse> GenerateMobileOtpAsync(string txnId, string mobile)
            => await PostAsync<M1V3GenerateOtpResponse>(
                "/v1/registration/aadhaar/generateMobileOTP", new { txnId, mobile });

        public async Task<M1V3EnrolResponse> VerifyMobileOtpAsync(string txnId, string otp)
            => await PostAsync<M1V3EnrolResponse>(
                "/v1/registration/aadhaar/verifyMobileOTP",
                new M1VerifyOtpRequest { TxnId = txnId, Otp = otp });

        // ── Driving License enrollment ─────────────────────────────────────────

        public async Task<M1V3GenerateOtpResponse> DlGenerateMobileOtpAsync(string mobile)
        {
            // dl-flow scope is only for the byDocument step; OTP request uses abha-enrol
            var payload = new M1V3GenerateOtpRequest
            {
                Scope     = new[] { "abha-enrol" },
                LoginHint = "mobile",
                LoginId   = await _enc.EncryptAsync(mobile),
                OtpSystem = "abdm"
            };
            return await PostAsync<M1V3GenerateOtpResponse>("/v3/enrollment/request/otp", payload);
        }

        public async Task<M1V3GenerateOtpResponse> DlVerifyMobileOtpAsync(string txnId, string otp)
        {
            var payload = new M1V3AuthByAbdmRequest
            {
                TxnId    = txnId,
                Scope    = new[] { "abha-enrol" },
                AuthData = new M1V3AuthData
                {
                    Otp = new M1V3OtpData
                    {
                        TxnId    = txnId,
                        OtpValue = await _enc.EncryptAsync(otp)
                    }
                }
            };
            return await PostAsync<M1V3GenerateOtpResponse>("/v3/enrollment/auth/byAbdm", payload);
        }

        public async Task<M1V3DlEnrolResponse> DlEnrolAsync(string txnId, M1V3DlDocumentData doc)
        {
            // HTML date input gives YYYY-MM-DD; ABDM expects DD-MM-YYYY
            var dob = doc.Dob;
            if (dob != null && dob.Length == 10 && dob[4] == '-')
            {
                var p = dob.Split('-');
                dob = $"{p[2]}-{p[1]}-{p[0]}";
            }
            doc.Dob = dob;
            var payload = new M1V3DlEnrolRequest
            {
                TxnId    = txnId,
                AuthData = new M1V3AuthDataDl { Document = doc },
                Consent  = new M1V3Consent()
            };
            return await PostAsync<M1V3DlEnrolResponse>("/v3/enrollment/enrol/byDocument", payload);
        }

        // ── Debug: test encryption only ───────────────────────────────────────

        public async Task<string> TestEncryptAsync(string plaintext)
            => await _enc.EncryptAsync(plaintext);
    }
}
