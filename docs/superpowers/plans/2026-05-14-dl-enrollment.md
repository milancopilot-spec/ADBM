# DL Enrollment + Dedicated Aadhaar Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ABHA creation via Driving License as a dedicated page, extract Aadhaar enrollment into its own page, and restructure the M1 page into a management hub.

**Architecture:** Three pages — M1 hub (Login/Update Mobile/Demographic/Deactivate + enrollment nav card), `/M1HealthId/AadhaarEnroll` (extracted Aadhaar 3-step flow), `/M1HealthId/DrivingLicense` (new DL 3-step flow). All new API actions stay in `M1HealthIdController` using the same `JsonSuccess`/`JsonError` helpers.

**Tech Stack:** ASP.NET MVC 5, C#, Newtonsoft.Json, Bootstrap 5, vanilla JS (fetch API, FileReader API)

---

## File Map

| File | Change |
|------|--------|
| `Models/AbdmModels.cs` | Add DL models (MVC binding model + 4 ABDM API models) |
| `Models/DemographicModels.cs` | No change — DL models go in AbdmModels.cs |
| `Services/M1HealthIdService.cs` | Add 3 DL service methods |
| `Controllers/M1HealthIdController.cs` | Add 2 GET views + 3 POST API actions |
| `Views/M1HealthId/AadhaarEnroll.cshtml` | **New** — dedicated Aadhaar enrollment page |
| `Views/M1HealthId/DrivingLicense.cshtml` | **New** — dedicated DL enrollment page |
| `Views/M1HealthId/Index.cshtml` | Remove Create ABHA tab; add enrollment nav card; remove moved JS |
| `Views/Shared/_Layout.cshtml` | Add two nav links (Aadhaar Enroll, DL Enroll) |

---

## Task 1: Add DL Models to AbdmModels.cs

**Files:**
- Modify: `Models/AbdmModels.cs` (append after the last existing class)

- [ ] **Step 1: Add the five new model classes**

Open `Models/AbdmModels.cs` and append the following after the closing `}` of `M1V3LoginVerifyResponse` (before the `// ── Shared / Utility` section):

```csharp
    // ── Driving License enrollment ────────────────────────────────────────────

    // MVC binding model — receives JSON from the DrivingLicense.cshtml form POST
    public class M1DlEnrolRequest
    {
        [JsonProperty("txnId")]       public string TxnId       { get; set; }
        [JsonProperty("documentId")]  public string DocumentId  { get; set; }
        [JsonProperty("firstName")]   public string FirstName   { get; set; }
        [JsonProperty("lastName")]    public string LastName    { get; set; }
        [JsonProperty("dob")]         public string Dob         { get; set; }  // YYYY-MM-DD (HTML date input)
        [JsonProperty("gender")]      public string Gender      { get; set; }
        [JsonProperty("address")]     public string Address     { get; set; }
        [JsonProperty("state")]       public string State       { get; set; }
        [JsonProperty("district")]    public string District    { get; set; }
        [JsonProperty("pinCode")]     public string PinCode     { get; set; }
        [JsonProperty("frontPhoto")]  public string FrontPhoto  { get; set; }  // Base64, prefix already stripped
        [JsonProperty("backPhoto")]   public string BackPhoto   { get; set; }
    }

    // ABDM document payload inside authData for /v3/enrollment/enrol/byDocument
    public class M1V3DlDocumentData
    {
        [JsonProperty("documentType")]   public string DocumentType   { get; set; } = "DRIVING_LICENSE";
        [JsonProperty("documentId")]     public string DocumentId     { get; set; }
        [JsonProperty("firstName")]      public string FirstName      { get; set; }
        [JsonProperty("lastName")]       public string LastName       { get; set; }
        [JsonProperty("dob")]            public string Dob            { get; set; }  // DD-MM-YYYY (converted in service)
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
        [JsonProperty("authMethods")] public string[]          AuthMethods { get; set; } = new[] { "dl" };
        [JsonProperty("document")]    public M1V3DlDocumentData Document   { get; set; }
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
    }
```

- [ ] **Step 2: Verify the project builds**

In Visual Studio: Build → Build Solution (Ctrl+Shift+B).  
Expected: `========== Build: 1 succeeded, 0 failed ==========`  
If it fails, check for missing `using` statements or typos in the new classes.

- [ ] **Step 3: Commit**

```
git add Models/AbdmModels.cs
git commit -m "feat: add DL enrollment models"
```

---

## Task 2: Add DL Service Methods to M1HealthIdService.cs

**Files:**
- Modify: `Services/M1HealthIdService.cs`

These three methods implement the DL enrollment three-step ABDM API flow.

- [ ] **Step 1: Add DlGenerateMobileOtpAsync (Step 1 — send OTP to mobile)**

In `Services/M1HealthIdService.cs`, add after the `VerifyMobileOtpAsync` method (before the `TestEncryptAsync` debug method):

```csharp
        // ── Driving License enrollment ─────────────────────────────────────────

        public async Task<M1V3GenerateOtpResponse> DlGenerateMobileOtpAsync(string mobile)
        {
            var payload = new M1V3GenerateOtpRequest
            {
                Scope     = new[] { "dl-flow" },
                LoginHint = "mobile",
                LoginId   = await _enc.EncryptAsync(mobile),
                OtpSystem = "abdm"
            };
            return await PostAsync<M1V3GenerateOtpResponse>("/v3/enrollment/request/otp", payload);
        }
```

