using Ookii.Dialogs.Wpf;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;



namespace MovieLibrary
{
    public partial class MainWindow : Window
    {
        // Movies, Series, Anime
        private ObservableCollection<MovieItem> Movies { get; set; } = new();
        private ObservableCollection<SeriesItem> Series { get; set; } = new();
        private ObservableCollection<SeriesItem> Anime { get; set; } = new();

        // Track folders
        private List<string> MovieFolders = new();
        private List<string> SeriesFolders = new();
        private List<string> AnimeFolders = new();

        public MainWindow()
        {
            InitializeComponent();
            MovieGrid.ItemsSource = Movies;
            SeriesGrid.ItemsSource = Series;
            AnimeGrid.ItemsSource = Anime;
            LoadAppData();
        }

        // ---------------- Movies ----------------
        private void AddMoviesFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = SelectFolder();
            if (!string.IsNullOrEmpty(folder) && !MovieFolders.Contains(folder))
            {
                MovieFolders.Add(folder);
                LoadFromFolders(MovieFolders, Movies);
            }
        }

        private void RescanMovies_Click(object sender, RoutedEventArgs e)
        {
            LoadFromFolders(MovieFolders, Movies);
        }

        private void ClearMovies_Click(object sender, RoutedEventArgs e)
        {
            MovieFolders.Clear();
            Movies.Clear();
        }

        private void MovieFilter_Changed(object sender, RoutedEventArgs e)
        {
            LoadFromFolders(MovieFolders, Movies); // Reload with filter
        }

