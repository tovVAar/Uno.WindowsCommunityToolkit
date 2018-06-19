// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.SampleApp.Common;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Newtonsoft.Json;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.System.Profile;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Microsoft.Toolkit.Uwp.SampleApp.Pages
{
    public sealed partial class About : INotifyPropertyChanged
    {
        private Compositor _compositor;

        private IEnumerable<Sample> _recentSamples;

        public IEnumerable<Sample> RecentSamples
        {
            get
            {
                return _recentSamples;
            }

            set
            {
                _recentSamples = value;
                OnPropertyChanged();
            }
        }

        private static List<Sample> _newSamples;

        public List<Sample> NewSamples
        {
            get
            {
                return _newSamples;
            }

            set
            {
                _newSamples = value;
                OnPropertyChanged();
            }
        }

        private List<GitHubRelease> _githubReleases;

        public List<GitHubRelease> GitHubReleases
        {
            get
            {
                return _githubReleases;
            }

            set
            {
                _githubReleases = value;
                OnPropertyChanged();
            }
        }

        private static LandingPageLinks _landingPageLinks;

        public LandingPageLinks LandingPageLinks
        {
            get
            {
                return _landingPageLinks;
            }

            set
            {
                _landingPageLinks = value;
                OnPropertyChanged();
            }
        }

        public About()
        {
            InitializeComponent();
        }

        public static Visibility VisibleIfCollectionEmpty(IEnumerable<Sample> collection)
        {
            return collection != null && collection.Count() > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

			Console.WriteLine("-> About: OnNavigatedTo");

            Shell.Current.ShowOnlyHeader("About");

#if NETFX_CORE // UNO TODO
            var packageVersion = Package.Current.Id.Version;
            Version.Text = $"Version {packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}";
#else
			Version.Text = $"Version not implemented";
#endif

            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

            var t = Init();

            Windows.UI.Xaml.Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;

			Console.WriteLine("<- About: OnNavigatedTo");
		}

		protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            Windows.UI.Xaml.Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
        }

        private void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            var keyChar = (char)args.VirtualKey;
            if (char.IsLetterOrDigit(keyChar))
            {
                var t = Shell.Current.StartSearch(keyChar.ToString());
            }
        }

        private async Task Init()
        {
			try
			{
				var loadDataTask = UpdateSections();
				var recentSamplesTask = Samples.GetRecentSamples();
				var gitHubTask = Data.GitHub.GetPublishedReleases();

				await Task.WhenAll(loadDataTask, recentSamplesTask, gitHubTask);

				RecentSamples = recentSamplesTask.Result;
				GitHubReleases = gitHubTask.Result;

				var counter = 1;
				var delay = 70;

				foreach (var child in InnerGrid.Children)
				{
					if (child is ItemsControl itemsControl)
					{
						foreach (var childOfChild in itemsControl.Items)
						{
							Implicit.GetShowAnimations((UIElement)childOfChild).Add(new OpacityAnimation()
							{
								From = 0,
								To = 1,
								Duration = TimeSpan.FromMilliseconds(300),
								Delay = TimeSpan.FromMilliseconds(counter++ * delay),
								SetInitialValueBeforeDelay = true
							});
						}
					}
					else
					{
#if NETFX_CORE
                   Implicit.GetShowAnimations(child).Add(new OpacityAnimation()
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(300),
                        Delay = TimeSpan.FromMilliseconds(counter++ * delay),
                        SetInitialValueBeforeDelay = true
                    });
#endif
					}
				}
				

				Root.Visibility = Visibility.Visible;
			}
			catch(Exception e)
			{
				Console.WriteLine($"About init failed: {e}");
			}
        }

        private void RecentSample_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as HyperlinkButton;
            if (button.DataContext is Sample sample)
            {
#if NETFX_CORE // UNO TODO
               TrackingManager.TrackEvent("LandingPageRecentClick", sample.Name);
#endif
				Shell.Current.NavigateToSample(sample);
            }
        }

        private void NewSample_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as HyperlinkButton;
            if (button.DataContext is Sample sample)
            {
#if NETFX_CORE // UNO TODO
                TrackingManager.TrackEvent("LandingPageNewClick", sample.Name);
#endif

				Shell.Current.NavigateToSample(sample);
            }
        }

        private void ReleaseNotes_Click(object sender, RoutedEventArgs e)
        {
#if NETFX_CORE // UNO TODO
            var button = sender as HyperlinkButton;
            if (button.DataContext is GitHubRelease release)
            {
                TrackingManager.TrackEvent("LandingPageReleaseClick", release.Name);
            }
#endif
		}

		private void Link_Clicked(object sender, RoutedEventArgs e)
        {
#if NETFX_CORE // UNO TODO
           var button = sender as HyperlinkButton;
            if (button.Content is TextBlock textBlock)
            {
                TrackingManager.TrackEvent("LandingPageLinkClick", textBlock.Text);
            }
#endif
		}

		private async Task UpdateSections()
        {
            if (LandingPageLinks == null)
            {
				var manifestName = typeof(Samples).Assembly
					.GetManifestResourceNames()
					.FirstOrDefault(n => n.EndsWith("landingPageLinks.json", StringComparison.OrdinalIgnoreCase));

				if (manifestName == null)
				{
					throw new InvalidOperationException($"Failed to find resource");
				}

				using (var jsonStream = typeof(Samples).Assembly.GetManifestResourceStream(manifestName))
				{
					var jsonString = await jsonStream.ReadTextAsync();
                    LandingPageLinks = JsonConvert.DeserializeObject<LandingPageLinks>(jsonString);
                }

                var samples = new List<Sample>();

                foreach (var newSample in LandingPageLinks.NewSamples)
                {
                    var sample = await Samples.GetSampleByName(newSample);
                    if (sample != null)
                    {
                        samples.Add(sample);
                    }
                }

                NewSamples = samples;
            }

            foreach (var section in LandingPageLinks.Resources.Reverse())
            {
                var stackPanel = new StackPanel()
                {
                    MinWidth = 267,
                    Margin = new Thickness(0, 0, 0, 48)
                };

                stackPanel.Children.Add(
                    new TextBlock()
                    {
                        FontSize = 20,
                        FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe UI"),
                        Text = section.Title
                    });

                stackPanel.Children.Add(
                    new ItemsControl()
                    {
                        Margin = new Thickness(0, 16, 0, 0),
                        ItemsSource = section.Links,
#if HAS_UNO
						ItemTemplate = StaticResources.LinkTemplate
#else
						ItemTemplate = Resources["LinkTemplate"] as DataTemplate
#endif
					});

                ResourcesSection.Items.Insert(0, stackPanel);
            }
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called when [property changed].
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var background = new Image()
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Photos/HERO.jpg")),
                Stretch = Windows.UI.Xaml.Media.Stretch.UniformToFill
            };

            if (ApiInformation.IsTypePresent("Windows.UI.Xaml.Controls.ParallaxView"))
            {
                var parallaxView = new ParallaxView()
                {
                    Source = Scroller,
                    VerticalShift = 50,
#if NETFX_CORE
					Child = background
#endif
                };

                BackgroundBorder.Child = parallaxView;
            }
            else
            {
                BackgroundBorder.Child = background;
            }
        }
    }
}