- [ ] **Step 2: Add DlVerifyMobileOtpAsync (Step 2 — verify OTP)**

Immediately after `DlGenerateMobileOtpAsync`, add:

```csharp
        public async Task<M1V3GenerateOtpResponse> DlVerifyMobileOtpAsync(string txnId, string otp)
        {
            var payload = new M1V3AuthByAbdmRequest
            {
                TxnId    = txnId,
                Scope    = new[] { "dl-flow" },
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
```

- [ ] **Step 3: Add DlEnrolAsync (Step 3 — submit DL details)**

Immediately after `DlVerifyMobileOtpAsync`, add:

```csharp
        public async Task<M1V3DlEnrolResponse> DlEnrolAsync(string txnId, M1V3DlDocumentData doc)
        {
            // HTML date input gives YYYY-MM-DD; ABDM expects DD-MM-YYYY
            if (doc.Dob != null && doc.Dob.Length == 10 && doc.Dob[4] == '-')
            {
                var p = doc.Dob.Split('-');
                doc.Dob = $"{p[2]}-{p[1]}-{p[0]}";
            }
            var payload = new M1V3DlEnrolRequest
            {
                TxnId    = txnId,
                AuthData = new M1V3AuthDataDl { Document = doc },
                Consent  = new M1V3Consent()
            };
            return await PostAsync<M1V3DlEnrolResponse>("/v3/enrollment/enrol/byDocument", payload);
        }
```

- [ ] **Step 4: Build to verify**

Build → Build Solution.  
Expected: 1 succeeded, 0 failed.

- [ ] **Step 5: Commit**

```
git add Services/M1HealthIdService.cs
git commit -m "feat: add DL enrollment service methods"
```

---

## Task 3: Add Controller Actions

**Files:**
- Modify: `Controllers/M1HealthIdController.cs`

- [ ] **Step 1: Add the two GET view actions**

In `M1HealthIdController.cs`, add after the existing `[HttpGet, Route(""), ...]` Index action:

```csharp
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
```

- [ ] **Step 2: Add the three DL POST actions**

Add these after the existing `VerifyMobileOtp` action, before the `Deactivate` action:

```csharp
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
```

- [ ] **Step 3: Build to verify**

Build → Build Solution.  
Expected: 1 succeeded, 0 failed.

- [ ] **Step 4: Commit**

```
git add Controllers/M1HealthIdController.cs
git commit -m "feat: add DL enrollment controller actions"
```

---

## Task 4: Create AadhaarEnroll.cshtml

**Files:**
- Create: `Views/M1HealthId/AadhaarEnroll.cshtml`

This is the existing Aadhaar enrollment content extracted from `Index.cshtml`'s "Create ABHA" tab into a standalone page.

- [ ] **Step 1: Create the file with full content**

Create `Views/M1HealthId/AadhaarEnroll.cshtml` with the following content (this is the Create ABHA tab content lifted out of Index.cshtml, with the outer tab wrapper removed):

