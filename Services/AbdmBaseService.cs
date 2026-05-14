using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ABDM.Models;

namespace ABDM.Services
{
    public abstract class AbdmBaseService
    {
        protected readonly AbdmAuthService _auth;
        protected string                   _baseUrl;

        protected static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        protected AbdmBaseService(AbdmAuthService auth)
        {
            _auth    = auth;
            _baseUrl = System.Configuration.ConfigurationManager.AppSettings["ABDM_BASE_URL"]
                       ?? "https://dev.abdm.gov.in/gateway";
        }

        protected async Task<TResponse> PostAsync<TResponse>(string url, object body, string xToken = null)
            => await SendWithRetryAsync<TResponse>(() => BuildRequest(HttpMethod.Post, url, body, xToken));

        protected async Task<TResponse> GetAsync<TResponse>(string url, string xToken = null)
            => await SendWithRetryAsync<TResponse>(() => BuildRequest(HttpMethod.Get, url, null, xToken));

        protected async Task<TResponse> PutAsync<TResponse>(string url, object body)
            => await SendWithRetryAsync<TResponse>(() => BuildRequest(HttpMethod.Put, url, body));

        protected async Task DeleteAsync(string url)
            => await SendWithRetryAsync<object>(() => BuildRequest(HttpMethod.Delete, url, null), expectBody: false);

        protected async Task<byte[]> GetBytesAsync(string url, string xToken = null)
        {
            var token = await _auth.GetAccessTokenAsync();
            var req   = BuildRequest(HttpMethod.Get, url, null, xToken);
            InjectBearerToken(req, token);
            var resp  = await Http.SendAsync(req);
            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadAsByteArrayAsync();

            var body = await resp.Content.ReadAsStringAsync();
            throw new AbdmException($"ABDM API error {(int)resp.StatusCode}",
                new AbdmApiError { HttpStatus = (int)resp.StatusCode, RawBody = body });
        }

        private async Task<TResponse> SendWithRetryAsync<TResponse>(
            Func<HttpRequestMessage> buildReq, bool expectBody = true)
        {
            var token = await _auth.GetAccessTokenAsync();
            var req   = buildReq();
            InjectBearerToken(req, token);
            var resp  = await Http.SendAsync(req);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                token = await _auth.RefreshTokenAsync();
                req   = buildReq();
                InjectBearerToken(req, token);
                resp  = await Http.SendAsync(req);
            }

            return await HandleResponseAsync<TResponse>(resp, expectBody);
        }

        private async Task<TResponse> HandleResponseAsync<TResponse>(HttpResponseMessage resp, bool expectBody)
        {
            var body = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                if (!expectBody || string.IsNullOrWhiteSpace(body)) return default;
                return JsonConvert.DeserializeObject<TResponse>(body);
            }

            AbdmApiError apiError;
            try   { apiError = JsonConvert.DeserializeObject<AbdmApiError>(body) ?? new AbdmApiError { Message = body }; }
            catch { apiError = new AbdmApiError { Message = body }; }
            apiError.HttpStatus = (int)resp.StatusCode;
            apiError.RawBody    = body;
            throw new AbdmException($"ABDM API error {(int)resp.StatusCode} - {apiError.Message}", apiError);
        }

        private HttpRequestMessage BuildRequest(HttpMethod method, string relativeUrl, object body, string xToken = null)
        {
            var req = new HttpRequestMessage(method, $"{_baseUrl}{relativeUrl}");
            req.Headers.TryAddWithoutValidation("REQUEST-ID", Guid.NewGuid().ToString());
            req.Headers.TryAddWithoutValidation("TIMESTAMP",  DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            req.Headers.TryAddWithoutValidation("X-CM-ID",
                System.Configuration.ConfigurationManager.AppSettings["ABDM_CM_ID"] ?? "sbx");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (xToken != null)
                req.Headers.TryAddWithoutValidation("X-Token", xToken);
            if (body != null)
                req.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            return req;
        }

        private static void InjectBearerToken(HttpRequestMessage req, string token)
            => req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
