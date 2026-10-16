// SPDX-License-Identifier: MIT

using Content.Shared.Medical.SuitSensor;
using Robust.Shared.Audio;

namespace Content.Server.Medical.CrewMonitoring;

[RegisterComponent]
[Access(typeof(CrewMonitoringConsoleSystem))]
public sealed partial class CrewMonitoringConsoleComponent : Component
{
    /// <summary>
    ///     List of all currently connected sensors to this console.
    /// </summary>
    public Dictionary<string, SuitSensorStatus> ConnectedSensors = new();

    /// <summary>
    ///     After what time sensor consider to be lost.
    /// </summary>
    [DataField("sensorTimeout"), ViewVariables(VVAccess.ReadWrite)]
    public float SensorTimeout = 10f;

    // Europa-Start
    /// <summary>
    ///     Enable or disable alerts.
    /// </summary>
    [DataField]
    public bool DoAlert = true;

    /// <summary>
    ///     Time interval between alerts in seconds.
    /// </summary>
    [DataField]
    public float AlertTime = 30f;

    /// <summary>
    ///     Sound to play when alert is triggered.
    /// </summary>
    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/_Orion/Machines/crew_monitoring_alert.ogg");

    [DataField]
    public AudioParams AlertAudioParams = AudioParams.Default.WithVolume(-8f).WithVariation(0.25f);
    /// <summary>
    ///     Timestamp of the last played alert.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextAlertTime = TimeSpan.Zero;

    [DataField(serverOnly: true)]
    public Color? NormalLightColor { get; set; }

    [DataField(serverOnly: true)]
    public float? NormalLightEnergy { get; set; }

    [DataField(serverOnly: true)]
    public float? NormalLightRadius { get; set; }
    // Europa-End
}
