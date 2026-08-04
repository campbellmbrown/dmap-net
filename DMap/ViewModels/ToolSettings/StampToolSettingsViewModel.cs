using System;
using System.Collections.Generic;
using System.Windows.Input;

using DMap.Commands;
using DMap.Models;

using ReactiveUI;

namespace DMap.ViewModels.ToolSettings;

/// <summary>
/// ViewModel for the Stamp tool settings panel.
/// </summary>
public sealed class StampToolSettingsViewModel : ToolSettingsViewModelBase
{
    bool _isApplyingSelectedStamp;

    public StampToolSettingsViewModel(
        Action bringSelectedStampToFront,
        Action bringSelectedStampForward,
        Action sendSelectedStampBackward,
        Action sendSelectedStampToBack,
        Action duplicateSelectedStamp,
        Action deleteSelectedStamp,
        Action<double> setSelectedStampAngle)
    {
        ArgumentNullException.ThrowIfNull(setSelectedStampAngle);

        BringSelectedStampToFrontCommand = new RelayCommand(bringSelectedStampToFront);
        BringSelectedStampForwardCommand = new RelayCommand(bringSelectedStampForward);
        SendSelectedStampBackwardCommand = new RelayCommand(sendSelectedStampBackward);
        SendSelectedStampToBackCommand = new RelayCommand(sendSelectedStampToBack);
        DuplicateSelectedStampCommand = new RelayCommand(duplicateSelectedStamp);
        DeleteSelectedStampCommand = new RelayCommand(deleteSelectedStamp);
        SetSelectedStampAngle = setSelectedStampAngle;
        SelectedTemplate = Templates[0];
    }

    public override string Name => "Stamp Settings";

    public IReadOnlyList<StampTemplate> Templates { get; } = StampCatalog.Templates;

    public StampTemplate SelectedTemplate
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(SelectedTemplateId));
        }
    }

    public string SelectedTemplateId => SelectedTemplate.Id;

    public bool HasSelectedStamp
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Angle
    {
        get;
        set
        {
            var normalized = NormalizeDegrees(value);
            if (field == normalized)
                return;

            this.RaiseAndSetIfChanged(ref field, normalized);
            if (!_isApplyingSelectedStamp)
                SetSelectedStampAngle(normalized);
        }
    }

    Action<double> SetSelectedStampAngle { get; }

    public void ApplySelectedStamp(StampInstance? stamp)
    {
        _isApplyingSelectedStamp = true;
        try
        {
            Angle = stamp is null ? 0 : stamp.RotationDegrees;
        }
        finally
        {
            _isApplyingSelectedStamp = false;
        }
    }

    public ICommand BringSelectedStampToFrontCommand { get; }

    public ICommand BringSelectedStampForwardCommand { get; }

    public ICommand SendSelectedStampBackwardCommand { get; }

    public ICommand SendSelectedStampToBackCommand { get; }

    public ICommand DuplicateSelectedStampCommand { get; }

    public ICommand DeleteSelectedStampCommand { get; }

    static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
