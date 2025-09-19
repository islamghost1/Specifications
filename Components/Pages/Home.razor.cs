using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Specifications.Models;

namespace Specifications.Components.Pages
{
    public partial class Home : ComponentBase, IAsyncDisposable
    {
        public string? Error;
        public double HourPirce;
        private bool disposed;
        public async Task AddSpecification()
        {
            try
            {
                var specifications = new SpeceficationsModel
                {
                    Title = "Title",
                    Description = "Description",
                    Version = "0.0",
                    Duration = 0.0
                };
                SpecificationsList.Add(specifications);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (SpecificationsGrid is not null && !disposed)
                {
                    await InvokeAsync(() => SpecificationsGrid.RefreshDataAsync());
                }

                Console.WriteLine("Specification added successfully.");
            }
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
                if (SpecificationsGrid != null)
                {
                    await SpecificationsGrid.RefreshDataAsync();
                }

            }
        }
        public async Task RemoveSelectedSpecifications()
        {
            foreach (var item in SpecificationsListToDelete)
            {
                await removeSpecification(item);
            }
        }

        //grid 
        List<SpeceficationsModel> SpecificationsList = new();
        HashSet<SpeceficationsModel> SpecificationsListToDelete = new();

        private Grid<SpeceficationsModel> SpecificationsGrid = default!;
        private async Task<GridDataProviderResult<SpeceficationsModel>> DataProvider(GridDataProviderRequest<SpeceficationsModel> request)
        {
            return await Task.FromResult(request.ApplyTo(SpecificationsList));
        }
        public async ValueTask DisposeAsync()
        {
            disposed = true;
        }
    }

}
