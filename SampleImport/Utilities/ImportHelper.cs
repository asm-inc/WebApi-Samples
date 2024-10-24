using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic.FileIO;
using SampleImport.Models;
using SampleImport.Collections;
using System.Data;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Net.Http;

namespace SampleImport.Utilities {
    public class ImportHelper {

        private FileImportAppSettings _fileImportAppSettings;
        private WebAPIAppSettings _webAPIsettings;
        private AuthorizationToken? _authorizationToken;
        public static readonly HttpClient httpClient = new HttpClient();
        public readonly SampleImport.Collections.DataRowCollection dataRowCollection;

        public ImportHelper() {
            /*STEP 1: Load the config keys*/
            var config = LoadConfig();

            /*STEP 2: Load the WebAPI & FileImport Setting Keys*/
            _fileImportAppSettings = new FileImportAppSettings(config);
            _webAPIsettings = new WebAPIAppSettings(config);

            if(_fileImportAppSettings.Debug)
                Console.WriteLine("Step 1: Config Keys Loaded");

            /*STEP 3: LOAD THE CSV DATA INTO COLLECTION/MEMORY*/
            dataRowCollection = ImportCSV.LoadFromFile(_fileImportAppSettings);

            /*STEP 4: Get Auth Token*/
            /*STEP 5: Check If Provider Exists(Query endpoint using FirstName, LastName and NPI)*/
            /*STEP 5a: If exists, do nothing for Provider and move onto next record*/
            /*STEP 5b: If not exists, create the provider*/
            /*STEP 6: Check if Insurance Record exists for provider (Query endpoint using Insurance Name to find match in Reference tied to provider*/
            /*STEP 6a: If exists, do nothing for Insurance and move onto next record*/
            /*STEP 6b: If not exists, check if the Insurance referencesource exists*/
            /*STEP 6b1: If not exists, create the Insurance referencesource and create the Reference record*/
        }

        public static IConfigurationRoot LoadConfig() {
            var builder = new ConfigurationBuilder()
                 .AddJsonFile($"appsettings.json", true, true);
            
            var config = builder.Build();
            return config;
        }

        public bool IsTokenInvalid() {
            AuthorizationToken invalidCheck = _authorizationToken;
            var currentDateTime = DateTime.Now;
            bool valid = false;

            if (invalidCheck != null) {
                var lastCheck = currentDateTime - invalidCheck.LastCheck;
                if (invalidCheck.Status == "Invalid" && lastCheck.TotalSeconds <= 60)
                    valid = true;
            }
            else 
                valid = true;
            return valid;
        }

