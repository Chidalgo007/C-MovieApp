using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;

namespace MovieLibrary
{
  public static class PosterService
  {
    private static readonly string ApiKey = "653f5b1225301539044cae7a67f8234e";
    private static readonly string CacheFolder =
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                     "MovieLibrary", "Posters");

    private static readonly HttpClient http = new();

    static PosterService()
    {
      if (!Directory.Exists(CacheFolder))
        Directory.CreateDirectory(CacheFolder);
    }

    public static async Task<string> GetPosterAsync(string title, int? year = null)
    {
      string safeName = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
      string cachedFile = Path.Combine(CacheFolder, safeName + ".jpg");

      if (File.Exists(cachedFile))
        return cachedFile;

      try
      {
        // TMDb search query
        string url = $"https://api.themoviedb.org/3/search/movie?api_key={ApiKey}&query={Uri.EscapeDataString(title)}";
        if (year != null)
          url += $"&year={year}";

        string json = await http.GetStringAsync(url);
        var results = JObject.Parse(json)["results"] as JArray;

        if (results != null && results.Count > 0)
        {
          string? posterPath = results[0]["poster_path"]?.ToString();
          if (!string.IsNullOrEmpty(posterPath))
          {
            string posterUrl = "https://image.tmdb.org/t/p/w500" + posterPath;
            var imgBytes = await http.GetByteArrayAsync(posterUrl);
            await File.WriteAllBytesAsync(cachedFile, imgBytes);
            return cachedFile;
          }
        }
      }
      catch
      {
        // ignore errors, fall back to placeholder
      }

      return "pack://application:,,,/Resources/placeholder.png";
    }
  }
}
