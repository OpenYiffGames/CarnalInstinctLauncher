static class DIHelper
{
    public static R Run<T, R>(this IServiceProvider services, T func) where T : Delegate
    {
        var @params = func.Method.GetParameters();
        var args = new object?[@params.Length];
        for (int i = 0; i < @params.Length; i++)
        {
            var parameterType = @params[i].ParameterType;
            var instance = services.GetService(@params[i].ParameterType);
            if (instance == null && Nullable.GetUnderlyingType(parameterType) != null)
            {
                throw new InvalidOperationException($"Service not found for type {parameterType}");
            }
            args[i] = instance;
        }
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
        return (R)func.DynamicInvoke(args);
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
    }
}

