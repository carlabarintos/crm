namespace CrmSales.SharedKernel.Application;

/// <summary>
/// Marker for commands that return no value.
/// Wolverine routes these via IMessageBus.InvokeAsync().
/// </summary>
public interface ICommand { }

/// <summary>
/// Marker for commands that return a Result&lt;TResponse&gt;.
/// </summary>
public interface ICommand<TResponse> { }