```cshtml
@{
    ViewBag.Title = "Aadhaar - ABHA Enrollment";
    ViewBag.Active = "AadhaarEnroll";
    Layout = "~/Views/Shared/_Layout.cshtml";
}

<h4 class="mb-4 fw-bold">Aadhaar - ABHA Enrollment</h4>

<div class="mb-3">
    <a href="@Url.Action("Index","M1HealthId")" class="btn btn-sm btn-outline-secondary">&larr; Back to M1 Hub</a>
</div>

<div class="row g-4 mb-4">
    <!-- Step 1 -->
    <div class="col-md-5">
        <div class="card p-4 h-100">
            <h6 class="fw-semibold mb-3">Step 1 - Send Aadhaar OTP</h6>
            <div class="mb-3">
                <label class="form-label">Aadhaar Number</label>
                <input id="c1Aadhaar" type="text" class="form-control" maxlength="12"
                       placeholder="12-digit Aadhaar" autocomplete="off" />
                <div class="form-text">Use sandbox test Aadhaar from ABDM portal.</div>
            </div>
            <div class="d-grid gap-2">
                <button id="c1Btn" class="btn btn-primary" onclick="generateOtp()">Send OTP</button>
                <button id="c1ResendBtn" class="btn btn-outline-secondary d-none" onclick="resendOtp()">
                    Resend OTP &nbsp;<span id="c1Countdown" class="text-muted small"></span>
                </button>
            </div>
            <div id="c1Result" class="mt-3"></div>
        </div>
    </div>

    <!-- Step 2 -->
    <div class="col-md-7">
        <div class="card p-4 h-100">
            <h6 class="fw-semibold mb-3">Step 2 - Verify OTP &amp; Create ABHA</h6>
            <div class="mb-2">
                <label class="form-label">Transaction ID</label>
                <input id="c2TxnId" type="text" class="form-control" placeholder="Auto-filled from Step 1" readonly />
                <div class="form-text">OTP valid for <span id="c2OtpTimer" class="fw-semibold text-danger"></span></div>
            </div>
            <div class="mb-2">
                <label class="form-label">OTP</label>
                <input id="c2Otp" type="text" class="form-control" maxlength="6" placeholder="6-digit OTP (sandbox: 123456)" />
            </div>
            <div class="mb-3">
                <label class="form-label">Mobile Number</label>
                <input id="c2Mobile" type="text" class="form-control" maxlength="10" placeholder="10-digit Aadhaar-linked mobile" />
            </div>
            <button id="c2Btn" class="btn btn-success w-100" onclick="verifyAndCreate()">Verify OTP &amp; Create ABHA</button>
            <div id="c2Result" class="mt-3"></div>
        </div>
    </div>
</div>

<!-- Step 3: ABHA Address Selection (shown after Step 2 success) -->
<div id="step3Panel" class="d-none">
    <div class="card p-4 border-primary">
        <h6 class="fw-semibold mb-3">Step 3 - Choose Your ABHA Address</h6>
        <p class="text-muted small mb-3">Select a suggested ABHA address or type your own.</p>
        <div class="row g-3 align-items-end">
            <div class="col-md-6">
                <label class="form-label">Suggested Addresses</label>
                <select id="c3Suggestions" class="form-select">
                    <option value="">-- Select a suggestion --</option>
                </select>
            </div>
            <div class="col-md-4">
                <label class="form-label">Or type your own</label>
                <input id="c3Custom" type="text" class="form-control" placeholder="yourname@abdm" />
            </div>
            <div class="col-md-2">
                <button class="btn btn-primary w-100" onclick="createAbhaAddress()">Confirm</button>
            </div>
        </div>
        <div id="c3Result" class="mt-3"></div>
    </div>
</div>

@section Scripts {
<script>
const OTP_TTL = 600;

const SS = {
    get txnId()   { return sessionStorage.getItem('abdm_txnId')   || ''; },
    get aadhaar() { return sessionStorage.getItem('abdm_aadhaar') || ''; },
    get otpTs()   { return parseInt(sessionStorage.getItem('abdm_otpTs') || '0'); },
    get xToken()  { return sessionStorage.getItem('abdm_xtoken')  || ''; },
    set txnId(v)   { sessionStorage.setItem('abdm_txnId',   v); },
    set aadhaar(v) { sessionStorage.setItem('abdm_aadhaar', v); },
    set otpTs(v)   { sessionStorage.setItem('abdm_otpTs',   String(v)); },
    set xToken(v)  { sessionStorage.setItem('abdm_xtoken',  v); },
    clear() { ['abdm_txnId','abdm_aadhaar','abdm_otpTs'].forEach(k => sessionStorage.removeItem(k)); }
};

window.addEventListener('DOMContentLoaded', () => {
    if (SS.txnId) {
        document.getElementById('c2TxnId').value = SS.txnId;
        if (SS.aadhaar) document.getElementById('c1Aadhaar').value = SS.aadhaar;
        startOtpTimer(); showResendBtn();
    }
});

function spin(id)   { const b=document.getElementById(id); if(b){b.disabled=true; b.dataset.label=b.textContent; b.innerHTML+=` <span class="spinner-border" role="status"></span>`;} }
function unspin(id) { const b=document.getElementById(id); if(b){b.disabled=false; b.textContent=b.dataset.label;} }
function escHtml(s) { return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

function showResult(id, ok, msg, data) {
    const body = data ? `<pre class="mt-2 bg-light p-2 rounded" style="max-height:220px;overflow:auto;font-size:.82rem">${JSON.stringify(data,null,2)}</pre>` : '';
    document.getElementById(id).innerHTML = `<div class="alert alert-${ok?'success':'danger'} mb-0 py-2">${escHtml(msg)}</div>${body}`;
}

function friendlyError(res) {
    const r = res.message || '';
    if (r.includes('Invalid LoginId')||r.includes('loginId')) return 'Invalid Aadhaar. Use sandbox test Aadhaar from ABDM portal.';
    if (r.includes('OTP')&&r.includes('expired'))             return 'OTP expired. Click Resend OTP.';
    if (r.includes('OTP')&&(r.includes('invalid')||r.includes('mismatch'))) return 'Incorrect OTP. Please try again.';
    if (r.includes('already')||r.includes('exist'))           return 'An ABHA already exists for this Aadhaar.';
    if (r.includes('mobile'))                                 return 'Invalid mobile number.';
    return r || 'An unexpected error occurred.';
}

async function post(url, payload) {
    const r = await fetch(url, { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(payload) });
    return r.json();
}

let _timer = null;

function startOtpTimer() {
    if (_timer) clearInterval(_timer);
    const el = document.getElementById('c2OtpTimer');
    const tick = () => {
        const left = OTP_TTL - Math.floor((Date.now()-SS.otpTs)/1000);
        if (left <= 0) { el.textContent='OTP expired - please resend'; el.className='fw-semibold text-warning'; clearInterval(_timer); _timer=null; }
        else { const m=String(Math.floor(left/60)).padStart(2,'0'), s=String(left%60).padStart(2,'0'); el.textContent=`${m}:${s} remaining`; }
    };
    tick(); _timer = setInterval(tick, 1000);
}

function showResendBtn() { document.getElementById('c1ResendBtn').classList.remove('d-none'); }

async function generateOtp() {
    const aadhaar = document.getElementById('c1Aadhaar').value.trim();
    if (!aadhaar||aadhaar.length!==12||!/^\d+$/.test(aadhaar)) { showResult('c1Result',false,'Enter a valid 12-digit Aadhaar.'); return; }
    spin('c1Btn');
    try {
        const res = await post('@Url.Action("GenerateOtp","M1HealthId")', {aadhaar});
        if (res.success) {
            SS.txnId=res.data?.txnId??''; SS.aadhaar=aadhaar; SS.otpTs=Date.now();
            document.getElementById('c2TxnId').value=SS.txnId;
            startOtpTimer(); showResendBtn();
            showResult('c1Result',true,res.message||'OTP sent.',{txnId:res.data?.txnId,message:res.data?.message});
        } else { showResult('c1Result',false,friendlyError(res),res.detail); }
    } catch(e) { showResult('c1Result',false,'Network error: '+e.message); }
    finally { unspin('c1Btn'); }
}

async function resendOtp() {
    const aadhaar=document.getElementById('c1Aadhaar').value.trim()||SS.aadhaar, txnId=SS.txnId;
    if (!txnId||!aadhaar) { showResult('c1Result',false,'Send OTP first.'); return; }
    spin('c1ResendBtn');
    try {
        const res = await post('@Url.Action("ResendOtp","M1HealthId")?txnId='+encodeURIComponent(txnId), {aadhaar});
        if (res.success) { SS.txnId=res.data?.txnId??txnId; SS.otpTs=Date.now(); document.getElementById('c2TxnId').value=SS.txnId; startOtpTimer(); showResult('c1Result',true,'OTP resent.',res.data); }
        else { showResult('c1Result',false,friendlyError(res),res.detail); }
    } catch(e) { showResult('c1Result',false,'Network error: '+e.message); }
    finally { unspin('c1ResendBtn'); }
}

async function verifyAndCreate() {
    const txnId=document.getElementById('c2TxnId').value.trim(), otp=document.getElementById('c2Otp').value.trim(), mobile=document.getElementById('c2Mobile').value.trim();
    if (!txnId) { showResult('c2Result',false,'txnId missing. Complete Step 1 first.'); return; }
    if (!otp||otp.length<4) { showResult('c2Result',false,'Enter the OTP from your mobile.'); return; }
    if (!mobile||mobile.length!==10) { showResult('c2Result',false,'Enter a valid 10-digit mobile number.'); return; }
    spin('c2Btn');
    try {
        const res = await post('@Url.Action("VerifyOtpAndCreate","M1HealthId")', {txnId,otp,mobile});
        if (res.success) {
            SS.clear(); if (_timer){clearInterval(_timer);_timer=null;} document.getElementById('c2OtpTimer').textContent='';
            const xToken = res.data?.tokens?.token || res.data?.token;
            if (xToken) SS.xToken = xToken;
            if (res.data?.ABHAProfile) {
                const p=res.data.ABHAProfile, name=[p.firstName,p.middleName,p.lastName].filter(Boolean).join(' ')||'-';
                document.getElementById('c2Result').innerHTML=`
                  <div class="alert alert-success mb-2">${res.data.isNew?'ABHA created.':'Existing ABHA linked.'}</div>
                  <div class="card p-3"><table class="table table-sm table-borderless mb-0">
                    <tr><th style="width:130px">ABHA Number</th><td><code>${p.ABHANumber??'-'}</code></td></tr>
                    <tr><th>Name</th><td>${escHtml(name)}</td></tr>
                    <tr><th>DOB</th><td>${p.dateOfBirth??'-'}</td></tr>
                    <tr><th>Gender</th><td>${p.gender??'-'}</td></tr>
                    <tr><th>Mobile</th><td>${p.mobile??'-'}</td></tr>
                    <tr><th>Status</th><td><span class="badge bg-${p.abhaStatus==='ACTIVE'?'success':'secondary'}">${p.abhaStatus??'-'}</span></td></tr>
                  </table></div>`;
            } else { showResult('c2Result',true,res.message,res.data); }
            if (SS.txnId||res.data?.txnId) {
                const sTxnId = res.data?.txnId || SS.txnId;
                await loadSuggestions(sTxnId);
            }
        } else { showResult('c2Result',false,friendlyError(res),res.detail); }
    } catch(e) { showResult('c2Result',false,'Network error: '+e.message); }
    finally { unspin('c2Btn'); }
}

async function loadSuggestions(txnId) {
    try {
        const r   = await fetch('@Url.Action("Suggestions","M1HealthId")?txnId='+encodeURIComponent(txnId));
        const res = await r.json();
        if (res.success && res.data?.abhaAddressList?.length) {
            const sel = document.getElementById('c3Suggestions');
            sel.innerHTML = '<option value="">-- Select --</option>';
            res.data.abhaAddressList.forEach(a => { const o=document.createElement('option'); o.value=a; o.textContent=a; sel.appendChild(o); });
            sel.onchange = () => { if(sel.value) document.getElementById('c3Custom').value=sel.value; };
            document.getElementById('c3Suggestions').dataset.txnId = res.data.txnId || txnId;
            document.getElementById('step3Panel').classList.remove('d-none');
        }
    } catch {}
}

async function createAbhaAddress() {
    const sel   = document.getElementById('c3Suggestions');
    const addr  = document.getElementById('c3Custom').value.trim() || sel.value;
    const txnId = sel.dataset.txnId;
    if (!addr) { showResult('c3Result',false,'Select or type an ABHA address.'); return; }
    try {
        const res = await post('@Url.Action("CreateAbhaAddress","M1HealthId")?txnId='+encodeURIComponent(txnId)+'&abhaAddress='+encodeURIComponent(addr), {});
        if (res.success) {
            const xToken = res.data?.tokens?.token;
            if (xToken) SS.xToken = xToken;
            showResult('c3Result',true,'ABHA address created: '+addr,res.data);
            document.getElementById('step3Panel').classList.add('d-none');
        } else { showResult('c3Result',false,res.message,res.detail); }
    } catch(e) { showResult('c3Result',false,'Network error: '+e.message); }
}
</script>
}
```