        // ---------------- Series ----------------
        private void AddSeriesFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = SelectFolder();
            if (!string.IsNullOrEmpty(folder) && !SeriesFolders.Contains(folder))
            {
                SeriesFolders.Add(folder);
                LoadSeriesFromFolders(SeriesFolders, Series);
            }
        }

        private void RescanSeries_Click(object sender, RoutedEventArgs e) =>
            LoadSeriesFromFolders(SeriesFolders, Series);

        private void ClearSeries_Click(object sender, RoutedEventArgs e)
        {
            SeriesFolders.Clear();
            Series.Clear();
        }

        // ---------------- Anime ----------------
        private void AddAnimeFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = SelectFolder();
            if (!string.IsNullOrEmpty(folder) && !AnimeFolders.Contains(folder))
            {
                AnimeFolders.Add(folder);
                LoadSeriesFromFolders(AnimeFolders, Anime);
            }
        }

        private void RescanAnime_Click(object sender, RoutedEventArgs e) =>
            LoadSeriesFromFolders(AnimeFolders, Anime);

        private void ClearAnime_Click(object sender, RoutedEventArgs e)
        {
            AnimeFolders.Clear();
            Anime.Clear();
        }

        // ---------------- Helpers ----------------
        private string SelectFolder()
        {
            var dialog = new VistaFolderBrowserDialog();
            return dialog.ShowDialog() == true ? dialog.SelectedPath : string.Empty;
        }

        private void LoadFromFolders(List<string> folders, ObservableCollection<MovieItem> target)
        {
            target.Clear();
            HashSet<string> seen = new();

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (file.EndsWith(".mp4") || file.EndsWith(".mkv") || file.EndsWith(".avi"))
                    {
                        string rawName = Path.GetFileNameWithoutExtension(file);
                        string cleanTitle = CleanTitle(rawName, out int? year);

                        if (seen.Add(cleanTitle))
                        {
                            var movie = new MovieItem
                            {
                                Title = cleanTitle,
                                FilePath = file,
                                Year = year
                            };

                            target.Add(movie);

                            // fetch poster async
                            Task.Run(async () =>
                            {
                                string poster = await PosterService.GetPosterAsync(cleanTitle, year);
                                Application.Current.Dispatcher.Invoke(() => movie.PosterPath = poster);
                            });
                        }
                    }
                }
            }
        }

        private bool PassesFilter(string title)
        {
            string filter = ((MovieFilter.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content as string) ?? "All";
            if (filter == "All") return true;
            if (filter == "Kids" && title.ToLower().Contains("kids")) return true;
            if (filter == "Horror" && title.ToLower().Contains("horror")) return true;
            if (filter == "Asian" && title.ToLower().Contains("asian")) return true;
            if (filter == "Movies" && !(title.ToLower().Contains("kids") || title.ToLower().Contains("horror"))) return true;
            return false;
        }

        private void Poster_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is MovieItem movie)
            {
                try
                {
                    Process.Start("vlc.exe", $"\"{movie.FilePath}\"");
                }
                catch
                {
                    MessageBox.Show("Could not open VLC. Make sure it's installed and in PATH.");
                }
            }
        }
        private readonly string AppDataPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MovieLibrary", "appdata.json");

        private void LoadAppData()
        {
            try
            {
                if (File.Exists(AppDataPath))
                {
                    var json = File.ReadAllText(AppDataPath);
                    var data = JsonConvert.DeserializeObject<AppData>(json);

                    if (data != null)
                    {
                        MovieFolders = data.MovieFolders;
                        SeriesFolders = data.SeriesFolders;
                        AnimeFolders = data.AnimeFolders;

                        // Load movies from saved folders
                        LoadFromFolders(MovieFolders, Movies);
                        LoadSeriesFromFolders(SeriesFolders, Series);
                        LoadSeriesFromFolders(AnimeFolders, Anime);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load app data: " + ex.Message);
            }
        }

        private void SaveAppData()
        {
            try
            {
                var data = new AppData
                {
                    MovieFolders = MovieFolders,
                    SeriesFolders = SeriesFolders,
                    AnimeFolders = AnimeFolders
                };

                var folder = Path.GetDirectoryName(AppDataPath)!;
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(AppDataPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save app data: " + ex.Message);
            }
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveAppData();
            base.OnClosing(e);
        }

        private string CleanTitle(string rawName, out int? year)
        {
            year = null;

            // 1. Try to extract year (1990–2029 range)
            var match = System.Text.RegularExpressions.Regex.Match(rawName, @"\b(19|20)\d{2}\b");
            if (match.Success)
            {
                year = int.Parse(match.Value);
                rawName = rawName.Replace(match.Value, "");
            }

            // 2. Remove common junk tags
            string[] junkTokens = {
        "1080p", "720p", "2160p", "4k", "x264", "x265", "h264", "h265",
        "bluray", "brrip", "bdrip", "dvdrip", "webrip", "web-dl", "hdrip",
        "aac", "dts", "ac3", "yify", "rarbg", "dc", "extended", "unrated"
    };

            foreach (var token in junkTokens)
                rawName = System.Text.RegularExpressions.Regex.Replace(rawName,
                    @"\b" + token + @"\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // 3. Replace separators with spaces
            rawName = rawName.Replace('.', ' ')
                             .Replace('_', ' ')
                             .Replace('-', ' ');

            // 4. Collapse multiple spaces
            rawName = System.Text.RegularExpressions.Regex.Replace(rawName, @"\s+", " ");

            return rawName.Trim();
        }
        private (int? season, int? episode) ParseSeasonEpisode(string filename)
        {
            // Match patterns like S01E05, s1e5, 1x05
            var match = System.Text.RegularExpressions.Regex.Match(filename,
                @"S(?<season>\d{1,2})E(?<ep>\d{1,2})|(?<season>\d{1,2})x(?<ep>\d{1,2})",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                int season = int.Parse(match.Groups["season"].Value);
                int ep = int.Parse(match.Groups["ep"].Value);
                return (season, ep);
            }

            // Match Episode (xx) or Ep xx
            match = System.Text.RegularExpressions.Regex.Match(filename, @"[Ee]p(?:isode)?\s*\(?(?<ep>\d{1,3})\)?");
            if (match.Success)
                return (1, int.Parse(match.Groups["ep"].Value)); // default season 1

            return (null, null);
        }

        private void LoadSeriesFromFolders(List<string> folders, ObservableCollection<SeriesItem> target)
        {
            target.Clear();
            HashSet<string> seenSeries = new();

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;

                // Each subfolder = a series
                var seriesDirs = Directory.GetDirectories(folder);
                foreach (var seriesDir in seriesDirs)
                {
                    string seriesTitle = Path.GetFileName(seriesDir);
                    if (!seenSeries.Add(seriesTitle)) continue;

                    var seriesItem = new SeriesItem
                    {
                        Title = seriesTitle,
                        FolderPath = seriesDir
                    };

                    // scan seasons
                    var seasonDirs = Directory.GetDirectories(seriesDir);
                    foreach (var seasonDir in seasonDirs)
                    {
                        int seasonNumber = 0;
                        var match = System.Text.RegularExpressions.Regex.Match(seasonDir, @"\d+");
                        if (match.Success) seasonNumber = int.Parse(match.Value);

                        var seasonItem = new SeasonItem { Number = seasonNumber };

                        var files = Directory.GetFiles(seasonDir, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => f.EndsWith(".mp4") || f.EndsWith(".mkv") || f.EndsWith(".avi"));

                        foreach (var file in files)
                        {
                            var epName = Path.GetFileNameWithoutExtension(file);
                            var (_, epNumber) = ParseSeasonEpisode(epName);

                            var episode = new EpisodeItem
                            {
                                Number = epNumber ?? 0,
                                Title = epName,
                                FilePath = file
                            };
                            seasonItem.Episodes.Add(episode);
                        }

                        if (seasonItem.Episodes.Count > 0)
                            seriesItem.Seasons.Add(seasonItem);
                    }

                    target.Add(seriesItem);

                    // Fetch poster async like movies
                    Task.Run(async () =>
                    {
                        string poster = await PosterService.GetPosterAsync(seriesTitle);
                        Application.Current.Dispatcher.Invoke(() => seriesItem.PosterPath = poster);
                    });
                }
            }
        }

        // Series click
        private void SeriesPoster_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is StackPanel panel && panel.DataContext is SeriesItem series)
            {
                SeriesBackButton.Visibility = Visibility.Visible;
                SeriesContentArea.Content = new SeriesDetailUserControl(series);
            }
        }

        // Series back
        private void SeriesBackButton_Click(object sender, RoutedEventArgs e)
        {
            SeriesBackButton.Visibility = Visibility.Collapsed;
            SeriesContentArea.Content = SeriesGrid; // restores grid
        }

        // Anime click
        private void AnimePoster_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is StackPanel panel && panel.DataContext is SeriesItem anime)
            {
                AnimeBackButton.Visibility = Visibility.Visible;
                AnimeContentArea.Content = new SeriesDetailUserControl(anime);
            }
        }

        // Anime back
        private void AnimeBackButton_Click(object sender, RoutedEventArgs e)
        {
            AnimeBackButton.Visibility = Visibility.Collapsed;
            AnimeContentArea.Content = AnimeGrid;
        }

    }

    public class AppData
    {
        public List<string> MovieFolders { get; set; } = new();
        public List<string> SeriesFolders { get; set; } = new();
        public List<string> AnimeFolders { get; set; } = new();
    }

}
