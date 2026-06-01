using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Mainframe.Models;
using Mainframe.Services;

namespace Mainframe.ViewModels;

public class MainViewModel : BaseViewModel, IDisposable
{
    private readonly DataService _dataService = new();
    private readonly ExcelExportService _excelExportService = new();
    private readonly AppData _appData;

    public MainViewModel()
    {
        _appData = _dataService.Load();

        ChargeCodes = new ObservableCollection<ChargeCode>(_appData.ChargeCodes);
        Projects = new ObservableCollection<Project>(_appData.Projects);
        _userName = string.IsNullOrWhiteSpace(_appData.UserName)
            ? Environment.UserName
            : _appData.UserName;

#if !PORTABLE
        // exports default location in user's documents/TaskingSheets
        _exportBaseDirectory = string.IsNullOrWhiteSpace(_appData.ExportBaseDirectory)
            ? DefaultExportBaseDirectory
            : _appData.ExportBaseDirectory;
        _exportFolderName = string.IsNullOrWhiteSpace(_appData.ExportFolderName)
            ? DefaultExportFolderName
            : _appData.ExportFolderName;
#endif

        // daily entry today by default
        _selectedDate = DateTime.Today;
        DailyEntries = [];

        // current week is default for overview
        var today = DateTime.Today;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        _overviewStartDate = today.AddDays(-diff);
        _overviewEndDate = _overviewStartDate.AddDays(6);
        ChargeCodeSummaries = [];
        ProjectSummaries = [];
        DailySummaries = [];

        _newChargeCodeCode = "";
        _newChargeCodeDescription = "";
        _newProjectName = "";
        _newTaskName = "";
        _newSubtaskName = "";

        // daily tab commands
        AddDailyEntryCommand = new RelayCommand(AddDailyEntry);
        RemoveDailyEntryCommand = new RelayCommand(RemoveDailyEntry);
        SaveDailyCommand = new RelayCommand(SaveDaily);

        // overview tab commands
        ExportToExcelCommand = new RelayCommand(ExportToExcel);

#if !PORTABLE
        // settings tab commands
        BrowseExportDirectoryCommand = new RelayCommand(BrowseExportDirectory);
        ResetExportLocationCommand = new RelayCommand(ResetExportLocation);
#endif

        // manage tab commands
        AddChargeCodeCommand = new RelayCommand(AddChargeCode, () => !string.IsNullOrWhiteSpace(NewChargeCodeCode));
        RemoveChargeCodeCommand = new RelayCommand(RemoveChargeCode, () => SelectedManageChargeCode != null);
        AddProjectCommand = new RelayCommand(AddProject, () => !string.IsNullOrWhiteSpace(NewProjectName));
        RemoveProjectCommand = new RelayCommand(RemoveProject, () => SelectedManageProject != null);
        AddTaskCommand = new RelayCommand(AddTask, () => SelectedManageProject != null && !string.IsNullOrWhiteSpace(NewTaskName));
        RemoveTaskCommand = new RelayCommand(RemoveTask, () => SelectedManageTask != null);
        AddSubtaskCommand = new RelayCommand(AddSubtask, () => SelectedManageTask != null && !string.IsNullOrWhiteSpace(NewSubtaskName));
        RemoveSubtaskCommand = new RelayCommand(RemoveSubtask, () => SelectedManageSubtask != null);

        LoadDay();
        RefreshOverview();
    }

    //shared
    public ObservableCollection<ChargeCode> ChargeCodes { get; }
    public ObservableCollection<Project> Projects { get; }

    private string _userName;
    public string UserName
    {
        get => _userName;
        set
        {
            if (SetProperty(ref _userName, value))
            {
                _appData.UserName = value;
                PersistData();
            }
        }
    }

    // settings props (tasking sheet save location)