- [ ] **Step 2: Build and navigate to the page**

Build → Build Solution. Then start the app (F5) and navigate to `/M1HealthId/AadhaarEnroll`.  
Expected: Page loads with the heading "Aadhaar - ABHA Enrollment", two cards side by side, and a "Back to M1 Hub" link.

- [ ] **Step 3: Test end-to-end Aadhaar enrollment**

1. Enter a valid sandbox Aadhaar (e.g. `999941057058`)
2. Click "Send OTP" → should show success + txnId in Step 1 result
3. `c2TxnId` field should auto-fill
4. Enter OTP + mobile → click "Verify OTP & Create ABHA"
5. Expected: ABHA profile card appears with ABHA number

- [ ] **Step 4: Commit**

```
git add Views/M1HealthId/AadhaarEnroll.cshtml
git commit -m "feat: add dedicated Aadhaar enrollment page"
```

---

## Task 5: Update Index.cshtml (M1 Hub)

**Files:**
- Modify: `Views/M1HealthId/Index.cshtml`

Remove the Create ABHA tab and its associated JS; add an enrollment navigation card at the top.

- [ ] **Step 1: Remove the Create ABHA tab nav button**

Find this block (around line 10):
```html
    <li class="nav-item"><button class="nav-link active" data-bs-toggle="tab" data-bs-target="#tabCreate">Create ABHA</button></li>
```
Delete it. Change the next `<li>` (Login) so its button has `class="nav-link active"` (it becomes the first tab now).

