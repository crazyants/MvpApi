﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using MvpApi.Common.Models;
using MvpApi.Uwp.Helpers;
using MvpApi.Uwp.Views;
using Telerik.Core.Data;
using Telerik.UI.Xaml.Controls.Grid;

namespace MvpApi.Uwp.ViewModels
{
    public class HomePageViewModel : PageViewModelBase
    {
        #region Fields
        
        private int? currentOffset = 0;
        private string displayTotal;
        private DataGridSelectionMode gridSelectionMode = DataGridSelectionMode.Single;
        private bool isMultipleSelectionEnabled = false;
        private bool isLoadingMoreItems = false;
        private IncrementalLoadingCollection<ContributionsModel> activities;
        private bool areAppbarButtonsEnabled = false;

        #endregion

        public HomePageViewModel()
        {
            Activities = new IncrementalLoadingCollection<ContributionsModel>(LoadMoreItems) { BatchSize = 50 };

            if (DesignMode.DesignModeEnabled)
            {
                var designItems = DesignTimeHelpers.GenerateContributions();

                foreach (var contribution in designItems)
                {
                    Activities.Add(contribution);
                }
            }
        }

        private async Task<IEnumerable<ContributionsModel>> LoadMoreItems(uint count)
        {
            try
            {
                // Here we use a different flag when the view model is busy loading items because we dont want to cover the UI
                // The IsBusy flag is used for when deleteing items, when we want to block the UI
                IsLoadingMoreItems = true;
                
                var result = await App.ApiService.GetContributionsAsync(currentOffset, (int) count);

                currentOffset = result.PagingIndex;
                
                DisplayTotal = $"{currentOffset} of {result.TotalContributions}";

                // If we've recieved all the contributions, return null to stop automatic loading
                if (result.PagingIndex == result.TotalContributions)
                    return null;

                return result.Contributions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadMoreItems Exception: {ex}");
                return null;
            }
            finally
            {
                IsLoadingMoreItems = false;
            }
        }

        #region Properties

        public IncrementalLoadingCollection<ContributionsModel> Activities
        {
            get => activities;
            set => Set(ref activities, value);
        }

        public ObservableCollection<object> SelectedContributions { get; set; }
        
        public string DisplayTotal
        {
            get => displayTotal;
            set => Set(ref displayTotal, value);
        }

        public bool IsMultipleSelectionEnabled
        {
            get => isMultipleSelectionEnabled;
            set
            {
                Set(ref isMultipleSelectionEnabled, value);

                GridSelectionMode = value 
                    ? DataGridSelectionMode.Multiple 
                    : DataGridSelectionMode.Single;
            }
        }

        public DataGridSelectionMode GridSelectionMode
        {
            get => gridSelectionMode;
            set => Set(ref gridSelectionMode, value);
        }

        public bool AreAppbarButtonsEnabled
        {
            get => areAppbarButtonsEnabled;
            set => Set(ref areAppbarButtonsEnabled, value);
        }

        public bool IsLoadingMoreItems
        {
            get => isLoadingMoreItems;
            set => Set(ref isLoadingMoreItems, value);
        }

        #endregion
        
        #region Event Handlers

        public async void AddActivityButton_Click(object sender, RoutedEventArgs e)
        {
            await NavigationService.NavigateAsync(typeof(AddContributionsPage));
        }

        public void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedContributions.Clear();
        }

        public async void DeleteSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsBusy = true;
                IsBusyMessage = "preparing to delete contributions...";
                
                foreach (ContributionsModel contribution in SelectedContributions)
                {
                    IsBusyMessage = $"deleting {contribution.Title}...";

                    await App.ApiService.DeleteContributionAsync(contribution);
                }

                SelectedContributions.Clear();

                // After deleting contributions from the server, we want to start fresh.
                // We can do this by resetting the offset and starting over
                IsBusyMessage = "refreshing contributions...";

                currentOffset = 0;

                Activities = new IncrementalLoadingCollection<ContributionsModel>(LoadMoreItems) { BatchSize = 50 };

                await Activities.LoadMoreItemsAsync(50);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteSelectedContributions Exception: {ex}");
            }
            finally
            {
                IsBusy = false;
                IsBusyMessage = "";
            }
        }

        public async void RadDataGrid_OnSelectionChanged(object sender, DataGridSelectionChangedEventArgs e)
        {
            // When in multiple selection mode, enable/diable delete instead of navigating to details page
            if (GridSelectionMode == DataGridSelectionMode.Multiple)
            {
                AreAppbarButtonsEnabled = e?.AddedItems.Any() == true;
                return;
            }
            
            // when in single selectin mode, go to the selected item's details page
            if (GridSelectionMode == DataGridSelectionMode.Single && e?.AddedItems?.FirstOrDefault() is ContributionsModel contribution)
            {
                await NavigationService.NavigateAsync(typeof(ContributionDetailPage), contribution);
            }
        }

        #endregion

        #region Navigation

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (App.ShellPage.DataContext is ShellPageViewModel shellVm && shellVm.IsLoggedIn)
            {

            }
            else
            {
                await NavigationService.NavigateAsync(typeof(LoginPage));
            }
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            return base.OnNavigatedFromAsync(pageState, suspending);
        }

        #endregion
    }
}