    private const string DefaultExportFolderName = "TaskingSheets";

#if PORTABLE
    // portable build: exports in TaskingSheets folder next to executable
    public string EffectiveExportDirectory =>
        Path.Combine(AppContext.BaseDirectory, DefaultExportFolderName);
#else
    private static string DefaultExportBaseDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private string _exportBaseDirectory;
    public string ExportBaseDirectory
    {
        get => _exportBaseDirectory;
        set
        {
            var dir = string.IsNullOrWhiteSpace(value) ? DefaultExportBaseDirectory : value.Trim();
            if (SetProperty(ref _exportBaseDirectory, dir))
            {
                _appData.ExportBaseDirectory = dir;
                PersistData();
                OnPropertyChanged(nameof(EffectiveExportDirectory));
            }
        }
    }

    private string _exportFolderName;
    public string ExportFolderName
    {
        get => _exportFolderName;
        set
        {
            // validate folder name
            var cleaned = new string((value ?? "")
                .Where(c => !Path.GetInvalidFileNameChars().Contains(c))
                .ToArray()).Trim();
            var name = string.IsNullOrWhiteSpace(cleaned) ? DefaultExportFolderName : cleaned;
            if (SetProperty(ref _exportFolderName, name))
            {
                _appData.ExportFolderName = name;
                PersistData();
                OnPropertyChanged(nameof(EffectiveExportDirectory));
            }
        }
    }

    public string EffectiveExportDirectory => Path.Combine(_exportBaseDirectory, _exportFolderName);

    public ICommand BrowseExportDirectoryCommand { get; }
    public ICommand ResetExportLocationCommand { get; }

    private void BrowseExportDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select the base folder for tasking sheets",
            InitialDirectory = Directory.Exists(_exportBaseDirectory)
                ? _exportBaseDirectory
                : DefaultExportBaseDirectory
        };

        if (dialog.ShowDialog() == true)
            ExportBaseDirectory = dialog.FolderName;
    }

    private void ResetExportLocation()
    {
        ExportBaseDirectory = DefaultExportBaseDirectory;
        ExportFolderName = DefaultExportFolderName;
    }
