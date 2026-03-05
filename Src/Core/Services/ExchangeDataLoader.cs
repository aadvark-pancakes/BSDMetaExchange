using Core.Contracts;
using Core.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Core.Services
{
    public class ExchangeDataLoader : IExchangeDataLoader
    {
        private readonly ILogger<ExchangeDataLoader> logger;

        public ExchangeDataLoader(ILogger<ExchangeDataLoader> logger)
        {
            this.logger = logger;
        }

        public List<Exchange> LoadExchanges(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}"); //TODO log?

            var exchanges = new List<Exchange>();

            // search recursively to pick up exchanges stored in subfolders as well
            var files = Directory.GetFiles(folderPath, "*.json", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    using var fs = File.OpenRead(file);

                    var exchange = JsonSerializer.Deserialize<Exchange>(fs);

                    if (exchange != null)
                        exchanges.Add(exchange);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to parse json and load exchange from file {FilePath}. Continuing onto next file.", file);
                }
            }

            return exchanges;
        }
    }
}
