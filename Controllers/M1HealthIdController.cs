using System;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ABDM.Models;
using ABDM.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ABDM.Controllers
{
    [RoutePrefix("M1HealthId")]
    public class M1HealthIdController : Controller
    {
        private readonly M1HealthIdService _service;

        public M1HealthIdController()
        {
            _service = new M1HealthIdService(new AbdmAuthService());
        }

        // ── UI ────────────────────────────────────────────────────────────────

        [HttpGet, Route(""), Route("~/"), Route("~/Home"), Route("~/Home/Index")]
        public ActionResult Index() => View();

        [HttpGet, Route("AadhaarEnroll")]
        public ActionResult AadhaarEnroll()
        {
            ViewBag.Title  = "Aadhaar Enrollment";
            ViewBag.Active = "AadhaarEnroll";
            return View();
        }

        [HttpGet, Route("DrivingLicense")]
        public ActionResult DrivingLicense()
        {
            ViewBag.Title  = "DL Enrollment";
            ViewBag.Active = "DL";
            return View();
        }

        // ── Enrollment ────────────────────────────────────────────────────────

        [HttpPost, Route("GenerateOtp")]
        public async Task<ActionResult> GenerateOtp(M1GenerateOtpRequest model)
        {
            if (string.IsNullOrWhiteSpace(model?.Aadhaar))
                return JsonError("Aadhaar number is required.");
            try
            {
                var r = await _service.GenerateAadhaarOtpAsync(model.Aadhaar);
                return JsonSuccess(r, "OTP sent successfully.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpPost, Route("ResendOtp")]
        public async Task<ActionResult> ResendOtp(string txnId, M1GenerateOtpRequest model)
        {
            if (string.IsNullOrWhiteSpace(txnId) || string.IsNullOrWhiteSpace(model?.Aadhaar))
                return JsonError("txnId and aadhaar are required.");
            try
            {
                var r = await _service.ResendOtpAsync(txnId, model.Aadhaar);
                return JsonSuccess(r, "OTP resent successfully.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpPost, Route("VerifyOtpAndCreate")]
        public async Task<ActionResult> VerifyOtpAndCreate(M1VerifyOtpRequest model)
        {
            if (string.IsNullOrWhiteSpace(model?.TxnId) || string.IsNullOrWhiteSpace(model.Otp))
                return JsonError("txnId and otp are required.");
            if (string.IsNullOrWhiteSpace(model.Mobile))
                return JsonError("mobile is required for ABHA enrollment.");
            try
            {
                var r = await _service.VerifyOtpAndCreateAsync(model.TxnId, model.Otp, model.Mobile);
                return JsonSuccess(r, r.IsNew ? "ABHA created successfully." : "Existing ABHA linked.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpPost, Route("AuthByAbdm")]
        public async Task<ActionResult> AuthByAbdm(string txnId, string otp)
        {
            if (string.IsNullOrWhiteSpace(txnId) || string.IsNullOrWhiteSpace(otp))
                return JsonError("txnId and otp are required.");
            try
            {
                var r = await _service.AuthByAbdmAsync(txnId, otp);
                return JsonSuccess(r, "Mobile OTP verified.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpGet, Route("Suggestions")]
        public async Task<ActionResult> Suggestions(string txnId)
        {
            if (string.IsNullOrWhiteSpace(txnId))
                return JsonError("txnId is required.");
            try
            {
                var r = await _service.GetAbhaAddressSuggestionsAsync(txnId);
                return JsonSuccess(r);
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpPost, Route("CreateAbhaAddress")]
        public async Task<ActionResult> CreateAbhaAddress(string txnId, string abhaAddress)
        {
            if (string.IsNullOrWhiteSpace(txnId) || string.IsNullOrWhiteSpace(abhaAddress))
                return JsonError("txnId and abhaAddress are required.");
            try
            {
                var r = await _service.CreateAbhaAddressAsync(txnId, abhaAddress);
                return JsonSuccess(r, $"ABHA address {abhaAddress} created.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        // ── Login ─────────────────────────────────────────────────────────────

        [HttpPost, Route("LoginRequestOtp")]
        public async Task<ActionResult> LoginRequestOtp(string abhaNumber)
        {
            if (string.IsNullOrWhiteSpace(abhaNumber))
                return JsonError("ABHA number or address is required.");
            try
            {
                var r = await _service.LoginRequestOtpAsync(abhaNumber);
                return JsonSuccess(r, "OTP sent to your ABHA-linked mobile.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpPost, Route("LoginVerify")]
        public async Task<ActionResult> LoginVerify(string txnId, string otp)
        {
            if (string.IsNullOrWhiteSpace(txnId) || string.IsNullOrWhiteSpace(otp))
                return JsonError("txnId and otp are required.");
            try
            {
                var r = await _service.LoginVerifyAsync(txnId, otp);
                return JsonSuccess(r, "Login successful.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        // ── Profile (requires X-Token from login/enrollment) ──────────────────

        [HttpGet, Route("Account")]
        public async Task<ActionResult> Account(string xToken)
        {
            if (string.IsNullOrWhiteSpace(xToken))
                return JsonError("xToken is required. Login first.");
            try
            {
                var r = await _service.GetAccountAsync(xToken);
                return JsonSuccess(r);
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpGet, Route("AbhaCard")]
        public async Task<ActionResult> AbhaCard(string xToken)
        {
            if (string.IsNullOrWhiteSpace(xToken))
                return JsonError("xToken is required.");
            try
            {
                var bytes = await _service.GetAbhaCardBytesAsync(xToken);
                return File(bytes, "image/png", "abha-card.png");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpGet, Route("QrCode")]
        public async Task<ActionResult> QrCode(string xToken)
        {
            if (string.IsNullOrWhiteSpace(xToken))
                return JsonError("xToken is required.");
            try
            {
                var bytes = await _service.GetQrCodeBytesAsync(xToken);
                var b64   = Convert.ToBase64String(bytes);
                return JsonSuccess(new { dataUrl = $"data:image/png;base64,{b64}" });
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        // ── Profile lookup ────────────────────────────────────────────────────

        [HttpGet, Route("GetProfile/{healthId}")]
        public async Task<ActionResult> GetProfile(string healthId)
        {
            if (string.IsNullOrWhiteSpace(healthId))
                return JsonError("healthId is required.");
            try
            {
                var r = await _service.GetHealthIdProfileAsync(HttpUtility.UrlDecode(healthId));
                return JsonSuccess(r);
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        // ── Demographic enrollment ────────────────────────────────────────────

        [HttpPost, Route("DemographicEnrol")]
        public async Task<ActionResult> DemographicEnrol(M1DemographicEnrolRequest model)
        {
            if (string.IsNullOrWhiteSpace(model?.TxnId))      return JsonError("txnId is required.");
            if (string.IsNullOrWhiteSpace(model.Name))        return JsonError("Name is required.");
            if (string.IsNullOrWhiteSpace(model.Gender))      return JsonError("Gender is required.");
            if (string.IsNullOrWhiteSpace(model.DateOfBirth)) return JsonError("Date of birth is required.");
            try
            {
                var r = await _service.DemographicEnrolAsync(
                    model.TxnId, model.Name, model.Gender, model.DateOfBirth, model.DistrictCode);
                return JsonSuccess(r, r.IsNew ? "ABHA created via demographic verification." : "Existing ABHA linked.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        // ── Mobile update ─────────────────────────────────────────────────────

        [HttpPost, Route("GenerateMobileOtp")]
        public async Task<ActionResult> GenerateMobileOtp(string txnId, string mobile)
        {
            try
            {
                var r = await _service.GenerateMobileOtpAsync(txnId, mobile);
                return JsonSuccess(r, "Mobile OTP sent.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpPost, Route("VerifyMobileOtp")]
        public async Task<ActionResult> VerifyMobileOtp(M1VerifyOtpRequest model)
        {
            try
            {
                var r = await _service.VerifyMobileOtpAsync(model.TxnId, model.Otp);
                return JsonSuccess(r, "Mobile updated successfully.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        // ── Driving License enrollment ────────────────────────────────────────

        [HttpPost, Route("DlGenerateMobileOtp")]
        public async Task<ActionResult> DlGenerateMobileOtp(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile) || mobile.Length != 10 || !System.Text.RegularExpressions.Regex.IsMatch(mobile, @"^\d{10}$"))
                return JsonError("Enter a valid 10-digit mobile number.");
            try
            {
                var r = await _service.DlGenerateMobileOtpAsync(mobile);
                return JsonSuccess(r, "OTP sent to your mobile.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpPost, Route("DlVerifyMobileOtp")]
        public async Task<ActionResult> DlVerifyMobileOtp(string txnId, string otp)
        {
            if (string.IsNullOrWhiteSpace(txnId) || string.IsNullOrWhiteSpace(otp))
                return JsonError("txnId and OTP are required.");
            try
            {
                var r = await _service.DlVerifyMobileOtpAsync(txnId, otp);
                return JsonSuccess(r, "Mobile OTP verified.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        [HttpPost, Route("DlEnrol")]
        public async Task<ActionResult> DlEnrol(M1DlEnrolRequest model)
        {
            if (string.IsNullOrWhiteSpace(model?.TxnId))      return JsonError("txnId missing. Complete Steps 1 and 2 first.");
            if (string.IsNullOrWhiteSpace(model.DocumentId))  return JsonError("DL number is required.");
            if (string.IsNullOrWhiteSpace(model.FirstName))   return JsonError("First name is required.");
            if (string.IsNullOrWhiteSpace(model.Dob))         return JsonError("Date of birth is required.");
            if (string.IsNullOrWhiteSpace(model.Gender))      return JsonError("Gender is required.");
            try
            {
                var doc = new M1V3DlDocumentData
                {
                    DocumentId     = model.DocumentId,
                    FirstName      = model.FirstName,
                    LastName       = model.LastName    ?? "",
                    Dob            = model.Dob,
                    Gender         = model.Gender,
                    Address        = model.Address     ?? "",
                    State          = model.State       ?? "",
                    District       = model.District    ?? "",
                    PinCode        = model.PinCode     ?? "",
                    FrontSidePhoto = model.FrontPhoto  ?? "",
                    BackSidePhoto  = model.BackPhoto   ?? ""
                };
                var r = await _service.DlEnrolAsync(model.TxnId, doc);
                return JsonSuccess(r, "ABHA enrollment initiated via Driving License.");
            }
            catch (AbdmException ex) { LogTrace(ex); return JsonError(ex.Message, ex.ApiError); }
        }

        // ── Deactivate ────────────────────────────────────────────────────────

        [HttpPost, Route("Deactivate")]
        public ActionResult Deactivate(string healthId)
        {
            return JsonError("ABHA deactivation via v3 API is not yet available in sandbox.");
        }

        // ── Debug ─────────────────────────────────────────────────────────────

        [HttpGet, Route("Config")]
        public ActionResult Config()
        {
            return JsonSuccess(new
            {
                ABDM_BASE_URL      = System.Configuration.ConfigurationManager.AppSettings["ABDM_BASE_URL"],
                ABDM_ABHA_BASE_URL = System.Configuration.ConfigurationManager.AppSettings["ABDM_ABHA_BASE_URL"],
                ABDM_CLIENT_ID     = System.Configuration.ConfigurationManager.AppSettings["ABDM_CLIENT_ID"],
                otpEndpoint        = (System.Configuration.ConfigurationManager.AppSettings["ABDM_ABHA_BASE_URL"]
                                     ?? "https://abhasbx.abdm.gov.in/abha/api")
                                     + "/v3/enrollment/request/otp"
            });
        }

        [HttpGet, Route("EncryptTest")]
        public async Task<ActionResult> EncryptTest(string aadhaar)
        {
            if (string.IsNullOrWhiteSpace(aadhaar))
                return JsonError("Pass ?aadhaar=12digitnumber");
            try
            {
                var enc = await _service.TestEncryptAsync(aadhaar);
                return JsonSuccess(new { encryptedLen = enc.Length, ok = enc.Length == 684 },
                    enc.Length == 684 ? "Encryption OK (RSA-4096)" : "Wrong length");
            }
            catch (System.Exception ex)
            {
                return JsonError("Encryption failed: " + ex.Message);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static readonly JsonSerializerSettings _camelCase = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private ContentResult JsonSuccess(object data, string message = null)
        {
            var json = JsonConvert.SerializeObject(new { success = true, message, data }, _camelCase);
            return Content(json, "application/json");
        }

        private ContentResult JsonError(string message, object detail = null)
        {
            Response.StatusCode = 400;
            var json = JsonConvert.SerializeObject(new { success = false, message, detail }, _camelCase);
            return Content(json, "application/json");
        }

        private void LogTrace(AbdmException ex)
            => System.Diagnostics.Trace.TraceError("[ABDM-M1] {0} | Raw: {1}",
                ex.Message, ex.ApiError?.RawBody ?? "(none)");
    }
}
