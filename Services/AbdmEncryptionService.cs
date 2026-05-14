using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ABDM.Services
{
    /// <summary>
    /// Fetches the ABDM RSA public key and encrypts sensitive fields
    /// (Aadhaar, OTP, mobile) using RSA/ECB/OAEPWithSHA-1AndMGF1Padding
    /// as required by the v3 ABHA API.
    /// </summary>
    public class AbdmEncryptionService
    {
        private static readonly HttpClient _http = new HttpClient();

        // Cache the key for 1 hour — ABDM rotates it infrequently
        private static string   _cachedKey;
        private static DateTime _keyExpiry = DateTime.MinValue;
        private static readonly object _keyLock = new object();

        private readonly string          _abhaBase;
        private readonly AbdmAuthService _auth;

        public AbdmEncryptionService(string abhaBase, AbdmAuthService auth)
        {
            _abhaBase = abhaBase;
            _auth     = auth;
        }

        /// <summary>Encrypts plaintext with the ABDM public key and returns Base64 ciphertext.</summary>
        public async Task<string> EncryptAsync(string plaintext)
        {
            var publicKeyBase64 = await GetPublicKeyAsync();
            var keyBytes        = Convert.FromBase64String(publicKeyBase64);
            var rsa             = BuildRsaFromSpki(keyBytes);
            var cipher          = rsa.Encrypt(Encoding.UTF8.GetBytes(plaintext), true); // OAEP
            return Convert.ToBase64String(cipher);
        }

        // ── Key fetching ───────────────────────────────────────────────────────

        private async Task<string> GetPublicKeyAsync()
        {
            lock (_keyLock)
            {
                if (_cachedKey != null && DateTime.UtcNow < _keyExpiry)
                    return _cachedKey;
            }

            // Cert endpoint requires Bearer token + standard headers
            var token = await _auth.GetAccessTokenAsync();
            var ts    = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_abhaBase}/v3/profile/public/certificate");

            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.TryAddWithoutValidation("REQUEST-ID", Guid.NewGuid().ToString());
            req.Headers.TryAddWithoutValidation("TIMESTAMP",  ts);
            req.Headers.TryAddWithoutValidation("X-CM-ID",
                System.Configuration.ConfigurationManager.AppSettings["ABDM_CM_ID"] ?? "sbx");
            req.Headers.Accept.ParseAdd("application/json");

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to fetch ABDM public key (HTTP {(int)resp.StatusCode}): {body}");

            dynamic parsed = JsonConvert.DeserializeObject(body);
            string key     = parsed?.publicKey?.ToString()
                             ?? throw new Exception("publicKey missing from cert response");

            lock (_keyLock)
            {
                _cachedKey = key;
                _keyExpiry = DateTime.UtcNow.AddHours(1);
            }

            return _cachedKey;
        }

        // ── RSA / ASN.1 parsing ────────────────────────────────────────────────

        /// <summary>
        /// Parses a SubjectPublicKeyInfo DER buffer and returns an RSACryptoServiceProvider.
        /// Structure:
        ///   SEQUENCE { SEQUENCE { OID, NULL } BIT-STRING { SEQUENCE { INT modulus, INT exponent } } }
        /// </summary>
        private static RSACryptoServiceProvider BuildRsaFromSpki(byte[] spki)
        {
            int pos = 0;

            Expect(spki, ref pos, 0x30);          // outer SEQUENCE
            ReadLen(spki, ref pos);

            Expect(spki, ref pos, 0x30);          // AlgorithmIdentifier SEQUENCE
            int algLen = ReadLen(spki, ref pos);
            pos += algLen;                         // skip OID + NULL

            Expect(spki, ref pos, 0x03);          // BIT STRING
            ReadLen(spki, ref pos);
            pos++;                                 // skip unused-bits byte (0x00)

            Expect(spki, ref pos, 0x30);          // RSAPublicKey SEQUENCE
            ReadLen(spki, ref pos);

            Expect(spki, ref pos, 0x02);          // modulus INTEGER
            var modulus  = ReadInt(spki, ref pos);

            Expect(spki, ref pos, 0x02);          // publicExponent INTEGER
            var exponent = ReadInt(spki, ref pos);

            var rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus  = modulus,
                Exponent = exponent
            });
            return rsa;
        }

        private static void Expect(byte[] buf, ref int pos, byte tag)
        {
            if (buf[pos] != tag)
                throw new Exception($"ASN.1: expected tag 0x{tag:X2} at offset {pos}, got 0x{buf[pos]:X2}");
            pos++;
        }

        private static int ReadLen(byte[] buf, ref int pos)
        {
            int b = buf[pos++];
            if (b < 0x80) return b;
            int n = b & 0x7F, len = 0;
            while (n-- > 0) len = (len << 8) | buf[pos++];
            return len;
        }

        private static byte[] ReadInt(byte[] buf, ref int pos)
        {
            int len   = ReadLen(buf, ref pos);
            int skip  = (buf[pos] == 0x00 && len > 1) ? 1 : 0;
            var result = new byte[len - skip];
            Array.Copy(buf, pos + skip, result, 0, len - skip);
            pos += len;
            return result;
        }
    }
}
