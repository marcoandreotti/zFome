namespace ZFome.Application.Abstractions.Queries;

/// <summary>
/// Representa uma consulta (query) que retorna um resultado do tipo <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult"></typeparam>
public interface IQuery<out TResult>;