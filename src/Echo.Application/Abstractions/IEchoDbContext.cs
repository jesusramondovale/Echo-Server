namespace Echo.Application.Abstractions;

public interface IEchoDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
