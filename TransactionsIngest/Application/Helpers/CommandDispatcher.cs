using Microsoft.Extensions.DependencyInjection;
using TransactionsIngest.Application.Helpers;

namespace TransactionsIngest.Helpers;
public sealed class CommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<string, ICommand> _commands;

    public CommandDispatcher(IServiceProvider serviceProvider, IEnumerable<ICommand> commands)
    {
        _serviceProvider = serviceProvider;
        _commands = commands.ToDictionary(c => c.GetType().Name, StringComparer.OrdinalIgnoreCase);
    }


    public async Task ExecuteAsync(string commandName, CancellationToken cancellationToken = default)
    {
        if (!_commands.TryGetValue(commandName, out var command))
        {
            Console.WriteLine($"Unknown command '{commandName}'.");
            Console.WriteLine($"Available commands: {string.Join(", ", _commands.Keys.OrderBy(k => k))}");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        var handler = scope.ServiceProvider.GetRequiredService(handlerType);
        var handleMethod = handlerType.GetMethod(nameof(ICommandHandler<ICommand>.HandleAsync));

        if (handleMethod is null)
            throw new InvalidOperationException($"Handler method '{nameof(ICommandHandler<ICommand>.HandleAsync)}' not found for {handlerType.Name}.");

        var task = (Task?)handleMethod.Invoke(handler, new object[] { command, cancellationToken });
        if (task is null)
            throw new InvalidOperationException($"Handler for command '{command.GetType().Name}' did not return a task.");

        await task;
    }
}

