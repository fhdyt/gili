using Gili.Core.Modules;

namespace Gili.Core.Tests.Modules;

public class NetworkAdaptersTests
{
    private static readonly (string Name, string Description)[] Laptop =
    [
        ("Wi-Fi", "Intel(R) Wi-Fi 6 AX201 160MHz"),
        ("vEthernet (Default Switch)", "Hyper-V Virtual Ethernet Adapter"),
        ("vSwitch (Default Switch)-Hyper-V Virtual Switch Extension Filter-0000", "Hyper-V Virtual Switch Extension Adapter-Hyper-V Virtual Switch Extension Filter-0000"),
        ("vEthernet (Default Switch)-QoS Packet Scheduler-0000", "Hyper-V Virtual Ethernet Adapter-QoS Packet Scheduler-0000"),
        ("Local Area Connection* 7", "WAN Miniport (IP)"),
        ("Local Area Connection* 7-QoS Packet Scheduler-0000", "WAN Miniport (IP)-QoS Packet Scheduler-0000"),
        ("vSwitch (Default Switch)", "Hyper-V Virtual Switch Extension Adapter"),
        ("Wi-Fi-WFP Native MAC Layer LightWeight Filter-0000", "Intel(R) Wi-Fi 6 AX201 160MHz-WFP Native MAC Layer LightWeight Filter-0000"),
        ("Wi-Fi-QoS Packet Scheduler-0000", "Intel(R) Wi-Fi 6 AX201 160MHz-QoS Packet Scheduler-0000"),
    ];

    [Fact]
    public void Filter_layers_of_an_adapter_are_not_counted_again() =>
        Assert.Equal(["Wi-Fi", "vEthernet (Default Switch)"], NetworkAdapters.Counted(Laptop).Select(a => a.Name));
}
