using Microsoft.Extensions.Configuration;

namespace EventDrivenDemo.Functions;

public abstract class FunctionBase
{
    public FunctionBase(IConfigurationRoot configuration)
    {
        _configuration = configuration;
    }

    protected readonly IConfigurationRoot _configuration;
}