- [ ] **Step 2: Remove the tabCreate tab pane**

Find and delete the entire `<!-- CREATE ABHA -->` section — from:
```html
    <!-- CREATE ABHA -->
    <div class="tab-pane fade show active" id="tabCreate">
```
to its closing `</div>` (just before `<!-- LOGIN -->`).

Change the Login tab pane `<div class="tab-pane fade" id="tabLogin">` to `<div class="tab-pane fade show active" id="tabLogin">` so Login is the active tab.

- [ ] **Step 3: Add enrollment navigation card above the tabs**

Insert this block immediately after `<h4 class="mb-4 fw-bold">M1 - ABHA Health ID</h4>` (before the `<ul class="nav nav-tabs ...>`):

```html
<div class="card p-4 mb-4 bg-light border-0">
    <h6 class="fw-semibold mb-2">Create New ABHA</h6>
    <div class="d-flex gap-3 flex-wrap">
        <a href="@Url.Action("AadhaarEnroll","M1HealthId")" class="btn btn-primary">
            Via Aadhaar
        </a>
        <a href="@Url.Action("DrivingLicense","M1HealthId")" class="btn btn-success">
            Via Driving License
        </a>
    </div>
</div>
```

- [ ] **Step 4: Remove Create ABHA JS from the Scripts section**

In the `@section Scripts { <script> ... </script> }` block, delete:
- The `const OTP_TTL = 600;` line
- The entire `const SS = { ... };` block
- The `window.addEventListener('DOMContentLoaded', ...)` block
- The `let _timer = null;` line
- The `startOtpTimer()` function
- The `showResendBtn()` function
- The `// ---- ENROLLMENT ----` comment block
- The `generateOtp()` function
- The `resendOtp()` function
- The `verifyAndCreate()` function
- The `loadSuggestions()` function
- The `createAbhaAddress()` function

Keep: `spin`, `unspin`, `escHtml`, `showResult`, `friendlyError`, `post`, and all Login/Demographic/Mobile Update functions.

- [ ] **Step 5: Build and verify**

Build → Build Solution. Start app and navigate to `/M1HealthId/`.  
Expected:
- "Create New ABHA" card with two buttons appears at top
- Tabs show: Login | Update Mobile | Demographic | Deactivate (no "Create ABHA" tab)
- "Via Aadhaar" button navigates to `/M1HealthId/AadhaarEnroll`
- "Via Driving License" button navigates to `/M1HealthId/DrivingLicense` (page will be empty until Task 6)

- [ ] **Step 6: Commit**

```
git add Views/M1HealthId/Index.cshtml
git commit -m "feat: restructure M1 page as management hub with enrollment nav card"
```

---

## Task 6: Create DrivingLicense.cshtml

**Files:**
- Create: `Views/M1HealthId/DrivingLicense.cshtml`

- [ ] **Step 1: Create the file with full content**

Create `Views/M1HealthId/DrivingLicense.cshtml`:

