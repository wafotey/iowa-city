using TransactionsIngest.Application.Helpers;
using TransactionsIngest.Data;

namespace TransactionsIngest.Application.Decorators;

public sealed class DatabaseTransactionDecorator<TCommand> : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    private readonly ICommandHandler<TCommand> _inner;
    private readonly IngestDbContext _dbContext;

    public DatabaseTransactionDecorator(ICommandHandler<TCommand> inner, IngestDbContext dbContext)
    {
        _inner = inner;
        _dbContext = dbContext;
    }

    public async Task HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        await _inner.HandleAsync(command, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}