using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ABDM.Models
{
    // ══════════════════════════════════════════════════════════════════════════
    //  AUTH
    // ══════════════════════════════════════════════════════════════════════════

    public class AbdmTokenResponse
    {
        [JsonProperty("accessToken")]  public string AccessToken  { get; set; }
        [JsonProperty("tokenType")]    public string TokenType    { get; set; }
        [JsonProperty("expiresIn")]    public int    ExpiresIn    { get; set; }
        [JsonProperty("refreshToken")] public string RefreshToken { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MODULE 1 - ABHA HEALTH ID
    // ══════════════════════════════════════════════════════════════════════════

    public class M1GenerateOtpRequest
    {
        [JsonProperty("aadhaar")] public string Aadhaar { get; set; }
    }

    public class M1VerifyOtpRequest
    {
        [JsonProperty("txnId")]  public string TxnId  { get; set; }
        [JsonProperty("otp")]    public string Otp    { get; set; }
        [JsonProperty("mobile")] public string Mobile { get; set; }
    }

    public class M1HealthIdProfile
    {
        [JsonProperty("healthId")]       public string HealthId       { get; set; }
        [JsonProperty("healthIdNumber")] public string HealthIdNumber { get; set; }
        [JsonProperty("name")]           public string Name           { get; set; }
        [JsonProperty("gender")]         public string Gender         { get; set; }
        [JsonProperty("yearOfBirth")]    public string YearOfBirth    { get; set; }
        [JsonProperty("address")]        public string Address        { get; set; }
        [JsonProperty("districtName")]   public string DistrictName   { get; set; }
        [JsonProperty("stateName")]      public string StateName      { get; set; }
        [JsonProperty("mobile")]         public string Mobile         { get; set; }
    }

    // ── Enrollment v3 ─────────────────────────────────────────────────────────

    public class M1V3GenerateOtpRequest
    {
        [JsonProperty("txnId")]     public string   TxnId     { get; set; } = "";
        [JsonProperty("scope")]     public string[] Scope     { get; set; } = new[] { "abha-enrol" };
        [JsonProperty("loginHint")] public string   LoginHint { get; set; } = "aadhaar";
        [JsonProperty("loginId")]   public string   LoginId   { get; set; }
        [JsonProperty("otpSystem")] public string   OtpSystem { get; set; } = "aadhaar";
    }

    public class M1V3GenerateOtpResponse
    {
        [JsonProperty("txnId")]   public string TxnId   { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
    }

    public class M1V3EnrolByAadhaarRequest
    {
        [JsonProperty("txnId")]    public string       TxnId    { get; set; }
        [JsonProperty("scope")]    public string[]     Scope    { get; set; } = new[] { "abha-enrol" };
        [JsonProperty("authData")] public M1V3AuthData AuthData { get; set; }
        [JsonProperty("consent")]  public M1V3Consent  Consent  { get; set; }
    }

    public class M1V3AuthData
    {
        [JsonProperty("authMethods")] public string[]    AuthMethods { get; set; } = new[] { "otp" };
        [JsonProperty("otp")]         public M1V3OtpData Otp        { get; set; }
    }

    public class M1V3OtpData
    {
        [JsonProperty("txnId")]    public string TxnId    { get; set; }
        [JsonProperty("otpValue")] public string OtpValue { get; set; }
        [JsonProperty("mobile")]   public string Mobile   { get; set; }
    }

    public class M1V3Consent
    {
        [JsonProperty("code")]    public string Code    { get; set; } = "abha-enrollment";
        [JsonProperty("version")] public string Version { get; set; } = "1.4";
    }

    public class M1V3EnrolResponse
    {
        [JsonProperty("txnId")]       public string          TxnId       { get; set; }
        [JsonProperty("ABHAProfile")] public M1V3AbhaProfile ABHAProfile { get; set; }
        [JsonProperty("tokens")]      public M1V3Tokens      Tokens      { get; set; }
        [JsonProperty("isNew")]       public bool            IsNew       { get; set; }
    }

    public class M1V3AbhaProfile
    {
        [JsonProperty("ABHANumber")]         public string       AbhaNumber        { get; set; }
        [JsonProperty("phrAddress")]         public List<string> PhrAddress        { get; set; }
        [JsonProperty("preferredAbhaAddress")]public string      PreferredAbhaAddress { get; set; }
        [JsonProperty("firstName")]          public string       FirstName         { get; set; }
        [JsonProperty("middleName")]         public string       MiddleName        { get; set; }
        [JsonProperty("lastName")]           public string       LastName          { get; set; }
        [JsonProperty("dateOfBirth")]        public string       DateOfBirth       { get; set; }
        [JsonProperty("gender")]             public string       Gender            { get; set; }
        [JsonProperty("mobile")]             public string       Mobile            { get; set; }
        [JsonProperty("email")]              public string       Email             { get; set; }
        [JsonProperty("address")]            public string       Address           { get; set; }
        [JsonProperty("stateCode")]          public string       StateCode         { get; set; }
        [JsonProperty("districtCode")]       public string       DistrictCode      { get; set; }
        [JsonProperty("pincode")]            public string       Pincode           { get; set; }
        [JsonProperty("abhaStatus")]         public string       AbhaStatus        { get; set; }
    }

    public class M1V3Tokens
    {
        [JsonProperty("token")]            public string Token           { get; set; }
        [JsonProperty("expiresIn")]        public int    ExpiresIn       { get; set; }
        [JsonProperty("refreshToken")]     public string RefreshToken    { get; set; }
        [JsonProperty("refreshExpiresIn")] public int    RefreshExpiresIn { get; set; }
    }

    // ── Auth by ABDM (mobile OTP verify after enrollment) ────────────────────

    public class M1V3AuthByAbdmRequest
    {
        [JsonProperty("txnId")]    public string       TxnId    { get; set; }
        [JsonProperty("scope")]    public string[]     Scope    { get; set; } = new[] { "abha-enrol" };
        [JsonProperty("authData")] public M1V3AuthData AuthData { get; set; }
    }

    // ── ABHA Address (suggestions + creation) ────────────────────────────────

    public class M1V3SuggestionResponse
    {
        [JsonProperty("txnId")]          public string       TxnId          { get; set; }
        [JsonProperty("abhaAddressList")] public List<string> AbhaAddressList { get; set; }
    }

    public class M1V3CreateAbhaAddressRequest
    {
        [JsonProperty("txnId")]       public string TxnId       { get; set; }
        [JsonProperty("abhaAddress")] public string AbhaAddress { get; set; }
        [JsonProperty("preferred")]   public bool   Preferred   { get; set; } = true;
    }

    public class M1V3CreateAbhaAddressResponse
    {
        [JsonProperty("txnId")]              public string TxnId              { get; set; }
        [JsonProperty("healthIdNumber")]     public string HealthIdNumber     { get; set; }
        [JsonProperty("preferredAbhaAddress")]public string PreferredAbhaAddress { get; set; }
        [JsonProperty("tokens")]             public M1V3Tokens Tokens         { get; set; }
    }

    // ── Login flow ────────────────────────────────────────────────────────────

    public class M1V3LoginRequestOtpRequest
    {
        [JsonProperty("scope")]     public string[] Scope     { get; set; } = new[] { "abha-login" };
        [JsonProperty("loginHint")] public string   LoginHint { get; set; } = "abha-number";
        [JsonProperty("loginId")]   public string   LoginId   { get; set; }
        [JsonProperty("otpSystem")] public string   OtpSystem { get; set; } = "abdm";
    }

    public class M1V3LoginVerifyRequest
    {
        [JsonProperty("txnId")]    public string       TxnId    { get; set; }
        [JsonProperty("scope")]    public string[]     Scope    { get; set; } = new[] { "abha-login" };
        [JsonProperty("authData")] public M1V3AuthData AuthData { get; set; }
    }

    public class M1V3LoginVerifyResponse
    {
        [JsonProperty("txnId")]   public string    TxnId  { get; set; }
        [JsonProperty("token")]   public string    Token  { get; set; }
        [JsonProperty("tokens")]  public M1V3Tokens Tokens { get; set; }
        [JsonProperty("expiresIn")]public int       ExpiresIn { get; set; }
    }

    // ── Shared / Utility ─────────────────────────────────────────────────────

    public class AbdmApiError
    {
        public int    HttpStatus { get; set; }
        public string Code       { get; set; }
        public string Message    { get; set; }
        public string RawBody    { get; set; }
    }

    public class AbdmException : Exception
    {
        public AbdmApiError ApiError { get; }
        public AbdmException(string message) : base(message) { }
        public AbdmException(string message, AbdmApiError error) : base(message) { ApiError = error; }
    }

    // ── Driving License enrollment ────────────────────────────────────────────

    // MVC binding model — receives JSON from the DrivingLicense.cshtml form POST
    public class M1DlEnrolRequest
    {
        [JsonProperty("txnId")]       public string TxnId       { get; set; }
        [JsonProperty("documentId")]  public string DocumentId  { get; set; }
        [JsonProperty("firstName")]   public string FirstName   { get; set; }
        [JsonProperty("lastName")]    public string LastName    { get; set; }
        [JsonProperty("dob")]         public string Dob         { get; set; }
        [JsonProperty("gender")]      public string Gender      { get; set; }
        [JsonProperty("address")]     public string Address     { get; set; }
        [JsonProperty("state")]       public string State       { get; set; }
        [JsonProperty("district")]    public string District    { get; set; }
        [JsonProperty("pinCode")]     public string PinCode     { get; set; }
        [JsonProperty("frontPhoto")]  public string FrontPhoto  { get; set; }
        [JsonProperty("backPhoto")]   public string BackPhoto   { get; set; }
    }

    // ABDM document payload inside authData for /v3/enrollment/enrol/byDocument
    public class M1V3DlDocumentData
    {
        [JsonProperty("documentType")]   public string DocumentType   { get; set; } = "DRIVING_LICENSE";
        [JsonProperty("documentId")]     public string DocumentId     { get; set; }
        [JsonProperty("firstName")]      public string FirstName      { get; set; }
        [JsonProperty("lastName")]       public string LastName       { get; set; }
        [JsonProperty("dob")]            public string Dob            { get; set; }
        [JsonProperty("gender")]         public string Gender         { get; set; }
        [JsonProperty("address")]        public string Address        { get; set; }
        [JsonProperty("state")]          public string State          { get; set; }
        [JsonProperty("district")]       public string District       { get; set; }
        [JsonProperty("pinCode")]        public string PinCode        { get; set; }
        [JsonProperty("frontSidePhoto")] public string FrontSidePhoto { get; set; }
        [JsonProperty("backSidePhoto")]  public string BackSidePhoto  { get; set; }
    }

    // authData wrapper for byDocument request
    public class M1V3AuthDataDl
    {
        [JsonProperty("authMethods")] public string[]           AuthMethods { get; set; } = new[] { "dl" };
        [JsonProperty("document")]    public M1V3DlDocumentData Document    { get; set; }
    }

    // Full ABDM API request body for /v3/enrollment/enrol/byDocument
    public class M1V3DlEnrolRequest
    {
        [JsonProperty("txnId")]    public string         TxnId    { get; set; }
        [JsonProperty("scope")]    public string[]       Scope    { get; set; } = new[] { "dl-flow" };
        [JsonProperty("authData")] public M1V3AuthDataDl AuthData { get; set; }
        [JsonProperty("consent")]  public M1V3Consent    Consent  { get; set; }
    }

    // ABDM API response from /v3/enrollment/enrol/byDocument
    public class M1V3DlEnrolResponse
    {
        [JsonProperty("txnId")]           public string          TxnId           { get; set; }
        [JsonProperty("enrolmentNumber")] public string          EnrolmentNumber { get; set; }
        [JsonProperty("ABHAProfile")]     public M1V3AbhaProfile ABHAProfile     { get; set; }
        [JsonProperty("tokens")]          public M1V3Tokens      Tokens          { get; set; }
        [JsonProperty("message")]         public string          Message         { get; set; }
        [JsonProperty("isNew")]           public bool            IsNew           { get; set; }
    }
}
