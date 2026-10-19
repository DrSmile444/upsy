using Microsoft.Extensions.Time.Testing;
using Upsy.Core.Power;
using static Upsy.Core.Tests.Fixtures;

namespace Upsy.Core.Tests;

public sealed class EnergyLogTests
{
    private readonly FakeTimeProvider _clock = new();

    private List<EnergySummary> Run(EnergyLog log, Func<int, Station.Telemetry> sample, int fromSecond, int toSecond)
    {
        var summaries = new List<EnergySummary>();
        for (var s = fromSecond; s <= toSecond; s += 2)
        {
            if (log.Add(sample(s)) is { } summary)
            {
                summaries.Add(summary);
            }

            _clock.Advance(TimeSpan.FromSeconds(2));
        }

        return summaries;
    }

    [Fact]
    public void Charge_energy_is_the_integral_of_net_power_under_a_cycling_load()
    {
        var log = new EnergyLog(_clock);
        // Input 330 W; the boiler switches the output between 40 W and 130 W every 3 minutes.
        Station.Telemetry At(int s) => T(50, acIn: 330, acOut: (s / 180) % 2 == 0 ? 40 : 130, volts: 228);
        var summaries = Run(log, At, 0, 1200);

        Assert.Equal(20, summaries.Count);
        Assert.All(summaries, x => Assert.Equal(EnergyDirection.Charge, x.Direction));
        var expectedWh = 0.0;
        for (var s = 0; s < 1200; s += 2)
        {
            expectedWh += (330 - ((s / 180) % 2 == 0 ? 40 : 130)) * 2 / 3600.0;
        }

        Assert.Equal(expectedWh, summaries[^1].EnergyWh, 1);
        Assert.Equal(TimeSpan.FromMinutes(20), summaries[^1].Elapsed);
        Assert.Contains(summaries, x => x.AverageOutW == 40);
        Assert.Contains(summaries, x => x.AverageOutW == 130);
    }

    [Fact]
    public void Discharge_counts_output_minus_dc_input()
    {
        var log = new EnergyLog(_clock);
        var summaries = Run(log, _ => T(72, acOut: 88, volts: 2.6), 0, 120);
        Assert.Equal(EnergyDirection.Discharge, summaries[^1].Direction);
        Assert.Equal(88 * 120 / 3600.0, summaries[^1].EnergyWh, 2);
        Assert.Equal(-88, summaries[^1].AverageNetW);
    }

    [Fact]
    public void Idle_states_write_nothing()
    {
        var log = new EnergyLog(_clock);
        Assert.Empty(Run(log, _ => T(100, acIn: 46, acOut: 46, volts: 228), 0, 300));   // full on mains
        Assert.Empty(Run(log, _ => T(92, acIn: 48, acOut: 48, volts: 228), 0, 300));    // thermal pause
        Assert.Empty(Run(log, _ => T(80, acOut: 5, volts: 2.6), 0, 300));               // on battery, no load
    }

    [Fact]
    public void A_change_of_direction_starts_a_new_run()
    {
        var log = new EnergyLog(_clock);
        Run(log, _ => T(41, acOut: 100, volts: 2.6), 0, 300);
        var charge = Run(log, _ => T(41, acIn: 360, acOut: 100, volts: 228), 302, 422);
        Assert.Equal(EnergyDirection.Charge, charge[0].Direction);
        Assert.Equal(TimeSpan.FromMinutes(1), charge[0].Elapsed);
        Assert.Equal(260 / 60.0, charge[0].EnergyWh, 1);
    }

    [Fact]
    public void A_pause_of_more_than_a_minute_starts_a_new_run()
    {
        var log = new EnergyLog(_clock);
        Run(log, _ => T(60, acIn: 330, acOut: 66, volts: 228), 0, 300);
        Run(log, _ => T(60, acIn: 48, acOut: 48, volts: 228), 302, 500);   // more than a minute paused
        var after = Run(log, _ => T(61, acIn: 330, acOut: 66, volts: 228), 502, 622);
        Assert.Equal(TimeSpan.FromMinutes(1), after[0].Elapsed);
    }
}
