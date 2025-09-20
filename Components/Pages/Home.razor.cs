using BlazorBootstrap;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.tool.xml;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Specifications.Models;
using System.Globalization;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Xml.Linq;
using Document = iTextSharp.text.Document;

namespace Specifications.Components.Pages
{
    public partial class Home : ComponentBase, IAsyncDisposable
    {
        public string? Error;
        public double HourPirce;
        public double ConvertionPrice;
        public string ProjectTitle = "Project Name";
        private bool disposed;
        private bool isInitialized = false;
        private readonly string specificationsDataFolder = "SpecificationsData";
        private Timer? autoSaveTimer;
        private bool hasUnsavedChanges = false;

        // Available JSON files in the folder
        public List<string> AvailableFiles { get; set; } = new();
        private string selectedFileName = string.Empty;
        private string currentSaveFile = string.Empty; // Track the current file being saved to

        // Add OnInitializedAsync to ensure proper component initialization
        protected override async Task OnInitializedAsync()
        {
            isInitialized = true;

            // Ensure the SpecificationsData folder exists
            await EnsureDataFolderExists();

            // Load available files
            await LoadAvailableFiles();

            // Setup auto-save timer (saves every 30 seconds if there are changes)
            autoSaveTimer = new Timer(async state => await AutoSaveCallback(state), null,
                          TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));


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
        private Task AutoSaveCallback(object? state)
        {
            return InvokeAsync(async () =>
            {
                if (hasUnsavedChanges && SpecificationsList.Count > 0)
                {
                    if (string.IsNullOrEmpty(currentSaveFile))
                        currentSaveFile = $"{ProjectTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                    await SaveToJsonFile(currentSaveFile);
                    hasUnsavedChanges = false;
                }
            });
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

                // Set the current save file to the loaded file
                currentSaveFile = fileName;
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
            // If no current save file, create a new one
            if (string.IsNullOrEmpty(currentSaveFile))
            {
                currentSaveFile = $"{ProjectTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            }

            await SaveToJsonFile(currentSaveFile);
        }

        /// <summary>
        /// Save as a new file
        /// </summary>
        public async Task SaveAsNewFile()
        {
            currentSaveFile = $"{ProjectTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            await SaveToJsonFile(currentSaveFile);
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

                    // If we're deleting the current save file, reset it
                    if (currentSaveFile == fileName)
                    {
                        currentSaveFile = string.Empty;
                    }

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

        #region PDF Generation

        /// <summary>
        /// Generates a PDF from the current specifications
        /// </summary>
        /// <summary>
        /// Generates a PDF from the current specifications
        /// </summary>
        /// <summary>
        /// Generates a PDF from the current specifications with dark theme
        /// </summary>
        /// <summary>
        /// Generates a PDF from the current specifications with dark theme
        /// </summary>
        public async Task GeneratePdf()
        {
            try
            {
                // Create a PDF document
                using (var memoryStream = new MemoryStream())
                {
                    var document = new Document(PageSize.A4, 50, 50, 25, 25);
                    var writer = PdfWriter.GetInstance(document, memoryStream);

                    // Create a page event handler to draw the black background
                    writer.PageEvent = new BlackBackgroundPageEvent();

                    document.Open();

                    // Add "I.SCR.DEV" header
                    var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 24, BaseColor.WHITE);
                    var header = new Paragraph("I.SCR.DEV", headerFont);
                    header.Alignment = Element.ALIGN_CENTER;
                    header.SpacingAfter = 10f;
                    document.Add(header);

                    // Add project title
                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.LIGHT_GRAY);
                    var title = new Paragraph(ProjectTitle, titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingAfter = 20f;
                    document.Add(title);

                    // Add summary information
                    var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.LIGHT_GRAY);
                    var summary = new Paragraph($"Total Hours: {GetTotalHours():F1} hrs | Total Cost: {GetTotalCost():C2} |Total Cost DA: {(GetTotalCost()*ConvertionPrice).ToString("C2", CultureInfo.GetCultureInfo("fr-DZ"))} | Hourly Rate: {HourPirce:C2}/hr", summaryFont);
                    summary.Alignment = Element.ALIGN_CENTER;
                    summary.SpacingAfter = 20f;
                    document.Add(summary);

                    // Create a table for the specifications
                    var table = new PdfPTable(5);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 2, 3, 1, 1, 1 });

                    // Add table headers with dark theme
                    var headerTableFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);