        public async Task<string> GetAuthorizationToken() {
            if (_authorizationToken == null && IsTokenInvalid()) {

                var client = httpClient;

                var request = new HttpRequestMessage {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://" + _webAPIsettings.InstanceCode + ".api.asm-cloud.com/api/tokens"),
                    Content = new StringContent("grant_type=" + _webAPIsettings.GrantType + "&client_id=" + _webAPIsettings.ClientId + "&client_secret=" + _webAPIsettings.ClientSecret + "&scope=" + _webAPIsettings.InstanceCode + "/" + _webAPIsettings.FacilityOrMarketID) {
                        Headers =
                        {
                            ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded")
                        }
                    }
                };

                using (var response = await client.SendAsync(request)) {
                    var body = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode.ToString() == "OK") {
                        
                        if (_webAPIsettings.Debug)
                            Console.WriteLine("Step 4: Successfully Received Authorization Token");

                        AuthorizationToken authorizationToken = JsonConvert.DeserializeObject<AuthorizationToken>(body);;
                        authorizationToken.RetryAttempts = 0;
                        authorizationToken.FailedTokenAttempts = 0;
                        _authorizationToken = authorizationToken;
                    }
                    //SOMETHING WENT WRONG, OR THE TOKEN EXPIRED
                    else {
                        if (_authorizationToken == null) {

                            if (_webAPIsettings.Debug)
                                Console.WriteLine("Step 4: Unable to retrieve Authorization Token");

                            AuthorizationToken authorizationToken = new AuthorizationToken("Invalid", "Invalid", "Invalid");
                            authorizationToken.Status = "Invalid";
                            authorizationToken.LastCheck = DateTime.Now;
                            authorizationToken.RetryAttempts++;
                            authorizationToken.FailedTokenAttempts++;

                            _authorizationToken = authorizationToken;
                        }
                        else {

                            if (_webAPIsettings.Debug)
                                Console.WriteLine("Step 4: Unable to retrieve Authorization Token");

                            _authorizationToken.Status = "Invalid";
                            _authorizationToken.LastCheck = DateTime.Now;
                            _authorizationToken.RetryAttempts++;
                            _authorizationToken.FailedTokenAttempts++;
                        }
                    }
                }
            }
            //TOKEN EXISTS
            else {
                //ATTEMPT TO GET A NEW TOKEN, WILL ONLY TRY 3 ADDITIONAL TIMES TOTAL .
                if (_authorizationToken.AccessToken == "Invalid" && _authorizationToken.RetryAttempts >= 0 && _authorizationToken.RetryAttempts <= 2) {

                    var client = httpClient;

                    var request = new HttpRequestMessage {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri("https://" + _webAPIsettings.InstanceCode + ".api.asm-cloud.com/api/tokens"),
                        Content = new StringContent("grant_type=" + _webAPIsettings.GrantType + "&client_id=" + _webAPIsettings.ClientId + "&client_secret=" + _webAPIsettings.ClientSecret + "&scope=" + _webAPIsettings.InstanceCode + "/" + _webAPIsettings.FacilityOrMarketID) {
                            Headers =
                            {
                                ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded")
                            }
                        }
                    };

                    using (var response = await client.SendAsync(request)) {
                        var body = await response.Content.ReadAsStringAsync();

                        if (response.StatusCode.ToString() == "OK") {

                            if (_webAPIsettings.Debug)
                                Console.WriteLine("Step 4: Successfully retrieved Authorization Token on expiration or retry");
                            AuthorizationToken authorizationToken = JsonConvert.DeserializeObject<AuthorizationToken>(body);
                            authorizationToken.LastCheck = DateTime.Now;
                            authorizationToken.RetryAttempts = 0;
                            authorizationToken.FailedTokenAttempts = 0;
                            _authorizationToken = authorizationToken;
                            

                        }
                        //SOMETHING CONTINUES GOING WRONG, NOT TOKEN EXPIRATION
                        else {

                            if (_webAPIsettings.Debug)
                                Console.WriteLine("Step 4: Unable to retrieve Authorization Token on expiration or retry");
                            var retry = _authorizationToken.RetryAttempts;
                            var failed = _authorizationToken.FailedTokenAttempts;

                            AuthorizationToken authorizationToken = new AuthorizationToken("Invalid", "Invalid", "Invalid");
                            authorizationToken.RetryAttempts = retry + 1;
                            authorizationToken.FailedTokenAttempts = failed + 1;
                            authorizationToken.LastCheck = DateTime.Now;
                            _authorizationToken = authorizationToken;
                        }
                    }
                }
                else if (_authorizationToken.AccessToken == "Invalid" && _authorizationToken.RetryAttempts > 2) {
                    if (_webAPIsettings.Debug)
                        Console.WriteLine("Step 4: Max Retry of 3 Attempts reached. Waiting 60 seconds until next Retry...");
                    int milliseconds = 60000;
                    _authorizationToken.RetryAttempts = 2;

                    if (_authorizationToken.FailedTokenAttempts >= 20) {
                        if (_webAPIsettings.Debug)
                            Console.WriteLine("Step 4: Attempted to obtain an Authorization Token 20 times in a row without success. Import process is being terminated. \nCheck your API Access Key permissions and connection details as likely this behavior is a result of a misconfiguration on the client side.\nIf you exceed 101 failed Authorization Token attempts in a row, your IP origin will be blacklisted and will need to be manually unlocked by an MD-Staff user.");
                        Environment.Exit(0);
                    }
                    else
                        Thread.Sleep(milliseconds);
                }
            }

