using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MovieLibrary
{
  public static class PosterService
  {

    // Method to clear memory cache if needed
    public static void ClearMemoryCache()
    {
      MemoryCache.Clear();
    }

    // Method to update memory cache with custom poster
    public static void UpdateMemoryCache(string cacheKey, string posterPath)
    {
      MemoryCache[cacheKey] = posterPath;
    }

    // Method to clear specific item from memory cache
    public static void ClearMemoryCacheItem(string cacheKey)
    {
      MemoryCache.TryRemove(cacheKey, out _);
    }
    private static readonly string ApiKey = "653f5b1225301539044cae7a67f8234e";
    private static readonly string CacheFolder =
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                     "MovieLibrary", "Posters");

    private static readonly HttpClient http = new();
    private static readonly string Placeholder = Path.Combine(CacheFolder, "placeholder.png");

    // Memory cache to avoid repeated disk checks
    private static readonly ConcurrentDictionary<string, string> MemoryCache = new();

    // Track ongoing requests to prevent duplicates
    private static readonly ConcurrentDictionary<string, Task<string>> OngoingRequests = new();

    static PosterService()
    {
      if (!Directory.Exists(CacheFolder))
        Directory.CreateDirectory(CacheFolder);

      string exeFolder = AppDomain.CurrentDomain.BaseDirectory;
      string source = Path.Combine(exeFolder, "Resources", "placeholder.png");
      if (File.Exists(source) && !File.Exists(Placeholder))
        File.Copy(source, Placeholder);
    }

    public static async Task<string> GetPosterAsync(string title, int? year = null)
    {
      // Create a better cache key that includes year
      string cacheKey = CreateCacheKey(title, year);

      // Check memory cache first (fastest)
      if (MemoryCache.TryGetValue(cacheKey, out string cachedPath))
        return cachedPath;

      // Check if there's already an ongoing request for this poster
      if (OngoingRequests.TryGetValue(cacheKey, out Task<string> ongoingTask))
        return await ongoingTask;

      // Create and track the request
      var requestTask = FetchPosterInternal(title, year, cacheKey);
      OngoingRequests[cacheKey] = requestTask;

      try
      {
        string result = await requestTask;
        MemoryCache[cacheKey] = result; // Cache the result in memory
        return result;
      }
      finally
      {
        OngoingRequests.TryRemove(cacheKey, out _); // Clean up
      }
    }

    private static async Task<string> FetchPosterInternal(string title, int? year, string cacheKey)
    {
      string safeName = CreateSafeFileName(title, year);
      string cachedFile = Path.Combine(CacheFolder, safeName + ".jpg");

      // Check disk cache
      if (File.Exists(cachedFile))
        return cachedFile;

      try
      {
        // Try movie search first
        string posterPath = await SearchMovie(title, year);

        // If movie search fails, try TV search (for series)
        if (string.IsNullOrEmpty(posterPath))
          posterPath = await SearchTV(title, year);

        if (!string.IsNullOrEmpty(posterPath))
        {
          string posterUrl = "https://image.tmdb.org/t/p/w500" + posterPath;

          // Add timeout for HTTP requests
          using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
          var imgBytes = await http.GetByteArrayAsync(posterUrl, cts.Token);

          await File.WriteAllBytesAsync(cachedFile, imgBytes, cts.Token);
          return cachedFile;
        }
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine($"Error fetching poster for {title}: {ex.Message}");
      }

      return Placeholder;
    }

    private static async Task<string> SearchMovie(string title, int? year)
    {
      try
      {
        string url = $"https://api.themoviedb.org/3/search/movie?api_key={ApiKey}&query={Uri.EscapeDataString(title)}";
        if (year != null)
          url += $"&year={year}";

        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        string json = await http.GetStringAsync(url, cts.Token);
        var results = JObject.Parse(json)["results"] as JArray;

        if (results != null && results.Count > 0)
        {
          return results[0]["poster_path"]?.ToString();
        }
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine($"Movie search failed for {title}: {ex.Message}");
      }

      return string.Empty;
    }

    private static async Task<string> SearchTV(string title, int? year)
    {
      try
      {
        string url = $"https://api.themoviedb.org/3/search/tv?api_key={ApiKey}&query={Uri.EscapeDataString(title)}";
        if (year != null)
          url += $"&first_air_date_year={year}";

        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        string json = await http.GetStringAsync(url, cts.Token);
        var results = JObject.Parse(json)["results"] as JArray;

        if (results != null && results.Count > 0)
        {
          return results[0]["poster_path"]?.ToString();
        }
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine($"TV search failed for {title}: {ex.Message}");
      }

      return string.Empty;
    }

    private static string CreateCacheKey(string title, int? year)
    {
      string cleanTitle = Regex.Replace(title, @"[^\w\s-]", "").Trim();
      return year != null ? $"{cleanTitle}_{year}" : cleanTitle;
    }

    private static string CreateSafeFileName(string title, int? year)
    {
      // More robust filename creation
      string safeName = Regex.Replace(title, @"[^\w\s-]", "");
      safeName = Regex.Replace(safeName, @"\s+", "_").Trim('_');

      if (year != null)
        safeName += $"_{year}";

      // Ensure filename isn't too long
      if (safeName.Length > 100)
        safeName = safeName.Substring(0, 100);

      return safeName;
    }

    // Method to get cache statistics
    public static (int MemoryCached, int DiskCached) GetCacheStats()
    {
      int memoryCached = MemoryCache.Count;
      int diskCached = 0;

      try
      {
        if (Directory.Exists(CacheFolder))
          diskCached = Directory.GetFiles(CacheFolder, "*.jpg").Length;
      }
      catch { }

      return (memoryCached, diskCached);
    }
  }
}