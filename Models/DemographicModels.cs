using Newtonsoft.Json;

namespace ABDM.Models
{
    public class M1DemographicEnrolRequest
    {
        [JsonProperty("txnId")]        public string TxnId        { get; set; }
        [JsonProperty("name")]         public string Name         { get; set; }
        [JsonProperty("gender")]       public string Gender       { get; set; }
        [JsonProperty("dateOfBirth")]  public string DateOfBirth  { get; set; }
        [JsonProperty("districtCode")] public string DistrictCode { get; set; }
    }

    public class M1V3DemographicData
    {
        [JsonProperty("txnId")]        public string TxnId        { get; set; }
        [JsonProperty("name")]         public string Name         { get; set; }
        [JsonProperty("gender")]       public string Gender       { get; set; }
        [JsonProperty("dateOfBirth")]  public string DateOfBirth  { get; set; }
        [JsonProperty("districtCode")] public string DistrictCode { get; set; }
    }

    public class M1V3AuthDataDemographic
    {
        [JsonProperty("authMethods")] public string[]            AuthMethods { get; set; } = new[] { "demographic" };
        [JsonProperty("demographic")] public M1V3DemographicData Demographic { get; set; }
    }

    public class M1V3EnrolByDemographicRequest
    {
        [JsonProperty("txnId")]    public string                  TxnId    { get; set; }
        [JsonProperty("scope")]    public string[]                Scope    { get; set; } = new[] { "abha-enrol" };
        [JsonProperty("authData")] public M1V3AuthDataDemographic AuthData { get; set; }
        [JsonProperty("consent")]  public M1V3Consent             Consent  { get; set; }
    }
}
