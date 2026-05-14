using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Caching;
using System.Web;
using Newtonsoft.Json;
using ABDM.Models;
using System.Collections.Generic;

namespace ABDM.Services
{
    /// <summary>
    /// Handles all ABDM Gateway authentication flows.
    /// Tokens are cached in-process (HttpRuntime.Cache) and auto-refreshed 60 s before expiry.
    /// </summary>
    public class AbdmAuthService
    {
        // ── base URLs ──────────────────────────────────────────────────────────
        // Swap ABDM_BASE_URL for the production gateway when you go live.
        private readonly string _baseUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private static readonly HttpClient _http = new HttpClient();
        private const string TOKEN_CACHE_KEY = "ABDM_ACCESS_TOKEN";

        public AbdmAuthService()
        {
            _baseUrl      = System.Configuration.ConfigurationManager.AppSettings["ABDM_AUTH_URL"]
                            ?? "https://dev.abdm.gov.in/gateway/v0.5/sessions";
            _clientId     = System.Configuration.ConfigurationManager.AppSettings["ABDM_CLIENT_ID"];
            _clientSecret = System.Configuration.ConfigurationManager.AppSettings["ABDM_CLIENT_SECRET"];
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a valid Bearer token, refreshing from ABDM if the cached one is
        /// absent or within 60 seconds of expiry.
        /// </summary>
        public async Task<string> GetAccessTokenAsync()
        {
            // 1. Return cached token if still fresh
            var cached = HttpRuntime.Cache[TOKEN_CACHE_KEY] as string;
            if (!string.IsNullOrEmpty(cached))
                return cached;

            // 2. Fetch a new token from the ABDM /sessions endpoint
            var tokenResponse = await FetchTokenAsync();

            // Cache for (expiresIn − 60) seconds so we never send an expired token
            int cacheSecs = Math.Max(tokenResponse.ExpiresIn - 60, 30);
            HttpRuntime.Cache.Insert(
                TOKEN_CACHE_KEY,
                tokenResponse.AccessToken,
                null,
                DateTime.UtcNow.AddSeconds(cacheSecs),
                Cache.NoSlidingExpiration
            );

            return tokenResponse.AccessToken;
        }

        /// <summary>
        /// Forces a fresh token fetch (call after a 401 to recover gracefully).
        /// </summary>
        public async Task<string> RefreshTokenAsync()
        {
            HttpRuntime.Cache.Remove(TOKEN_CACHE_KEY);
            return await GetAccessTokenAsync();
        }

        // ── Private Helpers ────────────────────────────────────────────────────

        private async Task<AbdmTokenResponse> FetchTokenAsync()
        {
            // v0.5 sessions uses clientId + clientSecret only
            // v3 sessions (production) additionally requires grantType
            var isV3 = _baseUrl.Contains("/v3/");
            object payload = isV3
                ? (object)new { clientId = _clientId, clientSecret = _clientSecret, grantType = "client_credentials" }
                : (object)new { clientId = _clientId, clientSecret = _clientSecret };

            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                )
            };

            // Add mandatory ABDM headers
            AddCommonHeaders(request);

            var response = await _http.SendAsync(request);
            var body     = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new AbdmException(
                    $"ABDM token fetch failed – HTTP {(int)response.StatusCode}: {body}");

            var tokenResp = JsonConvert.DeserializeObject<AbdmTokenResponse>(body);
            if (tokenResp == null || string.IsNullOrEmpty(tokenResp.AccessToken))
                throw new AbdmException("ABDM returned an empty/invalid token payload.");

            return tokenResp;
        }

        /// <summary>
        /// Attach headers required by every ABDM request.
        /// </summary>
        internal void AddCommonHeaders(HttpRequestMessage req)
        {
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // X-CM-ID identifies the health CM; use your registered CM ID here
            req.Headers.TryAddWithoutValidation("X-CM-ID", "sbx");  // "sbx" = sandbox CM
        }
    }
}
