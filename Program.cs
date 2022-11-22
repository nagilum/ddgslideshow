using Microsoft.Playwright;
using System.Timers;

using Timer = System.Timers.Timer;

namespace ddgslideshow
{
    internal static class Program
    {
        /// <summary>
        /// HTTP Client for all requests.
        /// </summary>
        private static HttpClient Client { get; set; } = new();

        /// <summary>
        /// List of image URLs fetched from the DDG search.
        /// </summary>
        private static List<ImageEntity> ImageEntities { get; set; } = new();

        /// <summary>
        /// Slideshow index.
        /// </summary>
        private static int SlideshowIndex { get; set; } = -1;

        /// <summary>
        /// Picture box for slideshow.
        /// </summary>
        private static PictureBox SlideshowPictureBox { get; set; } = null!;

        /// <summary>
        /// Slideshow timer.
        /// </summary>
        private static Timer SlideshowTimer { get; set; } = null!;

        /// <summary>
        /// Main window.
        /// </summary>
        private static Form Window { get; set; } = null!;

        /// <summary>
        /// Init all the things..
        /// </summary>
        [STAThread]
        private static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                MessageBox.Show(
                    "You have to specify search terms as parameters.",
                    "Missing Parameters",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            try
            {
                await QueryForImages(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred while querying DuckDuckGo for images.\r\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            // Initiate the app and add the main window.
            ApplicationConfiguration.Initialize();
            SetupMainWindow(args);

            Cursor.Hide();
            Application.Run(Window);
        }

        /// <summary>
        /// Handle user input.
        /// </summary>
        private static void WindowOnKeyUp(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    Application.Exit();
                    break;
            }
        }

        /// <summary>
        /// Setup the main window and its events.
        /// </summary>
        private static void SetupMainWindow(string[] searchTerms)
        {
            Window = new Form
            {
                BackColor = Color.Black,
                FormBorderStyle = FormBorderStyle.None,
                Text = $"[{string.Join(", ", searchTerms)}] DuckDuckGo Slideshow",
                WindowState = FormWindowState.Maximized
            };

            Window.KeyUp += WindowOnKeyUp;

            // Add picture box.
            SlideshowPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            Window.Controls.Add(SlideshowPictureBox);

            // Add timer.
            SlideshowTimer = new Timer
            {
                AutoReset = true,
                Enabled = true,
                Interval = 2500
            };

            SlideshowTimer.Elapsed += TimerElapsed;
        }

        /// <summary>
        /// Display a new image.
        /// </summary>
        private static void TimerElapsed(object? sender, ElapsedEventArgs e)
        {
            // Get the next index.
            var random = new Random();

            while (true)
            {
                SlideshowIndex = random.Next(ImageEntities.Count);

                if (ImageEntities[SlideshowIndex].Image != null)
                {
                    break;
                }
            }

            // Load image.
            SlideshowPictureBox.Image = ImageEntities[SlideshowIndex].Image;
        }

        /// <summary>
        /// Cycle the image entities and download them on a delay.
        /// </summary>
        private static async Task DownloadImages()
        {
            if (ImageEntities.Count == 0)
            {
                return;
            }

            foreach (var entity in ImageEntities)
            {
                try
                {
                    var bytes = await Client.GetByteArrayAsync(entity.Url);
                    entity.Image = Image.FromStream(new MemoryStream(bytes));
                }
                catch
                {
                    //
                }

                await Task.Delay(500);
            }
        }

        /// <summary>
        /// Query DuckDuckGo for images using those search terms.
        /// </summary>
        /// <param name="searchTerms">Terms to search for.</param>
        private static async Task QueryForImages(string[] searchTerms)
        {
            //var url = $"https://duckduckgo.com/?t=ffab&q={string.Join("+", searchTerms)}&atb=v348-1&iax=images&ia=images";
            var url = $"https://duckduckgo.com/?t=ffab&q={string.Join("+", searchTerms)}&atb=v348-1&iax=images&ia=images&pn=6&iaf=size%3AWallpaper";

            // Set up Playwright.
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();

            var page = await browser.NewPageAsync();

            await page.GotoAsync(url);
            await page.WaitForSelectorAsync("img.tile--img__img");

            // Cycle all image elements.
            var elements = await page.QuerySelectorAllAsync("img.tile--img__img");

            foreach (var element in elements)
            {
                var span = await page.EvaluateHandleAsync("(element) => element.parentNode", element) as IElementHandle;
                var div = await page.EvaluateHandleAsync("(element) => element.parentNode", span) as IElementHandle;

                if (await page.EvaluateHandleAsync("(element) => element.parentNode", div) is not IElementHandle wrapper)
                {
                    continue;
                }

                await wrapper.ClickAsync();
                await page.WaitForSelectorAsync("img.detail__media__img-highres");

                var images = await page.QuerySelectorAllAsync("img.detail__media__img-highres");

                foreach (var image in images)
                {
                    try
                    {
                        var src = await image.GetAttributeAsync("src");

                        if (src == null)
                        {
                            continue;
                        }

                        if (src.StartsWith("//"))
                        {
                            src = "https:" + src;
                        }

                        if (!ImageEntities.Any(n => n.Url == src))
                        {
                            ImageEntities.Add(new(src));
                        }
                    }
                    catch
                    {
                        //
                    }
                }
            }

            _ = Task.Run(DownloadImages);
        }
    }

    internal class ImageEntity
    {
        /// <summary>
        /// URL to download.
        /// </summary>
        public string Url { get; init; }

        /// <summary>
        /// Image.
        /// </summary>
        public Image? Image { get; set; }

        /// <summary>
        /// Create new entity.
        /// </summary>
        /// <param name="url">URL to download.</param>
        public ImageEntity(string url)
        {
            this.Url = url;
        }
    }
}