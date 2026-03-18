using Microsoft.Extensions.DependencyInjection;

namespace TransactionsIngest.Helpers;

public interface ICommand
{
    string Name { get; }
    string Description { get; }
}

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public sealed class CommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<string, ICommand> _commands;

    public CommandDispatcher(IServiceProvider serviceProvider, IEnumerable<ICommand> commands)
    {
        _serviceProvider = serviceProvider;
        _commands = commands.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }

    public Task RouteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (_commands.Count == 0)
        {
            Console.WriteLine("No commands are registered.");
            return Task.CompletedTask;
        }

        var commandName = args.Length == 0 ? "ingest" : args[0];
        return ExecuteAsync(commandName, cancellationToken);
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
            throw new InvalidOperationException($"Handler for command '{command.Name}' did not return a task.");

        await task;
    }
}using Microsoft.Extensions.DependencyInjection;

namespace TransactionsIngest.Helpers;

public interface ICommand
{
    string Name { get; }
    string Description { get; }
}

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public sealed class CommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<string, Type> _commandTypes;

    public CommandDispatcher(IServiceProvider serviceProvider, IEnumerable<ICommand> commands)
    {
        _serviceProvider = serviceProvider;
        _commandTypes = commands.ToDictionary(c => c.Name, c => c.GetType(), StringComparer.OrdinalIgnoreCase);
    }

    public Task RouteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var commandName = args.Length == 0 ? "ingest" : args[0];
        return ExecuteAsync(commandName, cancellationToken);
    }

    public async Task ExecuteAsync(string commandName, CancellationToken cancellationToken = default)
    {
        if (!_commandTypes.TryGetValue(commandName, out var commandType))
        {
            Console.WriteLine($"Unknown command '{commandName}'.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        var command = (ICommand)ActivatorUtilities.CreateInstance(services, commandType);

        var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
        var handler = services.GetRequiredService(handlerType);

        var method = handlerType.GetMethod("HandleAsync")!;
        var task = (Task)method.Invoke(handler, new object[] { command, cancellationToken })!;
        await task;
    }
}
