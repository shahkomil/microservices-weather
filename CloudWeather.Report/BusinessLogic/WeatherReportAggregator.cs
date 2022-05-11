using CloudWeather.Report.Config;
using CloudWeather.Report.DataAccess;
using CloudWeather.Report.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CloudWeather.Report.BusinessLogic
{
    /// <summary>
    /// Aggregates data from multiple external sources to build a weather report
    /// </summary>
    public interface IWeatherReportAggregator {
        /// <summary>
        /// Builds and returns a Weekly Weather Report.
        /// Persists Weather Weather Report data
        /// </summary>
        /// <param> name ="zip"</param>
        /// <param> name ="days"</param>
        /// <returns></returns>
        public Task<WeatherReport> BuildReport(string zip, string days);
    }

    

    public class WeatherReportAggregator :IWeatherReportAggregator
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger _logger;
        private readonly WeatherDataConfig _weatherDataConfig;
        private readonly WeatherReportDbContext _db;

        public WeatherReportAggregator(
            IHttpClientFactory http,
            ILogger<WeatherReportAggregator> logger,
            IOptions<WeatherDataConfig> weatherConfig,
            WeatherReportDbContext db)
        { 
            _http = http;
            _logger = logger;
            _weatherDataConfig = weatherConfig.Value;
            _db = db;
        }
            
        public async Task<WeatherReport> BuildWeeklyReport(string zip, string days)
        {
            var httpClient = _http.CreateClient();
            var precipData = await FetchPrecipitationData(httpClient, zip, days);
            var totalSnow = GetTotalSnow(precipData);
            var totalRain = GetTotalRain(precipData);

            _logger.LogInformation($"zip:{zip} over last {days} days:" +
                $"total snow:{totalSnow},rain:{totalRain}"
                );



            var tempData = await FetchTemperatureData(httpClient, zip, days);
            var averageHighTemp = tempData.Average(t => t.TempHighF);
            var averageLowTemp = tempData.Average(t => t.TempLowF);

            _logger.LogInformation($"zip:{zip} over last {days} days:" +
               $"lo temp:{averageLowTemp},hi temp: {averageHighTemp}"
               );

            var WeatherReport = new WeatherReport
            {
                AverageHighF = Math.Round(averageHighTemp, 1),
                AverageLowF = Math.Round(averageLowTemp, 1),
                RainfallTotalInches = (decimal)totalRain,
                SnowTotalInches = (decimal)totalSnow,
                ZipCode = zip,
                CreatedOn = DateTime.UtcNow,
            };
            // use cached weather report instead of making round trips when possible?
            _db.Add(WeatherReport);
            await _db.SaveChangesAsync();

            return WeatherReport;
        }

        private object GetTotalRain(List<PrecipitationModel> precipData)
        {
            var totalRain = precipData.Where(p => p.WeatherType == "rain")
                .Sum(p => p.AmountInches);
            return Math.Round(totalRain, 1);
        }

        private object GetTotalSnow(List<PrecipitationModel> precipData)
        {
            var totalSnow = precipData.Where(p => p.WeatherType == "snow")
                .Sum(p => p.AmountInches);
            return Math.Round(totalSnow,1);
        }

        private async Task<List<TemperatureModel>> FetchTemperatureData(HttpClient httpClient, string zip, string days)
        {
            var endpoint = BuildTemperatureServiceEndpoint(zip, days);
            var temperatureRecords = await httpClient.GetAsync(endpoint);
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var temperatureData = await temperatureRecords.Content.ReadFromJsonAsync<List<TemperatureModel>>(jsonSerializerOptions);
            return temperatureData ?? new List<TemperatureModel>();
        }

        private async Task<List<PrecipitationModel>> FetchPrecipitationData(HttpClient httpClient, string zip, string days)
        {
            var endpoint = BuildPrecipitationServiceEndpoint(zip, days);
            var precipitationRecords = await httpClient.GetAsync(endpoint);
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var precipitationData = await precipitationRecords
                                    .Content
                                    .ReadFromJsonAsync<List<PrecipitationModel>>(jsonSerializerOptions);
            return precipitationData ?? new List<PrecipitationModel>();
        }

        private string BuildPrecipitationServiceEndpoint(string zip, string days)
        {
            var precipServiceProtocol = _weatherDataConfig.TempDataProtocol;
            var precipServiceHost = _weatherDataConfig.TempDataHost;
            var precipServicePost = _weatherDataConfig.TempDataPort;
            return $"{precipServiceProtocol}:// {precipServiceHost}:{precipServicePost}/observation/{zip}?days={days}";
        }

        private string BuildTemperatureServiceEndpoint(string zip, string days)
        {
            var tempServiceProtocol = _weatherDataConfig.TempDataProtocol;
            var tempServiceHost = _weatherDataConfig.TempDataHost;
            var tempServicePost = _weatherDataConfig.TempDataPort;
            return $"{tempServiceProtocol}:// {tempServiceHost}:{tempServicePost}/observation/{zip}?days={days}";
        }
    }
}
