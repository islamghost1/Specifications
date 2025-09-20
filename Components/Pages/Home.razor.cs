using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Specifications.Models;

namespace Specifications.Components.Pages
{
    public partial class Home : ComponentBase, IAsyncDisposable
    {
        public string? Error;
        public double HourPirce;
        public string ProjectTitle = "Lavage Auto";
        private bool disposed;
        private bool isInitialized = false;

        // Add OnInitializedAsync to ensure proper component initialization
        protected override async Task OnInitializedAsync()
        {
            isInitialized = true;
            await base.OnInitializedAsync();
        }

        public async Task AddSpecification()
        {
            try
            {
                var specifications = new SpeceficationsModel();
                SpecificationsList.Add(specifications);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                await RefreshGrid();
            }
        }

        private double GetTotalHours()
        {
            return SpecificationsList.Sum(x => x.Duration);
        }

        private double GetTotalCost()
        {
            return SpecificationsList.Sum(x => x.Duration) * HourPirce;
        }

        public async Task removeSpecification(SpeceficationsModel specefication)
        {
            try
            {
                if (SpecificationsList.Count > 0)
                {
                    SpecificationsList.Remove(specefication);
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            finally
            {
                await RefreshGrid();
            }
        }

        public async Task RemoveSelectedSpecifications()
        {
            try
            {
                // Create a copy of the list to avoid collection modification issues
                var itemsToRemove = SpecificationsListToDelete.ToList();

                foreach (var item in itemsToRemove)
                {
                    SpecificationsList.Remove(item);
                }

                // Clear the selection after deletion
                SpecificationsListToDelete.Clear();
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            finally
            {
                await RefreshGrid();
            }
        }

        private async Task RefreshGrid()
        {
            if (SpecificationsGrid is not null && !disposed && isInitialized)
            {
                try
                {
                    await InvokeAsync(async () =>
                    {
                        await SpecificationsGrid.RefreshDataAsync();
                        StateHasChanged();
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error refreshing grid: {ex.Message}");
                }
            }
        }

        // Grid properties
        List<SpeceficationsModel> SpecificationsList = new();
        HashSet<SpeceficationsModel> SpecificationsListToDelete = new();
        private Grid<SpeceficationsModel> SpecificationsGrid = default!;

        private async Task<GridDataProviderResult<SpeceficationsModel>> DataProvider(GridDataProviderRequest<SpeceficationsModel> request)
        {
            try
            {
                return await Task.FromResult(request.ApplyTo(SpecificationsList));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DataProvider: {ex.Message}");
                return new GridDataProviderResult<SpeceficationsModel>
                {
                    Data = new List<SpeceficationsModel>(),
                    TotalCount = 0
                };
            }
        }

        public async ValueTask DisposeAsync()
        {
            disposed = true;
            isInitialized = false;

            // Clear collections to prevent memory leaks
            SpecificationsList?.Clear();
            SpecificationsListToDelete?.Clear();

            await Task.CompletedTask;
        }
    }
}