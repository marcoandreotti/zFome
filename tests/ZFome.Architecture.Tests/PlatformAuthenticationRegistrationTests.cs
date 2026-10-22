using Microsoft.Extensions.DependencyInjection;
using ZFome.Application;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Identity.Authentication;
using ZFome.Infrastructure;

namespace ZFome.Architecture.Tests;

public sealed class PlatformAuthenticationRegistrationTests
{
    [Fact]
    public void Platform_feature_replaces_existing_authentication_handlers()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddPlatformProvisioning();

        Assert.Single(services, x => x.ServiceType ==
            typeof(ICommandHandler<CompleteAuthenticationCommand, AuthenticationTokens>));
        Assert.Single(services, x => x.ServiceType ==
            typeof(ICommandHandler<RefreshAuthenticationCommand, AuthenticationTokens>));
        Assert.Single(services, x => x.ServiceType ==
            typeof(ICommandHandler<ChangeTemporaryPasswordCommand>));
    }
}
