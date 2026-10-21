using ZFome.Application.Identity.CreateAdministrativeUser;
using ZFome.Application.Identity.Management;
using ZFome.Domain.Identity;

namespace ZFome.Application.Tests.Identity;

public sealed class AdministrativeUserManagementValidationTests
{
    [Theory]
    [InlineData("123456789", false)]
    [InlineData("1234567890", true)]
    public void Administrative_password_policy_requires_at_least_ten_characters(string password, bool expected)
    {
        var command = new CreateAdministrativeUserCommand("Name", "email@example.test", password, AdministrativeRole.Admin, Guid.NewGuid());

        Assert.Equal(expected, new CreateAdministrativeUserCommandValidator().Validate(command).IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(1000001, 20)]
    public void Rejects_invalid_pagination(int page, int size) =>
        Assert.False(new SearchAdministrativeUsersQueryValidator().Validate(new SearchAdministrativeUsersQuery(Guid.NewGuid(), Page: page, PageSize: size)).IsValid);

    [Fact]
    public void Validates_ids_roles_profile_and_creation()
    {
        var unit = Guid.NewGuid(); var user = Guid.NewGuid();
        Assert.False(new SetAdministrativeUserRoleCommandValidator().Validate(new SetAdministrativeUserRoleCommand(unit, user, (AdministrativeRole)99, true)).IsValid);
        Assert.False(new SetAdministrativeUserActiveCommandValidator().Validate(new SetAdministrativeUserActiveCommand(unit, Guid.Empty, false)).IsValid);
        Assert.False(new SetAdministrativeUserAccessCommandValidator().Validate(new SetAdministrativeUserAccessCommand(Guid.Empty, user, true)).IsValid);
        Assert.False(new UpdateAdministrativeUserCommandValidator().Validate(new UpdateAdministrativeUserCommand(unit, user, " ")).IsValid);
        Assert.False(new CreateAdministrativeUserCommandValidator().Validate(new CreateAdministrativeUserCommand("Name", "email@example.test", "secure-password", AdministrativeRole.Admin)).IsValid);
        Assert.True(new CreateAdministrativeUserCommandValidator().Validate(new CreateAdministrativeUserCommand("Name", "email@example.test", "secure-password", AdministrativeRole.Admin, unit)).IsValid);
    }
}