            return _authorizationToken.Status;
        }

        public async Task<SampleImport.Collections.DataRowCollection> ImportData(SampleImport.Collections.DataRowCollection dataRowCollection) {

            if (_webAPIsettings.Debug)
                Console.WriteLine("\nProcessing Data for Import");
               
            var lastRowID = dataRowCollection.DataRows.Last();

            foreach (var row in dataRowCollection.DataRows) {
                
                /*Potentially 1-4 HTTPS requests per provider.
                    1. Find ProviderID using NPI, LastName and FirstName.
                    2. Find ProviderID using NPI and LastName
                    3. Create new Demographic record.
                    4. Create new Appointment record.
                */
                row.Provider = await SearchForExistingProvider(row.Provider);
                
                if (_webAPIsettings.Debug && row.Provider.MultipleMatches == false) {
                    Console.WriteLine("----------------------------------------------------");
                    if (row.Provider.IsNew == true)
                        Console.WriteLine($"Creating a new Provider (NPI: {row.Provider.NPI})");
                    else if(row.Provider.IsNew == false)
                        Console.WriteLine($"Found a matching Provider (NPI {row.Provider.NPI}). No record created.");
                }

                /*potentially 1-4 HTTPS requests per provider.
                    1. Find Insurance using Name, Expires, ProviderID, and InUse=1 .
                    2. Find ReferenceSource using Name and ReferenceType = Insurance
                    3. Create new ReferenceSource record.
                    4. Create new Insurance record.
                */

                row.Insurance.ProviderID = row.Provider.ProviderID;
                row.Insurance = await SearchForExistingProviderInsurance(row.Insurance);

                if (_webAPIsettings.Debug && row.Provider.MultipleMatches == false) {
                    if (row.Insurance.IsNew == true && row.Insurance.InsuranceRecordCreated == true && row.Insurance.ReferenceSourceRecordCreated == true)
                        Console.WriteLine($"Creating new ReferenceSource and Provider Insurance: {row.Insurance.InsuranceCarrier}");
                    else if (row.Insurance.IsNew == true && row.Insurance.InsuranceRecordCreated == true && row.Insurance.ReferenceSourceRecordCreated == false)
                        Console.WriteLine($"Found a matching ReferenceSource and created new Provider Insurance: {row.Insurance.InsuranceCarrier}");
                    else if (row.Insurance.IsNew == false)
                        Console.WriteLine($"Found a matching Provider Insurance record ({row.Insurance.InsuranceCarrier}). No record created.");
                    
                    Console.WriteLine("----------------------------------------------------");
                }

                if(row.ImportRowID != lastRowID.ImportRowID)
                    NextIterationDelay(row.Provider.NumberOfRequests, row.Insurance.NumberOfRequests);
            }

            return dataRowCollection;
        }

        public void NextIterationDelay(int? providerRequests, int? insuranceRequests) {
            
            int delay = 0;
            int total = 0;
            int totalRequests = providerRequests + insuranceRequests ?? total;

            switch (totalRequests) {
                case 0:
                    delay = 2000;
                    break;
                case 1:
                    delay = 5000;
                    break;
                case 2:
                    delay = 10000;
                    break;
                case 3:
                    delay = 15000;
                    break;
                case 4:
                    delay = 20000;
                    break;
                case 5:
                    delay = 30000;
                    break;
                case 6:
                    delay = 35000;
                    break;
                case 7:
                    delay = 40000;
                    break;
                case 8:
                    delay = 45000;
                    break;
                default:
                    delay = 50000;
                    break;
            }

            Thread.Sleep(delay);
        }
        
        public async Task<Provider> SearchForExistingProvider(Provider provider) {

            provider = await FindProviderLastNameFirstNameNPI(provider);

            if (provider.MultipleMatches == true) {
                if (_webAPIsettings.Debug)
                    Console.WriteLine($"Multiple Providers found in MD-Staff for NPI {provider.NPI}. Unable to import this record, record was skipped.");
                return provider;
            }

            if (provider.IsNew == true) {
                provider = await CreateDemographicRecord(provider);
            }

            return provider;
        }

