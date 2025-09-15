using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.IO;

namespace MovieLibrary
{
  public partial class SeriesDetailUserControl : UserControl
  {
    public SeriesDetailUserControl(SeriesItem series)
    {
      InitializeComponent();

      PosterImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(series.PosterPath));

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
            Tag = ep.FilePath,
            Style = (Style)FindResource("EpisodeButtonStyle"),
            Margin = new Thickness(0, 2, 0, 2)
          };
          btn.Click += Episode_Click;
          episodePanel.Children.Add(btn);
        }
        SeasonsPanel.Children.Add(episodePanel);
      }
    }

    private void Episode_Click(object sender, RoutedEventArgs e)
    {
      if (sender is Button btn && btn.Tag is string path)
      {
        try
        {
          string vlcPath = @"C:\Program Files\VideoLAN\VLC\vlc.exe";

          if (!File.Exists(vlcPath))
            vlcPath = @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe";

          if (File.Exists(vlcPath))
          {

            Process.Start(new ProcessStartInfo
            {
              FileName = vlcPath,
              Arguments = $"--fullscreen \"{path}\"",
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