```cshtml
@{
    ViewBag.Title = "DL - ABHA Enrollment";
    ViewBag.Active = "DL";
    Layout = "~/Views/Shared/_Layout.cshtml";
}

<h4 class="mb-4 fw-bold">DL - ABHA Enrollment via Driving License</h4>

<div class="mb-3">
    <a href="@Url.Action("Index","M1HealthId")" class="btn btn-sm btn-outline-secondary">&larr; Back to M1 Hub</a>
</div>

<!-- Step 1: Send Mobile OTP -->
<div class="card p-4 mb-4">
    <h6 class="fw-semibold mb-3">Step 1 - Send Mobile OTP</h6>
    <div class="row g-3 align-items-end">
        <div class="col-md-4">
            <label class="form-label">Mobile Number</label>
            <input id="dlMobile" type="text" class="form-control" maxlength="10"
                   placeholder="10-digit mobile number" autocomplete="off" />
        </div>
        <div class="col-md-3">
            <button id="dlStep1Btn" class="btn btn-primary w-100" onclick="dlSendOtp()">Send OTP</button>
        </div>
    </div>
    <div id="dlStep1Result" class="mt-3"></div>
</div>

<!-- Step 2: Verify Mobile OTP (hidden until Step 1 success) -->
<div id="dlStep2Panel" class="card p-4 mb-4 d-none">
    <h6 class="fw-semibold mb-3">Step 2 - Verify Mobile OTP</h6>
    <div class="row g-3 align-items-end">
        <div class="col-md-5">
            <label class="form-label">Transaction ID</label>
            <input id="dlStep2TxnId" type="text" class="form-control" readonly
                   placeholder="Auto-filled from Step 1" />
        </div>
        <div class="col-md-3">
            <label class="form-label">OTP</label>
            <input id="dlOtp" type="text" class="form-control" maxlength="6" placeholder="6-digit OTP" />
        </div>
        <div class="col-md-2">
            <button id="dlStep2Btn" class="btn btn-primary w-100" onclick="dlVerifyOtp()">Verify OTP</button>
        </div>
    </div>
    <div id="dlStep2Result" class="mt-3"></div>
</div>

<!-- Step 3: DL Details (hidden until Step 2 success) -->
<div id="dlStep3Panel" class="card p-4 d-none">
    <h6 class="fw-semibold mb-3">Step 3 - Enter Driving License Details</h6>
    <input type="hidden" id="dlStep3TxnId" />

    <div class="row g-3 mb-3">
        <div class="col-12">
            <label class="form-label">DL Number <span class="text-danger">*</span></label>
            <input id="dlNumber" type="text" class="form-control"
                   placeholder="e.g. MH01 20110012345" />
        </div>
    </div>
    <div class="row g-3 mb-3">
        <div class="col-md-6">
            <label class="form-label">First Name <span class="text-danger">*</span></label>
            <input id="dlFirstName" type="text" class="form-control"
                   placeholder="As on driving license" />
        </div>
        <div class="col-md-6">
            <label class="form-label">Last Name</label>
            <input id="dlLastName" type="text" class="form-control"
                   placeholder="As on driving license" />
        </div>
    </div>
    <div class="row g-3 mb-3">
        <div class="col-md-4">
            <label class="form-label">Date of Birth <span class="text-danger">*</span></label>
            <input id="dlDob" type="date" class="form-control" />
        </div>
        <div class="col-md-4">
            <label class="form-label">Gender <span class="text-danger">*</span></label>
            <select id="dlGender" class="form-select">
                <option value="">-- Select --</option>
                <option value="M">Male</option>
                <option value="F">Female</option>
                <option value="O">Other</option>
            </select>
        </div>
    </div>
    <div class="row g-3 mb-3">
        <div class="col-12">
            <label class="form-label">Address</label>
            <input id="dlAddress" type="text" class="form-control"
                   placeholder="Street address as on DL" />
        </div>
    </div>
    <div class="row g-3 mb-3">
        <div class="col-md-4">
            <label class="form-label">State</label>
            <input id="dlState" type="text" class="form-control" placeholder="State" />
        </div>
        <div class="col-md-4">
            <label class="form-label">District</label>
            <input id="dlDistrict" type="text" class="form-control" placeholder="District" />
        </div>
        <div class="col-md-4">
            <label class="form-label">Pincode</label>
            <input id="dlPinCode" type="text" class="form-control"
                   maxlength="6" placeholder="6-digit pincode" />
        </div>
    </div>
    <div class="row g-3 mb-4">
        <div class="col-md-6">
            <label class="form-label">Front Photo of DL <span class="text-danger">*</span></label>
            <input type="file" class="form-control" accept="image/*"
                   onchange="readPhoto(this,'dlFrontPhotoB64','dlFrontPreview')" />
            <input type="hidden" id="dlFrontPhotoB64" />
            <img id="dlFrontPreview" src="" alt="Front preview"
                 class="img-thumbnail mt-2 d-none" style="max-height:120px" />
        </div>
        <div class="col-md-6">
            <label class="form-label">Back Photo of DL <span class="text-danger">*</span></label>
            <input type="file" class="form-control" accept="image/*"
                   onchange="readPhoto(this,'dlBackPhotoB64','dlBackPreview')" />
            <input type="hidden" id="dlBackPhotoB64" />
            <img id="dlBackPreview" src="" alt="Back preview"
                 class="img-thumbnail mt-2 d-none" style="max-height:120px" />
        </div>
    </div>

    <button id="dlStep3Btn" class="btn btn-success w-100"
            onclick="dlEnrol()">Create ABHA via Driving License</button>
    <div id="dlStep3Result" class="mt-3"></div>
</div>

@section Scripts {
<script>
// ── Utilities ─────────────────────────────────────────────────────────────────

function escHtml(s) { return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

function spin(id)   { const b=document.getElementById(id); if(b){b.disabled=true; b.dataset.label=b.textContent; b.innerHTML+=` <span class="spinner-border" role="status"></span>`;} }
function unspin(id) { const b=document.getElementById(id); if(b){b.disabled=false; b.textContent=b.dataset.label;} }

function showResult(id, ok, msg, data) {
    const body = data ? `<pre class="mt-2 bg-light p-2 rounded" style="max-height:220px;overflow:auto;font-size:.82rem">${JSON.stringify(data,null,2)}</pre>` : '';
    document.getElementById(id).innerHTML = `<div class="alert alert-${ok?'success':'danger'} mb-0 py-2">${escHtml(msg)}</div>${body}`;
}

async function post(url, payload) {
    const r = await fetch(url, { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(payload) });
    return r.json();
}

// ── Photo upload ──────────────────────────────────────────────────────────────

function readPhoto(input, hiddenId, previewId) {
    const file = input.files[0];
    if (!file) return;
    if (file.size > 1048576) {
        alert('Image must be under 1 MB. Please choose a smaller file.');
        input.value = '';
        return;
    }
    const reader = new FileReader();
    reader.onload = function(e) {
        const b64 = e.target.result.split(',')[1];   // strip "data:image/...;base64," prefix
        document.getElementById(hiddenId).value = b64;
        const preview = document.getElementById(previewId);
        preview.src = e.target.result;
        preview.classList.remove('d-none');
    };
    reader.readAsDataURL(file);
}

// ── DL Enrollment ─────────────────────────────────────────────────────────────

let _dlTxnId1 = '', _dlTxnId2 = '';

async function dlSendOtp() {
    const mobile = document.getElementById('dlMobile').value.trim();
    if (!mobile || mobile.length !== 10 || !/^\d+$/.test(mobile)) {
        showResult('dlStep1Result', false, 'Enter a valid 10-digit mobile number.');
        return;
    }
    spin('dlStep1Btn');
    try {
        const res = await post('@Url.Action("DlGenerateMobileOtp","M1HealthId")', { mobile });
        if (res.success) {
            _dlTxnId1 = res.data?.txnId || '';
            document.getElementById('dlStep2TxnId').value = _dlTxnId1;
            document.getElementById('dlStep2Panel').classList.remove('d-none');
            showResult('dlStep1Result', true, res.message || 'OTP sent.', res.data);
        } else {
            showResult('dlStep1Result', false, res.message || 'Failed to send OTP.', res.detail);
        }
    } catch(e) { showResult('dlStep1Result', false, 'Network error: ' + e.message); }
    finally { unspin('dlStep1Btn'); }
}

async function dlVerifyOtp() {
    const otp = document.getElementById('dlOtp').value.trim();
    if (!_dlTxnId1) { showResult('dlStep2Result', false, 'Complete Step 1 first.'); return; }
    if (!otp || otp.length < 4) { showResult('dlStep2Result', false, 'Enter the OTP.'); return; }
    spin('dlStep2Btn');
    try {
        const url = '@Url.Action("DlVerifyMobileOtp","M1HealthId")'
                  + '?txnId=' + encodeURIComponent(_dlTxnId1)
                  + '&otp='   + encodeURIComponent(otp);
        const res = await post(url, {});
        if (res.success) {
            _dlTxnId2 = res.data?.txnId || _dlTxnId1;
            document.getElementById('dlStep3TxnId').value = _dlTxnId2;
            document.getElementById('dlStep3Panel').classList.remove('d-none');
            showResult('dlStep2Result', true, 'OTP verified. Fill DL details below.', res.data);
        } else {
            showResult('dlStep2Result', false, res.message || 'OTP verification failed.', res.detail);
        }
    } catch(e) { showResult('dlStep2Result', false, 'Network error: ' + e.message); }
    finally { unspin('dlStep2Btn'); }
}

async function dlEnrol() {
    const txnId      = _dlTxnId2 || document.getElementById('dlStep3TxnId').value.trim();
    const documentId = document.getElementById('dlNumber').value.trim();
    const firstName  = document.getElementById('dlFirstName').value.trim();
    const lastName   = document.getElementById('dlLastName').value.trim();
    const dob        = document.getElementById('dlDob').value.trim();
    const gender     = document.getElementById('dlGender').value;
    const address    = document.getElementById('dlAddress').value.trim();
    const state      = document.getElementById('dlState').value.trim();
    const district   = document.getElementById('dlDistrict').value.trim();
    const pinCode    = document.getElementById('dlPinCode').value.trim();
    const frontPhoto = document.getElementById('dlFrontPhotoB64').value;
    const backPhoto  = document.getElementById('dlBackPhotoB64').value;

    if (!txnId)      { showResult('dlStep3Result', false, 'Complete Steps 1 and 2 first.'); return; }
    if (!documentId) { showResult('dlStep3Result', false, 'DL number is required.'); return; }
    if (!firstName)  { showResult('dlStep3Result', false, 'First name is required.'); return; }
    if (!dob)        { showResult('dlStep3Result', false, 'Date of birth is required.'); return; }
    if (!gender)     { showResult('dlStep3Result', false, 'Gender is required.'); return; }
    if (!frontPhoto) { showResult('dlStep3Result', false, 'Front photo of DL is required.'); return; }
    if (!backPhoto)  { showResult('dlStep3Result', false, 'Back photo of DL is required.'); return; }

    spin('dlStep3Btn');
    try {
        const res = await post('@Url.Action("DlEnrol","M1HealthId")', {
            txnId, documentId, firstName, lastName, dob, gender,
            address, state, district, pinCode, frontPhoto, backPhoto
        });
        if (res.success) {
            showResult('dlStep3Result', true, res.message || 'ABHA enrollment initiated.', res.data);
        } else {
            showResult('dlStep3Result', false, res.message || 'Enrollment failed.', res.detail);
        }
    } catch(e) { showResult('dlStep3Result', false, 'Network error: ' + e.message); }
    finally { unspin('dlStep3Btn'); }
}
</script>
}
```