        public async Task<Insurance> SearchForExistingProviderInsurance(Insurance insurance) {
            insurance = await FindProviderInsuranceByName(insurance);

            if (insurance.IsNew == true) {
                insurance = await CreateInsuranceRecord(insurance);
            }

            return insurance;
        }

        public async Task<Insurance> FindProviderInsuranceByName(Insurance insurance) {

            var client = httpClient;
            List<Insurance> providerInsurance = new List<Insurance>();

            var request = new HttpRequestMessage {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://" + _webAPIsettings.InstanceCode + ".api.asm-cloud.com/api/" + _webAPIsettings.InstanceCode + "/query"),
                Headers =
                {
                        { "Authorization", "Bearer " + _authorizationToken.AccessToken },
                    },
                Content = new StringContent("{\n  \"source\": \"Insurance\",\n  \"fields\": [\n    \"InsuranceID\"\n  ], \n  \"settings\": {\n    \"IncludeArchivedProviders\": true,\n    \"IncludeApplicants\": true\n  }, \n\t   \"filter\": {\"ProviderID\": \"" + insurance.ProviderID + "\", \n \"ReferenceSourceID.Name\": \"" + insurance.InsuranceCarrier + "\", \n \"InUse\": \"true\",  \n \"ExpirationDate\": \"" + insurance.Expires + "\" }}\n     }") {
                    Headers =
                    {
                            ContentType = new MediaTypeHeaderValue("application/json")
                        }
                }
            };

            using (var response = await client.SendAsync(request)) {
                if (response.StatusCode.ToString() == "OK") {
                    var body = await response.Content.ReadAsStringAsync();

                    providerInsurance = JsonConvert.DeserializeObject<List<Insurance>>(body);
                }
                else if (response.StatusCode.ToString() == "Unauthorized" || response.StatusCode.ToString() == "BadRequest") {
                    await GetAuthorizationToken();
                    await FindProviderInsuranceByName(insurance);
                }
                else {
                    //YOUR RETRY LOGIC
                }
            }

            insurance.InsuranceID = providerInsurance.Select(x => x.InsuranceID).FirstOrDefault();
            insurance.NumberOfRequests++;

            if (insurance.InsuranceID == null)
                insurance.IsNew = true;

            return insurance;
        }

        public async Task<Insurance> FindReferenceSourceByName(Insurance insurance) {

            var client = httpClient;
            List<ReferenceSource> referenceSources = new List<ReferenceSource>();

            var request = new HttpRequestMessage {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://" + _webAPIsettings.InstanceCode + ".api.asm-cloud.com/api/" + _webAPIsettings.InstanceCode + "/query"),
                Headers =
                {
                        { "Authorization", "Bearer " + _authorizationToken.AccessToken },
                    },
                Content = new StringContent("{\n  \"source\": \"ReferenceSource\",\n  \"fields\": [\n    \"ReferenceSourceID\"\n  ],   \n  \"settings\": {\n    \"IncludeArchivedProviders\": true,\n    \"IncludeApplicants\": true\n  }, \n\t   \"filter\": {\"Name\": \"" + insurance.InsuranceCarrier + "\", \n \"ReferenceType\": \"Insurance Carrier\" }}\n     }") {
                    Headers =
                    {
                            ContentType = new MediaTypeHeaderValue("application/json")
                        }
                }
            };

            using (var response = await client.SendAsync(request)) {
                if (response.StatusCode.ToString() == "OK") {
                    var body = await response.Content.ReadAsStringAsync();
                    referenceSources = JsonConvert.DeserializeObject<List<ReferenceSource>>(body);      
                }
                else if (response.StatusCode.ToString() == "Unauthorized" || response.StatusCode.ToString() == "BadRequest") {
                    await GetAuthorizationToken();
                    await FindReferenceSourceByName(insurance);
                }
                else {
                    //YOUR RETRY LOGIC
                }
            }

            insurance.NumberOfRequests++;
            insurance.ReferenceSourceID = referenceSources.Select(x => x.ReferenceSourceID).FirstOrDefault();;

            return insurance;
        }

