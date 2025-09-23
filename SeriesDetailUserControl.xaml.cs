using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.IO;

namespace MovieLibrary
{
  public partial class SeriesDetailUserControl : UserControl
  {
    private string _seriesTitle;
    private readonly List<Button> _episodeButtons = new List<Button>();

    public SeriesDetailUserControl(SeriesItem series)
    {
      InitializeComponent();
      _seriesTitle = series.Title;

      PosterImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(series.PosterPath));

      var progress = WatchProgressManager.GetProgress(_seriesTitle);

      foreach (var season in series.Seasons)
      {
        var seasonHeader = new TextBlock
        {
          Text = $"Season {season.Number}",
          FontWeight = FontWeights.Bold,
          FontSize = 16,
          Foreground = System.Windows.Media.Brushes.White,
          TextAlignment = TextAlignment.Center,
          HorizontalAlignment = HorizontalAlignment.Center,
          Margin = new Thickness(0, 10, 0, 5)
        };
        SeasonsPanel.Children.Add(seasonHeader);

        var episodePanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        foreach (var ep in season.Episodes.OrderBy(ep => ep.Number))
        {
          var btn = new Button
          {
            Content = string.IsNullOrEmpty(ep.Title) ? $"Episode {ep.Number}" : ep.Title,
            Tag = new EpisodeTag { Path = ep.FilePath, Season = season.Number, Episode = ep.Number },
            Style = (Style)FindResource("EpisodeButtonStyle"),
            Margin = new Thickness(0, 2, 0, 2)
          };

          // highlight last watched
          if (progress != null && progress.Season == season.Number && progress.Episode == ep.Number)
          {
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CF5102"));
          }


          btn.Click += Episode_Click;
          episodePanel.Children.Add(btn);
          _episodeButtons.Add(btn);
        }
        SeasonsPanel.Children.Add(episodePanel);
      }
    }

    private void Episode_Click(object sender, RoutedEventArgs e)
    {
      if (sender is Button btn && btn.Tag is EpisodeTag tag)
      {
        try
        {
          string vlcPath = @"C:\Program Files\VideoLAN\VLC\vlc.exe";

          if (!File.Exists(vlcPath))
            vlcPath = @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe";

          if (File.Exists(vlcPath))
          {
            WatchProgressManager.SaveProgress(_seriesTitle, tag.Season, tag.Episode);

            // Update button highlights immediately
            foreach (var b in _episodeButtons)
            {
              b.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333")); // default
            }
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CF5102")); // last watched


            Process.Start(new ProcessStartInfo
            {
              FileName = vlcPath,
              Arguments = $"--fullscreen \"{tag.Path}\"",
              UseShellExecute = true
            });
          }
          else
          {
            MessageBox.Show("VLC not found. Please install it or add it to PATH.");
          }
        }
        catch
        {
          MessageBox.Show("Could not open VLC. Make sure it's installed and in PATH.");
        }
      }
    }
  }
}
public class EpisodeTag
{
  public string Path { get; set; } = string.Empty;
  public int Season { get; set; }
  public int Episode { get; set; }
}