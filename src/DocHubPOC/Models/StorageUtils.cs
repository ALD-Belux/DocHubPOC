using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Serilog;
using System;

namespace DocHubPOC.Models
{
    public static class StorageUtils
    {
        /// <summary>
        /// This static method return the Azure Storage parameters
        /// from AzureStorageSettings.json or environment variables.
        ///
        /// Either the Azure Storage Emulator parameters or the actual
        /// Azure Storage.
        /// </summary>
        public static CloudStorageAccount StorageAccount
        {
            get
            {
                var _thisLog = Log.ForContext<CloudStorageAccount>();

                _thisLog.Debug("Create IConfiguration for Azure Storage");
                var builder = new ConfigurationBuilder()
                    .AddJsonFile("AzureStorageSettings.json")
                    .AddEnvironmentVariables();
                IConfiguration config = builder.Build();

                string account = config.Get<string>("AppSettings:StorageAccountName");
                _thisLog.Debug("Use {@account} for storage access", account);
                // This enables the storage emulator when running locally using the Azure compute emulator.
                if (account == "{StorageAccountName}")
                {
                    _thisLog.Debug("Use development account/key (Azure Storage Emulator)");
                    return CloudStorageAccount.DevelopmentStorageAccount;
                }

                _thisLog.Debug("Use production key");
                string key = config.Get<string>("AppSettings:StorageAccountAccessKey");
                string connectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", account, key);
                return CloudStorageAccount.Parse(connectionString);
            }
        }
    }
}