namespace CrmSales.SharedKernel.Application;

/// <summary>
/// Marker for queries that return a Result&lt;TResponse&gt;.
/// Wolverine routes these via IMessageBus.InvokeAsync&lt;Result&lt;TResponse&gt;&gt;().
/// </summary>
public interface IQuery<TResponse> { }
