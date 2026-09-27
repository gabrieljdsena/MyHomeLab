using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Services;
using MyHomeLab.Domain;
using Xunit;

namespace MyHomeLab.Tests;

public class MachineServiceTests
{
    private static CreateMachineRequest CreateRequest(string name = "Gaming PC", string hostname = "gaming-pc") =>
        new(name, "Living room desktop", hostname, "computer", 10);

    private static PatchMachineRequest Patch(
        string? name = null,
        string? description = null,
        string? hostname = null,
        string? icon = null,
        bool? isEnabled = null,
        int? sortOrder = null) =>
        new(
            Name: name,
            Description: description,
            Hostname: hostname,
            Icon: icon,
            IsEnabled: isEnabled,
            SortOrder: sortOrder);

    private static MachineService Build() => new(new FakeMachineRepository());

    [Fact]
    public async Task CreateAsync_Adds_Machine_And_Returns_Detail()
    {
        var service = Build();

        var created = await service.CreateAsync(CreateRequest());

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Gaming PC", created.Name);
        Assert.Equal("gaming-pc", created.Hostname);
        Assert.Equal("unknown", created.Reachability);
        Assert.True(created.IsEnabled);
    }

    [Fact]
    public async Task CreateAsync_Duplicate_Name_Throws_Conflict()
    {
        var service = Build();
        await service.CreateAsync(CreateRequest());

        await Assert.ThrowsAsync<MachineConflictException>(() =>
            service.CreateAsync(CreateRequest(hostname: "other-pc")));
    }

