using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CalendarioWPF.Services
{
    public class OpenHolidaysApiService : IFestivosApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://openholidaysapi.org";

        public OpenHolidaysApiService()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        }

        public async Task<List<string>> ObtenerFestivosAsync(string isoCode, int year)
        {
            try
            {
                // isoCode comes as 'ES-MD' etc. We extract the country code ('ES')
                string countryCode = isoCode.Split('-')[0];
                string url = $"/PublicHolidays?countryIsoCode={countryCode}&subdivisionCode={isoCode}&validFrom={year}-01-01&validTo={year}-12-31&languageIsoCode=ES";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                
                using var document = JsonDocument.Parse(json);
                var festivos = new List<string>();

                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("startDate", out var startDateProp))
                    {
                        if (DateTime.TryParse(startDateProp.GetString(), out var date))
                        {
                            festivos.Add(date.ToString("dd/MM/yyyy"));
                        }
                    }
                }

                return festivos;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.Error($"Error obteniendo festivos de OpenHolidays API para {isoCode} - {year}: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<Dictionary<string, string>> ObtenerRegionesAsync(string countryCode = "ES")
        {
            try
            {
                string url = $"/Subdivisions?countryIsoCode={countryCode}&languageIsoCode=ES";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                
                using var document = JsonDocument.Parse(json);
                var regiones = new Dictionary<string, string>();

                foreach (var item in document.RootElement.EnumerateArray())
                {
                    string code = item.GetProperty("code").GetString() ?? string.Empty;
                    
                    // Prefer the translated name, fallback to shortName
                    string name = item.GetProperty("shortName").GetString() ?? code;
                    if (item.TryGetProperty("name", out var namesArray) && namesArray.GetArrayLength() > 0)
                    {
                        var firstTranslation = namesArray.EnumerateArray().FirstOrDefault();
                        if (firstTranslation.TryGetProperty("text", out var textProp))
                        {
                            name = textProp.GetString() ?? name;
                        }
                    }

                    if (!string.IsNullOrEmpty(code))
                    {
                        regiones[code] = name;
                    }
                }

                return regiones;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.Error($"Error obteniendo regiones de OpenHolidays API para {countryCode}: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }
    }
}
