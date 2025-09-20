using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Specifications.Models;
using System.Text.Json;

namespace Specifications.Components.Pages
{
    public partial class Home : ComponentBase, IAsyncDisposable
    {
        public string? Error;
        public double HourPirce;
        public string ProjectTitle = "Lavage Auto";
        private bool disposed;
        private bool isInitialized = false;
        private readonly string specificationsDataFolder = "SpecificationsData";
        private Timer? autoSaveTimer;
        private bool hasUnsavedChanges = false;

        // Available JSON files in the folder
        public List<string> AvailableFiles { get; set; } = new();
        private string selectedFileName = string.Empty;

        // Add OnInitializedAsync to ensure proper component initialization
        protected override async Task OnInitializedAsync()
        {
            isInitialized = true;

            // Ensure the SpecificationsData folder exists
            await EnsureDataFolderExists();

            // Load available files
            await LoadAvailableFiles();

            // Setup auto-save timer (saves every 30 seconds if there are changes)
            autoSaveTimer = new Timer(AutoSaveCallback, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            await base.OnInitializedAsync();
        }

        public async Task AddSpecification()
        {
            try
            {
                var specifications = new SpeceficationsModel();
                SpecificationsList.Add(specifications);
                hasUnsavedChanges = true;
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
                    hasUnsavedChanges = true;
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
                hasUnsavedChanges = true;
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

        #region JSON File Management

        /// <summary>
        /// Ensures the SpecificationsData folder exists
        /// </summary>
        private async Task EnsureDataFolderExists()
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(specificationsDataFolder))
                {
                    Directory.CreateDirectory(specificationsDataFolder);
                }
            });
        }