        public async Task<Provider> FindProviderLastNameFirstNameNPI(Provider provider) {

            var client = httpClient;
            List<Provider> providers = new List<Provider>();

            var request = new HttpRequestMessage {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://" + _webAPIsettings.InstanceCode + ".api.asm-cloud.com/api/" + _webAPIsettings.InstanceCode + "/query"),
                Headers =
                {
                        { "Authorization", "Bearer " + _authorizationToken.AccessToken },
                    },
                Content = new StringContent("{\n  \"source\": \"Demographic\",\n  \"fields\": [\n    \"ProviderId\"\n  ], \n \"sort\": [{\"DateCreated\": \"asc\" }] ,  \n  \"settings\": {\n    \"IncludeArchivedProviders\": true,\n    \"IncludeApplicants\": true\n  }, \n\t   \"filter\": {\"LastName\": \"" + provider.LastName + "\", \n \"FirstName\": \"" + provider.FirstName + "\",  \n \"NPI\": \"" + provider.NPI + "\" }}\n     }") {
                    Headers =
                    {
                            ContentType = new MediaTypeHeaderValue("application/json")
                        }
                }
            };

            using (var response = await client.SendAsync(request)) {
                if (response.StatusCode.ToString() == "OK") {
                    var body = await response.Content.ReadAsStringAsync();

                    providers = JsonConvert.DeserializeObject<List<Provider>>(body);
                }
                else if (response.StatusCode.ToString() == "Unauthorized" || response.StatusCode.ToString() == "BadRequest") {
                    await GetAuthorizationToken();
                    await FindProviderLastNameFirstNameNPI(provider);
                }
                else {
                    //YOUR RETRY LOGIC
                }
            }

            provider.ProviderID = providers.Select(x => x.ProviderID).FirstOrDefault();
            provider.NumberOfRequests++;

            if (providers.Count > 1) {
                provider.MultipleMatches = true;
                return provider;
            }
            
            /*Loose Search using NPI and LastName*/
            if (provider.ProviderID == null)
                provider = await FindProviderLastNameNPI(provider);
            
            return provider;
        }

