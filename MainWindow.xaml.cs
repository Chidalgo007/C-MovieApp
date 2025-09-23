using Ookii.Dialogs.Wpf;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Windows.Media.Animation;



namespace MovieLibrary
{
    public partial class MainWindow : Window
    {
        // Movies, Series, Anime
        private ObservableCollection<MovieItem> Movies { get; set; } = new();
        private ObservableCollection<SeriesItem> Series { get; set; } = new();
        private ObservableCollection<SeriesItem> Anime { get; set; } = new();
        private ObservableCollection<MovieItem> AllMovies { get; set; } = new(); // Keep all movies here

        // Track folders
        private List<string> MovieFolders = new();
        private List<string> SeriesFolders = new();
        private List<string> AnimeFolders = new();
        private string currentSearchText = "";
        private int totalMoviesToLoad = 0;
        private int moviesLoaded = 0;

        public MainWindow()
        {
            InitializeComponent();
            MovieGrid.ItemsSource = Movies;
            SeriesGrid.ItemsSource = Series;
            AnimeGrid.ItemsSource = Anime;
            _ = LoadAppData();
        }

        // ---------------- Movies ----------------
        private async void AddMoviesFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = SelectFolder();
            if (!string.IsNullOrEmpty(folder) && !MovieFolders.Contains(folder))
            {
                MovieFolders.Add(folder);
                await LoadFromFoldersAsync(MovieFolders, Movies);
            }
        }

        private async void RescanMovies_Click(object sender, RoutedEventArgs e)
        {
            await LoadFromFoldersAsync(MovieFolders, Movies);
        }

        // Clear Movies button - add AllMovies.Clear()
        private void ClearMovies_Click(object sender, RoutedEventArgs e)
        {
            MovieFolders.Clear();
            Movies.Clear();
            AllMovies.Clear(); // This was missing!

            // Clear the poster memory cache as well
            PosterService.ClearMemoryCache();

            // Update the count display
            UpdateMovieCount("All", 0);
        }

