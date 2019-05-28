﻿using System;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HomematicIp.Data;
using HomematicIp.Data.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HomematicIp.Services
{
    public abstract class HomematicServiceBase
    {
        protected readonly Func<HttpClient> HttpClientFactory;
        protected readonly HomematicConfiguration HomematicConfiguration;
        protected string _clientAuthToken;
        protected HttpClient HttpClient;
        protected string UrlWebSocket;
        protected StringContent ClientCharacteristicsStringContent;
        protected const string CLIENTAUTH= "CLIENTAUTH";

        protected HomematicServiceBase(Func<HttpClient> httpClientFactory, HomematicConfiguration homematicConfiguration)
        {
            HttpClientFactory = httpClientFactory;
            HomematicConfiguration = homematicConfiguration;
            HttpClient = httpClientFactory();
        }
        protected virtual async Task Initialize(ClientCharacteristicsRequestObject clientCharacteristicsRequestObject = null, CancellationToken cancellationToken = default)
        {
            if (clientCharacteristicsRequestObject == null)
                clientCharacteristicsRequestObject = new ClientCharacteristicsRequestObject(HomematicConfiguration.AccessPointId);
            ClientCharacteristicsStringContent = GetStringContent(clientCharacteristicsRequestObject);
            var httpResponseMessage = await HttpClient.PostAsync("https://lookup.homematic.com:48335/getHost", ClientCharacteristicsStringContent, cancellationToken);
            RestAndWebSocketUrls restAndWebSocketUrls;
            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var content = await httpResponseMessage.Content.ReadAsStringAsync();
                restAndWebSocketUrls = JsonConvert.DeserializeObject<RestAndWebSocketUrls>(content);
            }
            else
            {
                if ((int) httpResponseMessage.StatusCode == 418) //I'm a teapot message
                {
                    throw new ArgumentException($"It is highly likely that you accidentally mistyped your access point id.");
                }
                restAndWebSocketUrls = new RestAndWebSocketUrls { UrlREST = "https://ps1.homematic.com:6969/", UrlWebSocket = "wss://ps1.homematic.com:8888/" };
            }
            HttpClient = HttpClientFactory();
            HttpClient.BaseAddress = new Uri($"{restAndWebSocketUrls.UrlREST}/");
            HttpClient.DefaultRequestHeaders.Add("VERSION", "12");
            HttpClient.DefaultRequestHeaders.Add(CLIENTAUTH, ClientAuthToken);
            UrlWebSocket = restAndWebSocketUrls.UrlWebSocket;
        }

        private JsonSerializerSettings JsonSerializerSettings { get; } = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        protected StringContent GetStringContent(object obj) =>
            new StringContent(JsonConvert.SerializeObject(obj, JsonSerializerSettings));

        protected class RestAndWebSocketUrls
        {
            public string UrlREST { get; set; }
            public string UrlWebSocket { get; set; }
        }
        protected class ClientCharacteristicsRequestObject
        {
            public ClientCharacteristicsRequestObject(string id)
            {
                Id = id;
            }

            public ClientCharacteristics ClientCharacteristics { get; set; } = new ClientCharacteristics();
            public string Id { get; set; }
        }

        protected class StartInclusionModeForDeviceRequestObject
        {
            public StartInclusionModeForDeviceRequestObject(string deviceId)
            {
                DeviceId = deviceId;
            }
            public string DeviceId { get; set; }
        }

        protected class SetDeviceLabelRequestObject
        {
            public SetDeviceLabelRequestObject(string deviceId, string label)
            {
                DeviceId = deviceId;
                Label = label;
            }
            public string DeviceId { get; set; }
            public string Label { get; set; }
        }

        protected class ClientCharacteristics
        {
            public string ApiVersion => "10";
            public string ApplicationIdentifier { get; set; } = "homematicip-dotnetcore";
            public string ApplicationVersion { get; set; } = "1.0";
            public string DeviceManufacturer { get; set; } = "none";
            public ClientDeviceType DeviceType { get; set; } = ClientDeviceType.Computer;
            public string Language => CultureInfo.CurrentCulture.Name;
            public string OsType =>  Environment.OSVersion.Platform.ToString();
            public string OsVersion => Environment.OSVersion.Version.ToString();
        }
        private string GetAccessPointIdWithoutDashes(string accessPointId)
        {
            var accessPointIdWithoutDashes = accessPointId.Replace("-", "");
            if (accessPointIdWithoutDashes.Length != 24)
                throw new ArgumentException($"The accesspoint id (SGTIN) {accessPointId} is invalid. It needs to have exactly 24 digits without the dashes.");
            return accessPointIdWithoutDashes;
        }

        public string ClientAuthToken
        {
            get
            {
                if (_clientAuthToken == null)
                {
                    HomematicConfiguration.AccessPointId = GetAccessPointIdWithoutDashes(HomematicConfiguration.AccessPointId);
                    using (SHA512 shaM = new SHA512Managed())
                    {
                        var data = Encoding.UTF8.GetBytes($"{HomematicConfiguration.AccessPointId}jiLpVitHvWnIGD1yo7MA");
                        var hash = shaM.ComputeHash(data);
                        _clientAuthToken = BitConverter.ToString(hash).Replace("-", "").ToUpper();
                    }
                }
                return _clientAuthToken;
            }
        }
    }
}