- [ ] **Step 2: Build and navigate to the page**

Build → Build Solution. Start app and navigate to `/M1HealthId/DrivingLicense`.  
Expected: Page loads with three sections — Step 1 visible, Steps 2 and 3 hidden.

- [ ] **Step 3: Test Step 1 — mobile OTP**

Enter a valid 10-digit mobile number and click "Send OTP".  
Expected: Step 1 result shows success + txnId, Step 2 panel slides into view, `dlStep2TxnId` field is auto-filled.

- [ ] **Step 4: Test Step 2 — OTP verification**

Enter the OTP received and click "Verify OTP".  
Expected: Step 2 result shows "OTP verified", Step 3 panel slides into view.

- [ ] **Step 5: Test Step 3 — DL submission**

Fill all required fields, upload front and back photos (< 1 MB each), click "Create ABHA via Driving License".  
Expected: Result shows enrollment number or ABHA profile returned from ABDM API.  
Note: In the sandbox, DL verification may return an enrollment number (not a full ABHA number) since it requires manual KYC verification.

- [ ] **Step 6: Test photo validation**

Try uploading a file larger than 1 MB.  
Expected: Alert "Image must be under 1 MB" appears, file input is cleared.

- [ ] **Step 7: Commit**

```
git add Views/M1HealthId/DrivingLicense.cshtml
git commit -m "feat: add DL enrollment page with 3-step flow and photo upload"
```

