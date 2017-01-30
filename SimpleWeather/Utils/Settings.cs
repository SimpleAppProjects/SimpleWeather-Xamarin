﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SimpleWeather
{
    public static class Settings
    {
        public static bool WeatherLoaded { get { return isWeatherLoaded(); } set { setWeatherLoaded(value); } }
        public static string Unit { get { return getTempUnit(); } set { setTempUnit(value); } }
        public static string API { get { return getAPI(); } set { setAPI(value); } }
        public static string API_KEY { get { return getAPIKEY(); } set { setAPIKEY(value); } }

        private static StorageFolder appDataFolder = ApplicationData.Current.LocalFolder;
        private static StorageFile locationsFile;

        private static string Farenheit = "F";
        private static string Celsius = "C";

        private static string getTempUnit()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (!localSettings.Values.ContainsKey("Units") || localSettings.Values["Units"] == null)
            {
                return Farenheit;
            }
            else if (localSettings.Values["Units"].Equals("C"))
                return Celsius;

            return Farenheit;
        }

        private static void setTempUnit(string value)
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            if (value == Celsius)
                localSettings.Values["Units"] = Celsius;
            else
                localSettings.Values["Units"] = Farenheit;
        }

        private static bool isWeatherLoaded()
        {
            if (locationsFile == null)
                locationsFile = appDataFolder.CreateFileAsync("locations.json", CreationCollisionOption.OpenIfExists).AsTask().GetAwaiter().GetResult();

            FileInfo fileinfo = new FileInfo(locationsFile.Path);

            if (fileinfo.Length == 0 || !fileinfo.Exists)
                return false;

            var localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values["weatherLoaded"] == null)
            {
                return false;
            }
            else if (localSettings.Values["weatherLoaded"].Equals("true"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void setWeatherLoaded(bool isLoaded)
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            if (isLoaded)
            {
                localSettings.Values["weatherLoaded"] = "true";
            }
            else
            {
                localSettings.Values["weatherLoaded"] = "false";
            }
        }

        private static string getAPI()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (!localSettings.Values.ContainsKey("API") || localSettings.Values["API"] == null)
            {
                setAPI("WUnderground");
                return "WUnderground";
            }
            else
                return (string)localSettings.Values["API"];
        }

        private static void setAPI(string value)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["API"] = value;
        }

        #region Yahoo Weather
        public static async Task<List<WeatherYahoo.Coordinate>> getLocations()
        {
            if (locationsFile == null)
                locationsFile = await appDataFolder.CreateFileAsync("locations.json", CreationCollisionOption.OpenIfExists);

            FileInfo fileinfo = new FileInfo(locationsFile.Path);

            if (fileinfo.Length == 0 || !fileinfo.Exists)
                return null;

            while (FileUtils.IsFileLocked(locationsFile).GetAwaiter().GetResult())
            {
                await Task.Delay(100);
            }

            List<WeatherYahoo.Coordinate> locations;

            // Load locations
            using (FileRandomAccessStream fileStream = (await locationsFile.OpenAsync(FileAccessMode.Read).AsTask().ConfigureAwait(false)) as FileRandomAccessStream)
            {
                DataContractJsonSerializer deSerializer = new DataContractJsonSerializer(typeof(List<WeatherYahoo.Coordinate>));
                MemoryStream memStream = new MemoryStream();
                fileStream.AsStreamForRead().CopyTo(memStream);
                memStream.Seek(0, 0);

                locations = ((List<WeatherYahoo.Coordinate>)deSerializer.ReadObject(memStream));

                await fileStream.AsStream().FlushAsync();
                fileStream.Dispose();
                await memStream.FlushAsync();
                memStream.Dispose();
            }

            return locations;
        }

        public static async void saveLocations(List<WeatherYahoo.Coordinate> locations)
        {
            if (locationsFile == null)
                locationsFile = await appDataFolder.CreateFileAsync("locations.json", CreationCollisionOption.OpenIfExists);

            while (FileUtils.IsFileLocked(locationsFile).GetAwaiter().GetResult())
            {
                await Task.Delay(100);
            }

            using (FileRandomAccessStream fileStream = (await locationsFile.OpenAsync(FileAccessMode.ReadWrite).AsTask().ConfigureAwait(false)) as FileRandomAccessStream)
            {
                MemoryStream memStream = new MemoryStream();
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<WeatherYahoo.Coordinate>));
                serializer.WriteObject(memStream, locations);

                fileStream.Size = 0;
                memStream.WriteTo(fileStream.AsStream());

                await memStream.FlushAsync();
                memStream.Dispose();
                await fileStream.AsStream().FlushAsync();
                fileStream.Dispose();
            }
        }
        #endregion

        #region WeatherUnderground
        public static async Task<List<string>> getLocations_WU()
        {
            if (locationsFile == null)
                locationsFile = await appDataFolder.CreateFileAsync("locations.json", CreationCollisionOption.OpenIfExists);

            FileInfo fileinfo = new FileInfo(locationsFile.Path);

            if (fileinfo.Length == 0 || !fileinfo.Exists)
                return null;

            while (FileUtils.IsFileLocked(locationsFile).GetAwaiter().GetResult())
            {
                await Task.Delay(100);
            }

            List<string> locations;

            // Load locations
            using (FileRandomAccessStream fileStream = (await locationsFile.OpenAsync(FileAccessMode.Read).AsTask().ConfigureAwait(false)) as FileRandomAccessStream)
            {
                DataContractJsonSerializer deSerializer = new DataContractJsonSerializer(typeof(List<string>));
                MemoryStream memStream = new MemoryStream();
                fileStream.AsStreamForRead().CopyTo(memStream);
                memStream.Seek(0, 0);

                locations = ((List<string>)deSerializer.ReadObject(memStream));

                await fileStream.AsStream().FlushAsync();
                fileStream.Dispose();
                await memStream.FlushAsync();
                memStream.Dispose();
            }

            return locations;
        }

        public static async void saveLocations(List<string> locations)
        {
            if (locationsFile == null)
                locationsFile = await appDataFolder.CreateFileAsync("locations.json", CreationCollisionOption.OpenIfExists);

            while (FileUtils.IsFileLocked(locationsFile).GetAwaiter().GetResult())
            {
                await Task.Delay(100);
            }

            using (FileRandomAccessStream fileStream = (await locationsFile.OpenAsync(FileAccessMode.ReadWrite).AsTask().ConfigureAwait(false)) as FileRandomAccessStream)
            {
                MemoryStream memStream = new MemoryStream();
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<string>));
                serializer.WriteObject(memStream, locations);

                fileStream.Size = 0;
                memStream.WriteTo(fileStream.AsStream());

                await memStream.FlushAsync();
                memStream.Dispose();
                await fileStream.AsStream().FlushAsync();
                fileStream.Dispose();
            }
        }

        private static string getAPIKEY()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (!localSettings.Values.ContainsKey("API_KEY") || localSettings.Values["API_KEY"] == null)
            {
                String key = String.Empty;
                key = readAPIKEYfile().ConfigureAwait(false).GetAwaiter().GetResult();

                if (!String.IsNullOrWhiteSpace(key))
                    setAPIKEY(key);

                return key;
            }
            else
                return (string)localSettings.Values["API_KEY"];
        }

        private static async Task<string> readAPIKEYfile()
        {
            // Read key from file
            String key = String.Empty;
            try
            {
                StorageFile keyFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///API_KEY.txt")).AsTask().ConfigureAwait(false);
                FileInfo fileinfo = new FileInfo(keyFile.Path);

                if (fileinfo.Length != 0 || fileinfo.Exists)
                {
                    StreamReader reader = new StreamReader(await keyFile.OpenStreamForReadAsync());
                    key = reader.ReadLine();
                    reader.Dispose();
                }
            }
            catch (FileNotFoundException) { }

            return key;
        }

        private static void setAPIKEY(string API_KEY)
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            if (!String.IsNullOrWhiteSpace(API_KEY))
                localSettings.Values["API_KEY"] = API_KEY;
        }
        #endregion
    }
}
