﻿using SimpleWeather.Controls;
using SimpleWeather.Utils;
using SimpleWeather.UWP.Controls;
using SimpleWeather.WeatherData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Geolocation;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238
namespace SimpleWeather.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LocationsPage : Page, IWeatherLoadedListener, IWeatherErrorListener, IDisposable
    {
        private CancellationTokenSource cts = new CancellationTokenSource();

        public ObservableCollection<LocationPanelViewModel> GPSPanelViewModel { get; set; }
        public ObservableCollection<LocationPanelViewModel> LocationPanels { get; set; }
        public ObservableCollection<LocationQueryViewModel> LocationQuerys { get; set; }
        private string selected_query = string.Empty;
        Geolocator geolocal = null;

        public bool EditMode { get; set; } = false;
        private bool DataChanged = false;
        private bool[] ErrorCounter;

        public void OnWeatherLoaded(LocationData location, Weather weather)
        {
            if (weather != null)
            {
                if (Settings.FollowGPS && location.locationType == LocationType.GPS)
                {
                    GPSPanelViewModel.First().SetWeather(weather);
                }
                else
                {
                    LocationPanelViewModel panelView = LocationPanels.First(panelVM => panelVM.LocationData.query == location.query);
                    panelView.SetWeather(weather);
                }
            }
        }

        public void OnWeatherError(WeatherException wEx)
        {
            switch (wEx.ErrorStatus)
            {
                case WeatherUtils.ErrorStatus.NetworkError:
                case WeatherUtils.ErrorStatus.NoWeather:
                    // Show error message and prompt to refresh
                    // Only warn once
                    if (!ErrorCounter[(int)wEx.ErrorStatus])
                    {
                        Snackbar snackBar = Snackbar.Make(Content as Grid, wEx.Message, SnackbarDuration.Long);
                        snackBar.SetAction(App.ResLoader.GetString("Action_Retry"), (sender) =>
                        {
                            RefreshLocations();
                        });
                        snackBar.Show();
                        ErrorCounter[(int)wEx.ErrorStatus] = true;
                    }
                    break;
                default:
                    // Show error message
                    // Only warn once
                    if (!ErrorCounter[(int)wEx.ErrorStatus])
                    {
                        Snackbar.Make(Content as Grid, wEx.Message, SnackbarDuration.Long).Show();
                        ErrorCounter[(int)wEx.ErrorStatus] = true;
                    }
                    break;
            }
        }

        public LocationsPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            Application.Current.Resuming += LocationsPage_Resuming;

            GPSPanelViewModel = new ObservableCollection<LocationPanelViewModel>() { null };
            LocationPanels = new ObservableCollection<LocationPanelViewModel>();
            LocationPanels.CollectionChanged += LocationPanels_CollectionChanged;

            LocationQuerys = new ObservableCollection<LocationQueryViewModel>();

            int max = Enum.GetValues(typeof(WeatherUtils.ErrorStatus)).Cast<int>().Max();
            ErrorCounter = new bool[max];
        }

        private async void LocationsPage_Resuming(object sender, object e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { RefreshLocations(); });
        }

        public void Dispose()
        {
            ((IDisposable)cts).Dispose();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.Back || e.NavigationMode == NavigationMode.New)
            {
                // Remove all from backstack except home
                if (this.Frame.BackStackDepth > 1)
                {
                    var home = this.Frame.BackStack.ElementAt(0);
                    this.Frame.BackStack.Clear();
                    this.Frame.BackStack.Add(home);
                }
            }

            // Shell
            (Shell.Instance.BurgerBackground as SolidColorBrush).Color = App.AppColor;

            if (e.Parameter != null)
            {
                string arg = e.Parameter.ToString();

                switch (arg)
                {
                    case "toast-refresh":
                        RefreshLocations();
                        return;
                    default:
                        break;
                }
            }

            if (Settings.FollowGPS)
                GPSPanel.Visibility = Visibility.Visible;
            else
            {
                GPSPanelViewModel[0] = null;
                GPSPanel.Visibility = Visibility.Collapsed;
            }

            bool reload = (!Settings.FollowGPS && LocationPanels.Count == 0) ||
                (Settings.FollowGPS && GPSPanelViewModel.First() == null);

            if (reload || e.NavigationMode == NavigationMode.New)
            {
                // New instance; Get locations and load up weather data
                LoadLocations();
            }
            else
            {
                // Refresh view
                RefreshLocations();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            // Cancel edit mode if moving away
            if (EditMode)
                ToggleEditMode();

            // Reset error counter
            Array.Clear(ErrorCounter, 0, ErrorCounter.Length);
        }

        private void LocationPanels_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            bool dataMoved = (e.Action == NotifyCollectionChangedAction.Remove) || (e.Action == NotifyCollectionChangedAction.Move);
            bool onlyHomeIsLeft = (LocationPanels.Count == 1);

            // Flag that data has changed
            if (EditMode && dataMoved)
                DataChanged = true;
            
            // Cancel edit Mode
            if (EditMode && onlyHomeIsLeft)
                ToggleEditMode();

            // Disable EditMode if only single location
            EditButton.Visibility = onlyHomeIsLeft ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void LoadLocations()
        {
            // Lets load it up...
            var locations = Settings.LocationData;
            LocationPanels.Clear();

            // Setup saved favorite locations
            await LoadGPSPanel();
            foreach (LocationData location in locations)
            {
                int index = locations.IndexOf(location);

                LocationPanelViewModel panel = new LocationPanelViewModel()
                {
                    // Save index to tag (to easily retreive)
                    LocationData = location
                };

                LocationPanels.Add(panel);
            }

            foreach (LocationData location in locations)
            {
                var wLoader = new WeatherDataLoader(this, this, location);
                await wLoader.LoadWeatherData(false);
            }
        }

        private async Task LoadGPSPanel()
        {
            if (Settings.FollowGPS)
            {
                GPSPanel.Visibility = Visibility.Visible;
                var locData = await Settings.GetLastGPSLocData();

                if (locData == null || locData.query == null)
                {
                    locData = await UpdateLocation();
                }

                if (locData != null && locData.query != null)
                {
                    LocationPanelViewModel panel = new LocationPanelViewModel()
                    {
                        LocationData = locData
                    };
                    GPSPanelViewModel[0] = panel;

                    var wLoader = new WeatherDataLoader(this, this, locData);
                    await wLoader.LoadWeatherData(false);
                }
            }
        }

        private async void RefreshLocations()
        {
            // Reload all panels if needed
            var locations = Settings.LocationData;
            var homeData = await Settings.GetLastGPSLocData();
            bool reload = (locations.Count != LocationPanels.Count || Settings.FollowGPS && (GPSPanelViewModel.First() == null));

            // Reload if weather source differs
            if ((GPSPanelViewModel.First() != null && GPSPanelViewModel.First().WeatherSource != Settings.API) ||
                (LocationPanels.Count >= 1 && LocationPanels[0].WeatherSource != Settings.API))
                reload = true;

            // Reload if panel queries dont match
            if (!reload && (GPSPanelViewModel.First() != null && homeData.query != GPSPanelViewModel.First().LocationData.query))
                reload = true;

            if (reload)
            {
                LocationPanels.Clear();
                LoadLocations();
            }
            else
            {
                List<LocationPanelViewModel> dataset = LocationPanels.ToList();
                if (GPSPanelViewModel.First() != null)
                    dataset.Add(GPSPanelViewModel.First());

                foreach (LocationPanelViewModel view in dataset)
                {
                    WeatherDataLoader wLoader =
                        new WeatherDataLoader(this, this, view.LocationData);
                    await wLoader.LoadWeatherData(false);
                }
            }
        }

        private async Task<LocationData> UpdateLocation()
        {
            LocationData locationData = null;

            if (Settings.FollowGPS)
            {
                Geoposition newGeoPos = null;

                try
                {
                    newGeoPos = await geolocal.GetGeopositionAsync(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(10));
                }
                catch (Exception)
                {
                    GeolocationAccessStatus geoStatus = GeolocationAccessStatus.Unspecified;

                    try
                    {
                        geoStatus = await Geolocator.RequestAccessAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                    finally
                    {
                        if (geoStatus == GeolocationAccessStatus.Allowed)
                        {
                            newGeoPos = await geolocal.GetGeopositionAsync(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(10));
                        }
                        else if (geoStatus == GeolocationAccessStatus.Denied)
                        {
                            // Disable gps feature
                            Settings.FollowGPS = false;
                            GPSPanelViewModel[0] = null;
                            GPSPanel.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            GPSPanelViewModel[0] = null;
                            GPSPanel.Visibility = Visibility.Collapsed;
                        }
                    }

                    if (!Settings.FollowGPS)
                        return null;
                }

                // Access to location granted
                if (newGeoPos != null)
                {
                    string selected_query = string.Empty;

                    await Task.Run(async () =>
                    {
                        LocationQueryViewModel view = await GeopositionQuery.GetLocation(newGeoPos);

                        if (!String.IsNullOrEmpty(view.LocationQuery))
                            selected_query = view.LocationQuery;
                        else
                            selected_query = string.Empty;
                    });

                    if (String.IsNullOrWhiteSpace(selected_query))
                    {
                        // Stop since there is no valid query
                        GPSPanelViewModel[0] = null;
                        GPSPanel.Visibility = Visibility.Collapsed;
                    }

                    // Save location as last known
                    locationData = new LocationData(selected_query, newGeoPos);
                }
            }

            return locationData;
        }

        private void LocationsPanel_ItemClick(object sender, ItemClickEventArgs e)
        {
            LocationPanelViewModel panel = e.ClickedItem as LocationPanelViewModel;
            this.Frame.Navigate(typeof(WeatherNow), panel.LocationData);

            if (panel.LocationData.locationType == LocationType.GPS ||
                !Settings.FollowGPS && panel.LocationData.query == Settings.LocationData.First().query)
            {
                // Clear backstack since we're going home
                Frame.BackStack.Clear();
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            }
        }

        private void Location_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Cancel pending searches
            cts.Cancel();
            cts = new CancellationTokenSource();

            if (!String.IsNullOrWhiteSpace(sender.Text) && args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                String query = sender.Text;

                Task.Run(async () =>
                {
                    if (cts.IsCancellationRequested) return;

                    var results = await AutoCompleteQuery.GetLocations(query);

                    if (cts.IsCancellationRequested) return;

                    // Refresh list
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        LocationQuerys = results;
                        sender.ItemsSource = null;
                        sender.ItemsSource = LocationQuerys;
                        sender.IsSuggestionListOpen = true;
                    });
                });
            }
            else if (String.IsNullOrWhiteSpace(sender.Text))
            {
                // Cancel pending searches
                cts.Cancel();
                cts = new CancellationTokenSource();
                // Hide flyout if query is empty or null
                LocationQuerys.Clear();
                sender.IsSuggestionListOpen = false;
            }
        }

        private void Location_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is LocationQueryViewModel theChosenOne)
            {
                if (String.IsNullOrEmpty(theChosenOne.LocationQuery))
                    sender.Text = String.Empty;
                else
                    sender.Text = theChosenOne.LocationName;
            }

            sender.IsSuggestionListOpen = false;
        }

        private async void Location_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null)
            {
                // User selected an item from the suggestion list, take an action on it here.
                LocationQueryViewModel theChosenOne = args.ChosenSuggestion as LocationQueryViewModel;

                if (!String.IsNullOrEmpty(theChosenOne.LocationQuery))
                    selected_query = theChosenOne.LocationQuery;
                else
                    selected_query = string.Empty;
            }
            else if (!String.IsNullOrEmpty(args.QueryText))
            {
                // Use args.QueryText to determine what to do.
                LocationQueryViewModel result = (await AutoCompleteQuery.GetLocations(args.QueryText)).First();

                if (result != null && String.IsNullOrWhiteSpace(result.LocationQuery))
                {
                    sender.Text = result.LocationName;
                    selected_query = result.LocationQuery;
                }
            }
            else if (String.IsNullOrWhiteSpace(args.QueryText))
            {
                // Stop since there is no valid query
                return;
            }

            if (String.IsNullOrWhiteSpace(selected_query))
            {
                // Stop since there is no valid query
                return;
            }

            // Show loading dialog
            await LoadingDialog.ShowAsync();

            var locData = Settings.LocationData;
            var weatherData = Settings.WeatherData;
            int index = locData.Count;

            // Check if location already exists
            if (locData.Exists(l => l.query == selected_query))
            {
                // Hide dialog
                await LoadingDialog.HideAsync();
                ShowAddLocationsPanel(false);
                return;
            }

            Weather weather = weatherData[selected_query] as Weather;
            if (weather == null)
                weather = await WeatherLoaderTask.GetWeather(selected_query);

            if (weather == null)
            {
                // Hide dialog
                await LoadingDialog.HideAsync();
                return;
            }

            // Save coords to List
            var location = new LocationData(selected_query);
            locData.Add(location);
            weatherData[selected_query] = weather;

            // Save data
            Settings.SaveLocationData();
            Settings.SaveWeatherData();

            LocationPanelViewModel panelView = new LocationPanelViewModel(weather)
            {
                LocationData = location
            };

            // Set properties if necessary
            if (EditMode)
            {
                panelView.EditMode = true;
            }

            // Add to collection
            LocationPanels.Add(panelView);

            // Hide add locations panel
            await LoadingDialog.HideAsync();
            ShowAddLocationsPanel(false);

            sender.IsSuggestionListOpen = false;
        }

        private void AddLocationsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAddLocationsPanel(true);
        }

        private void ShowAddLocationsPanel(bool show)
        {
            AddLocationsButton.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            AddLocationPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            if (!show)
            {
                Location.Text = string.Empty;
                Location.IsSuggestionListOpen = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ShowAddLocationsPanel(false);
        }

        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleEditMode();
        }

        private void ToggleEditMode()
        {
            // Toggle EditMode
            EditMode = !EditMode;

            EditButton.Icon = new SymbolIcon(EditMode ? Symbol.Accept : Symbol.Edit);
            EditButton.Label = EditMode ? App.ResLoader.GetString("Label_Done") : App.ResLoader.GetString("Label_Edit");
            LocationsPanel.IsItemClickEnabled = !EditMode;

            foreach (LocationPanelViewModel view in LocationPanels)
            {
                view.EditMode = EditMode;
            }

            if (!EditMode && DataChanged) Settings.SaveLocationData();
            DataChanged = false;
        }

        private void LocationPanel_DeleteClick(object sender, RoutedEventArgs e)
        {
            FrameworkElement button = sender as FrameworkElement;
            if (button == null || (button != null && button.DataContext == null))
                return;

            LocationPanelViewModel view = button.DataContext as LocationPanelViewModel;
            LocationData data = view.LocationData;

            // Remove location from list
            Settings.LocationData.Remove(data);
            Settings.SaveLocationData();

            // Remove panel
            LocationPanels.Remove(view);
        }

        private void MoveData(LocationPanelViewModel view, int fromIdx, int toIdx)
        {
            // Move data in both location dictionary and local dataset
            var location = Settings.LocationData[fromIdx];
            Settings.LocationData.RemoveAt(fromIdx);
            Settings.LocationData.Insert(toIdx, location);

            // Only move panels if we haven't already
            if (LocationPanels.IndexOf(view) != toIdx)
                LocationPanels.Move(fromIdx, toIdx);
        }

        private void LocationsPanel_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (!EditMode) ToggleEditMode();
        }

        private void LocationsPanel_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (!(args.Items.First() is LocationPanelViewModel panel))
                return;

            var data = Settings.LocationData;
            int newIndex = LocationPanels.IndexOf(panel);
            int oldIndex = data.FindIndex(location => location.query == panel.LocationData.query);

            if (oldIndex != newIndex)
                MoveData(panel, oldIndex, newIndex);

            // Make sure we're still in EditMode after
            if (!EditMode) ToggleEditMode();

            if (oldIndex != newIndex)
                DataChanged = true;
        }

        private void LocationPanel_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if (!EditMode) ToggleEditMode();
                e.Handled = true;
            }
        }
    }
}