    [Fact]
    public async Task CreateAsync_Invalid_Hostname_Throws_Domain_Exception()
    {
        var service = Build();

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(CreateRequest(hostname: "bad$host")));
    }

    [Fact]
    public async Task GetByIdAsync_Missing_Throws_NotFound()
    {
        var service = Build();

        await Assert.ThrowsAsync<MachineNotFoundException>(() => service.GetByIdAsync(MachineId.New()));
    }

    [Fact]
    public async Task GetAllAsync_Filters_By_Search_And_EnabledOnly()
    {
        var service = Build();
        await service.CreateAsync(CreateRequest("Gaming PC", "gaming-pc"));
        await service.CreateAsync(CreateRequest("Office PC", "office-pc"));

        var bySearch = await service.GetAllAsync(new MachineQuery(Search: "office"));
        Assert.Single(bySearch);
        Assert.Equal("Office PC", bySearch[0].Name);

        var office = bySearch[0];
        await service.PatchAsync(MachineId.From(office.Id), Patch(isEnabled: false));

        var enabledOnly = await service.GetAllAsync(new MachineQuery(EnabledOnly: true));
        Assert.Single(enabledOnly);
        Assert.Equal("Gaming PC", enabledOnly[0].Name);
    }

    [Fact]
    public async Task GetAllAsync_Orders_By_SortOrder_Then_Name()
    {
        var service = Build();
        await service.CreateAsync(CreateRequest("Zeta", "zeta-pc"));
        await service.CreateAsync(new CreateMachineRequest("Alpha", "d", "alpha-pc", "computer", 10));
        await service.CreateAsync(new CreateMachineRequest("Beta", "d", "beta-pc", "computer", 1));

        var all = await service.GetAllAsync(new MachineQuery());

        Assert.Equal(["Beta", "Alpha", "Zeta"], all.Select(m => m.Name).ToArray());
    }

    [Fact]
    public async Task UpdateAsync_Applies_New_Details()
    {
        var service = Build();
        var created = await service.CreateAsync(CreateRequest());

        var updated = await service.UpdateAsync(
            MachineId.From(created.Id),
            new UpdateMachineRequest("Office PC", "Moved", "office-pc", "desktop_windows", 2));

        Assert.Equal("Office PC", updated.Name);
        Assert.Equal("office-pc", updated.Hostname);
        Assert.Equal(2, updated.SortOrder);
    }

    [Fact]
    public async Task UpdateAsync_Renaming_To_Existing_Name_Throws_Conflict()
    {
        var service = Build();
        var first = await service.CreateAsync(CreateRequest("Gaming PC", "gaming-pc"));
        await service.CreateAsync(CreateRequest("Office PC", "office-pc"));

        await Assert.ThrowsAsync<MachineConflictException>(() =>
            service.UpdateAsync(
                MachineId.From(first.Id),
                new UpdateMachineRequest("Office PC", "d", "gaming-pc", "computer", 0)));
    }

    [Fact]
    public async Task PatchAsync_Updates_Only_Supplied_Fields()
    {
        var service = Build();
        var created = await service.CreateAsync(CreateRequest());

        var patched = await service.PatchAsync(MachineId.From(created.Id), Patch(sortOrder: 45));

        Assert.Equal("Gaming PC", patched.Name);
        Assert.Equal("gaming-pc", patched.Hostname);
        Assert.Equal(45, patched.SortOrder);
    }

    [Fact]
    public async Task PatchAsync_Toggles_IsEnabled()
    {
        var service = Build();
        var created = await service.CreateAsync(CreateRequest());

        var disabled = await service.PatchAsync(MachineId.From(created.Id), Patch(isEnabled: false));
        Assert.False(disabled.IsEnabled);

        var enabled = await service.PatchAsync(MachineId.From(created.Id), Patch(isEnabled: true));
        Assert.True(enabled.IsEnabled);
    }

    [Fact]
    public async Task DeleteAsync_Removes_Machine()
    {
        var service = Build();
        var created = await service.CreateAsync(CreateRequest());

        await service.DeleteAsync(MachineId.From(created.Id));

        await Assert.ThrowsAsync<MachineNotFoundException>(() => service.GetByIdAsync(MachineId.From(created.Id)));
    }

    [Fact]
    public async Task RecordReachabilityAsync_Persists_Online_State()
    {
        var service = Build();
        var created = await service.CreateAsync(CreateRequest());

        await service.RecordReachabilityAsync(
            MachineId.From(created.Id), new MachineReachabilityResult(MachineReachability.Online, 12));

        var reloaded = await service.GetByIdAsync(MachineId.From(created.Id));
        Assert.Equal("online", reloaded.Reachability);
        Assert.Equal(12, reloaded.LastLatencyMs);
        Assert.NotNull(reloaded.LastSeenUtc);
    }

    [Fact]
    public async Task RecordReachabilityAsync_Persists_Resolved_Addresses()
    {
        var service = Build();
        var created = await service.CreateAsync(CreateRequest());

        await service.RecordReachabilityAsync(
            MachineId.From(created.Id),
            new MachineReachabilityResult(MachineReachability.Online, 4, "192.168.15.42", "a4:bb:6d:11:22:33"));

        var reloaded = await service.GetByIdAsync(MachineId.From(created.Id));
        Assert.Equal("192.168.15.42", reloaded.IpAddress);
        Assert.Equal("a4:bb:6d:11:22:33", reloaded.MacAddress);
    }

    [Fact]
    public async Task RecordReachabilityAsync_Keeps_Last_Address_When_Probe_Resolves_Nothing()
    {
        var service = Build();
        var created = await service.CreateAsync(CreateRequest());
        var id = MachineId.From(created.Id);

        await service.RecordReachabilityAsync(
            id, new MachineReachabilityResult(MachineReachability.Online, 4, "192.168.15.42", "a4:bb:6d:11:22:33"));
        await service.RecordReachabilityAsync(id, new MachineReachabilityResult(MachineReachability.Offline, null));

        var reloaded = await service.GetByIdAsync(id);
        Assert.Equal("offline", reloaded.Reachability);
        Assert.Equal("192.168.15.42", reloaded.IpAddress);
        Assert.Equal("a4:bb:6d:11:22:33", reloaded.MacAddress);
    }

    [Fact]
    public async Task RecordReachabilityAsync_Missing_Machine_Is_Ignored()
    {
        var service = Build();

        await service.RecordReachabilityAsync(
            MachineId.New(), new MachineReachabilityResult(MachineReachability.Offline, null));
    }

    [Fact]
    public async Task GetAllAsync_Never_Leaks_Internal_State_Order()
    {
        var service = Build();
        await service.CreateAsync(new CreateMachineRequest("Third", "d", "third-pc", "computer", 30));
        await service.CreateAsync(new CreateMachineRequest("First", "d", "first-pc", "computer", 1));

        var all = await service.GetAllAsync(new MachineQuery());

        Assert.Equal(["First", "Third"], all.Select(m => m.Name).ToArray());
    }
}
