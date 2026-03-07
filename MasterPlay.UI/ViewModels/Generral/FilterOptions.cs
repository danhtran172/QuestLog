// Enums for filter levels
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

public enum AvgPlayLevel
{
    VeryLong,
    Long,
    Medium,
    Short,
    VeryShort
}

public enum MetaScoreLevel
{
    VeryHigh,
    High,
    Medium,
    Low,
    VeryLow
}

// Filter options class
public class FilterOptions : ObservableObject
{
    private string _operatorFinish = "=";
    private double? _valueFinish;
    private string _operatorExcited = "=";
    private double? _valueExcited;
    private string _operatorScore = "=";
    private double? _valueScore;
    private string _operatorPlayTime = "=";
    private int? _valuePlayTime;
    private ObservableCollection<AvgPlayLevel> _selectedAvgPlays = new();
    private ObservableCollection<int> _selectedProgressIds = new();
    private ObservableCollection<int> _selectedOwnershipIds = new();
    private ObservableCollection<MetaScoreLevel> _selectedMetaScores = new();
    private bool _isReverse;

    public string OperatorFinish
    {
        get => _operatorFinish;
        set => SetProperty(ref _operatorFinish, value ?? "=");
    }
    public double? ValueFinish
    {
        get => _valueFinish;
        set => SetProperty(ref _valueFinish, value);
    }

    public string OperatorExcited
    {
        get => _operatorExcited;
        set => SetProperty(ref _operatorExcited, value ?? "=");
    }
    public double? ValueExcited
    {
        get => _valueExcited;
        set => SetProperty(ref _valueExcited, value);
    }

    public string OperatorScore
    {
        get => _operatorScore;
        set => SetProperty(ref _operatorScore, value ?? "=");
    }
    public double? ValueScore
    {
        get => _valueScore;
        set => SetProperty(ref _valueScore, value);
    }

    public string OperatorPlayTime
    {
        get => _operatorPlayTime;
        set => SetProperty(ref _operatorPlayTime, value ?? "=");
    }
    public int? ValuePlayTime
    {
        get => _valuePlayTime;
        set => SetProperty(ref _valuePlayTime, value);
    }

    public ObservableCollection<AvgPlayLevel> SelectedAvgPlays
    {
        get => _selectedAvgPlays;
        set => SetProperty(ref _selectedAvgPlays, value ?? new());
    }

    public ObservableCollection<int> SelectedProgressIds
    {
        get => _selectedProgressIds;
        set => SetProperty(ref _selectedProgressIds, value ?? new());
    }

    public ObservableCollection<int> SelectedOwnershipIds
    {
        get => _selectedOwnershipIds;
        set => SetProperty(ref _selectedOwnershipIds, value ?? new());
    }

    public ObservableCollection<MetaScoreLevel> SelectedMetaScores
    {
        get => _selectedMetaScores;
        set => SetProperty(ref _selectedMetaScores, value ?? new());
    }

    public bool IsReverse
    {
        get => _isReverse;
        set => SetProperty(ref _isReverse, value);
    }
}