using System.Collections.ObjectModel;
using Mainframe.Models;

namespace Mainframe.ViewModels;

public class TimeEntryRowViewModel : BaseViewModel
{
    private readonly ObservableCollection<ChargeCode> _allChargeCodes;
    private readonly ObservableCollection<Project> _allProjects;

    private ChargeCode? _selectedChargeCode;
    private Project? _selectedProject;
    private ProjectTask? _selectedTask;
    private Subtask? _selectedSubtask;
    private decimal _hours;
    private string _hoursText = "0.00";
    private bool _hoursInvalid;
    private bool _chargeCodeMissing;
    private string _notes = "";

    public TimeEntryRowViewModel(
        ObservableCollection<ChargeCode> chargeCodes,
        ObservableCollection<Project> projects)
    {
        _allChargeCodes = chargeCodes;
        _allProjects = projects;
    }

    public ObservableCollection<ChargeCode> AvailableChargeCodes => _allChargeCodes;
    public ObservableCollection<Project> AvailableProjects => _allProjects;
    public ObservableCollection<ProjectTask> AvailableTasks { get; } = [];
    public ObservableCollection<Subtask> AvailableSubtasks { get; } = [];

    public ChargeCode? SelectedChargeCode
    {
        get => _selectedChargeCode;
        set
        {
            if (SetProperty(ref _selectedChargeCode, value) && value != null)
                ChargeCodeMissing = false;
        }
    }

    public Project? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                RefreshAvailableTasks();
                SelectedTask = null;
            }
        }
    }

    public ProjectTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                RefreshAvailableSubtasks();
                SelectedSubtask = null;
            }
        }
    }

    public Subtask? SelectedSubtask
    {
        get => _selectedSubtask;
        set => SetProperty(ref _selectedSubtask, value);
    }

    // parsed hours value
    public decimal Hours
    {
        get => _hours;
        set
        {
            if (SetProperty(ref _hours, value))
                HoursChanged?.Invoke();
        }
    }

    // invalid hours set to 0 and produce warning to user
    public string HoursText
    {
        get => _hoursText;
        set
        {
            if (!SetProperty(ref _hoursText, value))
                return;

            var trimmed = value?.Trim() ?? "";

            if (trimmed.Length == 0)
            {
                HoursInvalid = false;
                SetHoursAndNormalize(0m);
            }
            else if (decimal.TryParse(trimmed, out var parsed))
            {
                HoursInvalid = false;
                SetHoursAndNormalize(parsed);
            }
            else
            {
                HoursInvalid = true;
                Hours = 0m; 
            }
        }
    }

    public bool HoursInvalid
    {
        get => _hoursInvalid;
        private set => SetProperty(ref _hoursInvalid, value);
    }

    private void SetHoursAndNormalize(decimal value)
    {
        Hours = value;

        var formatted = value.ToString("F2");
        if (!string.Equals(formatted, _hoursText, StringComparison.Ordinal))
        {
            _hoursText = formatted;
            OnPropertyChanged(nameof(HoursText));
        }
    }

    // need to know if charge code missing so can broder red if user attempts save
    public bool ChargeCodeMissing
    {
        get => _chargeCodeMissing;
        set => SetProperty(ref _chargeCodeMissing, value);
    }

    // clears invalid flag after save
    public void SyncHoursText() => HoursText = Hours.ToString("F2");

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public event Action? HoursChanged;
    public event Action? PersistRequested;

    public TimeEntry ToTimeEntry(DateOnly date) => new()
    {
        Date = date,
        ChargeCodeId = SelectedChargeCode?.Id ?? Guid.Empty,
        ProjectId = SelectedProject?.Id ?? Guid.Empty,
        TaskId = SelectedTask?.Id,
        SubtaskId = SelectedSubtask?.Id,
        Hours = Hours,
        Notes = Notes
    };

    public static TimeEntryRowViewModel FromTimeEntry(
        TimeEntry entry,
        ObservableCollection<ChargeCode> chargeCodes,
        ObservableCollection<Project> projects)
    {
        var vm = new TimeEntryRowViewModel(chargeCodes, projects);

        vm._selectedChargeCode = chargeCodes.FirstOrDefault(cc => cc.Id == entry.ChargeCodeId);
        vm._selectedProject = projects.FirstOrDefault(p => p.Id == entry.ProjectId);

        if (vm._selectedProject != null)
        {
            foreach (var t in vm._selectedProject.Tasks)
                vm.AvailableTasks.Add(t);

            if (entry.TaskId.HasValue)
            {
                vm._selectedTask = vm._selectedProject.Tasks
                    .FirstOrDefault(t => t.Id == entry.TaskId.Value);

                if (vm._selectedTask != null)
                {
                    foreach (var s in vm._selectedTask.Subtasks)
                        vm.AvailableSubtasks.Add(s);

                    if (entry.SubtaskId.HasValue)
                    {
                        vm._selectedSubtask = vm._selectedTask.Subtasks
                            .FirstOrDefault(s => s.Id == entry.SubtaskId.Value);
                    }
                }
            }
        }

        vm._hours = entry.Hours;
        vm._hoursText = entry.Hours.ToString("F2");
        vm._notes = entry.Notes;
        return vm;
    }

    public void CreateTaskFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || _selectedProject == null)
            return;

        var existing = AvailableTasks.FirstOrDefault(t =>
            string.Equals(t.Name, text, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            SelectedTask = existing;
            return;
        }

        var task = new ProjectTask { Name = text };
        _selectedProject.Tasks.Add(task);
        AvailableTasks.Add(task);
        SelectedTask = task;
        PersistRequested?.Invoke();
    }

    public void CreateSubtaskFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || _selectedTask == null)
            return;

        var existing = AvailableSubtasks.FirstOrDefault(s =>
            string.Equals(s.Name, text, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            SelectedSubtask = existing;
            return;
        }

        var subtask = new Subtask { Name = text };
        _selectedTask.Subtasks.Add(subtask);
        AvailableSubtasks.Add(subtask);
        SelectedSubtask = subtask;
        PersistRequested?.Invoke();
    }

    private void RefreshAvailableTasks()
    {
        AvailableTasks.Clear();
        if (_selectedProject != null)
        {
            foreach (var task in _selectedProject.Tasks)
                AvailableTasks.Add(task);
        }
    }

    private void RefreshAvailableSubtasks()
    {
        AvailableSubtasks.Clear();
        if (_selectedTask != null)
        {
            foreach (var subtask in _selectedTask.Subtasks)
                AvailableSubtasks.Add(subtask);
        }
    }
}
