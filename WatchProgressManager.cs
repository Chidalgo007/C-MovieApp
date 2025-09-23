using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MovieLibrary
{
  public class WatchProgress
  {
    public int Season { get; set; }
    public int Episode { get; set; }
  }

  public static class WatchProgressManager
  {
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MovieLibrary", "watchprogress.json");

    private static Dictionary<string, WatchProgress> _progress = new Dictionary<string, WatchProgress>();

    static WatchProgressManager()
    {
      Load();
    }

    public static void SaveProgress(string seriesTitle, int season, int episode)
    {
      _progress[seriesTitle] = new WatchProgress { Season = season, Episode = episode };
      Save();
    }

    public static WatchProgress? GetProgress(string seriesTitle)
    {
      return _progress.ContainsKey(seriesTitle) ? _progress[seriesTitle] : null;
    }

    private static void Load()
    {
      try
      {
        if (File.Exists(FilePath))
        {
          var json = File.ReadAllText(FilePath);
          _progress = JsonConvert.DeserializeObject<Dictionary<string, WatchProgress>>(json)
            ?? new Dictionary<string, WatchProgress>();

        }
        else
        {
          _progress = new Dictionary<string, WatchProgress>();
        }
      }
      catch
      {
        _progress = new Dictionary<string, WatchProgress>();
      }
    }

    private static void Save()
    {
      try
      {
        var dir = Path.GetDirectoryName(FilePath);
        if (dir != null)
          Directory.CreateDirectory(dir);

        var json = JsonConvert.SerializeObject(_progress, Formatting.Indented);
        File.WriteAllText(FilePath, json);
      }
      catch { }
    }
  }
}