---

## Task 7: Update _Layout.cshtml (Navigation)

**Files:**
- Modify: `Views/Shared/_Layout.cshtml`

- [ ] **Step 1: Add two nav links**

Find the existing nav `<ul>` block:
```html
            <ul class="navbar-nav ms-auto">
                <li class="nav-item">
                    <a class="nav-link @(ViewBag.Active == "M1" ? "active" : "")"
                       href="@Url.Action("Index","M1HealthId")">M1 - Health ID</a>
                </li>
            </ul>
```

Replace it with:
```html
            <ul class="navbar-nav ms-auto">
                <li class="nav-item">
                    <a class="nav-link @(ViewBag.Active == "M1" ? "active" : "")"
                       href="@Url.Action("Index","M1HealthId")">M1 - Health ID</a>
                </li>
                <li class="nav-item">
                    <a class="nav-link @(ViewBag.Active == "AadhaarEnroll" ? "active" : "")"
                       href="@Url.Action("AadhaarEnroll","M1HealthId")">Aadhaar Enroll</a>
                </li>
                <li class="nav-item">
                    <a class="nav-link @(ViewBag.Active == "DL" ? "active" : "")"
                       href="@Url.Action("DrivingLicense","M1HealthId")">DL Enroll</a>
                </li>
            </ul>
```

- [ ] **Step 2: Build and verify nav**

Build → Build Solution. Start app.  
Expected:
- Navbar shows: `ABDM Integration | M1 - Health ID | Aadhaar Enroll | DL Enroll`
- Clicking each link navigates to the correct page
- Active nav link is highlighted (the link matching the current page is bold/underlined)
- M1 - Health ID link is NOT active when on the Aadhaar Enroll or DL Enroll pages

- [ ] **Step 3: Final smoke test — full user journey**

1. Click "DL Enroll" in navbar → DL page loads
2. Click "Aadhaar Enroll" in navbar → Aadhaar page loads
3. Click "M1 - Health ID" in navbar → M1 hub loads with both enrollment buttons
4. Click "Via Aadhaar" button → Aadhaar page
5. Click "← Back to M1 Hub" → M1 hub
6. Click "Via Driving License" → DL page
7. Click "← Back to M1 Hub" → M1 hub
8. Verify M1 hub tabs (Login, Update Mobile, Demographic, Deactivate) all work as before

- [ ] **Step 4: Commit**

```
git add Views/Shared/_Layout.cshtml
git commit -m "feat: add Aadhaar Enroll and DL Enroll nav links to layout"
```

---

## Self-Review Checklist (completed inline)

- ✅ **Spec coverage:** All 3 spec goals covered — DL page (Task 6), Aadhaar dedicated page (Task 4), M1 hub restructure (Task 5). Nav card with buttons (Task 5 Step 3) + navbar links (Task 7).
- ✅ **No placeholders:** All code is complete. No TBD, TODO, or "similar to" references.
- ✅ **Type consistency:** `M1V3DlDocumentData` used in service and mapped from `M1DlEnrolRequest` in controller. `M1V3DlEnrolResponse` is the service return type and what `JsonSuccess` receives. `_dlTxnId1`/`_dlTxnId2` are consistent between `dlSendOtp`, `dlVerifyOtp`, and `dlEnrol`.
- ✅ **DOB conversion:** Implemented in `DlEnrolAsync` in service (Task 2 Step 3).
- ✅ **Photo Base64 prefix stripping:** Implemented in `readPhoto()` JS function (Task 6 Step 1) via `.split(',')[1]`.
- ✅ **1 MB photo validation:** Checked in `readPhoto()` before `FileReader` runs (Task 6 Step 1).
- ✅ **Mobile encryption for DL Step 1:** `DlGenerateMobileOtpAsync` calls `_enc.EncryptAsync(mobile)` (Task 2 Step 1).
- ✅ **Mobile in DL Step 1 is plain text from form** (user types it), encrypted in service — consistent with Aadhaar OTP flow.
