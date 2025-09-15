using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MovieLibrary
{
  public class MovieItem : INotifyPropertyChanged
  {
    private string _posterPath = "pack://application:,,,/Resources/placeholder.png";

    public string Title { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int? Year { get; set; } = null;
    public string DisplayTitle => Year.HasValue ? $"{Title} ({Year})" : Title;

    // for filtering
    public List<int> GenreIds { get; set; } = new();  // TMDb genre IDs
    public string CountryCode { get; set; } = "";           // For Asian classification
    public bool IsMovie { get; set; } = true;

    public string PosterPath
    {
      get => _posterPath;
      set { _posterPath = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
  public class SeriesItem : INotifyPropertyChanged
  {
    private string _posterPath = "pack://application:,,,/Resources/placeholder.png";
    public string Title { get; set; } = "";
    public string FolderPath { get; set; } = "";

    public string PosterPath
    {
      get => _posterPath;
      set { _posterPath = value; OnPropertyChanged(); }
    }

    public List<SeasonItem> Seasons { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }


  public class SeasonItem
  {
    public int Number { get; set; }
    public List<EpisodeItem> Episodes { get; set; } = new();
  }

  public class EpisodeItem
  {
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string FilePath { get; set; } = "";
  }
}