        /// <summary>
        /// Auto-save callback for timer
        /// </summary>
        private async void AutoSaveCallback(object? state)
        {
            if (hasUnsavedChanges && SpecificationsList.Count > 0)
            {
                await SaveToJsonFile($"{ProjectTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                hasUnsavedChanges = false;
            }
        }

        /// <summary>
        /// Saves the current specifications list to a JSON file
        /// </summary>
        /// <param name="fileName">The name of the file to save (with .json extension)</param>
        public async Task SaveToJsonFile(string fileName)
        {
            try
            {
                await EnsureDataFolderExists();

                // Ensure fileName has .json extension
                if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".json";
                }

                var filePath = Path.Combine(specificationsDataFolder, fileName);

                var saveData = new
                {
                    ProjectTitle = this.ProjectTitle,
                    HourPrice = this.HourPirce,
                    SavedDate = DateTime.Now,
                    Specifications = this.SpecificationsList
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var jsonString = JsonSerializer.Serialize(saveData, options);
                await File.WriteAllTextAsync(filePath, jsonString);

                hasUnsavedChanges = false;
                await LoadAvailableFiles(); // Refresh the file list

                Console.WriteLine($"Data saved to {filePath}");
            }
            catch (Exception ex)
            {
                Error = $"Error saving file: {ex.Message}";
                Console.WriteLine($"Error saving to JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Lists all JSON files in the SpecificationsData folder
        /// </summary>
        public async Task LoadAvailableFiles()
        {
            try
            {
                await EnsureDataFolderExists();

                await Task.Run(() =>
                {
                    var files = Directory.GetFiles(specificationsDataFolder, "*.json")
                                        .Select(Path.GetFileName)
                                        .Where(name => !string.IsNullOrEmpty(name))
                                        .OrderByDescending(name => File.GetLastWriteTime(Path.Combine(specificationsDataFolder, name!)))
                                        .ToList();

                    AvailableFiles = files!;
                });

                Console.WriteLine($"Found {AvailableFiles.Count} JSON files");
            }
            catch (Exception ex)
            {
                Error = $"Error loading file list: {ex.Message}";
                Console.WriteLine($"Error loading files: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads specifications data from a JSON file
        /// </summary>
        /// <param name="fileName">The name of the file to load (with .json extension)</param>
        public async Task LoadFromJsonFile(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    Error = "Please select a file to load";
                    return;
                }

                // Ensure fileName has .json extension
                if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".json";
                }

                var filePath = Path.Combine(specificationsDataFolder, fileName);

                if (!File.Exists(filePath))
                {
                    Error = $"File {fileName} does not exist";
                    return;
                }

                var jsonString = await File.ReadAllTextAsync(filePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                using var document = JsonDocument.Parse(jsonString);
                var root = document.RootElement;

                // Load project details
                if (root.TryGetProperty("projectTitle", out var projectTitleElement))
                {
                    ProjectTitle = projectTitleElement.GetString() ?? "Lavage Auto";
                }

                if (root.TryGetProperty("hourPrice", out var hourPriceElement))
                {
                    HourPirce = hourPriceElement.GetDouble();
                }

                // Load specifications
                if (root.TryGetProperty("specifications", out var specificationsElement))
                {
                    var specifications = JsonSerializer.Deserialize<List<SpeceficationsModel>>(
                        specificationsElement.GetRawText(), options);

                    if (specifications != null)
                    {
                        SpecificationsList.Clear();
                        SpecificationsList.AddRange(specifications);
                        SpecificationsListToDelete.Clear();
                    }
                }

                hasUnsavedChanges = false;
                await RefreshGrid();

                Console.WriteLine($"Data loaded from {filePath}");
            }
            catch (Exception ex)
            {
                Error = $"Error loading file: {ex.Message}";
                Console.WriteLine($"Error loading from JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Manually save the current data
        /// </summary>
        public async Task ManualSave()
        {
            var fileName = $"{ProjectTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            await SaveToJsonFile(fileName);
        }

        /// <summary>
        /// Delete a JSON file
        /// </summary>
        /// <param name="fileName">The name of the file to delete</param>
        public async Task DeleteJsonFile(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    Error = "Please select a file to delete";
                    return;
                }

                var filePath = Path.Combine(specificationsDataFolder, fileName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    await LoadAvailableFiles(); // Refresh the file list
                    Console.WriteLine($"File {fileName} deleted successfully");
                }
                else
                {
                    Error = $"File {fileName} does not exist";
                }
            }
            catch (Exception ex)
            {
                Error = $"Error deleting file: {ex.Message}";
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }
        }

        #endregion

        #region UI Event Handlers

        /// <summary>
        /// Handles file selection from dropdown
        /// </summary>
        private void OnFileSelected(ChangeEventArgs e)
        {
            selectedFileName = e.Value?.ToString() ?? string.Empty;
            StateHasChanged();
        }

        /// <summary>
        /// Loads the selected file
        /// </summary>
        public async Task LoadSelectedFile()
        {
            if (!string.IsNullOrEmpty(selectedFileName))
            {
                await LoadFromJsonFile(selectedFileName);
            }
        }

        /// <summary>
        /// Deletes the selected file
        /// </summary>
        public async Task DeleteSelectedFile()
        {
            if (!string.IsNullOrEmpty(selectedFileName))
            {
                await DeleteJsonFile(selectedFileName);
                selectedFileName = string.Empty; // Clear selection after deletion
                StateHasChanged();
            }
        }

        /// <summary>
        /// Handle changes to specifications to mark as unsaved
        /// </summary>
        public void OnSpecificationChanged()
        {
            hasUnsavedChanges = true;
        }

        #endregion

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

            // Dispose timer
            autoSaveTimer?.Dispose();

            // Save any unsaved changes before disposing
            if (hasUnsavedChanges && SpecificationsList.Count > 0)
            {
                await SaveToJsonFile($"{ProjectTitle}_AutoSave_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            }

            // Clear collections to prevent memory leaks
            SpecificationsList?.Clear();
            SpecificationsListToDelete?.Clear();
            AvailableFiles?.Clear();

            await Task.CompletedTask;
        }
    }
}