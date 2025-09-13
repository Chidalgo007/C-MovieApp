using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

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
          Margin = new Thickness(0, 10, 0, 5)
        };
        SeasonsPanel.Children.Add(seasonHeader);

        var episodePanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        foreach (var ep in season.Episodes)
        {
          var btn = new Button
          {
            Content = $"Episode {ep.Number}: {ep.Title}",
            Tag = ep.FilePath,
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
          Process.Start("vlc.exe", $"\"{path}\"");
        }
        catch
        {
          MessageBox.Show("Could not open VLC. Make sure it's installed and in PATH.");
        }
      }
    }
  }
}