#endif

    //daily entry props
    private DateTime _selectedDate;
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
                LoadDay();
        }
    }

    public ObservableCollection<TimeEntryRowViewModel> DailyEntries { get; }

    private decimal _dailyTotalHours;
    public decimal DailyTotalHours
    {
        get => _dailyTotalHours;
        private set => SetProperty(ref _dailyTotalHours, value);
    }

    private string _dailyStatusMessage = "";
    public string DailyStatusMessage
    {
        get => _dailyStatusMessage;
        set => SetProperty(ref _dailyStatusMessage, value);
    }

    private string _dailyStatusError = "";
    public string DailyStatusError
    {
        get => _dailyStatusError;
        set => SetProperty(ref _dailyStatusError, value);
    }

    private string _dailyStatusWarning = "";
    public string DailyStatusWarning
    {
        get => _dailyStatusWarning;
        set => SetProperty(ref _dailyStatusWarning, value);
    }

    public ICommand AddDailyEntryCommand { get; }
    public ICommand RemoveDailyEntryCommand { get; }
    public ICommand SaveDailyCommand { get; }

    private void LoadDay()
    {
        ReloadDailyRows();
        DailyStatusMessage = "";
        DailyStatusError = "";
        DailyStatusWarning = "";
    }

    // rebuild the rows from saved data
    private void ReloadDailyRows()
    {
        DailyEntries.Clear();
        var date = DateOnly.FromDateTime(SelectedDate);
        var entries = _appData.TimeEntries.Where(e => e.Date == date);

        foreach (var entry in entries)
        {
            var row = TimeEntryRowViewModel.FromTimeEntry(entry, ChargeCodes, Projects);
            row.HoursChanged += RecalcDailyTotal;
            row.PersistRequested += PersistData;
            DailyEntries.Add(row);
        }

        RecalcDailyTotal();
    }

    private void AddDailyEntry()
    {
        var row = new TimeEntryRowViewModel(ChargeCodes, Projects);
        row.HoursChanged += RecalcDailyTotal;
        row.PersistRequested += PersistData;
        DailyEntries.Add(row);
    }

    private void RemoveDailyEntry(object? param)
    {
        if (param is TimeEntryRowViewModel row)
        {
            row.HoursChanged -= RecalcDailyTotal;
            row.PersistRequested -= PersistData;
            DailyEntries.Remove(row);
            RecalcDailyTotal();
        }
    }

    private void SaveDaily()
    {
        var date = DateOnly.FromDateTime(SelectedDate);

        // only requirement: charge code (holidays, pto, etc.)
        var toSave = DailyEntries.Where(row => row.SelectedChargeCode != null).ToList();
        var missing = DailyEntries.Where(row => row.SelectedChargeCode == null).ToList();
        var skipped = missing.Count;

        // flag rows w no charge code for a red border. clear it on the rest
        foreach (var row in missing)
            row.ChargeCodeMissing = true;
        foreach (var row in toSave)
            row.ChargeCodeMissing = false;

        if (toSave.Count == 0 && skipped > 0)
        {
            DailyStatusMessage = "";
            DailyStatusWarning = "";
            DailyStatusError = skipped == 1
                ? "Not saved: the entry needs a charge code."
                : $"Not saved: {skipped} entries each need a charge code.";
            return;
        }

        _appData.TimeEntries.RemoveAll(e => e.Date == date);

        foreach (var row in toSave)
            _appData.TimeEntries.Add(row.ToTimeEntry(date));

        // report success unless db committed
        try
        {
            PersistData();
        }
        catch (Exception ex)
        {
            DailyStatusMessage = "";
            DailyStatusWarning = "";
            DailyStatusError = $"Save failed: {ex.Message}";
            return;
        }

        DailyStatusMessage = $"Saved {toSave.Count} entries for {SelectedDate:d}";

        // skipped rows (no charge code): not saved at all
        DailyStatusError = skipped switch
        {
            0 => "",
            1 => "1 entry skipped: no charge code",
            _ => $"{skipped} entries skipped: no charge code"
        };

        // hours couldn't be parsed: saved as 0.00 with wrning
        var invalidHours = toSave.Count(row => row.HoursInvalid);
        DailyStatusWarning = invalidHours switch
        {
            0 => "",
            1 => "1 entry had invalid hours, saved as 0.00",
            _ => $"{invalidHours} entries had invalid hours, saved as 0.00"
        };

        // refresh saved rows to mirror what was persisted
        // rows with no charge code are left in place but red border added so the user can fix them
        foreach (var row in toSave)
            row.SyncHoursText();
    }

    private void RecalcDailyTotal()
    {
        DailyTotalHours = DailyEntries.Sum(e => e.Hours);
    }

    // overview props

    private DateTime _overviewStartDate;
    public DateTime OverviewStartDate
    {
        get => _overviewStartDate;
        set
        {
            // refresh when range changes
            if (SetProperty(ref _overviewStartDate, value))
                RefreshOverview();
        }
    }

    private DateTime _overviewEndDate;
    public DateTime OverviewEndDate
    {
        get => _overviewEndDate;
        set
        {
            if (SetProperty(ref _overviewEndDate, value))
                RefreshOverview();
        }
    }

    private decimal _overviewTotalHours;
    public decimal OverviewTotalHours
    {
        get => _overviewTotalHours;
        private set => SetProperty(ref _overviewTotalHours, value);
    }

    public ObservableCollection<ChargeCodeSummary> ChargeCodeSummaries { get; }
    public ObservableCollection<ProjectSummary> ProjectSummaries { get; }
    public ObservableCollection<DailySummary> DailySummaries { get; }

    public ICommand ExportToExcelCommand { get; }

    private void ExportToExcel()
    {
        RefreshOverview();

        if (DailySummaries.Count == 0)
        {
            MessageBox.Show("No data to export for the selected date range.", "Export",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var startDate = DateOnly.FromDateTime(OverviewStartDate);
        var endDate = DateOnly.FromDateTime(OverviewEndDate);

        var namePart = string.IsNullOrWhiteSpace(UserName) ? "" : $"_{UserName.Trim().Replace(" ", "_")}";
        var exportDir = EffectiveExportDirectory;
        Directory.CreateDirectory(exportDir);
        var dialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"Timesheet{namePart}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx",
            DefaultExt = ".xlsx",
            InitialDirectory = exportDir
        };

        if (dialog.ShowDialog() != true)
            return;

        var allTimeEntries = _appData.TimeEntries;
        var allTimeTotalHours = allTimeEntries.Sum(e => e.Hours);

        var allTimeCCSummaries = allTimeEntries
            .GroupBy(e => e.ChargeCodeId)
            .Select(ccGroup =>
            {
                var cc = _appData.ChargeCodes.FirstOrDefault(c => c.Id == ccGroup.Key);
                return new ChargeCodeSummary
                {
                    Name = cc?.ToString() ?? "Unknown",
                    TotalHours = ccGroup.Sum(e => e.Hours),
                    Projects = ccGroup.GroupBy(e => e.ProjectId)
                        .Select(pg =>
                        {
                            var proj = _appData.Projects.FirstOrDefault(p => p.Id == pg.Key);
                            return new ProjectHoursSummary
                            {
                                Name = proj?.Name ?? "Unknown",
                                TotalHours = pg.Sum(e => e.Hours)
                            };
                        }).ToList()
                };
            }).ToList();

        var allTimeProjSummaries = allTimeEntries
            .GroupBy(e => e.ProjectId)
            .Select(projGroup =>
            {
                var proj = _appData.Projects.FirstOrDefault(p => p.Id == projGroup.Key);
                return new ProjectSummary
                {
                    Name = proj?.Name ?? "Unknown",
                    TotalHours = projGroup.Sum(e => e.Hours)
                };
            }).ToList();

        var oldestDate = allTimeEntries.Any() ? allTimeEntries.Min(e => e.Date) : (DateOnly?)null;

        _excelExportService.Export(
            dialog.FileName,
            UserName.Trim(),
            startDate,
            endDate,
            [.. DailySummaries],
            [.. ChargeCodeSummaries],
            [.. ProjectSummaries],
            allTimeCCSummaries,
            allTimeProjSummaries,
            allTimeTotalHours,
            oldestDate);

        MessageBox.Show($"Exported to {dialog.FileName}", "Export Complete",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void RefreshOverview()
    {
        var startDate = DateOnly.FromDateTime(OverviewStartDate);
        var endDate = DateOnly.FromDateTime(OverviewEndDate);

        var entries = _appData.TimeEntries
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .ToList();

        OverviewTotalHours = entries.Sum(e => e.Hours);

        ChargeCodeSummaries.Clear();
        foreach (var ccGroup in entries.GroupBy(e => e.ChargeCodeId))
        {
            var cc = _appData.ChargeCodes.FirstOrDefault(c => c.Id == ccGroup.Key);
            ChargeCodeSummaries.Add(new ChargeCodeSummary
            {
                Name = cc?.ToString() ?? "Unknown",
                TotalHours = ccGroup.Sum(e => e.Hours),
                Projects = ccGroup.GroupBy(e => e.ProjectId)
                    .Select(pg =>
                    {
                        var proj = _appData.Projects.FirstOrDefault(p => p.Id == pg.Key);
                        return new ProjectHoursSummary
                        {
                            Name = proj?.Name ?? "Unknown",
                            TotalHours = pg.Sum(e => e.Hours)
                        };
                    }).ToList()
            });
        }

        ProjectSummaries.Clear();
        foreach (var projGroup in entries.GroupBy(e => e.ProjectId))
        {
            var proj = _appData.Projects.FirstOrDefault(p => p.Id == projGroup.Key);
            var projSummary = new ProjectSummary
            {
                Name = proj?.Name ?? "Unknown",
                TotalHours = projGroup.Sum(e => e.Hours),
                Tasks = []
            };

            foreach (var taskGroup in projGroup.Where(e => e.TaskId.HasValue).GroupBy(e => e.TaskId!.Value))
            {
                var task = proj?.Tasks.FirstOrDefault(t => t.Id == taskGroup.Key);
                var taskSummary = new TaskSummary
                {
                    Name = task?.Name ?? "Unknown",
                    TotalHours = taskGroup.Sum(e => e.Hours),
                    Subtasks = []
                };

                foreach (var subGroup in taskGroup.Where(e => e.SubtaskId.HasValue).GroupBy(e => e.SubtaskId!.Value))
                {
                    var sub = task?.Subtasks.FirstOrDefault(s => s.Id == subGroup.Key);
                    taskSummary.Subtasks.Add(new SubtaskSummary
                    {
                        Name = sub?.Name ?? "Unknown",
                        TotalHours = subGroup.Sum(e => e.Hours)
                    });
                }

                projSummary.Tasks.Add(taskSummary);
            }

            var untaskedHours = projGroup.Where(e => !e.TaskId.HasValue).Sum(e => e.Hours);
            if (untaskedHours > 0)
            {
                projSummary.Tasks.Insert(0, new TaskSummary
                {
                    Name = "(No Task)",
                    TotalHours = untaskedHours
                });
            }

            ProjectSummaries.Add(projSummary);
        }

        DailySummaries.Clear();
        foreach (var dayGroup in entries.GroupBy(e => e.Date).OrderBy(g => g.Key))
        {
            DailySummaries.Add(new DailySummary
            {
                Date = dayGroup.Key,
                TotalHours = dayGroup.Sum(e => e.Hours),
                Entries = dayGroup.Select(e =>
                {
                    var cc = _appData.ChargeCodes.FirstOrDefault(c => c.Id == e.ChargeCodeId);
                    var proj = _appData.Projects.FirstOrDefault(p => p.Id == e.ProjectId);
                    var task = e.TaskId.HasValue
                        ? proj?.Tasks.FirstOrDefault(t => t.Id == e.TaskId.Value)
                        : null;
                    var sub = e.SubtaskId.HasValue
                        ? task?.Subtasks.FirstOrDefault(s => s.Id == e.SubtaskId.Value)
                        : null;

                    return new DailyEntryDetail
                    {
                        ChargeCode = cc?.ToString() ?? "",
                        Project = proj?.Name ?? "",
                        Task = task?.Name ?? "",
                        Subtask = sub?.Name ?? "",
                        Hours = e.Hours,
                        Notes = e.Notes
                    };
                }).ToList()
            });
        }
    }

    //manage props
    private ChargeCode? _selectedManageChargeCode;
    public ChargeCode? SelectedManageChargeCode
    {
        get => _selectedManageChargeCode;
        set
        {
            if (SetProperty(ref _selectedManageChargeCode, value))
            {
                if (value != null)
                {
                    NewChargeCodeCode = value.Code;
                    NewChargeCodeDescription = value.Description;
                }
            }
        }
    }

    private string _newChargeCodeCode;
    public string NewChargeCodeCode
    {
        get => _newChargeCodeCode;
        set => SetProperty(ref _newChargeCodeCode, value);
    }

    private string _newChargeCodeDescription;
    public string NewChargeCodeDescription
    {
        get => _newChargeCodeDescription;
        set => SetProperty(ref _newChargeCodeDescription, value);
    }

    public ICommand AddChargeCodeCommand { get; }
    public ICommand RemoveChargeCodeCommand { get; }

    private void AddChargeCode()
    {
        if (SelectedManageChargeCode != null)
        {
            SelectedManageChargeCode.Code = NewChargeCodeCode;
            SelectedManageChargeCode.Description = NewChargeCodeDescription;

            var idx = ChargeCodes.IndexOf(SelectedManageChargeCode);
            if (idx >= 0)
            {
                var item = ChargeCodes[idx];
                ChargeCodes[idx] = item; 
            }
        }
        else
        {
            var cc = new ChargeCode
            {
                Code = NewChargeCodeCode,
                Description = NewChargeCodeDescription
            };
            ChargeCodes.Add(cc);
            _appData.ChargeCodes.Add(cc);
        }

        NewChargeCodeCode = "";
        NewChargeCodeDescription = "";
        SelectedManageChargeCode = null;
        PersistData();
    }

    private void RemoveChargeCode()
    {
        if (SelectedManageChargeCode == null) return;

        ChargeCodes.Remove(SelectedManageChargeCode);
        _appData.ChargeCodes.Remove(SelectedManageChargeCode);
        SelectedManageChargeCode = null;
        NewChargeCodeCode = "";
        NewChargeCodeDescription = "";
        PersistData();
    }

    private Project? _selectedManageProject;
    public Project? SelectedManageProject
    {
        get => _selectedManageProject;
        set
        {
            if (SetProperty(ref _selectedManageProject, value))
            {
                ManageTasks.Clear();
                if (value != null)
                {
                    foreach (var t in value.Tasks)
                        ManageTasks.Add(t);
                }
                SelectedManageTask = null;
                OnPropertyChanged(nameof(ManageTasks));
            }
        }
    }

    private string _newProjectName;
    public string NewProjectName
    {
        get => _newProjectName;
        set => SetProperty(ref _newProjectName, value);
    }

    public ICommand AddProjectCommand { get; }
    public ICommand RemoveProjectCommand { get; }

    private void AddProject()
    {
        var project = new Project { Name = NewProjectName };
        Projects.Add(project);
        _appData.Projects.Add(project);
        NewProjectName = "";
        PersistData();
    }

    private void RemoveProject()
    {
        if (SelectedManageProject == null) return;

        Projects.Remove(SelectedManageProject);
        _appData.Projects.Remove(SelectedManageProject);
        SelectedManageProject = null;
        PersistData();
    }

    public ObservableCollection<ProjectTask> ManageTasks { get; } = [];

    private ProjectTask? _selectedManageTask;
    public ProjectTask? SelectedManageTask
    {
        get => _selectedManageTask;
        set
        {
            if (SetProperty(ref _selectedManageTask, value))
            {
                ManageSubtasks.Clear();
                if (value != null)
                {
                    foreach (var s in value.Subtasks)
                        ManageSubtasks.Add(s);
                }
                SelectedManageSubtask = null;
                OnPropertyChanged(nameof(ManageSubtasks));
            }
        }
    }

    private string _newTaskName;
    public string NewTaskName
    {
        get => _newTaskName;
        set => SetProperty(ref _newTaskName, value);
    }

    public ICommand AddTaskCommand { get; }
    public ICommand RemoveTaskCommand { get; }

    private void AddTask()
    {
        if (SelectedManageProject == null) return;

        var task = new ProjectTask { Name = NewTaskName };
        SelectedManageProject.Tasks.Add(task);
        ManageTasks.Add(task);
        NewTaskName = "";
        PersistData();
    }

    private void RemoveTask()
    {
        if (SelectedManageProject == null || SelectedManageTask == null) return;

        SelectedManageProject.Tasks.Remove(SelectedManageTask);
        ManageTasks.Remove(SelectedManageTask);
        SelectedManageTask = null;
        PersistData();
    }

    // ========== Manage - Subtasks ==========

    public ObservableCollection<Subtask> ManageSubtasks { get; } = [];

    private Subtask? _selectedManageSubtask;
    public Subtask? SelectedManageSubtask
    {
        get => _selectedManageSubtask;
        set => SetProperty(ref _selectedManageSubtask, value);
    }

    private string _newSubtaskName;
    public string NewSubtaskName
    {
        get => _newSubtaskName;
        set => SetProperty(ref _newSubtaskName, value);
    }

    public ICommand AddSubtaskCommand { get; }
    public ICommand RemoveSubtaskCommand { get; }

    private void AddSubtask()
    {
        if (SelectedManageTask == null) return;

        var subtask = new Subtask { Name = NewSubtaskName };
        SelectedManageTask.Subtasks.Add(subtask);
        ManageSubtasks.Add(subtask);
        NewSubtaskName = "";
        PersistData();
    }

    private void RemoveSubtask()
    {
        if (SelectedManageTask == null || SelectedManageSubtask == null) return;

        SelectedManageTask.Subtasks.Remove(SelectedManageSubtask);
        ManageSubtasks.Remove(SelectedManageSubtask);
        SelectedManageSubtask = null;
        PersistData();
    }

    // data persistence

    private void PersistData()
    {
        _appData.ChargeCodes = [.. ChargeCodes];
        _appData.Projects = [.. Projects];
        _dataService.Save(_appData);
    }

    public void Dispose()
    {
        _dataService.Dispose();
        GC.SuppressFinalize(this);
    }
}