        public async Task<Provider> FindProviderLastNameNPI(Provider provider) {

            var client = httpClient;
            List<Provider> providers = new List<Provider>();

            var request = new HttpRequestMessage {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://" + _webAPIsettings.InstanceCode + ".api.asm-cloud.com/api/" + _webAPIsettings.InstanceCode + "/query"),
                Headers =
                {
                        { "Authorization", "Bearer " + _authorizationToken.AccessToken },
                    },
                Content = new StringContent("{\n  \"source\": \"Demographic\",\n  \"fields\": [\n    \"ProviderId\"\n  ], \n \"sort\": [{\"DateCreated\": \"asc\" }] ,  \n  \"settings\": {\n    \"IncludeArchivedProviders\": true,\n    \"IncludeApplicants\": true\n  }, \n\t   \"filter\": {\"LastName\": \"" + provider.LastName + "\",  \n \"NPI\": \"" + provider.NPI + "\" }}\n     }") {
                    Headers =
                    {
                            ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

            using (var response = await client.SendAsync(request)) {
                if (response.StatusCode.ToString() == "OK") {
                    var body = await response.Content.ReadAsStringAsync();

                    providers = JsonConvert.DeserializeObject<List<Provider>>(body);
                }
                else if (response.StatusCode.ToString() == "Unauthorized" || response.StatusCode.ToString() == "BadRequest") {
                    await GetAuthorizationToken();
                    await FindProviderLastNameNPI(provider);
                }
                else {
                    //YOUR RETRY LOGIC
                }
            }

            provider.ProviderID = providers.Select(x => x.ProviderID).FirstOrDefault();
            provider.NumberOfRequests++;

            if (providers.Count > 1)
                provider.MultipleMatches = true;

            if (provider.ProviderID == null)
                provider.IsNew = true;

            return provider;
        }

        public async Task<Insurance> CreateReferenceSourceRecord(Insurance insurance) {
            var client = httpClient;
            ReferenceSource referenceSource = new ReferenceSource();

            var request = new HttpRequestMessage {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://" + _webAPIsettings.InstanceCode + ".api.asm-cloud.com/api/" + _webAPIsettings.InstanceCode + "/object/ReferenceSource"),
                Headers =
                {
                        { "Authorization", "Bearer " + _authorizationToken.AccessToken },
                },
                Content = new StringContent("{\n  \"ReferenceType\": \"Insurance Carrier\", \n  \"Name\": \"" + insurance.InsuranceCarrier + "\", \n  \"Address\": \"" + insurance.Address + "\", \n  \"Address2\": \"" + insurance.Address2 + "\", \n  \"City\": \"" + insurance.City + "\", \n  \"State\": \"" + insurance.State + "\", \n  \"Zip\": \"" + insurance.ZipCode + "\", \n  \"Country\": \"" + insurance.Country + "\",  \n  \"Telephone\": \"" + insurance.Telephone + "\"     }") {
                    Headers =
                    {
                            ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

            using (var response = await client.SendAsync(request)) {
                if (response.StatusCode.ToString() == "OK") {
                    var body = await response.Content.ReadAsStringAsync();

                    referenceSource = JsonConvert.DeserializeObject<ReferenceSource>(body);
                }
                else if (response.StatusCode.ToString() == "Unauthorized" || response.StatusCode.ToString() == "BadRequest") {
                    await GetAuthorizationToken();
                    await CreateReferenceSourceRecord(insurance);
                }
                else {
                    //YOUR RETRY LOGIC
                }
            }

            insurance.ReferenceSourceRecordCreated = true;
            insurance.ReferenceSourceID = referenceSource.ReferenceSourceID;
            insurance.NumberOfRequests++;

            return insurance;
        }

        public async Task<Insurance> CreateInsuranceRecord(Insurance insurance) {

            /*CHECK IF REFERENCESOURCE EXISTS*/
            insurance = await FindReferenceSourceByName(insurance);

            if (insurance.ReferenceSourceID == null) {
                insurance = await CreateReferenceSourceRecord(insurance);
            }

            var client = httpClient;
            Insurance newInsurance = new Insurance();

            var request = new HttpRequestMessage {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://" + _webAPIsettings.InstanceCode + ".api.asm-cloud.com/api/" + _webAPIsettings.InstanceCode + "/object/insurance"),
                Headers =
                {
                        { "Authorization", "Bearer " + _authorizationToken.AccessToken },
                    },
                Content = new StringContent("{\n  \"ReferenceSourceID\": \"" + insurance.ReferenceSourceID + "\", \n  \"ProviderID\": \"" + insurance.ProviderID + "\", \n  \"PolicyNumber\": \"" + insurance.PolicyNumber + "\",\n  \"Coverage\": \"" + insurance.Coverage + "\", \n  \"IssuedDate\": \"" + insurance.IssueDate + "\",\n  \"ExpirationDate\": \"" + insurance.Expires + "\",    }") {
                    Headers =
                    {
                            ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

            using (var response = await client.SendAsync(request)) {
                if (response.StatusCode.ToString() == "OK") {
                    var body = await response.Content.ReadAsStringAsync();

                    newInsurance = JsonConvert.DeserializeObject<Insurance>(body);
                }
                else if (response.StatusCode.ToString() == "Unauthorized" || response.StatusCode.ToString() == "BadRequest") {
                    await GetAuthorizationToken();
                    await CreateInsuranceRecord(insurance);
                }
                else {
                    //YOUR RETRY LOGIC
                }
            }

            insurance.InsuranceRecordCreated = true;
            insurance.InsuranceID = newInsurance.InsuranceID;
            insurance.NumberOfRequests++;

            return insurance;
        }

        public async Task<Provider> CreateDemographicRecord(Provider provider) {
            var client = httpClient;
            Provider newProvider = new Provider();

            var request = new HttpRequestMessage {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://" + _webAPIsettings.InstanceCode + ".api.asm-cloud.com/api/" + _webAPIsettings.InstanceCode + "/object/demographic"),
                Headers =
                {
                        { "Authorization", "Bearer " + _authorizationToken.AccessToken },
                    },
                Content = new StringContent("{\n  \"FirstName\": \"" + provider.FirstName + "\", \n  \"LastName\": \"" + provider.LastName + "\", \n  \"NPI\": \"" + provider.NPI + "\",     }") {
                    Headers =
                    {
                            ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

            using (var response = await client.SendAsync(request)) {
                if (response.StatusCode.ToString() == "OK") {
                    var body = await response.Content.ReadAsStringAsync();

                    newProvider = JsonConvert.DeserializeObject<Provider>(body);
                }
                else if (response.StatusCode.ToString() == "Unauthorized" || response.StatusCode.ToString() == "BadRequest") {
                    await GetAuthorizationToken();
                    await CreateDemographicRecord(provider);
                }
                else {
                    //YOUR RETRY LOGIC
                }
            }

            provider.DemographicCreated = true;
            provider.ProviderID = newProvider.ProviderID;
            provider.NumberOfRequests++;

            provider = await CreateAppointmentRecord(provider);

            return provider;
        }

        public async Task<Provider> CreateAppointmentRecord(Provider provider) {
            var client = httpClient;
            Appointment newAppointment = new Appointment();

            var request = new HttpRequestMessage {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://" + _webAPIsettings.InstanceCode + ".api.asm-cloud.com/api/" + _webAPIsettings.InstanceCode + "/object/appointment"),
                Headers =
                {
                        { "Authorization", "Bearer " + _authorizationToken.AccessToken },
                    },
                Content = new StringContent("{\n  \"ProviderID\": \"" + provider.ProviderID + "\", \n  \"FacilityID\": \"" + _webAPIsettings.ImportIntoFacilityID + "\"  }") {
                    Headers =
                    {
                            ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

            using (var response = await client.SendAsync(request)) {
                if (response.StatusCode.ToString() == "OK") {
                    var body = await response.Content.ReadAsStringAsync();

                    newAppointment = JsonConvert.DeserializeObject<Appointment>(body);
                }
                else if (response.StatusCode.ToString() == "Unauthorized" || response.StatusCode.ToString() == "BadRequest") {
                    await GetAuthorizationToken();
                    await CreateAppointmentRecord(provider);
                }
                else {
                    //YOUR RETRY LOGIC
                }
            }

            provider.AppointmentCreated = true;
            provider.AppointmentID = newAppointment.AppointmentID;
            provider.NumberOfRequests++;

            return provider;
        }
    }

    public class WebAPIAppSettings {
        #region Fields 
        private string apiUrl;
        private string grantType;
        private string clientId;
        private string clientSecret;
        private string instanceCode;
        private string facilityOrMarketID;
        private string importIntoFacilityID;
        private bool debug;
        #endregion

        #region Properties
        public string APIUrl { get { return apiUrl; } set { apiUrl = value; } }
        public string GrantType { get { return grantType; } set { grantType = value; } }
        public string ClientId { get { return clientId; } set { clientId = value; } }
        public string ClientSecret { get { return clientSecret; } set { clientSecret = value; } }
        public string InstanceCode { get { return instanceCode; } set { instanceCode = value; } }
        public string FacilityOrMarketID { get { return facilityOrMarketID; } set { facilityOrMarketID = value; } }
        public string ImportIntoFacilityID { get { return importIntoFacilityID; } set { importIntoFacilityID = value; } }
        public bool Debug { get { return debug; } set { debug = value; } }

        #endregion

        public WebAPIAppSettings(IConfigurationRoot configuration) {
            apiUrl = configuration["WebAPIConnection:APIUrl"];
            grantType = configuration["WebAPIConnection:GrantType"];
            clientId = configuration["WebAPIConnection:ClientId"];
            clientSecret = configuration["WebAPIConnection:ClientSecret"];
            instanceCode = configuration["WebAPIConnection:InstanceCode"];
            facilityOrMarketID = configuration["WebAPIConnection:FacilityOrMarketID"];
            importIntoFacilityID = configuration["WebAPIConnection:ImportIntoFacilityID"];
            debug = Convert.ToBoolean(configuration["WebAPIConnection:Debug"]);
        }
    }

    public class FileImportAppSettings {
        #region Fields 
        private string filePath;
        private string fileName;
        private bool debug;
        #endregion

        #region Properties
        public string FilePath { get { return filePath; } set { filePath = value; } }
        public string FileName { get { return fileName; } set { fileName = value; } }
        public bool Debug { get { return debug; } set { debug = value; } }

        #endregion

        public FileImportAppSettings(IConfigurationRoot configuration) {
            filePath = configuration["FileImport:FilePath"];
            fileName = configuration["FileImport:FileName"];
            debug = Convert.ToBoolean(configuration["FileImport:Debug"]);
        }
    }

    public static class ImportCSV {

        public static SampleImport.Collections.DataRowCollection LoadFromFile(FileImportAppSettings fileImportAppSettings) {
            string fullPath = fileImportAppSettings.FilePath + fileImportAppSettings.FileName;
            SampleImport.Collections.DataRowCollection dataRowCollection = new SampleImport.Collections.DataRowCollection();
            List<SampleImport.Models.DataRow> dataRows = new List<SampleImport.Models.DataRow>();
            int totalRows = -1;

            using (TextFieldParser parser = new TextFieldParser(fullPath)) {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                bool firstLine = true;

                if (fileImportAppSettings.Debug)
                    Console.WriteLine("Step 2: Loading data from CSV into memory");

                while (!parser.EndOfData) {
                    try {
                        string[] fields = parser.ReadFields();
                        if (firstLine) {
                            firstLine = false;
                            continue;
                        }

                        var dataRow = new SampleImport.Models.DataRow();
                        var provider = new Provider();
                        var insurance = new Insurance();

                        provider.FirstName = fields[0];
                        provider.LastName = fields[1];
                        provider.NPI = fields[2];
                        insurance.InsuranceCarrier = fields[3];
                        insurance.Address = fields[4];
                        insurance.Address2 = fields[5];
                        insurance.City = fields[6];
                        insurance.State = fields[7];
                        insurance.ZipCode = fields[8];
                        insurance.Country = fields[9];
                        insurance.Telephone = fields[10];
                        insurance.PolicyNumber = fields[11];
                        insurance.Coverage = fields[12];
                        insurance.IssueDate = fields[13];
                        insurance.Expires = fields[14];

                        dataRow.Provider = provider;
                        dataRow.Insurance = insurance;

                        if (DataRowValidForProcessing(dataRow))
                            dataRows.Add(dataRow);
                    }
                    catch (Exception ex) {
                        Console.WriteLine(ex.ToString());
                    }
                    finally {
                        totalRows++;
                    }
                }
            }

            dataRowCollection.DataRows = dataRows;

            if (fileImportAppSettings.Debug) {
                int rowcount = totalRows;
                int recordsLoaded = dataRowCollection.DataRows.Count;
                Console.WriteLine(String.Format($"Step 3: {recordsLoaded} rows successfully loaded out of {rowcount} rows in CSV."));
            }

            return dataRowCollection;
        }

        public static bool DataRowValidForProcessing(SampleImport.Models.DataRow dataRow) {
            if (dataRow.Provider.FirstName == "" || dataRow.Provider.LastName == "" || dataRow.Provider.NPI == "")
                return false;
            else if (dataRow.Insurance.InsuranceCarrier == "")
                return false;
            else
                return true;
        }
    }    
}