        // search functionality - new method
        private void MovieSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox searchBox)
            {
                currentSearchText = searchBox.Text?.Trim() ?? "";
                ApplyCurrentFilter(); // Reapply filter with search
            }
        }

        private void MovieFilter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyCurrentFilter(); // This should only filter, not rescan
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

        // Clear Series button
        private void ClearSeries_Click(object sender, RoutedEventArgs e)
        {
            SeriesFolders.Clear();
            Series.Clear();
            // Clear poster cache for series too
            PosterService.ClearMemoryCache();
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

        // Clear Anime button
        private void ClearAnime_Click(object sender, RoutedEventArgs e)
        {
            AnimeFolders.Clear();
            Anime.Clear();
            // Clear poster cache for anime too  
            PosterService.ClearMemoryCache();
        }

        // ---------------- Helpers ----------------
        private string SelectFolder()
        {
            var dialog = new VistaFolderBrowserDialog();
            return dialog.ShowDialog() == true ? dialog.SelectedPath : string.Empty;
        }

        private void ShowLoadingOverlay()
        {
            moviesLoaded = 0; // Reset counter when starting
            if (LoadingOverlay != null)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                // Start rotation animation if you have one
                if (LoadingSpinner != null && LoadingSpinner.FindResource("SpinAnimation") is Storyboard spinStoryboard)
                {
                    spinStoryboard.Begin();
                }
            }
        }

        private void HideLoadingOverlay()
        {
            if (LoadingOverlay != null)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                // Stop rotation animation
                if (LoadingSpinner != null && LoadingSpinner.FindResource("SpinAnimation") is Storyboard spinStoryboard)
                {
                    spinStoryboard.Stop();
                }
                // Clear the progress text when hiding
                if (LoadingProgressText != null)
                {
                    LoadingProgressText.Text = "";
                }
            }
        }

        private async Task LoadFromFoldersAsync(List<string> folders, ObservableCollection<MovieItem> target)
        {
            ShowLoadingOverlay();

            try
            {
                AllMovies.Clear();
                HashSet<string> seen = new();
                List<Task> metadataTasks = new();

                // First pass: count total movies
                totalMoviesToLoad = 0;
                foreach (var folder in folders)
                {
                    if (!Directory.Exists(folder)) continue;
                    var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                    totalMoviesToLoad += files.Count(f => f.EndsWith(".mp4") || f.EndsWith(".mkv") || f.EndsWith(".avi"));
                }

                // Update initial progress
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (LoadingProgressText != null)
                    {
                        LoadingProgressText.Text = $"Found {totalMoviesToLoad} movies to load...";
                    }
                });

                foreach (var folder in folders)
                {
                    if (!Directory.Exists(folder)) continue;
                    var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        if (!file.EndsWith(".mp4") && !file.EndsWith(".mkv") && !file.EndsWith(".avi"))
                            continue;

                        string rawName = Path.GetFileNameWithoutExtension(file);
                        string cleanTitle = CleanTitle(rawName, out int? year);

                        if (!seen.Add(cleanTitle))
                            continue;

                        var movie = new MovieItem
                        {
                            Title = cleanTitle,
                            FilePath = file,
                            Year = year
                        };

                        AllMovies.Add(movie);

                        var task = Task.Run(async () =>
                        {
                            string poster = await PosterService.GetPosterAsync(cleanTitle, year);
                            var metadata = await FetchMovieMetadataAsync(cleanTitle, year);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                movie.PosterPath = poster;
                                movie.GenreIds = metadata.GenreIds;
                                movie.CountryCode = metadata.CountryCode;
                                movie.IsMovie = metadata.IsMovie;

                                // Update progress counter
                                moviesLoaded++;
                                if (LoadingProgressText != null)
                                {
                                    LoadingProgressText.Text = $"Loading... {moviesLoaded} of {totalMoviesToLoad}";
                                }
                            });
                        });

                        metadataTasks.Add(task);
                    }
                }

                await Task.WhenAll(metadataTasks);
                ApplyCurrentFilter();
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        // Update ApplyCurrentFilter to handle search
        private void ApplyCurrentFilter()
        {
            if (MovieFilter.SelectedItem is ComboBoxItem selected && selected.Content != null)
            {
                string filter = selected.Content.ToString() ?? "All";

                var filtered = AllMovies.Where(m =>
                {
                    // First apply category filter
                    bool categoryMatch = filter switch
                    {
                        "All" => true,
                        "Kids" => m.GenreIds.Any(id => new[] { 10751, 16 }.Contains(id)),
                        "Horror" => m.GenreIds.Any(id => id == 27),
                        "Asian" => new[] { "JP", "KR", "CN", "HK", "TW" }.Contains(m.CountryCode),
                        "Movies" => m.IsMovie &&
                                   !m.GenreIds.Any(id => new[] { 10751, 16, 27 }.Contains(id)) && // Not kids or horror
                                   !new[] { "JP", "KR", "CN", "HK", "TW" }.Contains(m.CountryCode), // Not Asian
                        _ => true
                    };

                    // Then apply search filter if there's search text
                    bool searchMatch = string.IsNullOrEmpty(currentSearchText) ||
                                     m.Title.Contains(currentSearchText, StringComparison.OrdinalIgnoreCase);

                    return categoryMatch && searchMatch;
                }).ToList();

                // Clear and repopulate the display collection
                Movies.Clear();
                foreach (var item in filtered)
                    Movies.Add(item);

                // Update the count with the filtered results
                UpdateMovieCount(filter, filtered.Count);
            }
        }

        // Update the UpdateMovieCount method to show current filter and count
        private void UpdateMovieCount(string currentFilter = "All", int? filteredCount = null)
        {
            if (MovieCountLabel == null)
                return;

            int count = filteredCount ?? Movies.Count;
            int totalCount = AllMovies.Count(m => m.IsMovie);

            string filterText = currentFilter switch
            {
                "All" => "All Movies",
                "Kids" => "Kids Movies",
                "Horror" => "Horror Movies",
                "Asian" => "Asian Movies",
                "Movies" => "Regular Movies",
                _ => "Movies"
            };

            // Add search indicator if searching
            string searchIndicator = !string.IsNullOrEmpty(currentSearchText) ? $" (searching: '{currentSearchText}')" : "";

            MovieCountLabel.Text = $"{filterText}: {count} / {totalCount}{searchIndicator}";
        }

        // FetchMovieMetadataAsync method with this one
        private async Task<(List<int> GenreIds, string CountryCode, bool IsMovie)> FetchMovieMetadataAsync(string title, int? year)
        {
            try
            {
                // Use the same API key as PosterService
                string apiKey = "653f5b1225301539044cae7a67f8234e";

                // Search for the movie
                string searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={Uri.EscapeDataString(title)}";
                if (year != null)
                    searchUrl += $"&year={year}";

                using var http = new HttpClient();
                string searchJson = await http.GetStringAsync(searchUrl);
                var searchResults = JObject.Parse(searchJson)["results"] as JArray;

                if (searchResults != null && searchResults.Count > 0)
                {
                    var movie = searchResults[0];
                    int movieId = movie["id"]?.Value<int>() ?? 0;

                    if (movieId > 0)
                    {
                        // Get detailed movie info including production countries
                        string detailUrl = $"https://api.themoviedb.org/3/movie/{movieId}?api_key={apiKey}";
                        string detailJson = await http.GetStringAsync(detailUrl);
                        var movieDetails = JObject.Parse(detailJson);

                        // Extract genre IDs
                        var genreIds = new List<int>();
                        var genres = movieDetails["genres"] as JArray;
                        if (genres != null)
                        {
                            foreach (var genre in genres)
                            {
                                int genreId = genre["id"]?.Value<int>() ?? 0;
                                if (genreId > 0)
                                    genreIds.Add(genreId);
                            }
                        }

                        // Extract country code (use first production country)
                        string countryCode = "US"; // default
                        var countries = movieDetails["production_countries"] as JArray;
                        if (countries != null && countries.Count > 0)
                        {
                            countryCode = countries[0]["iso_3166_1"]?.ToString() ?? "US";
                        }

                        // It's a movie (not a TV series)
                        bool isMovie = true;

                        return (genreIds, countryCode, isMovie);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error or handle it as needed
                System.Diagnostics.Debug.WriteLine($"Error fetching metadata for {title}: {ex.Message}");
            }

            // Fallback: return default values
            return (new List<int>(), "US", true);
        }

        private void Poster_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is MovieItem movie)
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
                            Arguments = $"--fullscreen \"{movie.FilePath}\"",
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
        private readonly string AppDataPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MovieLibrary", "appdata.json");

        private async Task LoadAppData()
        {
            try
            {
                if (File.Exists(AppDataPath))
                {
                    var json = File.ReadAllText(AppDataPath);
                    var data = JsonConvert.DeserializeObject<AppData>(json);

                    if (data != null)
                    {
                        MovieFolders = data.MovieFolders ?? new List<string>(); // Add null check
                        SeriesFolders = data.SeriesFolders ?? new List<string>(); // Add null check
                        AnimeFolders = data.AnimeFolders ?? new List<string>(); // Add null check

                        // Load movies from saved folders
                        if (MovieFolders.Any())
                        {
                            await LoadFromFoldersAsync(MovieFolders, Movies);
                        }
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
            try
            {
                // Step 1: Replace separators
                string title = rawName.Replace('.', ' ')
                                      .Replace('_', ' ')
                                      .Replace('-', ' ')
                                      .Trim();

                // Step 2: Extract year (1900–2099)
                var yearMatch = System.Text.RegularExpressions.Regex.Match(title, @"\b(19|20)\d{2}\b");
                if (yearMatch.Success)
                {
                    year = int.Parse(yearMatch.Value);
                    // Remove year from title (poster fetch will handle it separately)
                    title = title.Replace(yearMatch.Value, "");
                }

                // Step 3: Remove common junk tokens
                string[] junkTokens = {
    "1080p","720p","2160p","4k","x264","x265","h264","h265",
    "bluray","brrip","bdrip","dvdrip","webrip","web-dl","hdrip",
    "aac","dts","ac3","dd5.1","atmos",
    "yify","rarbg","amzn","hd","fhd","uhd","hdr",
    "extended","unrated","remastered","directors.cut",
    "multi","dual","dubbed","sub","vostfr",
    "repack","proper","limited","uncut","internal",
    "DC","RARBG", "ETRG", "Ganool", "YTS", "EVO", "FGT", "MkvCage", "MkvCinemas",
    "BluRay", "BRRip", "WEBRip", "WEB-DL", "HDRip", "DVDRip", "HDTV",
    "XviD", "DivX", "HDCAM", "CAM", "TS", "TC", "SCR", "DVDScr",
    "HDR", "UHD", "HEVC", "H.265", "H.264",
    "DD5.1", "DTS-HD", "DTS", "Atmos", "AC3",
    "Subs", "Dubbed", "Dual Audio", "VOSTFR", "MULTI",
    "Director's Cut", "Extended Edition", "Unrated", "Remastered",
    "WEB", "HD", "FHD", "10bit", "AAC","AAC5.1","AAC2.0","AAC5 1"
};

                foreach (var token in junkTokens)
                {
                    title = System.Text.RegularExpressions.Regex.Replace(title,
                        @"\b" + token + @"\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }

                // Step 4: Remove brackets and parentheses content
                title = System.Text.RegularExpressions.Regex.Replace(title, @"\[.*?\]", "");
                title = System.Text.RegularExpressions.Regex.Replace(title, @"\(.*?\)", "");

                // Step 5: Collapse multiple spaces
                title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+", " ").Trim();

                return title;
            }
            catch
            {
                // Fallback simple cleaning
                var fallback = rawName.Replace('.', ' ')
                                      .Replace('_', ' ')
                                      .Replace('-', ' ');
                fallback = System.Text.RegularExpressions.Regex.Replace(fallback, @"\s+", " ").Trim();
                return fallback;
            }
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
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Method to manually replace a poster
        private void ReplacePoster_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is MovieItem movie)
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select New Poster Image",
                    Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
                    FilterIndex = 1
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        // Create a safe filename for the custom poster
                        string safeName = string.Join("_", movie.Title.Split(Path.GetInvalidFileNameChars()));
                        if (movie.Year != null)
                            safeName += $"_{movie.Year}";

                        string customPosterPath = Path.Combine(
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MovieLibrary", "Posters"),
                            safeName + "_custom.jpg"
                        );

                        // Copy the selected image to our poster cache folder
                        File.Copy(openFileDialog.FileName, customPosterPath, true);

                        // Update the movie's poster path
                        movie.PosterPath = customPosterPath;

                        // Update the memory cache
                        string cacheKey = movie.Year != null ? $"{movie.Title}_{movie.Year}" : movie.Title;
                        PosterService.UpdateMemoryCache(cacheKey, customPosterPath);

                        MessageBox.Show("Poster updated successfully!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to update poster: {ex.Message}");
                    }
                }
            }
        }

        // Method to reset poster to original API version
        private void ResetPoster_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is MovieItem movie)
            {
                try
                {
                    // Remove custom poster file if it exists
                    string safeName = string.Join("_", movie.Title.Split(Path.GetInvalidFileNameChars()));
                    if (movie.Year != null)
                        safeName += $"_{movie.Year}";

                    string customPosterPath = Path.Combine(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MovieLibrary", "Posters"),
                        safeName + "_custom.jpg"
                    );

                    if (File.Exists(customPosterPath))
                        File.Delete(customPosterPath);

                    // Clear from memory cache
                    string cacheKey = movie.Year != null ? $"{movie.Title}_{movie.Year}" : movie.Title;
                    PosterService.ClearMemoryCacheItem(cacheKey);

                    // Re-fetch from API
                    Task.Run(async () =>
                    {
                        string newPoster = await PosterService.GetPosterAsync(movie.Title, movie.Year);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            movie.PosterPath = newPoster;
                        });
                    });

                    MessageBox.Show("Poster reset to original!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to reset poster: {ex.Message}");
                }
            }
        }

    }

    public class AppData
    {
        public List<string> MovieFolders { get; set; } = new();
        public List<string> SeriesFolders { get; set; } = new();
        public List<string> AnimeFolders { get; set; } = new();
    }

}
