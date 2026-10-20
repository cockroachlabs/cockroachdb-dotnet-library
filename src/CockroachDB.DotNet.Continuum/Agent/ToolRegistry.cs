using System.Reflection;
using System.Text;

namespace CockroachDB.DotNet.Continuum.Agent;

/// <summary>
/// Reflects over <c>[AgentTool]</c>-annotated methods on provider objects and
/// exposes them as described, invokable tools. Least-privilege: destructive tools
/// are only registered when <c>allowDestructive</c> is set.
/// </summary>
public sealed class ToolRegistry
{
    private sealed record Registered(object Target, MethodInfo Method, ToolDescriptor Descriptor);

    private readonly Dictionary<string, Registered> _tools = new(StringComparer.Ordinal);
    private readonly List<Registered> _order = [];

    /// <summary>Register every eligible <c>[AgentTool]</c> method on <paramref name="provider"/>.</summary>
    public void Register(object provider, bool allowDestructive)
    {
        ArgumentNullException.ThrowIfNull(provider);
        foreach (var method in provider.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var tool = method.GetCustomAttribute<AgentToolAttribute>();
            if (tool is null || (tool.Destructive && !allowDestructive))
                continue;

            var description = string.IsNullOrWhiteSpace(tool.Description) ? tool.Name : tool.Description;
            var descriptor = new ToolDescriptor(tool.Name, description, SchemaFor(method));
            var registered = new Registered(provider, method, descriptor);

            if (_tools.TryAdd(tool.Name, registered))
                _order.Add(registered);
        }
    }

    /// <summary>All registered tool descriptors, in registration order (MCP <c>tools/list</c>).</summary>
    public IReadOnlyList<ToolDescriptor> Descriptors()
        => _order.Select(r => r.Descriptor).ToList();

    /// <summary>Whether a tool with <paramref name="name"/> is registered.</summary>
    public bool Has(string name) => _tools.ContainsKey(name);

    /// <summary>Invoke a tool by name with named arguments (MCP <c>tools/call</c>).</summary>
    public async Task<object?> InvokeAsync(string name, IReadOnlyDictionary<string, object>? args)
    {
        if (!_tools.TryGetValue(name, out var registered))
            throw new ArgumentException($"Unknown tool: {name}", nameof(name));

        var parameters = registered.Method.GetParameters();
        var call = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            object? value = null;
            var pname = parameters[i].Name;
            if (args is not null && pname is not null)
                args.TryGetValue(pname, out value);
            call[i] = Coerce(value, parameters[i].ParameterType);
        }

        object? result;
        try
        {
            result = registered.Method.Invoke(registered.Target, call);
        }
        catch (TargetInvocationException e)
        {
            throw new InvalidOperationException($"Tool invocation failed: {name}", e.InnerException ?? e);
        }

        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            var taskType = task.GetType();
            return taskType.IsGenericType
                ? taskType.GetProperty("Result")?.GetValue(task)
                : null;
        }

        return result;
    }

    private static string SchemaFor(MethodInfo method)
    {
        var props = new StringBuilder();
        var required = new StringBuilder();
        var parameters = method.GetParameters();
        var first = true;
        foreach (var p in parameters)
        {
            var pname = p.Name ?? "arg";
            if (!first)
            {
                props.Append(',');
                required.Append(',');
            }

            first = false;
            props.Append($"\"{pname}\":{{\"type\":\"{JsonType(p.ParameterType)}\"}}");
            required.Append($"\"{pname}\"");
        }

        return $"{{\"type\":\"object\",\"properties\":{{{props}}},\"required\":[{required}]}}";
    }

    private static string JsonType(Type t)
    {
        if (t == typeof(bool)) return "boolean";
        if (t == typeof(int) || t == typeof(long) || t == typeof(short)) return "integer";
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return "number";
        return "string";
    }

    private static object? Coerce(object? value, Type target)
    {
        if (value is null)
            return target.IsValueType ? Activator.CreateInstance(target) : null;
        if (target.IsInstanceOfType(value))
            return value;

        var s = value.ToString()?.Trim() ?? "";
        if (target == typeof(string)) return value.ToString();
        if (target == typeof(bool)) return bool.Parse(s);
        if (target == typeof(int)) return int.Parse(s);
        if (target == typeof(long)) return long.Parse(s);
        if (target == typeof(double)) return double.Parse(s);
        return value;
    }
}
