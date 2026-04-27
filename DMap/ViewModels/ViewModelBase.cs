using ReactiveUI;

namespace DMap.ViewModels;

/// <summary>
/// Base class for all ViewModels in the application.
/// Inherits <see cref="ReactiveObject"/> to provide property-change notification
/// via <c>this.RaiseAndSetIfChanged</c>.
/// </summary>
public abstract class ViewModelBase : ReactiveObject
{
}