                    var titleHeader = new PdfPCell(new Phrase("Title", headerTableFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        BackgroundColor = new BaseColor(64, 64, 64),
                        BorderColor = BaseColor.LIGHT_GRAY,
                        Padding = 8
                    };
                    table.AddCell(titleHeader);

                    var descHeader = new PdfPCell(new Phrase("Description", headerTableFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        BackgroundColor = new BaseColor(64, 64, 64),
                        BorderColor = BaseColor.LIGHT_GRAY,
                        Padding = 8
                    };
                    table.AddCell(descHeader);

                    var durationHeader = new PdfPCell(new Phrase("Duration (hrs)", headerTableFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        BackgroundColor = new BaseColor(64, 64, 64),
                        BorderColor = BaseColor.LIGHT_GRAY,
                        Padding = 8
                    };
                    table.AddCell(durationHeader);

                    var costHeader = new PdfPCell(new Phrase("Cost ($)", headerTableFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        BackgroundColor = new BaseColor(64, 64, 64),
                        BorderColor = BaseColor.LIGHT_GRAY,
                        Padding = 8
                    };
                    table.AddCell(costHeader);

                    var progressHeader = new PdfPCell(new Phrase("Progress", headerTableFont))
                    {
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        BackgroundColor = new BaseColor(64, 64, 64),
                        BorderColor = BaseColor.LIGHT_GRAY,
                        Padding = 8
                    };
                    table.AddCell(progressHeader);

                    // Add table rows with dark theme
                    var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.LIGHT_GRAY);
                    var rowBackgroundColor = new BaseColor(32, 32, 32);

                    foreach (var spec in SpecificationsList)
                    {
                        var titleCell = new PdfPCell(new Phrase(spec.Title ?? "", cellFont))
                        {
                            BackgroundColor = rowBackgroundColor,
                            BorderColor = BaseColor.LIGHT_GRAY,
                            Padding = 5
                        };
                        table.AddCell(titleCell);

                        var descCell = new PdfPCell(new Phrase(spec.Description ?? "", cellFont))
                        {
                            BackgroundColor = rowBackgroundColor,
                            BorderColor = BaseColor.LIGHT_GRAY,
                            Padding = 5
                        };
                        table.AddCell(descCell);

                        var durationCell = new PdfPCell(new Phrase(spec.Duration.ToString("F1"), cellFont))
                        {
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            BackgroundColor = rowBackgroundColor,
                            BorderColor = BaseColor.LIGHT_GRAY,
                            Padding = 5
                        };
                        table.AddCell(durationCell);

                        var costCell = new PdfPCell(new Phrase((spec.Duration * HourPirce).ToString("C2"), cellFont))
                        {
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            BackgroundColor = rowBackgroundColor,
                            BorderColor = BaseColor.LIGHT_GRAY,
                            Padding = 5
                        };
                        table.AddCell(costCell);

                        var progressCell = new PdfPCell(new Phrase(spec.StatusProgess.ToString(), cellFont))
                        {
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            BackgroundColor = rowBackgroundColor,
                            BorderColor = BaseColor.LIGHT_GRAY,
                            Padding = 5
                        };
                        table.AddCell(progressCell);
                    }

                    document.Add(table);

                    // Add footer with date
                    var footerFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.LIGHT_GRAY);
                    var footer = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}", footerFont);
                    footer.Alignment = Element.ALIGN_CENTER;
                    footer.SpacingBefore = 20f;
                    document.Add(footer);

                    document.Close();

                    // Download the PDF
                    var fileName = $"{ProjectTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    var fileBytes = memoryStream.ToArray();

                    // Trigger download using JavaScript interop
                    await TriggerFileDownload(fileBytes, fileName, "application/pdf");
                }
            }
            catch (Exception ex)
            {
                Error = $"Error generating PDF: {ex.Message}";
                Console.WriteLine($"Error generating PDF: {ex.Message}");
            }
        }

        // Page event handler to draw black backgrounds
        public class BlackBackgroundPageEvent : PdfPageEventHelper
        {
            public override void OnEndPage(PdfWriter writer, Document document)
            {
                var canvas = writer.DirectContentUnder;
                canvas.SaveState();
                canvas.SetColorFill(BaseColor.BLACK);
                canvas.Rectangle(0, 0, document.PageSize.Width, document.PageSize.Height);
                canvas.Fill();
                canvas.RestoreState();
            }
        }
        // JavaScript interop for file download
        private async Task TriggerFileDownload(byte[] data, string fileName, string contentType)
        {
            try
            {
                // Convert byte array to base64 string
                var base64Data = Convert.ToBase64String(data);

                // Invoke JavaScript function to download the file
                await JSRuntime.InvokeVoidAsync("downloadFile", base64Data, fileName, contentType);
            }
            catch (Exception ex)
            {
                Error = $"Error downloading file: {ex.Message}";
                Console.WriteLine($"Download error: {ex.Message}");
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
            InvokeAsync(StateHasChanged);
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
                await InvokeAsync(StateHasChanged);
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
            autoSaveTimer?.DisposeAsync();

            // Save any unsaved changes before disposing
            if (hasUnsavedChanges && SpecificationsList.Count > 0)
            {
                // If no current save file, create a new one
                if (string.IsNullOrEmpty(currentSaveFile))
                {
                    currentSaveFile = $"{ProjectTitle}_AutoSave_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                }
                await SaveToJsonFile(currentSaveFile);
            }

            // Clear collections to prevent memory leaks
            SpecificationsList?.Clear();
            SpecificationsListToDelete?.Clear();
            AvailableFiles?.Clear();

            await Task.CompletedTask;
        }
    }
}